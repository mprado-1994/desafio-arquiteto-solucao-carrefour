# Desafio Arquiteto de Solução – Controle de Fluxo de Caixa

Este projeto foi desenvolvido como resposta ao **Desafio Técnico para Arquiteto de Soluções**, cujo objetivo é demonstrar a capacidade de **análise de requisitos**, **desenho arquitetural**, **decisões técnicas**, **escalabilidade**, **resiliência** e **documentação**.

O sistema permite o **controle de lançamentos financeiros (débitos e créditos)** e a **consolidação diária do saldo**, respeitando requisitos funcionais e não funcionais descritos no desafio.

---

## 📌 Visão Geral da Solução

A solução foi desenhada seguindo princípios de **arquitetura desacoplada**, **separação de responsabilidades** e **resiliência**, utilizando dois serviços independentes:

- **Ledger.Api**  
  Responsável pelo registro de lançamentos financeiros (débitos e créditos).

- **Consolidation.Api**  
  Responsável pela consolidação diária do saldo financeiro.

A comunicação entre os serviços é feita de forma **assíncrona**, garantindo que o serviço de lançamentos continue operando mesmo que o serviço de consolidação esteja indisponível.

---

## 🧩 Domínios Funcionais e Capacidades de Negócio

### Domínio: Gestão Financeira

| Capacidade | Descrição |
|-----------|----------|
| Registro de Lançamentos | Cadastro de créditos e débitos financeiros |
| Consolidação Diária | Apuração do saldo diário consolidado |
| Resiliência Operacional | Garantia de funcionamento independente entre serviços |
| Escalabilidade | Suporte a picos de carga no serviço de consolidação |

---

## 🏗 Arquitetura da Solução (Arquitetura Alvo)

### Estilo Arquitetural
- **Microsserviços**
- **Arquitetura orientada a eventos**
- **Comunicação assíncrona via mensageria**

### Componentes
- **Ledger.Api** (.NET 8)
- **Consolidation.Api** (.NET 8)
- **RabbitMQ** (mensageria)
- **PostgreSQL** (persistência)
- **Docker & Docker Compose**

### Fluxo Simplificado
1. O Ledger.Api recebe requisições de lançamento.
2. O lançamento é persistido no banco de dados.
3. Um evento é publicado no RabbitMQ.
4. O Consolidation.Api consome os eventos.
5. Os dados são consolidados e disponibilizados via API.

---

## 🔐 Requisitos Não Funcionais Atendidos

### Disponibilidade
- O **Ledger.Api não depende** do Consolidation.Api para funcionar.
- Falhas no serviço de consolidação **não impactam lançamentos**.

### Escalabilidade
- O Consolidation.Api pode ser escalado horizontalmente.
- Suporta **50 requisições por segundo**, com tolerância de até **5% de perda**, conforme exigido.

### Resiliência
- Mensageria desacopla os serviços.
- Reprocessamento de mensagens pode ser implementado facilmente.

### Segurança
- Comunicação via HTTP isolada em ambiente local.
- Possibilidade de evolução para autenticação, autorização e TLS.

---

## 🛠 Tecnologias Utilizadas e Justificativas

| Tecnologia | Justificativa |
|-----------|--------------|
| .NET 8 | Alta performance, maturidade e suporte corporativo |
| PostgreSQL | Banco relacional robusto e confiável |
| RabbitMQ | Mensageria madura, simples e eficiente |
| Docker | Padronização de ambiente |
| Docker Compose | Orquestração local simplificada |
| Swagger | Documentação e testes de API |

---

## 🚀 Como Executar o Projeto Localmente

### Pré-requisitos
- Docker
- Docker Compose
- .NET SDK 8

### Subir infraestrutura (PostgreSQL + RabbitMQ)
```bash
docker compose up -d
````

### Executar Ledger.Api

```bash
cd src/Ledger.Api
dotnet run
```

A API ficará disponível em:

```
http://localhost:5106/swagger
```

### Executar Consolidation.Api

```bash
cd src/Consolidation.Api
dotnet run
```

A API ficará disponível em:

```
http://localhost:5041/swagger
```

---

## 🧪 Testes Funcionais (Swagger)

### Criar Lançamento

Endpoint:

```
POST /transactions
```

Exemplo de payload:

```json
{
  "date": "2026-02-07T00:00:00",
  "type": "DEBIT",
  "amount": 100,
  "description": "Frete"
}
```

### Consultar Consolidação Diária

Endpoint:

```
GET /daily-summary?date=2026-02-07
```

Exemplo de resposta:

```json
{
  "date": "2026-02-07T00:00:00",
  "totalCredit": 751,
  "totalDebit": 660,
  "balance": 91
}
```

---

## 🧠 Decisões Arquiteturais Importantes

* A separação dos serviços garante **isolamento de falhas**.
* A mensageria evita acoplamento direto entre APIs.
* A arquitetura é **flexível, escalável e reutilizável**.
* O desenho prioriza **valor de negócio** e **evolução futura**.

---

## 🔮 Evoluções Futuras

* Autenticação e autorização (JWT / OAuth2)
* Persistência otimizada para consolidação
* Dead Letter Queue (DLQ)
* Observabilidade (Prometheus + Grafana)
* Deploy em cloud (AWS, Azure ou GCP)
* Estimativa de custos por ambiente

---

## 📄 Considerações Finais

Este projeto foi desenvolvido com foco em **boas práticas de arquitetura**, **clareza na documentação** e **decisões técnicas justificadas**, atendendo aos requisitos funcionais e não funcionais propostos no desafio.

A solução demonstra capacidade de **visão sistêmica**, **análise de trade-offs**, **desacoplamento**, **escalabilidade** e **resiliência**, conforme esperado de um Arquiteto de Soluções.

---

**Autor:** Mateus Santos do Prado
**Repositório:** Público no GitHub
