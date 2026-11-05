# LC08 Slice 3/4 — ausgeführter Zwischenstand (keine finale Abnahme)

Branch `codex/low-code-lc00`, Basis `07f5615a`. Vorhandene Slices A+2 und ADR-0008 erhalten. Kein Commit/Push.

## Implementiert und geprüft

- Success-Continuation wird ohne Completed-Job, CompletionId, ResultHash und CompletedAt verworfen. Echter RED: unerwartete UserTask `after`; GREEN: keine Aufgabe/kein Output, Continuation Cancelled.
- Ablauf einer Lease: Heartbeat und Complete liefern im realen SQLite-Service `lease_lost`; vollständige Job/Attempt/Process/Token-Snapshots bleiben unverändert. Bereits vorhandenes Verhalten, kein RED behauptet. HTTP-409-Mapping bleibt vorhandener Controllervertrag.
- `ExternalTaskNodeSession`: expliziter stabiler Wirkungsschlüssel `external-task:{ActivityExecutionId:N}:{EffectName}` unabhängig von Leasegeneration; Completion erst nach bestätigtem Schlüssel; stabile Completion-/Failure-IDs innerhalb der Session; Leaseverlust blockiert Completion/Heartbeat; Business-Fail nutzt eingefrorene Allowlist.
- `ExternalTaskNodeConfiguration`: Credentialreferenz statt Bearerfeld; exakte Tenant-/Topic-/Profil-Bindung, keine Wildcards/Kommalisten; ClaimCommand und Load-Mapper über echte Lease-/Status-DTOs, Generation und Releasebindung geprüft.
- `ExternalTaskProcessStartNode`: separate Startaktion, Route `api/runtime/start`, Tenant/ProcessManager-Schranke in der Vorlage, expliziter IdempotencyKey.
- Unabhängige dateibasierte SQLite-Wirkungsdatenbank: zwei tatsächliche Writes mit UNIQUE EffectKey ergeben eine Fachzeile; echte Recovery/Reclaim nach Leaseablauf (simulierter Zustandsverlust) bzw. technischem Fail, Generation 1→2; doppelte Completion und zweimal Continuationpump ergeben eine UserTask. Kein OS-Kill und kein echter HTTP-Antwortverlust behauptet.

## Ausführungsnachweise

Sanitized Logs: `/home/azureuser/.hermes/cache/scratch/lc08-slice34/`.

Direkter Test-Apphost `tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests -method <Filter>`; exakte Filter und Total/Errors/Failed/Skipped/NotRun in `suite-results.json`, vollständige Einzelprotokolle in `final-<Klasse>.log`.

Behavioral RED→GREEN: `red1.log` → `green1.log`; `red2.log` bis `red13.log` → jeweilige `green*.log`. Compiler-/Analyzerfehler waren Setupfehler und sind nicht RED. Die DB-Replay- und Lease-Snapshot-Tests sind zusätzliche Regression, kein behaupteter RED.

Letzter vollständiger Dependency-/Testprojektbuild mit Analyzern: `build-green13.log`, 0 Warnungen/0 Fehler. Baseline A+2: 80/0/0; finale Suiten: LeaseService 18, Resolver 23, ApiContract 13, Worker 6, Persistence 13, SchedulingConcurrency 8, Completion 13, NodeSemantics 15; jeweils Failed/Skipped/Errors 0.

## Nicht abgeschlossen — innerhalb des beauftragten Umfangs

Die Vorlage ist noch keine vollständige produktive Slice-3/4-Integration:

- `ConfirmBusinessEffect(string)` ist eine lokale Adapterbestätigung, kein serverseitig verifizierter Fachreceipt. Ein unehrlicher Adapter kann die Bestätigung behaupten; direkte HTTP-Completion kann keine unabhängige externe Fachwirkung überprüfen. Die Engine-Schranke bestätigt nur ihren eigenen Completionreceipt. Es fehlt ein definierter vertrauenswürdiger Effect-Owner-/Receiptvertrag für serverseitige Bestätigung.
- Der Wirkungsschlüssel ist in der .NET-Session, noch nicht als persistierte fachliche Effect-Receipt-Zeile im produktiven Weiterführungspfad; UNIQUE fachlicher EffectKey ist bisher im unabhängigen Testdienst nachgewiesen.
- Keine echte HTTP-Fachservice-Antwortverlustprüfung; simulierter Verarbeitungsausfall durch Leaseablauf statt OS-Prozessabsturz. Node-RED-Kill/Restart ausdrücklich nicht durchgeführt.
- Kein ausführender Claim→Get-/Status-/Start-Adapter, nur Vertragsmapper/DTOs; Standard `OAuthClientCredentialsTokenProvider` weiterhin bestehend, aber nicht an diese Vorlage angeschlossen. Kein neuer OIDC-Authnachweis.
- Konfiguration validiert nicht alle Endpoint-/Identitäts-/Grenzfälle; Profilversion/Leasezeiten und Technical-Fail-Code-Allowlist sind noch nicht vollständig in der Vorlage gefenced. Status beschränkt sich bisher auf den bestehenden workergebundenen API-Vertrag; eigener Statusaktionsmapper fehlt.
- Startvorlage verlangt ProcessManager; die existierende RuntimeController-Route erzwingt diese Rolle selbst nicht. Vorlage ist somit eine strengere Adapterregel, keine API-Authhärtung.
- Finale LC-AC-10..12, PostgreSQL-Replica/Konkurrenz, Admin/Edit/Deploy, Secret-/Sandbox-/Paketprofile bleiben offen; npm und Node.js wurden nicht angelegt.

`git diff --check` war im abschließenden Suiteaufruf sauber. Neue Dateien LF. Fremde `.agents/skills` und `skills-lock.json` nicht verändert.
