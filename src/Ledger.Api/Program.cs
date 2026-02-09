using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using RabbitMQ.Client;

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

string pgConn = builder.Configuration.GetConnectionString("Postgres")!;
var rabbitCfg = builder.Configuration.GetSection("RabbitMQ");

app.MapPost("/transactions", async (TransactionCreateRequest req) =>
{
    if (req.Amount <= 0) return Results.BadRequest("Amount must be > 0");
    if (req.Type is not ("CREDIT" or "DEBIT")) return Results.BadRequest("Type must be CREDIT or DEBIT");

    var tx = new Transaction(
        Id: Guid.NewGuid(),
        CreatedAt: DateTime.UtcNow,
        Date: req.Date.Date,
        Type: req.Type,
        Amount: req.Amount,
        Description: req.Description
    );

    // 1) salva no Postgres
    await using var conn = new NpgsqlConnection(pgConn);
    await conn.ExecuteAsync(@"
        INSERT INTO transactions (id, created_at, date, type, amount, description)
        VALUES (@Id, @CreatedAt, @Date, @Type, @Amount, @Description)
    ", tx);

    // 2) publica evento no RabbitMQ
    PublishRabbit(tx, rabbitCfg);

    return Results.Created($"/transactions/{tx.Id}", tx);
});

app.MapGet("/transactions", async () =>
{
    await using var conn = new NpgsqlConnection(pgConn);

    var list = await conn.QueryAsync<Transaction>(@"
        SELECT
            id           AS ""Id"",
            created_at   AS ""CreatedAt"",
            (date::timestamp) AS ""Date"",
            type         AS ""Type"",
            amount       AS ""Amount"",
            description  AS ""Description""
        FROM transactions
        ORDER BY created_at DESC
        LIMIT 100
    ");

    return Results.Ok(list);
});

app.Run();

static void PublishRabbit(Transaction tx, IConfigurationSection rabbitCfg)
{
    var factory = new ConnectionFactory()
    {
        HostName = rabbitCfg["Host"],
        Port = int.Parse(rabbitCfg["Port"]!),
        UserName = rabbitCfg["User"],
        Password = rabbitCfg["Pass"]
    };

    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();

    var queue = rabbitCfg["Queue"]!;
    channel.QueueDeclare(queue: queue, durable: true, exclusive: false, autoDelete: false);

    var payload = JsonSerializer.Serialize(new TransactionCreatedEvent(
        tx.Id, tx.Date, tx.Type, tx.Amount
    ));

    var body = Encoding.UTF8.GetBytes(payload);

    var props = channel.CreateBasicProperties();
    props.Persistent = true;

    channel.BasicPublish(exchange: "", routingKey: queue, basicProperties: props, body: body);
}

record TransactionCreateRequest(DateTime Date, string Type, decimal Amount, string? Description);
record Transaction(Guid Id, DateTime CreatedAt, DateTime Date, string Type, decimal Amount, string? Description);
record TransactionCreatedEvent(Guid Id, DateTime Date, string Type, decimal Amount);
