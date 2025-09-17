# Spec – Standard Tasks

HTTP Task
- Methoden: GET/POST/PUT/PATCH/DELETE
- Auth: Basic, Bearer, mTLS (optional)
- Mapping: request.variables → body/headers/query; response → variables
- Sicherheit: Header-Redaction, Max-Payload, Timeout, Retry, 429/5xx Backoff

Email Task
- SMTP/STARTTLS, Templates (Razor/Handlebars), Attachments, CC/BCC
- Resultat in Variablen (messageId, accepted, rejected)

Script Task
- Engine: C# Roslyn Script oder JS (Jint) in Sandbox
- Zugriff: read-only Process Variables + scoped helpers
- Limits: Timebox, Memory cap, No IO

Gemeinsam
- Fehlerstrategie: BPMN Error vs. Incident, Retry Policy
- Tenancy: Secrets/Configs tenant-scoped
