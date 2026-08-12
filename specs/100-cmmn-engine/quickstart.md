# Quickstart – CMMN

1) Lade eine CMMN-XML (Order-Case) und POSTe Deployment.
2) Starte einen Case mit BusinessKey.
3) Triggere Sentry-Events (Message/Timer) und beobachte PlanItem-Transitions.
4) Prüfe History-Events und KPI-Metriken.

Beispiel (REST, Pseudopfad):
- POST /cmmn/deployments
- POST /cmmn/case-definition/key/{key}/start  { businessKey, variables }
- POST /cmmn/case-instance/{id}/signal { eventRef, payload }
- GET  /cmmn/history/case-instance/{id}
