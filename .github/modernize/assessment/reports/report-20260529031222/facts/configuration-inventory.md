# Configuration & Externalized Settings Inventory

Configuration is split between `app.config`, environment variables, and Docker image runtime environment. The worker uses environment-first overrides for broker and database connectivity.

## Configuration Sources

| Source | Type | Path/Location | Notes |
|---|---|---|---|
| `app.config` | XML app settings | `/app.config` | Default RabbitMQ and database settings |
| Environment variables | Runtime externalized config | Process environment | Takes precedence over `app.config` values |
| `Dockerfile` | Container runtime config | `/Dockerfile` | Defines base image and launch entrypoint |

## Build Profiles

| Profile | Activation | Purpose | Key Dependencies/Plugins |
|---|---|---|---|
| Debug | `Configuration=Debug` | Local debug build | Standard .NET Framework compile settings |
| Release | `Configuration=Release` | Optimized release build | Standard .NET Framework compile settings |

## Runtime Profiles

| Profile | Activation Method | Config Files | Key Overrides |
|---|---|---|---|
| Default | No environment overrides | `app.config` | RabbitMQ host/port/vhost/user/password, queue names, poll interval, DB connection string |
| Environment Override | Presence of env vars | Environment variables | `RABBITMQ_*`, `AUDIT_QUEUE`, `WORKER_POLL_INTERVAL_MS`, `DB_*` and `DATABASE_*` |

## Properties Inventory

| Property Key | Default | Profiles | Source |
|---|---|---|---|
| `RabbitMQ.Host` / `RABBITMQ_HOST` | `rabbitmq` | Default, override | app.config / env |
| `RabbitMQ.Port` / `RABBITMQ_PORT` | `5672` | Default, override | app.config / env |
| `RabbitMQ.VHost` / `RABBITMQ_VHOST` | `/zavabank` | Default, override | app.config / env |
| `RabbitMQ.User` / `RABBITMQ_USER` | `zava_app` | Default, override | app.config / env |
| `RabbitMQ.Password` / `RABBITMQ_PASSWORD` | `[MASKED]` | Default, override | app.config / env |
| `RabbitMQ.AuditQueue` / `AUDIT_QUEUE` | `audit.events` | Default, override | app.config / env |
| `RabbitMQ.DeadLetterExchange` / `RABBITMQ_DLX` | `zava.dlx` | Default, override | app.config / env |
| `Worker.PollIntervalMs` / `WORKER_POLL_INTERVAL_MS` | `5000` | Default, override | app.config / env |
| `Database.ConnectionString` / `DB_CONNECTION_STRING` | Contains SQL Server connection | Default, override | app.config / env |
| `DB_HOST`/`DATABASE_HOST` | `sqlserver` | Override chain | env |
| `DB_PORT`/`DATABASE_PORT` | `1433` | Override chain | env |
| `DB_NAME`/`DATABASE_NAME` | `ZavaBankDB` | Override chain | env |
| `DB_USER`/`DATABASE_USER` | `sa` | Override chain | env |
| `DB_PASSWORD`/`DATABASE_PASSWORD` | `[MASKED]` | Override chain | env |

## Startup Parameters & Resource Requirements

| Service | JVM/Runtime Options | Memory | Instance Count |
|---|---|---|---|
| ZavaAuditWorker | No explicit CLI runtime flags detected | Not specified | Not specified |

## Startup Dependency Chain

1. `ZavaAuditWorker` starts and loads configuration.
2. Worker attempts RabbitMQ connection and queue declaration/read.
3. Worker opens SQL connection when processing each message.

No explicit readiness probe, startup wait script, or orchestration-level dependency wiring was found in this repository.

## Secrets & Sensitive Configuration

| Secret Reference | Type | Storage (masked) |
|---|---|---|
| `RabbitMQ.Password` / `RABBITMQ_PASSWORD` | Broker credential | app.config or environment (`[MASKED]`) |
| `Database.ConnectionString` / `DB_CONNECTION_STRING` | DB credential payload | app.config or environment (`[MASKED]`) |
| `DB_PASSWORD` / `DATABASE_PASSWORD` | DB password | Environment (`[MASKED]`) |

### Secrets Provisioning Workflow

Secrets are read from environment variables first, with `app.config` as fallback. No external secret manager integration (for example Key Vault or Vault) or managed identity flow is defined in this codebase.

## Feature Flags

No feature flag framework or conditional feature toggle configuration was detected.

## Framework & Runtime Versions

| Component | Version | Source |
|---|---|---|
| .NET Framework target | 4.8 | `ZavaAuditWorker.csproj` |
| C# language version | 7.3 | `ZavaAuditWorker.csproj` |
| RabbitMQ.Client | 5.2.0 | `packages.config` |
| Base container image | mcr.microsoft.com/dotnet/framework/runtime:4.8 | `Dockerfile` |
