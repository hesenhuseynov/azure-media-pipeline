# Runbook

## Common issues

### Local host consuming Service Bus messages
If VS Code / local Functions host is running, it may consume messages and Azure portal won't show invocations.

### 401/403 on Storage/Service Bus
RBAC propagation can take a few minutes. Verify Managed Identity roles.

### DLQ
Messages go to DLQ after MaxDeliveryCount retries (unless explicitly dead-lettered).