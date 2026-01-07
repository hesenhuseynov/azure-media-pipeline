# Azure Media Pipeline (Monorepo)

This repository contains an end-to-end Azure media processing pipeline.

## Projects

- **MediaGateway.Api** (ASP.NET Core)
  - Issues SAS URLs to allow clients to upload media to **raw-media** container securely.

- **media-processor** (Azure Functions .NET isolated)
  - **Service Bus trigger** consumes events and copies blobs from **raw-media** to **processed-media**.

## Architecture

See: `infra/diagrams/architecture.mmd`

High-level flow:

1. Client requests upload initialization from **MediaGateway.Api**
2. API returns a **SAS URL**
3. Client uploads file to **raw-media**
4. **Event Grid** publishes blob-created event
5. Event is forwarded to **Service Bus** queue `q-media-processing`
6. **Azure Function** processes the message and moves/copies file to **processed-media**

## Local run

### Prerequisites
- .NET SDK
- Azure Functions Core Tools
- Azure CLI (`az login`)

### Notes
- `local.settings.json` is **not committed**
- If local Functions host is running, it may consume Service Bus messages (Azure invocations may not appear)

## Troubleshooting
See `docs/runbook.md`
