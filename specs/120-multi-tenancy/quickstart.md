# Quickstart – Tenancy
- Sende alle Requests mit `X-Tenant-Id: acme`
- Deploye je Tenant eigene Modelle
- Tests: Query ohne Header → 400/401; Query mit anderem Tenant → 403
