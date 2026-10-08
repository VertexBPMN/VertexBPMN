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

## Weiterarbeit: reiner Abgleich nach unbekanntem Remote-Ergebnis

`GitHubPullRequestReconciliation` ergänzt einen operationsgebundenen Marker für den PR-Text und prüft bereits gelesene PR-Detailantworten. Nur ein eindeutiger Treffer mit passendem Repository, Head/Base und angenommenem Commit wird bestätigt. Fehlender oder mehrdeutiger Treffer bleibt `ResultUnknown`; diese Komponente besitzt keine Create-Funktion. Der spätere Provider muss alle relevanten Seiten und Detailantworten laden und diesen Pfad nach unbekannten Schreibausgängen verwenden. Ein Marker ist kein Freigabe- oder Authentifizierungsnachweis.

Vier neue Regressionen: verlorene Antwort mit eindeutigem Treffer, kein/mehrdeutiger Treffer, fremde Operation/eingebetteter Marker und veränderter Headcommit. Decoder plus Abgleich: 12 Tests bestanden, 0 Fehler, 0 übersprungen. Diagnosebuild mit deaktivierten Analyzern/Warnungsfehlern: 8 Warnungen, 0 Fehler. Kein HTTP-, GitHub- oder Browser-Abnahmenachweis; G07 bleibt offen.

Scoped Clean-Code-Prüfung der zwei neuen Dateien: D1–D7 und manuelle Lesbarkeitsprüfung; keine bestätigten Befunde. Async/Cancellation nicht anwendbar (reine Berechnung); Collection-Eingabe passend; keine Modusflags, unklaren Typnamen, Regions, neuen Interfaces oder Vererbung. Git-Diff-Whitespace-Prüfung erfolgreich; kein vollständiger Formatter-/Analyzer-Nachweis.

## HTTP-Transport – weiterer Zwischenstand

`GitHubPullRequestTransport` ergänzt genau einen POST-Versuch und Detail-GET mit Tokenlease, festem API-Host, Repositorypfadvalidierung, begrenztem Antwortbody (1 MiB), kopiertem JSON-Lifetime und sanitisierten Fehlern. Ungewisser POST-Ausgang wird `ResultUnknown`; kein automatischer Retry. Ein fremder PR-Nummer-/Commit-/Branchbezug wird nicht akzeptiert. Transport ist intern und noch nicht im Hintergrunddienst oder der API registriert.

Zwei HTTP-Handler-Regressionen (keine echte GitHub-Abnahme): verlorene Antwort nach serverseitigem Schreiben führt zu genau einem Versuch; HTTP 429 gibt keinen sensitiven Body weiter. Alle 14 PR-Tests bestanden, 0 Fehler/Skips. Letzter inkrementeller Diagnosebuild: 0 Warnungen, 0 Fehler; vorheriger Infrastruktur-Neubau: 8 Warnungen. Analyzer/Warnungsfehler weiterhin deaktiviert.

Noch zu implementieren: vollständige paginierte PR-Suche und Detailabgleich, persistente Write-Intent-/Receipt-Recovery, Executor/Runner/DI, API/Studio, Audit/Review-/Releaseverifikation. Anschließend verpflichtende lokale und echte GitHub-Abnahmen. Die vorhandene isolierte GitHub-App benötigt dazu eine vom Nutzer bestätigte Erweiterung von Pull requests Read-only auf Read/write; keine Rechteänderung vorgenommen. G07 bleibt ausdrücklich offen.

## Nachfolgender Stand: Installation bestätigt und paginierte Suche

Die oben erwähnte Rechtevoraussetzung ist inzwischen erfüllt: nach ausdrücklicher Nutzerfreigabe wurden Pull requests Read/write gespeichert und für Installation `168820124` angenommen. Sichtbar bestätigt: nur ein ausgewähltes Repository, `VertexBPMN/vertexbpmn-git-acceptance`; keine Erweiterung auf alle Repositories.

`ReconcileAsync` durchsucht offene und geschlossene PRs, maximal zehn Seiten mit je 100 Einträgen. Ein voller letzter erlaubter Batch bleibt `ResultUnknown`, da die Suche dann möglicherweise unvollständig ist. Doppelte IDs, widersprüchliche Details und mehrdeutige Operationsmarker werden nicht bestätigt. Nummern werden aus Listendaten übernommen, aber Detailantworten erneut auf Repository/Branches/Commit/Nummer geprüft; keine serverseitigen Link-URLs verfolgt und niemals POST aus dem Recovery-Pfad ausgeführt.

`SourceControlHttps.ValidateRequestTarget` erlaubt ausschließlich die feste Pagination-Query auf `api.github.com/repos/{owner}/{repo}/pulls`; allgemeine Remote-URL-Validierung bleibt queryfrei. DNS-/Socket-Pinning, TLS, Allowlist und Redirect-Schutz bleiben bestehen.

Lokale Ergebnisse: 20 PR-Tests bestanden, einschließlich Treffer auf zweiter Seite nach 100 unpassenden Einträgen; 4 HTTPS-Boundary-Tests bestanden, keine Fehler/Skips. Diagnosebuild: 8 bestehende Warnungen, 0 Fehler, Analyzer deaktiviert. Nicht nachgewiesen: echter GitHub-Aufruf, persistente Intent-/Receipt-Recovery, ausführender Worker, API/Studio, Audit/Reviewverifikation und Gesamtregression. G07 bleibt offen.

## Weiterarbeit: persistente Intent-/Receipt-Grenze

`SavePullRequestIntentAsync` speichert den geschützten, unveränderlichen PR-Auftrag einmalig vor dem geplanten Remote-Schreiben. Bei bereits gespeichertem Intent liefert es false; für einen Reconciling-Auftrag weist es einen neuen Schreibversuch ausdrücklich zurück. `CompletePullRequestAsync` prüft aktuelle Autorisierung, Revision, Lease/Fence, ursprünglichen Intent und PR-Beleg und speichert Receipt plus Succeeded-Zustand in einer gemeinsamen Transaktion. Operation, Headcommit, Hostingprovider, URL und Mergezustand müssen zusammenpassen.

SQLite-Regression simuliert Running -> ResultUnknown -> Übernahme durch neuen Worker -> bestätigtes Ergebnis. Sie prüft auch doppelte Intent-Speicherung und tatsächlichen Rechteentzug bei einem weiterhin aktiven separaten Auftrag. Gesamte Persistenzklasse: 18 bestanden, 0 Fehler/Skips. Infrastruktur-Diagnosebuild: 8 Warnungen/0 Fehler; separater Testprojektbuild: 195 Warnungen/0 Fehler, Analyzer deaktiviert.

Diese Methoden sind noch kein angeschlossener Remote-Executor. Noch offen sind insbesondere die unmittelbar am Remote-Effekt durchzuhaltende Autoritäts-/Lease-Grenze, Runner/DI, API/Studio, Audit/Reviewstatus und echte GitHub-/Browser-Abnahme. Die Simulatorbelege sind keine Remote-Nachweise; keine G07-Gesamtabnahme markiert.

## Nachfolgender Stand: Executor und Remote-Aufrufgrenze

`SourceControlPullRequestExecutor` verbindet Tokenbroker, gehärteten HTTP-Client, persistente Intent-Speicherung und bestätigten Abschluss. Er löst aktuelle Rollen vor Vorbereitung, Remote-Aufruf und Abschluss erneut auf. Ohne gespeicherte Absicht darf nur ein Running-Auftrag Create ausführen; mit Absicht wird ausschließlich Reconcile verwendet. Ein Reconciling-Auftrag ohne nachweisbare Absicht bleibt unbekannt.

`InvokePullRequestRemoteAsync` hält eine Serializable-Transaktion mit Revisions-/Claim-Sperren über den begrenzten Remote-Aufruf. Eine verknüpfte Deadline endet spätestens mit der verbleibenden Lease. Der Remote-Abschluss und der anschließende Receipt-/Succeeded-Commit sind getrennt; die davor separat gespeicherte Absicht bleibt bei einem Fehler für lesende Recovery erhalten.

Erweiterter SQLite-/Executor-Test: exakt ein Create-Delegate wirft ResultUnknown, nach Neuübernahme wird exakt ein Reconcile-Delegate ausgeführt und atomar abgeschlossen; Create-Zähler bleibt eins. Alle 18 Persistenztests bestanden (9,511 s), keine Fehler/Skips. Infrastruktur-Diagnosebuild: 8 Warnungen/0 Fehler; separater Testprojektbuild: 195 Warnungen/0 Fehler. Keine echten GitHub-Aufrufe, kein strenger Analyzer-Build oder Gesamtregressionsnachweis.

Noch offen: Hosted Runner und DI-Registrierung, API-/Studio-Anschluss, Audit/Reviewverifikation und tatsächliche Remote-/Browser-Abnahme. Keine G07-Abnahmecheckbox geschlossen.

## Nachfolgender Stand: Hintergrund-Runner und Registrierung

`SourceControlClaimedPullRequestRunner` führt angenommene PR-Aufträge in eigenem Scope aus und finalisiert Fehler separat mit begrenzter Deadline. Gespeicherte Schreibabsicht/Reconciling hält den Zustand ResultUnknown; ohne Schreibabsicht wird ein Rechtefehler Failed, ohne Remote-Aufruf. Claims sind zeitlich begrenzt statt unbegrenzt verlängert; Remote-Aufrufe bleiben an die vorhandene Lease gebunden.

Executor/Runner sind in `InfrastructureModule` registriert. `SourceControlJobsHostedService` berücksichtigt PR-Queued/ResultUnknown sowie abgelaufene Running-/Reconciling-Claims. Nach Wiederaufnahme entscheidet der persistente Intent, niemals ein pauschaler Schreib-Retry.

SQLite-Regression nutzt jetzt den Runner mit eigenem DI-/Datenbank-Scope für bestätigte Recovery und prüft Rechteentzug vor Remote-Ausführung. API-/Studio-Anschluss, Audit-/Reviewstatus und echte GitHub-/Browser-Abnahme bleiben offen. Diagnosebuilds mit deaktivierten Analyzern weiterhin kein strenger Gesamtbuild-Nachweis.

## Nachfolgender Stand: API-/Studio-Anschluss implementiert

`SourceControlController` ergänzt JWT-geschützten POST `repositories/{id}/pull-requests` (202 und Operation-Location) und GET `operations/{id}/pull-request-receipt`. Aktuelle Actorrollen und Repository-PR-Rechte gelten weiterhin. `GetPullRequestReceiptAsync` gibt ausschließlich einen eigenen, erfolgreichen Auftrag mit geschütztem Beleg zurück; kein Zugriff aus fremdem Tenant.

`HttpSourceControlService` und `SourceControlWorkspacePanel` ergänzen einen separaten PR-Auftrag nach bestätigtem Push, Zielbranchauswahl aus Default/Release, Titel/Beschreibung, eingefrorenen Idempotenzauftrag, Statusabfrage und GitHub-Link mit noopener/noreferrer. Repository-/Tenant-/Dokumentwechsel lösen den PR-Kontext; ein neuer Commit setzt ihn zurück. Spätere Editoränderungen werden nicht in den PR eingeschlossen. Gespeicherter PR-Zustand ist ausdrücklich keine Live-Reviewfreigabe; keine automatische Merge-/Deploymentaktion.

Diagnosebuilds: API 14 Warnungen/0 Fehler (isoliertes Ausgabeverzeichnis, laufende API unverändert), Studio 1 Warnung/0 Fehler, Infrastruktur 8 Warnungen/0 Fehler. Testprojekt 195 Warnungen/0 Fehler, 18 Persistenztests bestanden einschließlich eigenem Beleg und fehlender Sichtbarkeit aus fremdem Tenant. Der erste API-Build ohne aktualisierte Infrastruktur scheiterte an fehlendem neuen Store-Member; nach Infrastruktur-Neubau erfolgreich. Analyzer waren deaktiviert.

Noch offen: HTTP-Endpunkt-/UI-Regressionen und tatsächlicher Browserablauf mit neu gestarteter Version, Live-Hostingstatus/Audit-/Reviewverifikation, echte GitHub-Abnahme sowie strenger Build/Gesamtregression. Die laufende Studio-Sitzung wurde nicht neu gestartet; keine Browser-Abnahme behauptet. G07 bleibt offen.

## Sichtbare Browser-Abnahme: Studio → Commit → Push → GitHub-PR

Am 2026-10-08 wurde die aktualisierte API auf Port 51870 und das aktualisierte Studio auf Port 5263 gestartet. Im sichtbaren In-App-Browser wurde ausschließlich das isolierte Repository `VertexBPMN/vertexbpmn-git-acceptance` verwendet. Der Benutzer genehmigte die zusätzliche Repository-PR-Berechtigung; die gespeicherten Grants wurden anschließend lesend in SQLite als 47 (Read/Commit/Push/PullRequest/Manage, ohne Deploy) verifiziert.

- `models/acceptance.bpmn` aus Branch `vertex/a7443b49a4be44b2a8b051c28cc2ecb5` bei Commit `2434455d180e0092a454ebd3fe3ea0b44d263930` im vorhandenen Editor geöffnet.
- Den geöffneten Snapshot ohne weitere Modelländerungen über das Studio bestätigt und lokal committed: `d1cd9c4d4f7fbb24d143aad5f2f28b70f64393aa`, Workbranch `vertex/118a3d0bd1804f6f8e6273d378b73575`.
- Genau diesen Commit über das Studio gepusht; dauerhafter Status `Pushed` bestätigt.
- PR-Titel und Beschreibung im Studio eingegeben, Ziel `master` gewählt und Erstellung bestätigt. Studio meldete `Succeeded`, `Confirmed PR state: Open` und den GitHub-Link.
- Den tatsächlich erzeugten [PR #1](https://github.com/VertexBPMN/vertexbpmn-git-acceptance/pull/1) sichtbar auf GitHub geöffnet: Autor GitHub-App-Bot, korrekter Headbranch, Ziel `master`, zwei Commits einschließlich `d1cd9c4`, eine geänderte Datei. Kein Merge und kein Runtime-Deployment ausgeführt.

Screenshot: `tests/VertexBPMN.Studio.UiTests/TestResults/g07-visible-pr-acceptance.jpg` (lokales Testartefakt).

Beobachtete Einschränkungen: Dateiladen überschritt zunächst den Studio-Polly-Timeout von 10 Sekunden und wurde wiederholt; die Dateiliste erschien schließlich. Die erste automatisierte Grants-Eingabe zeigte 47 im Browser, übernahm aber nicht den gebundenen Wert: SQLite enthielt weiter 39, PR-Anfragen wurden korrekt mit 403 abgewiesen. Erst eine tatsächliche Tastatur-Wertänderung von 39 auf 47 mit Fokuswechsel speicherte die genehmigten Rechte. Danach wurde derselbe eingefrorene PR-Auftrag erfolgreich angenommen. Dies ist kein Nachweis eines allgemeinen Produktfehlers gegenüber einem Browser-Automationsproblem; ein gezielter Eingabe-/Timeout-Regressionsfall bleibt zu prüfen.

Damit ist der sichtbare positive PR-Erstellungsablauf abgenommen, nicht G07 insgesamt. Weiter offen: Live-Hostingstatus/Audit-/Reviewverifikation, echte Lost-Response-/Rechteentzugsfälle, automatisierte HTTP-/UI-Regression sowie strenger Build und Gesamtregression. Keine Produktionsfreigabe abgeleitet.

## Folgepaket: aktueller Hostingzustand nach Merge

Der Benutzer hat Test-PR #1 selbst gemergt. Im sichtbaren GitHub-Browser verifiziert: Zustand `Merged`, Mergecommit `616db2854e84a42e1bd656dbc1aca9ba6af810f2`. Das ist der Merge des Testrepositorys, nicht des VertexBPMN-Implementierungsbranches.

`SourceControlPullRequestStatusReader` ergänzt eine reine GET-Abfrage des bestätigten PRs. Der geschützte ursprüngliche Auftrag bestimmt Repository, Nummer, Head und Ziel. Vor dem Hostingaufruf und nach der Antwort werden aktuelle Actorrollen und PR-Grants geprüft. Der Installationstoken wird mit Leserechten angefordert. Der historische Erstellungsbeleg bleibt unverändert. API: `GET /api/source-control/operations/{id}/pull-request-status`; Studio: separater Button `Check current GitHub PR status`, letzter geprüfter Zustand und tatsächlicher Mergecommit. Keine Review-/Deploymentfreigabe und kein Mergeaufruf.

Lokale Prüfungen: 23 PR-Decoder-/Reconciliation-/Transporttests bestanden, darunter neue GET-only-Statusfälle für Open, Closed und Merged sowie Ausschluss eines Vorschau-Mergecommits. 18 SQLite-Persistenztests bestanden (11,351 Sekunden), erweitert um geschützten Auftrag und Ablehnung bei fremdem Tenant, anderem Benutzer, ReadOnly-Rolle und entzogenem PR-Grant. Diagnosebuilds: Infrastruktur 8 Warnungen, API 14, Studio 1, Adaptertests 0, Haupttests 195; jeweils 0 Fehler, Analyzer deaktiviert. Der anfängliche Aufruf mit `--filter-class` wurde vom direkten Runner abgelehnt; erfolgreiche Wiederholung mit dessen dokumentierter Option `-class`.

Der neue Live-Status-Button ist noch nicht im neu gestarteten Studio sichtbar abgenommen; die laufende Browser-Sitzung verwendet weiterhin den vorherigen Publishstand. Ein sichtbarer GitHub-Merge ersetzt diese Abnahme nicht. Automatisierte HTTP-/UI-Regression, unabhängige Review-/Auditverifikation, echte Lost-Response-/Rechteentzugsabnahme und strenger Build/Gesamtregression bleiben offen. G07 bleibt offen.
