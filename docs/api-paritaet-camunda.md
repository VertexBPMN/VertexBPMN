# VertexBPMN vs. Camunda REST-API: Paritätsmatrix

| Camunda Endpoint                | VertexBPMN-Status | Bemerkung / Mapping / TODO |
|---------------------------------|-------------------|---------------------------|
| /process-definition             | Teilweise         | RepositoryController, aber Filter/Paging/DTOs fehlen |
| /process-instance               | Teilweise         | RuntimeController, aber Camunda-typische Query/DTOs fehlen |
| /task                           | Teilweise         | TaskController, aber Filter, Paging, DTOs, Variablen fehlen |
| /history/task                   | Teilweise         | HistoryController, aber Query/DTOs fehlen |
| /deployment                     | Teilweise         | RepositoryController, aber Camunda-typische Response fehlt |
| /message                        | Fehlend           | Muss ergänzt werden |
| /signal                         | Fehlend           | Muss ergänzt werden |
| /job                            | Fehlend           | Muss ergänzt werden |
| /incident                       | Fehlend           | Muss ergänzt werden |
| /variable                       | Fehlend           | Muss ergänzt werden |
| /user, /group, /authorization   | Teilweise         | IdentityController, aber Camunda-typische Endpunkte fehlen |
| /filter                         | Fehlend           | Muss ergänzt werden |
| /decision-definition            | Teilweise         | DecisionController, aber Camunda-typische Query/DTOs fehlen |
| /decision-instance              | Fehlend           | Muss ergänzt werden |

**Nächste Schritte:**
- [ ] Camunda-typische DTOs für Task, ProcessInstance, Variable, etc. einführen
- [ ] Filter, Paging, Query-Parameter nach Camunda-Semantik ergänzen
- [ ] Fehlende Endpunkte (z.B. /message, /signal, /job, /incident, /variable, /filter, /decision-instance) implementieren
- [ ] OpenAPI/Swagger-Doku an Camunda-API angleichen
- [ ] Migrationstabelle und Mapping-Doku bereitstellen
