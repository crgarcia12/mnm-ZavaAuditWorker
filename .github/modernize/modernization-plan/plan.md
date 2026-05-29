# Modernization Plan: ZavaAuditWorker Azure Migration

**Project**: ZavaAuditWorker

---

## Technical Framework

- **Language**: C# / .NET Framework 4.8
- **Framework**: .NET Framework 4.8 console application (worker service)
- **Build Tool**: MSBuild
- **Database**: SQL Server (via System.Data.SqlClient, connection string in app.config)
- **Key Dependencies**: RabbitMQ.Client 5.2.0, System.Data.SqlClient (BCL)

---

## Overview

> This migration modernizes the ZavaAuditWorker service to use Azure-native managed services. The application currently consumes audit events from a self-hosted RabbitMQ queue and writes them to an on-premises SQL Server database, using hardcoded credentials stored in app.config and Program.cs. The new architecture will:
>
> - Replace RabbitMQ with Azure Service Bus, eliminating the need to manage a message broker and enabling cloud-native event processing
> - Replace the SQL Server connection with Azure SQL Database using Managed Identity, removing all hardcoded credentials from the codebase
> - Remediate known CVEs in project dependencies to ensure the application is secure before deployment
>
> The migration follows a transform-first approach: each Azure service migration is executed independently, followed by a security remediation pass to address any vulnerable dependencies identified in the assessment.

---

## Migration Impact Summary

| Application      | Original Service | New Azure Service    | Authentication   | Comments                                        |
|------------------|------------------|----------------------|------------------|-------------------------------------------------|
| ZavaAuditWorker  | RabbitMQ         | Azure Service Bus    | Managed Identity | Migrate audit event queue consumption           |
| ZavaAuditWorker  | SQL Server       | Azure SQL Database   | Managed Identity | Migrate audit event storage; remove hardcoded credentials |

---

## Migration Tasks

### Task 1 — Migrate RabbitMQ to Azure Service Bus

Migrate the audit event consumer from RabbitMQ to Azure Service Bus. The worker currently polls a RabbitMQ queue (`audit.events`) using hardcoded host/user/password configuration. This task replaces the RabbitMQ client with the Azure Service Bus SDK, updates message consumption to use a Service Bus queue or subscription, and authenticates via Managed Identity to eliminate all hardcoded broker credentials.

**Addresses assessment issues**: Hardcoded sensitive data detected (RabbitMQ credentials in app.config and Program.cs)

---

### Task 2 — Migrate SQL Server to Azure SQL Database with Managed Identity

Migrate the audit event persistence layer from SQL Server (with username/password authentication) to Azure SQL Database using Managed Identity. This task updates the database connection to use passwordless authentication, removes hardcoded connection strings from app.config and Program.cs, and ensures the existing schema-adaptive insert logic (legacy/modern AuditLog columns) continues to work.

**Addresses assessment issues**: Hardcoded sensitive data detected (SQL credentials in Program.cs and app.config), Connection strings without configuration builders detected

---

### Task 3 — Security: CVE Remediation

Scan all project dependencies for known CVEs and remediate any identified vulnerabilities to ensure the application is secure. Upgrade vulnerable dependencies to the minimum patched version. If a CVE fix requires a major version upgrade, document the affected dependency, the current version, the upgraded major version, and the breaking change risk. Verify that the project builds and all tests pass after remediation.

---

## Open Questions & Questionnaire

- [x] Q: Should the plan include environment/infrastructure provisioning? → A: No — code migration only; focus on migrating to Azure-managed services without provisioning new infrastructure
- [x] Q: Should the plan include integration testing? → A: No — no integration testing requested; skipping integration test tasks
- [x] Q: Should the plan include a security scan and CVE remediation task? → A: Yes — include security/CVE remediation (default)
- [x] Q: Which Azure deployment target should the plan use? → A: No deployment — migration only, no cloud deployment tasks included
