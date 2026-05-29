# Architecture Diagram

This repository contains a single background worker application that consumes audit messages from RabbitMQ and persists them into SQL Server.

## Application Architecture

```mermaid
flowchart TD
    subgraph Input["Message Input Layer"]
        Rabbit[("RabbitMQ queue audit.events")]
    end
    subgraph App["Application Layer - .NET Framework 4.8"]
        Worker["Program Main Loop"]
        Parser["JSON XML Event Parser"]
        Persister["Audit Persistence Logic"]
    end
    subgraph Data["Data Layer"]
        SQL[("SQL Server ZavaBankDB")]
    end
    subgraph External["External Services"]
        DLX["Dead Letter Exchange zava.dlx"]
    end

    Rabbit -->|"BasicGet audit event"| Worker
    Worker -->|"parse payload"| Parser
    Parser -->|"normalized audit event"| Persister
    Persister -->|"insert audit rows"| SQL
    Worker -->|"BasicNack on failure"| DLX
```

### Technology Stack Summary

| Layer | Technology | Version | Purpose |
|---|---|---|---|
| Application | .NET Framework Console App | 4.8 | Long-running worker process |
| Messaging | RabbitMQ.Client | 5.2.0 | Queue polling and message ack/nack |
| Data Access | ADO.NET `SqlConnection`/`SqlCommand` | System.Data (framework) | SQL write operations |
| Data Store | SQL Server | Not pinned in repo | Persistent audit storage |

### Data Storage & External Services

The worker depends on RabbitMQ as the event source and SQL Server as the persistent audit store. On processing failures it negatively acknowledges messages, routing to the configured dead-letter exchange.

### Key Architectural Decisions

- Poll-based worker loop uses `BasicGet` and explicit ack/nack handling for deterministic message completion.
- Supports both legacy and modern `AuditLog` schemas by checking for `ActionID` column at runtime.
- Reads configuration from environment variables first, then app settings, enabling externalized deployment configuration.

## Component Relationships

```mermaid
flowchart LR
    subgraph Presentation["Presentation"]
        MainLoop["Program.Main"]
    end
    subgraph Business["Business Logic"]
        PollQueue["PollQueue"]
        ParseEvent["ParseAuditEvent ParseJson ParseXml"]
        SaveEvent["SaveAuditEvent"]
        LegacyInsert["InsertLegacyAuditRow"]
        ModernInsert["InsertModernAuditRow"]
    end
    subgraph DataAccess["Data Access"]
        EnsureAction["EnsureAction"]
        SqlClient["SqlConnection SqlCommand"]
    end
    subgraph Infrastructure["Infrastructure"]
        Config["LoadConfig Env"]
        RabbitClient["RabbitMQ ConnectionFactory"]
    end

    MainLoop -->|"initializes"| Config
    MainLoop -->|"runs"| PollQueue
    PollQueue -->|"receives message"| RabbitClient
    PollQueue -->|"deserializes"| ParseEvent
    PollQueue -->|"persists"| SaveEvent
    SaveEvent -->|"legacy path"| LegacyInsert
    SaveEvent -->|"modern path"| ModernInsert
    LegacyInsert -->|"resolve action id"| EnsureAction
    LegacyInsert -->|"execute SQL"| SqlClient
    ModernInsert -->|"execute SQL"| SqlClient
```

### Component Inventory

| Component | Layer | Type | Responsibility |
|---|---|---|---|
| Program.Main | Presentation | Entry point | Starts worker lifecycle and poll loop |
| LoadConfig/Env helpers | Infrastructure | Configuration utility | Loads settings from env + app config |
| PollQueue | Business Logic | Orchestrator | Retrieves messages and coordinates processing |
| ParseAuditEvent/ParseJson/ParseXml | Business Logic | Parser | Normalizes message payload to `AuditEvent` |
| SaveAuditEvent | Business Logic | Persistence coordinator | Chooses legacy or modern insert path |
| EnsureAction | Data Access | SQL helper | Resolves/inserts action metadata for legacy schema |
| InsertLegacyAuditRow | Business Logic | Persistence operation | Writes legacy `AuditLog` record |
| InsertModernAuditRow | Business Logic | Persistence operation | Writes modern `AuditLog` record |
