# Git GS07: dauerhafte SSH-Auftragsannahme und Claiming

Stand: 13. Oktober 2026, Entwicklungsbranch `codex/git-generic-servers`. Fortsetzung des [gemeinsamen Azure-/SSH-/Audit-Berichts](2026-10-13_Git-GS06-GS07_Azure-SSH-und-Audit.md).

## Implementierter Umfang

Der interne SSH-Read-Vertrag verwendet `ExecutionContractVersion = 4`. `AcceptedGitSshExecution` enthält das vollständige akzeptierte Repository-/Profilziel sowie die administrative Netzwerk- und Hostschlüsselkonfiguration. DNS-Adressen, private Schlüssel und Credentialleases sind nicht Teil des dauerhaften Vertrags. Eine zusätzliche Datenbankmigration ist nicht erforderlich; die bestehenden Auftragsfelder werden verwendet.

`GitSshPolicyRegistry` löst ausschließlich Tenant-/Profilreferenzen gegen die aktuelle Deploymentkonfiguration auf. Referenzen und Scope werden für Netzwerk und Hostschlüssel getrennt geprüft; Listen werden kopiert. `GitSshExecutionProtection` bindet den verschlüsselten Vertrag an Tenant, Repository, Actor, Operation, Art, Idempotenzschlüssel und Requesthash. Der Fingerprint enthält den Vertrag und die vollständige kanonische Leseanforderung. Derselbe Idempotenzschlüssel mit anderen Inhalten oder Regeln wird abgewiesen.

`EnqueueSshReadAsync` nimmt intern feste Commit-Reads und typisierte Branch-/File-/History-/Diff-Anforderungen an. Wiederaufnahme decodiert und prüft die ursprünglichen Anforderungen, das aktuelle Profil, Repositoryrechte sowie die vollständigen Netzwerk-/Hostkeyregeln. Ein neuer Kontext übernimmt nicht stillschweigend geänderte Regeln. `TryClaimSshReadAsync` verwendet die bestehenden gemeinsamen Gesamt-/Tenantquoten, Serializable-Transaktion, Lease und Fence. Ein älterer Worker kann weder den SSH-Auftrag übernehmen noch einen späteren Fence abschließen.

`ProfiledGitSshTransport` verbindet einen intern geclaimten Auftrag mit DNS-Freigabe und dem vorbereiteten nativen SSH-Transport. Nach DNS wird der Anspruch erneut gegen gespeicherten Inhalt, Profil, Rechte und Lease geprüft. Die Vorbereitung hat ein begrenztes Zeitbudget; Aufruferabbruch bleibt von internem Timeout getrennt. Erst danach werden Zugangsdaten aufgelöst. Weitere Prüfungen vor und nach dem nativen Prozess schützen gegen Änderung während der Ausführung. Workspace-ID und Fence müssen zum Auftrag gehören.

## Grenzen

Die Annahme- und Claimmethoden sind intern. Der produktive Hosted Worker selektiert weiterhin nur Legacy-, HTTPS- und Hostingverträge; er selektiert Version 4 absichtlich noch nicht. Öffentliche Job-/API-/Studioannahme, dauerhafte Ergebnisprojektion und der ausführende SSH-Lebenszyklus mit Heartbeats und Cleanup müssen noch geschlossen werden. Es wird keine Fähigkeit über die Verfügbarkeitsansicht angekündigt. SSH-Commit/Push, Passphrase, Orphan-Recovery sowie reale Windows-/Linux-Serverabnahmen bleiben offen.

Lokale SQLite-/Vertragstests ersetzen keinen nativen Server-/Browser-/Recoverylauf. Die früher dokumentierte C.8.0-FEEL-Instabilität ist von diesem Paket nicht als behoben zu betrachten. Azure benötigt weiterhin reale PAT-/Entra-/Policyabnahmen; Audit benötigt zusätzliche Mehrreplikat-/Recovery-/Langzeitabnahmen.

## Prüfung

Vollständiger Release-Build mit unveränderten Editorassets und dem bestehenden Asset-Skip-Schalter: null Warnungen und Fehler (2:40,32). Der erste Build war an S8970 in `BpmnViewerDialog.razor` gestoppt: Der bestehende nicht-nullbare injizierte Dialog verwendete `default!` ohne expliziten Nullable-Kontext. Genau eine `#nullable enable`-Zeile im Codeblock stellt den vorgesehenen Kontext her; Dialogablauf und ursprüngliche Zeilenenden bleiben erhalten. Kein Analyzer wurde abgeschaltet.

308 gezielte Tests erfolgreich, keine Fehler/Skips (62,340 Sekunden). Die 26 neuen Fälle prüfen dauerhafte SSH-Annahme, Neustart, Ausschluss von Legacy-/HTTPS-Claimern, Idempotenz mit geändertem Inhalt/Netzwerk/Hostkey, aktuelle Rechte/Profil/Trust vor Claim, Lease-Recovery/Fence und gemeinsame Jobquoten, verschlüsselte Row-/Requestbindung, Änderungen während DNS und persistente typisierte Branchqueries. Keine reale SSH-Anmeldung ausgeführt.

Check-only-Whitespaceprüfung der zehn neuen Infrastrukturdateien und beiden neuen Testdateien erfolgreich; gemeldete Whitespacekorrekturen wurden ausschließlich in diesen eigenen Dateien angewendet. D1–D7 und alle acht manuellen Clean-Code-Regeln im Umfang dieser zwölf C#-Dateien sowie der beiden DI-Registrierungen geprüft, keine bestätigten Befunde. Razor-Kontextkorrektur separat geprüft; unveränderte Dialogmethoden sind nicht Teil dieses Reviews.

| Regel | Ergebnis im eigenen Umfang |
| --- | --- |
| Async/Cancellation | Awaitbare Async-Verträge; Token durch Actor-, DB-, DNS-, Broker- und Prozesspfad; definierte Lease-/Dateilebensdauer |
| Collections | Administrative Listen als kopierte begrenzte Snapshots; keine neue nullable Collection |
| Boolean-Modi | `Matches` vergleicht Zustand; keine neuen Produktions-Modusschalter |
| Namen | Policyauflösung, Schutz, Annahme, Claiming und nativer Transport nach Verantwortung getrennt; bestehende gemeinsam genutzte Claim-/Quotenprüfungen erhalten |
| Regions/Struktur | Keine Regions; kohäsive Partial-Dateien im bestehenden Store/Transport |
| Interfaces | Bestehende Actor-/Credential-/Optionsports erhalten; keine neuen Interfaces |
| Vererbung | Keine neue Vererbungshierarchie |
| Lesbarkeit | Request-/Policybindung, Guards und Quoten manuell geprüft; synthetische Testdaten und feste Runnernamen als Testausnahmen |

Logs liegen unter `artifacts/git-generic-servers/ssh-durable-*.log` und werden nicht versioniert. Die CI-safe-Prüfung endet mit 1.950 Erfolgen, einem C.8.0-Fehler und acht bestehenden externen Skips, insgesamt 1.959 Fällen (2:15,987). Das erste protokollierte Problem ist der bestehende FEEL-/Jint-Timeout, danach fehlt Vacation Approval. Beide C.8.0-Tests bestehen isoliert ohne Skips (2,936 Sekunden); dies behebt oder widerlegt den Timeout im breiten Lauf nicht. Die Engine-/DMNimplementierung und Conformanceassertions wurden nicht verändert. Der Gesamtlauf ist nicht grün.
