# GS05: profilgebundene Hosting-HTTP-Routen

Stand: 13. Oktober 2026. Branch: `codex/git-generic-servers`; Ausgangscommit: `5a172616cd41956c3a628f1bb6c98ae9d9aad03a`. Die Übergabe an DeepSeek bleibt auf Wunsch des Nutzers verschoben.

## Änderung

Der profilgebundene GitHub-HTTP-Client erlaubte bislang jeden Unterpfad innerhalb des angenommenen API-Repositorys. `GitHubHostingHttpPolicy` begrenzt diesen Zugang jetzt zusätzlich auf die tatsächlich implementierten PR-Operationen. Der `TargetGuard` prüft jeden Request vor dem HTTP-Handler und damit vor DNS beziehungsweise Socketkontakt. Diese Prüfung gilt auch bei wiederverwendeten Verbindungen.

| Methode | Route relativ zum gebundenen API-Repository | Query |
| --- | --- | --- |
| POST | `/pulls` | Keine |
| GET | `/pulls` | `state=all&per_page=100&page=1…10` |
| GET | `/pulls/{positive kanonische Nummer}` | Keine |
| GET | `/pulls/{positive kanonische Nummer}/reviews` | `per_page=100&page=1…10` |

Andere Repositorys, ähnliche Pfadpräfixe, Actions-/Secret-/Merge-Routen, fremde HTTP-Methoden, zusätzliche Queryparameter, Userinfo, Fragmente, abweichende Hosts/Ports und HTTP werden abgewiesen. Die bestehenden öffentlichen HTTPS-Verträge bleiben erhalten. Die interne Clientfabrik mit Resolver und Repositoryscope ist für isolierte Tests erreichbar; die Produktfabrik verwendet weiterhin ausschließlich die vorher freigegebenen IP-Adressen.

## Nachweise

`GitHubHostingHttpPolicyTests` umfasst 32 neue Fälle:

- Sechs reine positive Routenfälle einschließlich der Paginggrenze.
- Vier positive Clientfälle erreichen den zählenden DNS-Einstieg und werden dort kontrolliert vor jedem Socketkontakt gestoppt.
- 22 negative Clientfälle werfen den erwarteten Sicherheitsfehler; der DNS-Einstieg wird kein einziges Mal aufgerufen.

Release-Build des Testprojekts und vollständiger Solution-Release-Build: jeweils null Warnungen und Fehler. Betroffene Klassen: 299 Erfolge, keine Fehler oder Skips. Bestehende GitHub-Transport-/Response-/Reconciliationklassen: 31 Erfolge, keine Fehler oder Skips. Formatprüfung der beiden neuen Dateien erfolgreich. Lokale Logs liegen unter `artifacts/git-generic-servers/hosting-http-*.log` und werden nicht versioniert.

CI-safe: 1.863 Erfolge, acht bestehende externe Skips, keine Fehler (1:57,777). Die Skips bleiben unperformierte externe Abnahmen.

Buildkommando: `dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers`. Die Editorassets wurden nicht geändert. Die CI-safe Suite folgt unverändert dem Schritt **Test (CI-safe suite)** in `.github/workflows/ci.yml`.

## Begrenzte Clean-Code-Prüfung

Umfang: die beiden neuen C#-Dateien und die geänderten Mitglieder von `SourceControlHttps.cs`. D1–D7 und manuelle Prüfung aller acht Regeln gemäß `.agents/skills/dotnet-clean-code-review/SKILL.md`: keine offenen Befunde in diesem Umfang. Cancellation wird durch den Handler weitergereicht; Handler-/Clientlebensdauer bleibt unverändert; keine neuen Boolean-Modusschalter, öffentlichen Interfaces oder Vererbungsstrukturen. Bestehende Formatierung außerhalb der geänderten Mitglieder wurde erhalten.

## Offene Abnahmen

Diese Tests belegen die Routenbegrenzung vor DNS, keinen realen TLS-/Socket-/Rebinding-/GitHub-Abnahmelauf. GS05 bleibt wegen Audit, öffentlichem Hostingvertrag und kombiniertem API-/Studio-/Recoverypfad offen. Azure-PR-Adapter, SSH, weitere Anbieter und deren getrennte externe Pflichtabnahmen bleiben ausstehend. Aus dieser Änderung folgt keine Anbieter- oder Produktionsfreigabe und keine Website-/Wiki-Veröffentlichung.
