# Generische Git-Server – GS00/GS01: Start und Verbindungsverträge

Stand: 11. Oktober 2026. Branch: `codex/git-generic-servers`. Basiscommit: `02ebe35` (`master`, strenge Solution-Analyzerbereinigung aus PR #51).

Status: **Implementierung gestartet; erster GS01-Vertragsbaustein umgesetzt. Strenger Solution-Build, gezielte Tests, Formatcheck und CI-safe-Gesamtsuite bestanden.** Kein generischer Produkttransport oder Anbieteradapter freigegeben. Dieser Bericht gehört zum [GS00–GS10-Plan](2026-10-09_Git-Generische-Server-Unterstuetzung_Implementierungsplan.md).

## GS00 – aktuelle Inventur

- Branch wurde aus dem aktuellen `master` angelegt. Die vorhandenen untracked Diagnose-Dateien `engine-current-compiler-arguments.json` und `tests-current-compiler-arguments.json` bleiben außerhalb dieses Auftrags.
- `AGENTS.md`, README, `.editorconfig`, `global.json`, `Directory.Build.props`, CI und die Git-Pläne gelesen. Keine zusätzlichen untergeordneten `AGENTS.md` in `src`/`tests` gefunden.
- Host: Windows/PowerShell; installiertes SDK `10.0.303`, angefordertes SDK `10.0.302` mit `latestPatch`; Git `2.56.0.windows.2`; OpenSSH `9.5p2`.
- GitHub-Kopplung aus der Planung besteht weiterhin: GitHub-App-Broker, Credentialkanal und PR-/Hostingpfad werden durch diesen ersten Baustein noch nicht ersetzt.
- Bestehende `RepositoryBinding`, `SourceControlOptions`, API-DTOs, DI, EF-Modell, gespeicherte Aufträge und Runtimepfade wurden nicht geändert. Es wurde keine Datenbankmigration angewendet und kein Secret aufgelöst.
- Strenger Baseline-Solution-Build mit vorhandenen Restore-Artefakten erfolgreich: 0 Warnungen, 0 Fehler, 3:14,58 Minuten. Kein Restore/Netzwerkzugriff für neue Abhängigkeiten erforderlich; keine Dependencies ergänzt.

Baselinebefehl:

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

Lokaler Log: `.artifacts/git-generic-baseline-build.log`. Editorassets unverändert; deshalb Asset-Skip verwendet. Keine externe oder Browser-Baseline in dieser Runde.

## GS01 – umgesetzter Vertrag

Neue Typen in `src/VertexBPMN.SourceControl.Abstractions`:

| Typ | Verantwortung |
| --- | --- |
| `GitTransportKind` | Explizite HTTPS-/SSH-Auswahl, unabhängig von Hosting |
| `GitAuthenticationKind` | Anonymous, HTTP Basic, GitHub App, HTTP Bearer und SSH-Schlüssel als deklarierte Verfahren |
| `RepositoryEndpoint` | Basis-URI, Referenz auf administrative Netzwerkpolicy und optionales Trustprofil |
| `RepositoryHostingConnection` | Provider-ID, eigene API-Endpunktbindung, Anbieter-Repositoryidentität, Credential-Referenz und Hostingfähigkeiten |
| `RepositoryConnectionProfile` | Unveränderliche ID, Tenant, positive Revision, Schema-Version, Transport/Auth, Git-Endpunkt, Credential-Referenz und Gitfähigkeiten; optionales Hosting |

Ein Enumwert beschreibt den gewünschten Vertrag, **keinen bereits vorhandenen Transportadapter**. Die Typen enthalten weder Secrets noch administrative CIDRs/CA-Dateien. Netzwerk-/Trustreferenzen werden in GS03 administrativ aufgelöst und am tatsächlichen Peer durchgesetzt; sie sind in diesem Paket nur strukturelle Referenzen.

`RepositoryConnectionProfilePolicy` in Application prüft ohne I/O:

- Schema-Version 1, gültige ID/Tenant/Revision und definierte Transport-/Authentifizierungswerte.
- Getrennte Git- und Hostingfähigkeiten; Git benötigt `Read` und darf keine Hostingbits enthalten.
- Anonymen Git-Zugriff ausschließlich mit `Read` und ohne Credential-Referenz; andere Verfahren benötigen eine Referenz.
- SSH ausschließlich mit Schlüsselverfahren und explizitem Trustprofil; HTTPS darf kein SSH-Verfahren verwenden. SSH-Endpunkt verlangt einen expliziten Port, da die URI-Verarbeitung hierfür keinen Standardport liefert.
- Absolute Endpunkt-URI mit passendem Scheme und gültigem Port, ohne UserInfo, Query, Fragment, Loopback, trailing Hostdot, Backslashes oder rohe Whitespaces/Steuerzeichen.
- Pfade einschließlich `OriginalString` vor URI-Normalisierung: kein `.`/`..`, keine codierten Pfadseparatoren, Steuerzeichen oder doppeltes Percent-Encoding. Percent-codierte Leerzeichen in Projektnamen bleiben möglich.
- Separate Hosting-Provider-ID, Repositoryidentität und Credential-Referenz; `null` Hosting bedeutet keine Hostingintegration.
- Bei `DemandRepository`: passende Tenantbindung sowie gleicher Host/Port und exakte Pfadgrenze. `/team` gewährt keinen Zugriff auf `/teams` oder `/Team`.

Ungültige Verbindungsdaten liefern ausschließlich `InvalidInput`, fremde Tenantbindungen `NotFound`, jeweils über den vorhandenen öffentlichen sicheren Fehlervertrag. Eingabe-/Providertexte werden nicht in diese Fehler übernommen.

`DemandRepository` ersetzt keine Repository-ACL oder aktuelle Actorprüfung. Neue Profile sind **noch nicht an aktive Options/API/DI/Provider angeschlossen** und verändern deshalb weder den bestehenden Netzwerkzugriff noch die verfügbaren Provider.

## Noch offene GS01-Entscheidungen

- Sichere Bearer-Vermittlung für den tatsächlichen nativen Gitprozess prototypisieren; kein Token in Argumenten oder persistenter Gitkonfiguration. Nur der deklarative Verfahrenstyp existiert.
- Profilrevision/-schema und Anbieteridentität in angenommene Aufträge einfrieren; aktuelle Policy-/Credentialwiderrufe bei Ausführung prüfen (GS02).
- Legacy-GitHub-Zuordnung in echter Persistenzmigration festlegen und abnehmen. Bestehende Bindungs-JSON wird in einem Vertragstest gelesen; das ist keine Migration-/Recoveryabnahme.
- Netzwerk-/CA-/Hostkeypolicy sowie echte Peerprüfung (GS03) und Providerregistry/Actor-Capabilities (GS05/GS09) anschließen.
- Hosting-Antworten und Reviewzustände anbieterweise qualifizieren. Dieses Paket enthält keine neuen PR-/MR- oder Releasefreigaben.

GS01 insgesamt bleibt deshalb offen. GS02–GS10 sind durch diese Verträge noch nicht implementiert.

## Regression und Builds

Neue Tests: `tests/VertexBPMN.Tests/Unit/Application/RepositoryConnectionProfileTests.cs`, reine Vertrags-/Policytests ohne Git, Netzwerk oder Browser. Sie gehören in die normale CI-safe Suite; keine neuen Kategorien/Ausschlüsse wurden eingeführt.

Geprüfte Szenarien: JSON-Roundtrip eines Git-only-Profils; getrennte Hosting-Credentials; Anonymous-/HTTPS-/SSH-Verfahrensregeln; malformed/gefährliche URLs; interne DNS-/IPv4-/IPv6-Endpunkte mit zwingender Policyreferenz; ungültige Schema-/ID-/Revision-/Capabilitywerte; Host-/Port-/Pfadgrenzen; Fremdtenant; unveränderte alte GitHub-Bindungs-JSON.

Der erste Kandidatenbuild zeigte vier neue Analyzerbefunde (Enumvergleich und Nullguard), ein Zwischenbuild einen Nullable- und einen Nullguardbefund. Anschließend vier neue Test-Analyzerbefunde (Stringvergleich, rohe URL-Testdatenparameter und explizite positive Assertion) korrigiert. Alle Korrekturen im neuen Code; keine Analyzer- oder Warnungsregeln deaktiviert.

Abschließender Kandidatenbuild: vollständige Solution mit demselben strengen Baselinebefehl erfolgreich, **0 Warnungen, 0 Fehler**, 1:02,32 Minuten. Log: `.artifacts/git-generic-build.log`.

Gezielte Vertrags-/bestehende Options-/ACL-Regression:

```powershell
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*RepositoryConnectionProfileTests' -class '*SourceControlOptionsTests' -class '*SourceControlSecurityTests'
```

Ergebnis: **101 bestanden**, 0 Fehler, 0 übersprungen, 0 nicht ausgeführt; 0,488 Sekunden. Runnerhilfe vorher gelesen; der direkte xUnit-Runner verwendet `-class`. Log: `.artifacts/git-generic-target-tests.log`.

Check-only-Formatprüfung erfolgreich (Exit 0): `dotnet format whitespace VertexBPMN.sln --verify-no-changes --no-restore --include` mit den sieben neuen C#-Dateien. Keine Formatter-Schreiboperation. Log: `.artifacts/git-generic-format.log`. `git diff --check` für die getrackten Änderungen und explizite Newline-/Whitespaceprüfung aller neuen C#-Dateien erfolgreich.

CI-safe-Gesamtsuite aus `.github/workflows/ci.yml` mit unveränderter Ausschlussliste bestanden (Exit 0): **1.492 erfolgreich, 0 fehlgeschlagen, 8 übersprungen**, insgesamt 1.500; Dauer 1:25,606 Minuten. Die übersprungenen Prüfungen verlangen externe Voraussetzungen und gelten nicht als bestanden. Log: `.artifacts/git-generic-ci-safe.log`.

```powershell
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --no-build --no-restore `
  --filter-not-trait "Category=Phase3ExternalAcceptance" `
  --filter-not-trait "Category=Phase4OutageAcceptance" `
  --filter-not-trait "Category=Phase5RecoveryAcceptance" `
  --filter-not-trait "Category=Phase6LoadAndSoakAcceptance" `
  --filter-not-trait "Category=Phase7ConformanceAcceptance" `
  --filter-not-trait "Category=Phase8MonitoringAcceptance" `
  --filter-not-trait "Category=ExternalTaskPostgres" `
  --filter-not-trait "Category=ExternalAgentProcessRecovery" `
  --filter-not-trait "Category=ContractReviewOllama" `
  --filter-not-trait "Category=ContractReviewBenchmark" `
  --filter-not-class "VertexBPMN.Tests.Conformance.DistributedConformanceTests" `
  --max-parallel-test-modules 1
```

## Wartbarkeitsprüfung

Scope: sieben neue C#-Dateien, Basis `02ebe35`; keine bestehenden/fremden C#-Änderungen. Skill: `.agents/skills/dotnet-clean-code-review/SKILL.md`; Referenzen gelesen. D1–D7 über die vollständige Dateiliste ausgeführt, Exitcodes geprüft; D8 durch Lesen der Verträge, Policy und Tests.

Keine bestätigten Clean-Code-Befunde im derzeit geprüften Scope.

| Regel | Ergebnis | Nachweis / Grenze |
| --- | --- | --- |
| Async/Cancellation | N/A | Reine synchrone Vertrags-/Policylogik, kein I/O |
| Collection-Verträge | N/A | Keine neuen Collection-Rückgaben |
| Boolean-Modusflags | PASS | Nur benannte Validierungsprädikate, keine bool-Modeparameter |
| Verantwortungsnamen | PASS | Profile, Endpunkte und Policy mit engem Source-Control-Scope |
| Regions/Struktur | PASS | Keine Regions; cohesive Validation ohne Transport-/Persistenzarbeit |
| Interfaces/Ports | N/A | Keine neuen Interfaces; vorhandene Git-/Hostingports unverändert |
| Vererbung | PASS | Sealed Records und abgeschlossene Testklasse, keine eigene Vererbungshierarchie |
| Lesbarkeit | PASS | Guard clauses, benannte Capabilitymasken, sichere Fehler; Datensatzparameter bilden einen kohärenten Vertrag |

Review ist keine Transport-/Security-/Produktabnahme. D1–D7 nach den abschließenden C#-Korrekturen erneut ausgeführt; lokales JSON-Ergebnis in `artifacts/git-generic-servers/git-generic-clean-code.json`. Formatcheck und strenger Build sind erfolgreich.

## Nächster konkreter Schritt

GS01-Sicherheits-/Legacy-/Workerverträge schließen, dann GS02-Profilpersistenz mit Upgrade- und Claim-/Restarttests. Danach GS03/GS04 für einen echten eigenen HTTPS-Gitserver. Kein allgemeiner Git-Server-Support allein aus den Vertragsprüfungen ableiten.

Nachfolgender GS01-Baustein: [Zielbindung, Legacy-Mapping und Credentialkanal](2026-10-11_Git-GS01_Zielbindung-Legacy-und-Credentialkanal.md). Die Ergebnisse oben gehören zum ersten Kandidaten; aktuelle Erweiterungen und ihre Prüfung stehen im verlinkten Folgebericht.
