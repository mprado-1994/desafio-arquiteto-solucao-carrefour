using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string pgConn = builder.Configuration.GetConnectionString("Postgres")!;
var rabbitCfg = builder.Configuration.GetSection("RabbitMQ");

builder.Services.AddHostedService(_ => new RabbitConsumerHostedService(pgConn, rabbitCfg));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/daily-summary", async (DateTime date) =>
{
    await using var conn = new NpgsqlConnection(pgConn);

    // ✅ FORÇA o campo "date" (DATE do Postgres) virar DateTime (timestamp) pra Dapper não surtar
    var row = await conn.QueryFirstOrDefaultAsync<DailySummary>(@"
        SELECT
            date::timestamp AS ""Date"",
            total_credit    AS ""TotalCredit"",
            total_debit     AS ""TotalDebit"",
            balance         AS ""Balance""
        FROM daily_summary
        WHERE date = @Date
    ", new { Date = date.Date });

    return row is null ? Results.NotFound() : Results.Ok(row);
});

app.Run();

// ✅ DailySummary em DateTime (não DateOnly)
record DailySummary(DateTime Date, decimal TotalCredit, decimal TotalDebit, decimal Balance);
record TransactionCreatedEvent(Guid Id, DateTime Date, string Type, decimal Amount);

class RabbitConsumerHostedService : BackgroundService
{
    private readonly string _pgConn;
    private readonly IConfigurationSection _rabbitCfg;

    public RabbitConsumerHostedService(string pgConn, IConfigurationSection rabbitCfg)
    {
        _pgConn = pgConn;
        _rabbitCfg = rabbitCfg;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory()
        {
            HostName = _rabbitCfg["Host"],
            Port = int.Parse(_rabbitCfg["Port"]!),
            UserName = _rabbitCfg["User"],
            Password = _rabbitCfg["Pass"]
        };

        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();

        var queue = _rabbitCfg["Queue"]!;
        channel.QueueDeclare(queue: queue, durable: true, exclusive: false, autoDelete: false);
        channel.BasicQos(0, 10, false);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var ev = JsonSerializer.Deserialize<TransactionCreatedEvent>(json)!;

                await ApplyConsolidation(ev);

                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch
            {
                channel.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        };

        channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ApplyConsolidation(TransactionCreatedEvent ev)
    {
        await using var conn = new NpgsqlConnection(_pgConn);

        var credit = ev.Type == "CREDIT" ? ev.Amount : 0m;
        var debit  = ev.Type == "DEBIT"  ? ev.Amount : 0m;

        // ✅ o banco é DATE, então mandamos só a parte "Date"
        var day = ev.Date.Date;

        await conn.ExecuteAsync(@"
            INSERT INTO daily_summary (date, total_credit, total_debit, balance)
            VALUES (@Date, @Credit, @Debit, (@Credit - @Debit))
            ON CONFLICT (date) DO UPDATE SET
              total_credit = daily_summary.total_credit + EXCLUDED.total_credit,
              total_debit  = daily_summary.total_debit  + EXCLUDED.total_debit,
              balance      = (daily_summary.total_credit + EXCLUDED.total_credit)
                           - (daily_summary.total_debit  + EXCLUDED.total_debit)
        ", new { Date = day, Credit = credit, Debit = debit });
    }
}
