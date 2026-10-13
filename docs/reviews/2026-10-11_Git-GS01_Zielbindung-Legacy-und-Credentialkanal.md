# Generische Git-Server – GS01: Zielbindung, Legacy-Mapping und Credentialkanal

Stand: 11. Oktober 2026. Branch `codex/git-generic-servers`, Basis `02ebe35`. Fortsetzung des [ersten GS00/GS01-Bausteins](2026-10-11_Git-GS00-GS01_Start-und-Verbindungsvertraege.md) und des [Implementierungsplans](2026-10-09_Git-Generische-Server-Unterstuetzung_Implementierungsplan.md).

Status: **Weitere GS01-Verträge und Basic-/PAT-IPC-Baustein implementiert und lokal geprüft.** Der spätere [GS02-Baustein](2026-10-11_Git-GS02_Profilpersistenz-und-Schema-Upgrade.md) speichert Profile; Zielbindungen sind noch nicht an die aktive Auftragsannahme, API oder Worker angeschlossen. Der aktive Remote-Broker bleibt GitHub-spezifisch; kein neuer Git-Server-Support freigegeben.

## Unveränderlicher Zielkontext

`SourceControlExecutionTarget` enthält Repository-ID, Bindungsrevision, exakte Remote-URI, das unveränderliche Verbindungsprofil und die benötigten Fähigkeiten. Das Envelope besitzt eine eigene Schema-Version. Credentialwerte sind nicht enthalten; die Profilreferenzen werden beim späteren Ausführen frisch aufgelöst.

`SourceControlExecutionTargetPolicy.Capture` prüft Tenant, Endpunktbereich, positive Revisionen und verfügbare Fähigkeiten. `DemandCurrent` vergleicht den angenommenen Kontext mit frisch geladenen Daten. Geänderte Repository-ID, Remote, Profil-ID/-Revision, Verfahren, Credentialreferenzen, Netzwerk-/Trustreferenzen und Hostingidentität dürfen einen Auftrag nicht stillschweigend umlenken, auch nicht bei wiederverwendeter Revision. Nicht mehr verfügbare Fähigkeiten ergeben `ProviderUnavailable`, Ziel-/Revisionswechsel `RevisionConflict`, Fremdtenant `NotFound`; fehlende oder unbekannte Snapshot-Versionen `ContentUnsafe`.

Konservative Regel: Ein neueres Profil verlangt eine neue Annahme; ein unverändertes Profil darf frische Secretwerte aus derselben Credentialreferenz beziehen. Der Snapshot ersetzt weder aktuelle Actor-/Repository-Grants noch Profilwiderruf, Credentialstatus oder tatsächlich durchgesetzte Netzwerk-/Trustpolicy.

Dateien:

- `src/VertexBPMN.SourceControl.Abstractions/SourceControlExecutionTarget.cs`
- `src/VertexBPMN.Application/SourceControl/SourceControlExecutionTargetPolicy.cs`
- `tests/VertexBPMN.Tests/Unit/Application/SourceControlExecutionTargetTests.cs`

## Explizite Legacy-GitHub-Zuordnung

`LegacyGitHubConnectionMapping.Create` bildet eine bestehende, als `GitHubApp` verifizierte Bindung auf ein stabiles Profil ab: Profil-ID entspricht der bestehenden Repository-ID, Revision 1, exakter HTTPS-Remote auf `github.com:443`, API-Wurzel `https://api.github.com/`, eigene administrative Netzwerkreferenzen für Git und API, GitHub-Hostingidentität `owner/repository`. Die Credentialreferenz bleibt erhalten; GitHub benutzt hier bewusst denselben Credentialtyp für Git und API.

Die Funktion akzeptiert ausschließlich zwei eindeutige Pfadsegmente und optional `.git` beziehungsweise einen terminalen Slash. Andere Hosts/Ports, mehrdeutige Pfade, codierte Identitäten, Credentialtyp-/Referenzfehler oder abweichende API-Ziele werden abgelehnt. Die ursprüngliche Bindung und deren JSON werden nicht verändert. Keine automatischen Defaults für neue generische Bindungen.

Der Aufrufer muss Credentialtyp und Tenant anhand der geschützten Credentialverwaltung prüfen. Das Mapping prüft keine GitHub-Installation oder numerische Remote-Repository-ID über das Netzwerk. Administrative Netzwerkreferenzen müssen separat registriert/qualifiziert werden; die Funktion ist noch keine angewendete Upgrade-Migration.

Dateien:

- `src/VertexBPMN.Application/SourceControl/LegacyGitHubConnectionMapping.cs`
- `tests/VertexBPMN.Tests/Unit/Application/LegacyGitHubConnectionMappingTests.cs`

## Basic-/PAT-IPC und GitHub-Kompatibilität

Der vorhandene `GitCredentialChannel` erhält mit `IGitCredentialLease` einen engen gemeinsamen Port für den Username-/Passwortkanal. `GitHubTokenLease` implementiert ihn mit dem bisherigen `x-access-token`; Hosting benutzt weiter seinen bestehenden `Authorization()`-Pfad. Die GitHub-Broker-/Transportauswahl bleibt unverändert.

`GitHttpBasicCredentialLease` bindet synthetisch beziehungsweise später geschützt aufgelöste Basic-/PAT-Werte an genau einen HTTPS-Remote und eine kurze Operation. Sie lehnt unzulässige Benutzernamen, Protokollinjektion, leere/überlange Secrets, abgelaufene oder zu lange Laufzeiten sowie Weitergabe an andere Remote-URIs ab. Der Secretpuffer wird bei Dispose gelöscht; Diagnoseausgabe ist redigiert. Cancellation wird an beide IPC-Schreiboperationen weitergereicht.

Der echte Auth-Helper akzeptiert den vom Dienst ausgewählten Benutzernamen. Ein vom Gitprozess mitgelieferter Username muss mit der Dienstantwort übereinstimmen; er wählt weder Identität noch Secret aus. Passwortvorgaben, andere Hosts/Ports und Unterpfade bleiben abgelehnt. Secrets bleiben außerhalb von Argumenten und Environment; Store/Erase persistieren weiterhin nichts. Ein verweigerter/abgelaufener Lease trennt eine bestehende Pipe sofort, statt den Helper bis zu dessen Timeout warten zu lassen.

Dieser Port ist **kein Bearer-Headerkanal**. `HttpBearer` bleibt ein deklarativer Profilwert ohne angeschlossenen Broker/Übertragungsweg; es gibt keinen Token-in-Argumenten-Fallback. Basic-/PAT-Secrets werden in diesem Baustein noch nicht aus der produktiven Credentialverwaltung aufgelöst. Der Helper wird zusammen mit seinem Host gebaut und paketiert; gemischte Helper-/Host-Versionen sind vor einem Rollout zu qualifizieren.

Dateien:

- `src/VertexBPMN.Infrastructure/SourceControl/IGitCredentialLease.cs`
- `src/VertexBPMN.Infrastructure/SourceControl/GitHttpBasicCredentialLease.cs`
- `src/VertexBPMN.Infrastructure/SourceControl/GitHubTokenLease.cs`
- `src/VertexBPMN.Infrastructure/SourceControl/GitCredentialChannel.cs`
- `src/VertexBPMN.SourceControl.AuthHelper/Program.cs`
- `tests/VertexBPMN.SourceControl.Tests/BasicCredentialChannelTests.cs`

## Im nativen Regressionstest behobener Bestandsfehler

Die erste Ausführung der 28 vorhandenen nativen HTTPS-/Git-Boundaryfälle brach bereits bei der Git-Versionsprüfung ab: `ControlledGitProcess.GitVersionPattern` benutzte `ExplicitCapture` mit unbenannten Gruppen, während `VersionAsync` die Gruppen 1–3 als Zahlen las. Die Gruppen waren leer und `int.Parse` warf `FormatException`.

In `src/VertexBPMN.Infrastructure/SourceControl/ControlledGitProcess.cs` sind die drei Gruppen jetzt benannt (`major`, `minor`, `patch`) und werden über diese Namen gelesen. `ExplicitCapture`, Regex-Timeout, NonBacktracking und die Mindestversion Git 2.56.0 bleiben erhalten. Die realen vorhandenen Tests prüfen diesen Ausführungspfad; kein Parserersatz oder Testskip hinzugefügt.

## Verifikation

Die neuen reinen Ziel-/Legacytests sind in der normalen CI-safe Suite. Die lokalen Helpertests starten die tatsächlich gebaute Helperdatei und verwenden ausschließlich synthetische Testcredentials; sie öffnen keinen Gitserver und benötigen keine Azure-/GitHub-Zugänge. Bestehende GitHub-Helperregression wird separat mit ausgeführt.

Strenger Solution-Build nach allen Codekorrekturen erfolgreich: **0 Warnungen, 0 Fehler**, 3 Minuten 11,86 Sekunden (GS01-Abschluss vor GS02). Keine neuen Abhängigkeiten und kein Restore; Editorassets unverändert.

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

Gezielte Application-Vertrags-/Sicherheitsregression: **140 bestanden**, keine Fehler/Skips; 0,541 Sekunden.

```powershell
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*RepositoryConnectionProfileTests' -class '*SourceControlExecutionTargetTests' -class '*LegacyGitHubConnectionMappingTests' -class '*SourceControlOptionsTests' -class '*SourceControlSecurityTests'
```

Helper-/IPC-/Hostingregression: **109 bestanden**, keine Fehler/Skips; 12,655 Sekunden. Der echte Helperprozess wurde mit synthetischen Credentials gestartet; keine externe Hostingverbindung.

```powershell
dotnet tests/VertexBPMN.SourceControl.Tests/bin/Release/net10.0/VertexBPMN.SourceControl.Tests.dll -class '*BasicCredentialChannelTests' -class '*CredentialChannelAcceptanceTests' -class '*SourceControlContractTests' -class '*HttpsBoundaryTests' -class '*GitHubPullRequestTransportTests' -class '*GitHubPullRequestResponseTests' -class '*GitHubPullRequestReconciliationTests'
```

Check-only-Formatprüfung der 15 neuen C#-Dateien erfolgreich (Exit 0): `dotnet format whitespace VertexBPMN.sln --verify-no-changes --no-restore --include <Dateiliste>`. Formatter nicht im Schreibmodus ausgeführt; gefundene Stellen in zwei neuen Testdateien manuell korrigiert. Getrackte Änderungen zusätzlich mit `git diff --check` und alle neuen Dateien auf Whitespace/Final-Newline geprüft. Frühere Kandidaten-Analyzerbefunde im neuen Code korrigiert; keine Regeln deaktiviert.

Die erste CI-safe-Ausführung parallel zur CPU-intensiven nativen Suite hatte einen Fehler in `C_8_0_Test.Test_C_8_0_Bpmn` (FEEL-Variable `Vacation Approval` fehlte): 1.530 bestanden, ein Fehler, acht externe Skips. Der separate, unveränderte Konformanznachlauf bestand mit zwei gefundenen Tests (2,821 Sekunden). Eine Lastabhängigkeit ist damit eine Vermutung, kein bewiesener Ursachennachweis. Der vollständige Nachlauf nach Ende der nativen Prüfung bestand mit 1.531 Tests und acht externen Skips (1 Minute 24,654 Sekunden), noch vor dem abschließenden Schema-Pflichttest; keine Assertion, Fixture oder Engineausführung geändert.

Nativer HTTPS-/Git-Nachlauf nach der Versionskorrektur: **28 bestanden**, keine Fehler/Skips; 212,921 Sekunden. Echte lokale TLS-Challenge, Helper, Fetch, revisionsgebundene Originalbytes, Push/Ref-CAS, zweiter Clone, Unknown-/Restart-Recovery und Cancellation über die vorhandenen isolierten Testadapter geprüft. Diese Abnahme betrifft den vorhandenen GitHub-Lease-Pfad gegen den lokalen Server; sie ist keine Basic-/PAT-Produktanbindung, öffentliche GitHub-Abnahme oder Prüfung privater Produktionsnetzwerke.

```powershell
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet tests/VertexBPMN.SourceControl.Tests/bin/Release/net10.0/VertexBPMN.SourceControl.Tests.dll -class '*GitHttpsTransportTests' -class '*NativeGitBoundaryTests'
```

Der GS01-CI-safe-Nachlauf ist bestanden; die abschließende Profil-/Ziel-Schemaversion ist jetzt beim JSON-Lesen verpflichtend. Der weitere aktuelle Build und die Gesamtsuite einschließlich Profilpersistenz stehen im [GS02-Bericht](2026-10-11_Git-GS02_Profilpersistenz-und-Schema-Upgrade.md). Lokale Logs: `artifacts/git-generic-servers/gs01-*.log`; erster fehlgeschlagener CI-Lauf separat in `gs01-ci-safe-first.log` erhalten. Der CI-Befehl und seine unveränderten Ausschlüsse stehen im [Startbericht](2026-10-11_Git-GS00-GS01_Start-und-Verbindungsvertraege.md#regression-und-builds).

## Wartbarkeitsprüfung

Scope: alle 19 derzeit auf diesem Branch geänderten/neuen C#-Dateien einschließlich des ersten Vertragsbausteins, Basis `02ebe35`; Diagnose-JSONs und fremde Änderungen ausgeschlossen. Skill `.agents/skills/dotnet-clean-code-review/SKILL.md` und beide Referenzdateien gelesen. D1–D7 per `rg`, Exitcodes geprüft; D8 durch vollständiges Lesen der neuen Dateien und der geänderten Bestandsmitglieder mit ihrem Kontext. Neuer Port besitzt zwei konkrete Implementierungen und einen aktiven IPC-Verbraucher.

Keine bestätigten Clean-Code-Befunde im geprüften Scope. Suchergebnisse lokal in `artifacts/git-generic-servers/gs01-clean-code.json`.

| Regel | Ergebnis | Nachweis / Grenze |
| --- | --- | --- |
| Async/Cancellation | PASS | Awaitable IPC-/Helper-I/O; Tokens durchgereicht; Testnamen durch xUnit bestimmt |
| Collection-Verträge | PASS | Keine neuen Collection-Rückgaben; vorhandene Prozessmethoden behalten ihre Materialisierungs-/I/O-Semantik |
| Boolean-Modusflags | PASS | Nur Validierungsprädikate, keine neuen bool-Modusschalter |
| Verantwortungsnamen | PASS | Zielpolicy, Legacy-Mapping und Credential-Leases mit getrennten Aufgaben |
| Regions/Struktur | PASS | Keine Regions oder neue übergreifende Verantwortlichkeiten |
| Interfaces/Ports | PASS | IPC-Port mit GitHub- und Basic-Implementierung, keine Entfernung bestehender Ports |
| Vererbung | PASS | Sealed Records/Klassen; nur bestehende Framework-/Disposal-Verträge |
| Lesbarkeit | PASS | Guards, kohärente Vertragsparameter und explizite sichere Fehler |

## Offen und nächster Schritt

GS01 bleibt für Bearer-Sicherheitsnachweis, Hostingzustände/-Versionsverträge und Worker-Kompatibilität offen. In GS02 müssen Profile und Zielkontext in den vorhandenen Store integriert, Aufträge versioniert und reale Upgrade-/Restart-/Claimtests ausgeführt werden. Insbesondere dürfen alte Worker neue Aufträge nicht claimen. Hier wurden keine neuen Jobversionen aktiviert und keine Datenbankmigrationen angewendet.

Danach folgen administrative Netzwerk-/Trustpolicy mit tatsächlicher Peerprüfung (GS03) und der vollständige generische HTTPS-Gitpfad mit Basic-/PAT-Auflösung sowie anonymem Lesen (GS04). Reale Git-, TLS-/DNS-, Windows/Linux- und Browserabnahmen bleiben getrennte Pflichtnachweise.

Fortsetzung: [GS02-Profilpersistenz und Schema-Upgrade](2026-10-11_Git-GS02_Profilpersistenz-und-Schema-Upgrade.md).
