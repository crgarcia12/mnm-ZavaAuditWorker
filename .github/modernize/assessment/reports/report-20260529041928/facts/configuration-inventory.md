# Configuration & Externalized Settings Inventory

Configuration is centralized in `app.config` with environment-variable override support in code, covering messaging, worker timing, and database connectivity.

## Configuration Sources

| Source | Type | Path/Location | Notes |
|---|---|---|---|
| app.config | XML app settings | `app.config` | Default runtime values for RabbitMQ and DB connection |
| Environment variables | Process environment | Runtime environment | Overrides app settings via helper methods in `Program.cs` |
| Project file | Build configuration | `ZavaAuditWorker.csproj` | Debug/Release and framework/language settings |
| Dockerfile | Container build/runtime | `Dockerfile` | Restores/builds app and runs worker on Mono |

## Build Profiles

| Profile | Activation | Purpose | Key Dependencies/Plugins |
|---|---|---|---|
| Debug | `Configuration=Debug` | Local debug build | .NET Framework build targets |
| Release | `Configuration=Release` | Optimized production build | .NET Framework build targets |

## Runtime Profiles

| Profile | Activation Method | Config Files | Key Overrides |
|---|---|---|---|
| Default | App startup | `app.config` | RabbitMQ/DB defaults |
| Env override mode | Presence of env vars | Env + `app.config` | `RABBITMQ_*`, `AUDIT_QUEUE`, `WORKER_POLL_INTERVAL_MS`, `DB_*`, `DATABASE_*` |

## Properties Inventory

| Property Key | Default | Profiles | Source |
|---|---|---|---|
| `RabbitMQ.Host` / `RABBITMQ_HOST` | `rabbitmq` | Default, env override | app.config + env |
| `RabbitMQ.Port` / `RABBITMQ_PORT` | `5672` | Default, env override | app.config + env |
| `RabbitMQ.VHost` / `RABBITMQ_VHOST` | `/zavabank` | Default, env override | app.config + env |
| `RabbitMQ.User` / `RABBITMQ_USER` | `zava_app` | Default, env override | app.config + env |
| `RabbitMQ.Password` / `RABBITMQ_PASSWORD` | `[MASKED]` | Default, env override | app.config + env |
| `RabbitMQ.AuditQueue` / `AUDIT_QUEUE` | `audit.events` | Default, env override | app.config + env |
| `RabbitMQ.DeadLetterExchange` / `RABBITMQ_DLX` | `zava.dlx` | Default, env override | app.config + env |
| `Worker.PollIntervalMs` / `WORKER_POLL_INTERVAL_MS` | `5000` | Default, env override | app.config + env |
| `Database.ConnectionString` / `DB_CONNECTION_STRING` | `[MASKED]` | Default, env override | app.config + env |
| `DB_HOST` / `DATABASE_HOST` | `sqlserver` | Env fallback path | env-derived |
| `DB_PORT` / `DATABASE_PORT` | `1433` | Env fallback path | env-derived |
| `DB_NAME` / `DATABASE_NAME` | `ZavaBankDB` | Env fallback path | env-derived |
| `DB_USER` / `DATABASE_USER` | `sa` | Env fallback path | env-derived |
| `DB_PASSWORD` / `DATABASE_PASSWORD` | `[MASKED]` | Env fallback path | env-derived |

## Startup Parameters & Resource Requirements

| Service | JVM/Runtime Options | Memory | Instance Count |
|---|---|---|---|
| ZavaAuditWorker | None explicitly configured in repo | Not specified | Not specified |

## Startup Dependency Chain

1. `ZavaAuditWorker` starts and loads config.
2. Worker expects RabbitMQ connectivity (`rabbitmq:5672`) before successful polling.
3. Worker expects SQL Server connectivity before persisting audit events.

## Secrets & Sensitive Configuration

| Secret Reference | Type | Storage (masked) |
|---|---|---|
| `RabbitMQ.Password` / `RABBITMQ_PASSWORD` | Messaging credential | app.config/env (`[MASKED]`) |
| `Database.ConnectionString` / `DB_CONNECTION_STRING` | Database credential | app.config/env (`[MASKED]`) |
| `DB_PASSWORD` / `DATABASE_PASSWORD` | Database password | env (`[MASKED]`) |

### Secrets Provisioning Workflow

Secrets can be provided directly in `app.config` defaults or injected at runtime through environment variables. The runtime first checks environment values and falls back to app settings, allowing deployment systems to externalize credentials without code changes. No external secret manager integration is defined in this repository.

## Feature Flags

| Flag Name | Default | Controlled By |
|---|---|---|
| None detected | N/A | N/A |

## Framework & Runtime Versions

| Component | Version | Source |
|---|---|---|
| .NET Framework target | 4.8 | `ZavaAuditWorker.csproj` |
| C# language version | 7.3 | `ZavaAuditWorker.csproj` |
| RabbitMQ.Client | 5.2.0 | `packages.config` |
| Base container runtime | mono:6.12 | `Dockerfile` |
