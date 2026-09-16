# External Tasks und Agent-Vertragsprüfung – A06-Prüfbericht

Stand: 2026-09-15
Branch: `codex/external-agent-a00-inventory`
Status: Implementiert und real lokal abgenommen

## Umgesetzter Umfang

- Tenantgebundener, read-only Profilkatalog unter `api/external-task-operations/catalog`. Die Antwort enthält nur Profil-, Topic-, Limit- und Feldmetadaten; keine Provider-Endpunkte, Credentials oder Secrets.
- Prozessgebundene Betriebssicht unter `api/external-task-operations/process/{processInstanceId}`. Es gibt bewusst keine globale External-Task-Jobliste. Die Antwort enthält keine Input-/Result-Payloads, Lease-ID oder Workeridentität.
- Bestehendes Studio erweitert: `vertex:ExternalTask` im Moddle, Agentprofil und Limits im Properties Panel, automatische Inputs aus dem Tenantprofil, ein atomarer `result`-Output sowie das Muster „Agent contract review → human review“.
- Das Muster erzeugt immer einen nachgelagerten Human User Task. Providerkonfiguration und Secrets werden nicht in BPMN-XML geschrieben.
- Clientvalidierung für Topic, Profil, Retries, Deadline und gemischte Connector-/External-Task-Konfiguration; die bestehende serverseitige Deploymentvalidierung bleibt maßgeblich und fail-closed.
- Bestehende Seite „Execution Details“ zeigt Status, Versuche, Deadline und Fehlercode pro Prozessinstanz. `Ready` und `RetryScheduled` werden ausdrücklich als wartende Workerzustände dargestellt.
- OpenTelemetry-Messpunkte für Queue-Alter, Lease-Verlust, Retry, Laufzeit und Schemafehler sowie Worker-Messpunkte für Reviewlaufzeit, Budget- und Schemafehler. Modellgemeldete Tokenwerte werden ausdrücklich nicht als Abrechnungsdaten exportiert.
- Der lokale reale Studio-E2E-Host stellt einen isolierten Agent-Tenant und den Vertragskatalog bereit; ein E14-Test für Preset, Deployment, persistierten Wait und Betriebssicht ist vorhanden.

## Sicherheitsgrenzen

- Die neuen Operator-Endpunkte verwenden `TenantReadOnly`; Cross-Tenant-Anfragen werden verweigert.
- Worker-API und Operator-API bleiben getrennt. Die Operator-API akzeptiert keine Mutationen.
- Jobpayload, Ergebnis, Schemasnapshot, Lease-ID, Issuer und Worker-Subject verlassen die Operatorabfrage nicht.
- Profile werden exakt nach Tenant geladen. Ohne ausgewählten Tenant oder verfügbares Profil lässt sich das Preset nicht einfügen; ohne gültigen Katalogeintrag lehnt der Server das Deployment ab.
- Die UI behauptet keine Worker-Onlineerkennung. Ein `Ready`-Job ist nur als „waiting for worker“ sichtbar; Workerpräsenz wird nicht erfunden.

## Nachweise

Erfolgreich:

- `npm run build:bpmnio`: 9 Bundles erzeugt, Descriptor kopiert.
- `npm run test:vertex-moddle`: Roundtrip einschließlich `vertex:externalTask`; Secret-/Providerattribute ausgeschlossen.
- `node tools/bpmn-io/test/flow-validation.test.mjs`: 9/9 bestanden.
- Release-Builds API, Studio und AgentWorker: jeweils 0 Fehler; Produktprojekte 0 Warnungen.
- External-Task-/Agent-/Operatorauswahl einschließlich Deploymentvertrag: 43/43 bestanden, 0 fehlgeschlagen, 0 übersprungen. Im übergeordneten Testprojekt bleiben bereits vorhandene Analyzerwarnungen außerhalb A06 sichtbar.
- Lokaler Headless-Chromium-Test `BpmnEditorInsertionTests`: 1/1 bestanden. Er beweist Preset-XML, Human-Review-Schritt, Roundtrip, Clientvalidierung sowie Undo/Redo; bestehende Muster bleiben grün.

Reale WSLC-Läufe:

1. Der erste Lauf scheiterte fail-closed, weil kein Tenant ausgewählt war und daher kein Agentprofil geladen wurde. Der Test wählt nun explizit den isolierten Agent-Tenant.
2. Der zweite Lauf erreichte den echten API-Deploy und wurde mit HTTP 400 abgewiesen. Ursache war das vom Preset erzeugte Mapping `${document}`, während der serverseitige External-Task-Vertrag direkte Variablennamen verlangt. Das Preset erzeugt nun `expression="document"` und der isolierte Browsertest sichert diesen Vertrag.
3. Der danach angeforderte Wiederholungslauf konnte wegen einer fehlgeschlagenen automatischen Freigabe des lokalen WSLC-/Browser-Prozessstarts nicht ausgeführt werden. Er darf nicht als grün oder als E14-Abnahme gezählt werden.
4. Lauf `aaed23f5ad824801b46464ef9a85f825` deckte einen zweiten echten Vertragsfehler auf: Das Preset erzeugte je Schemafeld ein Output-Mapping, obwohl der Worker das vollständig schema-validierte Ergebnis atomar als `result` liefert. Das Preset und das Properties Panel erzeugen nun genau `result=contractReview`; die Clientvalidierung weist abweichende Mehrfachausgaben zurück.
5. Lauf `f516fadd1e484ed89a7b99a2264cf14f` bewies bereits Deployment, Prozessstart und Jobpersistenz. Die Betriebssicht blieb leer, weil der Test durch einen erzwungenen Full-Page-Reload den sitzungsgebundenen Tenant verlor und das MudBlazor-Feld ohne Change/Blur auslas. Der Test verwendet nun den echten Studio-Navigationslink im bestehenden Blazor-Circuit und bestätigt die Eingabe per Tab.
6. Finaler Lauf `482343e581a44f718b527e5bf48b3e39`: **1/1 bestanden, 0 fehlgeschlagen, 0 übersprungen** in 31,485 s. Nachgewiesen sind WSLC/PostgreSQL, reale API und Studio, Preset, Deployment, Prozessstart, persistierter `Ready`-Job und die prozess-/tenantgebundene Anzeige „Ready — waiting for worker“.

## Reproduzierbare Abnahme

Der erfolgreiche Nachweis ist lokal reproduzierbar mit:

```powershell
./scripts/test-studio-e2e.ps1 -Infrastructure Wslc -SkipBuild -TestMethod ExternalAgentPreset_DeploysAndShowsProcessScopedWaitingJob
```

Preset, reales API-Deployment, PostgreSQL-Jobpersistenz und Studio-Betriebsanzeige sind damit gemeinsam nachgewiesen. Die anschließende echte Ollama-Ausführung mit Human-Review-Ergebnis gehört zur A07-Gesamtabnahme und ist mangels lokal verfügbarer Ollama-Runtime weiterhin offen.

## Urteil

A06 und E14 sind technisch implementiert und real lokal abgenommen. Daraus folgt noch keine Produktionsfreigabe des Gesamtfeatures: A07 mit realer Ollama-Fachabnahme, vollständiger E01–E14-Gesamtregression und sauberem Checkout bleibt offen.
