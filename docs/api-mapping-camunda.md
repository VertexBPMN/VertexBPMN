# VertexBPMN API Mapping-Tabelle

| Camunda Endpoint                | VertexBPMN Endpoint                | DTO/Mapping                | Status      |
|---------------------------------|------------------------------------|----------------------------|-------------|
| /process-definition             | /api/vertex/process-definition     | ProcessDefinitionDto       | Fertig      |
| /process-instance               | /api/vertex/process-instance       | ProcessInstanceDto         | Fertig      |
| /task                           | /api/vertex/task                   | TaskDto                    | Fertig      |
| /history/task                   | /api/vertex/history/task           | HistoricTaskInstanceDto    | Fertig      |
| /deployment                     | /api/vertex/deployment             | DeploymentDto              | Fertig      |
| /message                        | /api/vertex/message                | MessageCorrelationResultDto| Fertig      |
| /signal                         | /api/vertex/signal                 | -                          | Fertig      |
| /job                            | /api/vertex/job                    | JobDto                     | Fertig      |
| /incident                       | /api/vertex/incident               | IncidentDto                | Fertig      |
| /variable                       | /api/vertex/variable               | VariableValueDto           | Fertig      |
| /user                           | /api/vertex/user                   | UserDto                    | Fertig      |
| /group                          | /api/vertex/group                  | GroupDto                   | Fertig      |
| /authorization                  | /api/vertex/authorization          | AuthorizationDto           | Fertig      |
| /decision-definition            | /api/vertex/decision-definition    | DecisionDefinitionDto      | Fertig      |
| /decision-instance              | /api/vertex/decision-instance      | DecisionInstanceDto        | Fertig      |

**Hinweise:**
- Paging, Filter, OpenAPI/Swagger und Batch-APIs werden als nächstes ergänzt.
- Die Endpunkte sind für Camunda-Clients kompatibel, Mapping erfolgt über DTOs.
- Migration und Integration sind mit minimalem Anpassungsaufwand möglich.
