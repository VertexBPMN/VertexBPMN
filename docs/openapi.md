# VertexBPMN OpenAPI/Swagger Dokumentation

Die Datei `openapi.json` beschreibt die gesamte REST-API der VertexBPMN-Engine im OpenAPI 3.0-Format. Sie ist kompatibel mit:
- bpmn.io (bpmn-js, dmn-js, form-js)
- Camunda Modeler
- Swagger UI, ReDoc, Postman, u.v.m.

## Nutzung

- **Swagger UI:**
  - Starte die API (`dotnet run -p VertexBPMN.Api`)
  - Rufe `http://localhost:5000/swagger` im Browser auf
- **bpmn-js/dmn-js/form-js:**
  - Verwende die Endpunkte `/process-definition/{id}/xml`, `/decision-definition/{key}/xml`, `/task/{id}/form-schema` für Import/Export
- **Postman:**
  - Importiere `openapi.json` als Collection
- **Automatisierte Tests:**
  - Nutze die OpenAPI-Datei für Contract-Tests und Mocking

## Endpunkt-Highlights

- **BPMN-XML:**
  - `GET/PUT /camunda/process-definition/{id}/xml`
- **DMN-XML:**
  - `GET/PUT /camunda/decision-definition/{key}/xml`
- **User-Task-Formulare:**
  - `GET/PUT /camunda/task/{id}/form-schema`

## Hinweise
- Die API ist Camunda-kompatibel und für moderne Cloud-Workflows optimiert.
- Die OpenAPI-Datei wird bei jedem Build automatisch aktualisiert.

---

*Letztes Update: 2025-08-12*
