# Git GS02: HTTPS-Auftragsannahme und Wiederaufnahme

Stand: 11. Oktober 2026. Branch `codex/git-generic-servers`, Basis `02ebe35`, lokale Änderungen. Fortsetzung des [generischen Git-Plans](2026-10-09_Git-Generische-Server-Unterstuetzung_Implementierungsplan.md) und der [administrativen Netzwerk-/CA-Profile](2026-10-11_Git-GS03-GS04_Adminprofile-und-einmalige-Netzwerkfreigabe.md).

## Ergebnis und Grenze

Ein vorbereiteter interner Annahmepfad speichert profilgebundene HTTPS-Leseaufträge dauerhaft. Er friert den vollständigen Zielvertrag einschließlich Profilrevision, Credentialreferenz, Netzwerkpolicy und öffentlichem CA-Bündel ein. Idempotenz umfasst sowohl Auftragsinhalt als auch diesen administrativen Kontext. Eine erneute Annahme desselben Schlüssels mit geändertem Inhalt oder geänderter Policy wird abgewiesen.

Der eigenständige Ausführungsvertrag **Version 2** verändert die Bedeutung des bereits vorbereiteten Zielvertrags Version 1 nicht. Die neue Data-Protection-Purpose bindet den Beleg an Tenant, Actor, Repository, Auftrags-ID, Auftragstyp, Idempotenzschlüssel und Requesthash. Beim Rekonstruieren werden zusätzlich der entschlüsselte Requesthash, aktuelle Grants/Rollen, Binding-/Profilrevision, Profilwiderruf und der vollständige aktuelle Netzwerk-/Trustkontext geprüft. Die Persistenz verwendet die bereits vorbereiteten Spalten; diese Fortsetzung benötigt keine weitere Migration.

**Dieser Annahmepfad wird noch nicht von API, Studio oder Produktprovider aufgerufen.** Der bestehende Claimpfad und der aktive Jobworker akzeptieren weiterhin ausschließlich den Legacyvertrag Version 0. Version-2-Aufträge bleiben daher unausführbar. GS02 und GS04 sind insgesamt nicht abgenommen. Die Tests belegen Annahme und Rekonstruktion mit SQLite, keinen vollständigen Worker-Neustart mit erfolgreicher Git-Ausführung.

## Geänderte Dateien

| Datei | Verantwortung |
| --- | --- |
| `src/VertexBPMN.Infrastructure/SourceControl/AcceptedGitHttpsExecution.cs` | Getrennter versionierter Kontext aus Zielvertrag und administrativem Snapshot; verpflichtende JSON-Felder |
| `src/VertexBPMN.Infrastructure/SourceControl/GitHttpsExecutionProtection.cs` | Operationsgebundener geschützter Beleg, begrenzte Serialisierung und sichere Fehler bei ungültigem Format/Version/Schutz |
| `src/VertexBPMN.Infrastructure/SourceControl/PersistentSourceControlStore.HttpsExecution.cs` | Interne transaktionale Read-Annahme, kontextgebundene Idempotenz und aktuelle Prüfung bei Rekonstruktion |
| `src/VertexBPMN.Infrastructure/SourceControl/GitHttpsPolicyRegistry.cs` | Gemeinsame aktuelle Prüfung eines gespeicherten administrativen Snapshots ohne DNS-/Credentialauflösung |
| `tests/VertexBPMN.Tests/Unit/Application/SourceControlConnectionBindingTests.HttpsExecution.cs` | 31 neue lokale Regressionfälle |

## Persistenz- und Sicherheitsvertrag

- Annahme ausschließlich für HTTPS mit Anonymous oder HttpBasic und exakt erforderlicher Read-Capability; ein Commitziel kann nicht als Readziel angenommen werden.
- Auftragsbytes und Rollen werden vor dem ersten Await kopiert. Die Registry erzeugt kopierte administrative CIDR-Snapshots. Profil- und Endpunktdaten bestehen aus unveränderlichen Records und skalaren Werten.
- Read-Request maximal 3 MiB; Ausführungs-JSON maximal 512 KiB; geschützter Kontext maximal 768 KiB Zeichen. Der Kontext benötigt wegen des erlaubten 64-KiB-CA-Bündels und JSON-Escaping eine separate Grenze vom Version-1-Zielbeleg.
- Secrets, Credentialleases und DNS-Adressen werden nicht gespeichert. Credentials und DNS-Freigabe müssen vor der späteren Ausführung erneut aufgelöst werden.
- Wiederaufnahme verwendet den ursprünglichen geschützten Kontext. Änderungen an administrativer Policy oder CA werden auch ohne erhöhte Revision erkannt; neue Ziele werden nicht stillschweigend übernommen.
- Der geschützte Request wird gegen den gemeinsam berechneten Hash von Request und Ausführungskontext geprüft. Ein gültiger Requestbeleg eines anderen Auftrags lässt sich nicht unter einen fremden Ausführungskontext setzen.
- Neustartnachweis: neue EF-/Storeinstanz auf derselben isolierten SQLite-Datenbank, identischer Data-Protection-Provider. Persistente Schlüsselbereitstellung über echte Prozess-/Hostneustarts bleibt eine separate Betriebsabnahme.

## Prüfungen

Die Runner- und CI-Auswahl stammen aus `global.json` und `.github/workflows/ci.yml`. Keine Wiederherstellung, Werkzeuginstallation, Editorassetänderung oder Benutzerdatenbankmigration wurde ausgeführt.

| Prüfung | Ergebnis |
| --- | --- |
| Strenger Gesamtbuild, Release, ohne Restore, Editorassets unverändert | 0 Warnungen, 0 Fehler; abschließender Lauf 58,05 Sekunden, siehe `gs02-https-execution-final-build.log` |
| Gezielte Git-/Sicherheitsauswahl im Haupttestprojekt | 285 bestanden, 0 Fehler/Skips, 16,752 Sekunden |
| CI-safe Auswahl mit sämtlichen bestehenden Ausschlüssen | 1.676 bestanden, 0 fehlgeschlagen, 8 externe Skips, insgesamt 1.684; 1 Minute 37,836 Sekunden |
| Check-only Whitespaceprüfung der fünf C#-Dateien | Exit 0 nach manueller Korrektur |
| Scoped Clean-Code-Review | D1–D7 ohne Suchfehler; geänderte Mitglieder und neue Dateien manuell gelesen |

Logs liegen unter `artifacts/git-generic-servers/gs02-https-execution-*`. Der erste Build fand einen fehlenden Namespaceimport, ein weiterer einen unnötigen Null-Forgiving-Operator; beides wurde behoben. Der erste Testlauf erwartete bei fehlender Leserolle `Forbidden`; die vorhandene `RepositoryAccessPolicy` verbirgt fehlenden Lesezugang mit `NotFound`. Ausschließlich diese neue Testerwartung wurde korrigiert; die Zugriffspolitik blieb unverändert. Die Formatprüfung fand Whitespace in neuen Initialisierern und Switchblöcken, anschließend manuell korrigiert.

Die abschließenden Änderungen nach den erfolgreichen Tests betreffen ausschließlich Whitespace/XML-Kommentarformatierung; danach wurde die gesamte Lösung erneut streng gebaut. Die native HTTPS-Suite wurde in dieser Fortsetzung nicht erneut ausgeführt: native Prozess-, TLS- und Credentialkanaldateien blieben unverändert. Der zuvor beobachtete sporadische C8-FEEL-Fehler trat im aktuellen CI-Lauf nicht auf; seine Ursache bleibt ungeklärt.

Buildbefehl:

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

Gezielte Auswahl:

```powershell
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*GitHttpsPolicyRegistryTests' -class '*GitHttpsNetworkBoundaryTests' -class '*SourceControlConnectionBindingTests' -class '*SourceControlConnectionProfilePersistenceTests' -class '*RepositoryConnectionProfileTests' -class '*SourceControlExecutionTargetTests' -class '*LegacyGitHubConnectionMappingTests' -class '*SourceControlSecurityTests' -class '*SourceControlOptionsTests' -class '*SharedSqliteDbFixtureIsolationTests'
```

CI-safe Befehl: unveränderte vollständige Filter aus dem Schritt **Test (CI-safe suite)** in `.github/workflows/ci.yml`, mit `--configuration Release --no-build --no-restore`. Die acht Skips betreffen bestehende Azure-, PostgreSQL- und Live-TypeSafe-Voraussetzungen; sie stellen keine externe Abnahme dar.

## Clean-Code-Prüfung

Skill `.agents/skills/dotnet-clean-code-review/SKILL.md`. Scope: fünf C#-Dateien dieser Fortsetzung, neue Dateien vollständig und bestehende Registry nur geänderte Mitglieder samt Aufrufern. Basis `02ebe35` und lokale Änderungen. Generierte Dateien ausgeschlossen. D1–D7 mit `rg`; keine Suchfehler oder abgeschnittenen Kandidatenausgaben. D8: alle zwölf Lesbarkeitskategorien manuell geprüft.

| Regel | Ergebnis und Evidenz |
| --- | --- |
| 1 Async/Cancellation | PASS: awaitbare Verträge mit Async-Suffix, Token für Transaktion/Abfragen/Speichern; keine async-void-Methode; Abbruchtest ohne gespeicherten Auftrag |
| 2 Collection-Verträge | PASS: Rollen vor Await kopiert; bounded administrative Snapshots; keine neue verzögerte Collection-Rückgabe |
| 3 Boolean-Modi | PASS: keine bool-Modusschalter |
| 4 Verantwortungsnamen | PASS: Ausführungskontext, Schutzcodec, Store und Policyregistry getrennt |
| 5 Regions | PASS: keine neuen Regions |
| 6 Interfaces/Boundaries | PASS: keine neuen Interfaces; vorhandene Persistence-/Protection-/Options-Grenzen beibehalten |
| 7 Vererbung/Komposition | PASS: keine neue Vererbungshierarchie; bestehende Partialklasse ergänzt |
| 8 Lesbarkeit | PASS: frühe Ablehnung, begrenzte Methoden, Versions-/Größenkonstanten; neue Formatbefunde korrigiert |

Keine bestätigten verbleibenden Clean-Code-Befunde in diesem Scope. Diese Prüfung ersetzt keine Transport- oder externe Sicherheitsabnahme.

## Offene Arbeit

1. Den gespeicherten Version-2-Kontext mit profilgebundenem Claiming, Executor und frischer Actorauflösung verbinden. Vor DNS muss bereits der ursprüngliche Netzwerk-/Trustkontext gegen die aktuelle Registry geprüft sein; vor I/O folgen erneut Autorisierung und Credentialprüfung.
2. Aktive ältere Worker vor Freigabe drainen/stoppen und kompatibel aktualisieren. Die neuen Workerfilter können bereits laufende alte Binärversionen nicht nachträglich schützen.
3. Parallele Annahme, relationale Transaktionskonflikte und echte Prozess-/Hostneustarts mit persistenten Schlüsseln gesondert abnehmen. Der interne Pfad nutzt Serializable-Transaktionen und den bestehenden Unique-Key; dieser Teilnachweis enthält keine externe Mehrprozess-Abnahme.
4. Generische Branch-/History-/Diffpfade sowie Commit, Ref-CAS-Push und Unknown-Reconciliation vervollständigen; danach API und Studio aktiv anbinden.
5. Pflichtabnahmen am echten eigenen HTTPS-Server und anschließend Azure DevOps, Hosting und SSH gemäß Plan. Website und Wiki erst um den tatsächlich belegten Supportumfang erweitern.

Kein Commit, Push, Deployment, externer Testwrite oder Veröffentlichung ausgeführt.
