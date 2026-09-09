# Phase 3 – Sicherheitsnachprüfung und Remediation

Datum: 2026-09-09. Ausgangspunkt: Commit `d45ab78`; die nachfolgend beschriebenen Korrekturen wurden in `a9829e8` committed. Historischer Zwischenbericht.

Aktuelle Fortsetzung: [Restpunkte und Abschlussgrenzen](2026-09-09_Phase3_Restpunkte_Abschluss.md). Die technischen Maßnahmen zu den untenstehenden Punkten 1–5 wurden inzwischen umgesetzt und nachgeprüft; deren ursprüngliche Problembeschreibung bleibt hier als Historie erhalten. Die vollständige Adapter-/Zielumgebungsfreigabe folgt daraus nicht. Echter IdP und unabhängiges Review bleiben offen.

## Status

Phase 3 bleibt offen. Dieser Bericht ersetzt die pauschale Abschlussaussage im Bericht vom 2026-09-08, nicht dessen historische Testergebnisse.

## Implementierte Korrekturen

| Bereich | Korrektur | Regression |
|---|---|---|
| Jobs / Tenant-Claims | `TenantReadOnly` verlangt bekannte Rolle und nichtleeren Tenant; Admin darf ohne Tenant arbeiten. Jobs ohne Claim liefern keine globalen Daten. | `TenantReadOnlyPolicyTests`, `TenantIsolationPhase3SecurityTests` |
| Visual Debugging | Instanz-Tenant vor State, Start und Trace geprüft; jede Sessionoperation prüft die zugehörige Instanz. Mutationen benötigen ProcessManager/Admin. | Fremde Session: neun Operationen plus Start/Trace abgelehnt, keine nachgelagerte Mutation aufgerufen. |
| Management / Worker | Suspend, Resume, Delete benötigen ProcessManager/Admin; Worker-Registrierung und Heartbeat Admin. | Echte HTTP-403-Prüfungen in `PrivilegeGateSecurityTests`. |
| Analytics | Query-/Body-Tenant wird für Nicht-Admins auf den Claim festgelegt; fehlender Claim wird abgelehnt. Training ist Admin-only. | Tenant-gepinnter Export; unscoped Export ohne Servicezugriff abgelehnt. |
| Simulation / Variablen / IO | Tenant-Policy statt bloßer Authentifizierung; leere Claims gelten nicht als Zugriff auf globale Daten. Szenario-Mutationen benötigen ProcessManager/Admin. | Tenant-Controller-Tests, Policy-Tests und HTTP-Mutationsprüfungen. |
| Skriptausführung | Deployment-Prüfung durch Laufzeitprüfung in persistentem und verteiltem Executor ergänzt. C# standardmäßig verboten; `Scripts:Enabled=false` blockiert alle Skripte, auch bei C#-Opt-in. | Direkte Executor-Tests und `PersistentScriptSecurityTests` mit SQLite: gespeicherte Definition beim Start blockiert; nach User-Task-Wait entzogene Freigabe blockiert Ausführung und erzeugt Incident/Suspended ohne Skriptvariablen. |

Produktionsdateien: `src/VertexBPMN.Api/Controllers/`, `src/VertexBPMN.Api/Security/SecurityConfiguration.cs`, `src/VertexBPMN.Infrastructure/Scripting/ScriptTaskExecution.cs`, `src/VertexBPMN.Engine/Execution/{PersistentProcessExecutionRuntime,DistributedProcessEngine}.cs`.

Tests: `tests/VertexBPMN.Tests/Unit/Api/{TenantIsolationPhase3SecurityTests,TenantReadOnlyPolicyTests}.cs`, `Integration/Api/PrivilegeGateSecurityTests.cs`, `Integration/Handlers/ScriptTaskTests.cs`, `Acceptance/PersistentScriptSecurityTests.cs`.

## Tatsächlich ausgeführte Prüfung

- Release-Build des Core-Testprojekts erfolgreich: 0 Fehler, 23 Warnungen. Keine Aussage über Warnungsfreiheit.
- Abschließende gezielte Suite: **55 Tests, 55 bestanden, 0 Fehler, 0 Skips** (`TestResults/phase3-security-final-targeted.xml`).
- Erster voller Core-Lauf: **894 Tests, 887 bestanden, 0 Fehler, 7 Skips**. Dieser Lauf liegt vor den letzten Tenant-/Debug-Ergänzungen und ist kein finaler Gesamtnachweis.
- Finaler Core-Lauf: **907 Tests, 900 bestanden, 0 Fehler, 7 Skips**, 148,773 Sekunden (`TestResults/phase3-security-final-full.xml`). Die sieben externen Infrastrukturtests wurden mangels Connection-Konfiguration übersprungen und sind nicht bestanden. Keine Testfehler ausgeblendet.
- Keine neuen UI-/Browser-, gRPC-, echten IdP- oder externen Infrastruktur-Abnahmen in diesem Durchlauf. Keine erneute Dependency-Audit-Aussage. Lokale TestResults sind keine eingecheckten Release-Artefakte.

## Ursprünglich offene Arbeit in Umsetzungsreihenfolge (historischer Stand)

1. **Connector-Nachweis (M1/M5, Muss, M):** DNS und Socket-Verbindung als kontrollierbare Transportabhängigkeiten isolieren, dieselbe produktive Handler-Registrierung verwenden; öffentliche erste Auflösung, private zweite Auflösung und öffentliche erfolgreiche Verbindung prüfen. Gegenwärtiger Redirect-Test spiegelt nur eine Handler-Einstellung, Rebinding-Test weist lediglich Loopback-Abweisung nach. Kein vollständiger Rebinding-Nachweis.
2. **OAuth2-Bindung (M6, Muss, M):** Browser-/Studio-Callback-Topologie festlegen; State an initiierende Sitzung/Identität binden und atomar einmalig konsumieren. Fremder Benutzer, fremder Browser, Ablauf und paralleler Callback müssen scheitern. Studio startet den Flow serverseitig (`HttpCredentialService`), deshalb genügt ein nur von der API gesetztes Cookie nicht.
3. **Webhook-Replay (M4, Muss, M):** Signiertes Zeitfenster und Delivery-ID mit persistenter, tenant-/triggergebundener Unique-Constraint und atomarer Reservierung implementieren; wiederholte und parallele Zustellung sowie alte Signaturen testen. Signaturformat/Übergang für bestehende Sender explizit dokumentieren; reine In-Memory-Nonce-Liste reicht bei zwei API-Replikaten nicht.
4. **Export-Redaktion (M3, Muss, M):** Roh-XML-/Export-Routen inventarisieren; Credential-Referenzen erhalten, eingebettete Secrets nach definierter Export-Policy maskieren. Export und erneuter Import dürfen keine irreführende ausführbare Secret-Konfiguration erzeugen; Negativtests auf Original-Secret in Response und Log.
5. **Adapter-Abnahme (Req 1/4, Muss, M):** Rollen-/Tenant-Matrix gegen Studio, SDK, gRPC und weitere aktive Adapter verifizieren; aktuelle API-Tests nicht pauschal auf alle Adapter übertragen. C#-Opt-in ist ausdrücklich keine Sandbox.
6. **Echter IdP (Req 2, Muss, M):** Produktiven Anbieter, Authority, Audience, Rollen-/Tenant-Mapping und Callback-URLs festlegen. Login/Logout, Ablauf, Rollenentzug und verweigerte Zugriffe gegen diesen Anbieter prüfen. Keine Secrets in Dokumentation.
7. **Unabhängiges Review (Req 6, Muss, extern):** Finalen, reproduzierbaren Kandidaten durch unabhängigen Reviewer prüfen lassen; Findings und Freigabe nachvollziehbar ablegen. Hängt von den vorigen Punkten ab.

Keine vollständige Sicherheits- oder Produktionsfreigabe aus grünen Core-Tests ableiten.
