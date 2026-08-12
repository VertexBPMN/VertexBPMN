# Data Model – Standard Tasks

ConnectorTemplate (Id, Type[http|email|script], Name, Config(json), TenantId)
ConnectorSecretRef (Id, TemplateId, SecretName, TenantId)
TaskInvocationLog (Id, InstanceId, TaskKey, StartedAtUtc, EndedAtUtc, Status, RedactedRequest, RedactedResponse, TenantId)

Indizes
- template: (tenant_id, type, name)
- log: (instance_id, task_key, started_at_utc)
