# G07 – PR-Antwortvalidierung, erster Teilstand

Status: begonnen, nicht vollständig implementiert oder abgenommen. Basis: `4d0764e`, Branch `codex/studio-session-expiry`. Die vorhandenen Dokumentationsänderungen zur Studio-Abnahme bleiben erhalten.

## Umsetzung

`GitHubPullRequestResponse` ist ein interner, noch nicht angeschlossener Antwortdecoder. Er bindet Repository, Head-/Base-Branch, Head-Commit, PR-Nummer und GitHub-Link an den angenommenen Auftrag. Fremde Kontexte, veränderte Heads und inkonsistente Zustände werden mit einem bereinigten Providerfehler abgelehnt. Offene beziehungsweise ungemergte PRs erhalten keinen Merge-Commit aus einem spekulativen `merge_commit_sha`. Dieser Baustein erzeugt keine Releasefreigabe.

Vertrag anhand der [GitHub-Pull-Request-API](https://docs.github.com/en/rest/pulls/pulls) geprüft. Der Decoder erwartet die Detailantwort mit `merged`; Listenantworten sind nicht ohne Detailabfrage gleichwertig.

## Verifikation

- Diagnostischer Build des SourceControl-Testprojekts erfolgreich, 9 bestehende Warnungen, Analyzer deaktiviert. Kein strenger Buildnachweis.
- `dotnet tests/VertexBPMN.SourceControl.Tests/bin/Release/net10.0/VertexBPMN.SourceControl.Tests.dll -class '*GitHubPullRequestResponseTests'`: 8 bestanden, 0 Fehler, 0 übersprungen; 0,466 Sekunden.
- Rein isolierte Antwort-/Vertragstests; keine echte GitHub-Anfrage, kein HTTP-Transportnachweis und kein Browsernachweis für PRs.
- Clean-Code-Prüfung der zwei neuen C#-Dateien: D1–D7 und manuelle Lesbarkeit geprüft, keine bestätigten Befunde. Synchroner Decoder ohne I/O; bool im Test beschreibt Providerdaten, keinen Produktions-Modusschalter. Keine neuen Interfaces oder Vererbungsstrukturen.
- Kein CI-safe-Gesamtlauf. Kein Commit, Push, PR oder Berechtigungsausbau.

## Nächste Schritte

1. Persistente PR-Annahme einschließlich Actor/Tenant, Head/Base, Auftragsschlüssel und Inhaltsbindung anschließen.
2. Autorisierten Hosting-Transport und Vorab-/Nachfehlerabgleich nach gespeichertem Auftrag implementieren; keine blinden POST-Retries bei unbekanntem Ergebnis.
3. Frische Berechtigungsprüfung, begrenzte Requests, Rate-Limit-/Tokenfehler und PR-Audit ergänzen.
4. API-/Studio-Erstellung und Statusanzeige anschließen.
5. Echte Review-/Merge-/Releasefreigabe getrennt implementieren und fail-closed prüfen.
6. Isolierte Remoteabnahme erst mit erforderlichen, ausdrücklich autorisierten GitHub-App-Rechten. Die aktuelle Testinstallation hatte PR-Leserecht; Schreibrecht nicht eigenmächtig erweitern.
# Ergänzung: persistente PR-Annahme

`PullRequestJobSubmission` und `PersistentSourceControlStore.PullRequest.cs` nehmen einen PR-Auftrag ausschließlich aus einem bestätigten, geschützten Push-Ergebnis desselben Actors, Tenants und Repositorys an. Zielbranch und aktuelle PR-Berechtigung werden geprüft; identische Wiederholungen liefern dieselbe Operation, geänderte Inhalte unter demselben Schlüssel werden abgelehnt.

SQLite-Regression `SourceControlPersistenceTests.Pull_request_acceptance_requires_grant_and_confirmed_push_and_is_idempotent`: lokal 1 Test bestanden, 0 Fehler, 0 übersprungen. Der Test erzeugt kontrollierte persistente Push-Belege; er ist kein Nachweis eines tatsächlichen Remote-Pushs oder einer GitHub-PR-Erstellung.

Testprojekt-Diagnosebuild mit `BuildProjectReferences=false`, deaktivierten Analyzern und deaktiviertem Warnings-as-errors: erfolgreich, 195 Warnungen, 0 Fehler. Der vorherige Build mit Projektabhängigkeiten scheiterte an von der laufenden API gesperrten DLLs (MSB3027/MSB3021). Kein erfolgreicher strenger Gesamtbuild und keine vollständige Regression behauptet.

Worker-Decodierung ergänzt: `ReadPullRequestWorkAsync` rekonstruiert den geschützten Auftrag nur bei aktiver, Actor-/Tenant-gebundener Lease mit korrektem Fence. Aktuelle PR-Rechte, Bindungsrevision, Requesthash, Branchgrenzen und vollständiger Headcommit werden vor Übergabe an den späteren Executor geprüft. `AcceptedPullRequestWork` transportiert diesen unveränderlichen Kontext.

Erweiterte lokale Regression: `dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*SourceControlPersistenceTests'` – 18 bestanden, 0 Fehler, 0 übersprungen (12,599 s). Der PR-Test prüft zusätzlich falschen Fence, fremden Tenant und aktuellen Rechteentzug. Infrastruktur-Diagnosebuild: 8 Warnungen, 0 Fehler; Testprojekt-Diagnosebuild ohne Projektneubau: 195 Warnungen, 0 Fehler. Analyzer waren deaktiviert; kein strenger Buildnachweis.

Noch offen: ausführender Worker/Hosting-Provider, Abgleich nach unbekanntem Erstellungsergebnis, API-/Studio-Anschluss und echte GitHub-/Browser-Abnahme. G07 bleibt offen.
