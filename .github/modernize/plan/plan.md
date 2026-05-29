# Modernization Plan: Modernize ZavaAuditWorker for Azure

**Project**: ZavaAuditWorker

---

## Technical Framework

- **Language**: C# (.NET Framework 4.8)
- **Framework**: .NET Framework console worker
- **Build Tool**: MSBuild / dotnet CLI (project file build)
- **Database**: SQL Server (AuditLog / AuditActions tables)
- **Key Dependencies**: RabbitMQ.Client 5.2.0, System.Data.SqlClient

---

## Overview

> This migration modernizes ZavaAuditWorker to the latest supported .NET runtime and deploys it to Azure using cloud-native managed services. The application currently runs as a legacy .NET Framework worker using RabbitMQ and SQL Server integrations.
>
> The modernized architecture will:
>
> - Upgrade the runtime and project format to current .NET LTS for long-term support
> - Move core messaging and data integrations to Azure managed services
> - Deploy the worker as a cloud-native Azure workload with managed identity-first authentication
>
> The migration follows a phased approach: runtime upgrade first, Azure service migration second, security remediation third, and deployment last.

---

## Migration Impact Summary

| Application     | Original Service | New Azure Service      | Authentication   | Comments |
|-----------------|------------------|------------------------|------------------|----------|
| ZavaAuditWorker | RabbitMQ         | Azure Service Bus      | Managed Identity | Cloud-native messaging migration |
| ZavaAuditWorker | SQL Server       | Azure SQL Database     | Managed Identity | Cloud-native data service migration |
| ZavaAuditWorker | Local host       | Azure Container Apps   | Managed Identity | Cloud deployment target |

---

## Open Questions & Questionnaire

- [x] Q: Should the plan include environment/infrastructure provisioning? → A: No — no infrastructure provisioning requested; focus on application modernization and deployment workflow.
- [x] Q: Should the plan include integration testing? → A: No — user did not explicitly request integration-test generation.
- [x] Q: Should the plan include security/CVE remediation? → A: Yes — included by default.
- [x] Q: Which Azure deployment target should the plan use? → A: Azure Container Apps (default).
- [x] Q: Should containerization be added as a separate task? → A: No — deployment task already includes containerization for Azure Container Apps.
