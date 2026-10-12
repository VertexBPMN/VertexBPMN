# GS03/GS04: Netzwerkgrenze und vorbereiteter generischer HTTPS-Pfad

Stand: 11. Oktober 2026. Branch: `codex/git-generic-servers`, uncommittierte Implementierung auf Basis `02ebe35`. Fortsetzung des [Implementierungsplans](2026-10-09_Git-Generische-Server-Unterstuetzung_Implementierungsplan.md).

## Ergebnis und Grenzen

Eine mandantengebundene HTTPS-Netzwerkpolicy und ein interner, profilgebundener Fetch-Pfad sind implementiert. Der native Credentialkanal kann Basic/PAT mit einem serverseitig bestimmten Benutzernamen übertragen. Anonymes Lesen startet ohne Credentialhelper; anonyme Schreibbefehle werden vor dem Gitprozess abgewiesen.

**Der generische Produktpfad ist noch nicht freigeschaltet.** Neue Auftragsverträge bleiben gesperrt, wie im [GS02-Bericht](2026-10-11_Git-GS02_Profilzuordnung-und-Worker-Vertragsgrenze.md) beschrieben. Dieser Schritt beweist weder Azure-DevOps-Unterstützung noch SSH, Bearer, eigene produktive Server oder vollständige Commit-/Push-/Recovery-Abläufe über neue Profile. GS03 und GS04 sind nicht insgesamt abgeschlossen.

## Implementierung

- `GitHttpsNetworkPolicy.cs`: administrativ aufgelöste Policy mit Tenant, Referenz, Repositoryscope und erlaubten/gesperrten CIDR-Bereichen. Keine neuen API-Felder oder frei vom Client einstellbaren Netzwerkfreigaben.
- `GitHttpsNetworkBoundary.cs`: zuerst Tenant/Profil/Host/Port/Repositorypfad und Policyreferenz prüfen; dann begrenzte CIDR-Listen und sämtliche DNS-Antworten prüfen. Standardmäßig nur öffentliche Adressen; RFC1918 und IPv6-ULA ausschließlich in ausdrücklich erlaubten Bereichen. Denials haben Vorrang. Eine unsichere Antwort verwirft das gesamte Ziel.
- `ApprovedGitHttpsTarget.cs`: kopierter, operationslokaler DNS-Nachweis. Native Gitprozesse erhalten diese Adressen über den vorhandenen `http.curloptResolve`-Pfad und lösen den Host dort nicht erneut auf. Der TLS-Hostname bleibt erhalten.
- `ControlledGitProcess.ProfiledHttps.cs`: Read-Capability, Profilzuordnung und Authentifizierungsart vor DNS/Git prüfen; gesamter Ablauf mit Deadline und Cancellation. Zunächst Basic und Anonymous mit Systemtrust. Nicht angeschlossene Authentifizierungsarten und referenzierte eigene CA-Profile liefern `ProviderUnavailable`, fehlende/falsche Leasearten `CredentialUnavailable`.
- `ControlledGitProcess.cs`, `.Push.cs`, `.Revisions.cs`: interner Credentialport statt konkretem GitHub-Lease. Bestehende GitHub-Aufrufer verwenden weiterhin ihren eigenen Lease. Anonymous erzeugt keine Pipe und benötigt keinen installierten Helper. Push ohne Lease wird abgewiesen. Redirect-, Proxy-, Ambient-Config-, TLS- und feste Befehlsgrenzen bleiben bestehen.

Loopback, Link-local, CGNAT, Multicast, Dokumentationsadressen und scoped IPv6 bleiben auch bei breiten Allow-Bereichen gesperrt. Zusätzlich gesperrt sind Azure `168.63.129.16`, AWS `fd00:ec2::254` und Google `fd20:ce::254`. Die Adressbedeutung wurde gegen Primärquellen geprüft: [Azure-Plattformadresse](https://learn.microsoft.com/en-us/azure/virtual-network/what-is-ip-address-168-63-129-16), [AWS-Metadaten](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/instancedata-data-retrieval.html), [Google-Metadaten](https://docs.cloud.google.com/compute/docs/metadata/querying-metadata). Die lokal begründete S1313-Ausnahme betrifft ausschließlich diese feste Sicherheitsdenylist; Adressen werden nicht zur Deploymentkonfiguration hartcodiert.

## Regressionstests

`GitHttpsNetworkBoundaryTests.cs` enthält 27 neue Fälle: explizite private IPv4/IPv6-Freigabe, IPv4-mapped IPv6 und Scopeabwehr, Infrastrukturadressen trotz breiter Allow-Policy, Denials, gemischte DNS-Antworten, Grenzen/ungültige Konfiguration, Tenant-/Referenz-/Port-/Host-/Pfadbindung vor DNS, Cancellation und unveränderliche Adresskopien. DNS wird hier kontrolliert eingespeist; dies ist keine reale private Peerabnahme.

`GitHttpsTransportTests.GenericCredentials.cs` ergänzt drei native Fälle mit echten Gitprozessen und lokalem TLS-Smart-HTTP-Server: zwei Basic-Benutzernamen und anonymes Lesen, jeweils mit exakten Modellbytes. Basic prüft Challenge/Helper und fehlende Secretartefakte; Anonymous prüft, dass keine Authorizationheader gesendet wurden, und verwendet einen absichtlich nicht vorhandenen Helperpfad. Der synthetische Benutzername `azure-pat-user` ist ausdrücklich kein Azure-DevOps-Integrationstest. Der bereits vorhandene Loopback-Testadapter bleibt isoliert; die Produktionspolicy erlaubt Loopback nicht.

Aktueller strenger Solution-Build bestanden: **0 Warnungen, 0 Fehler**, 30,34 Sekunden. Gezielte Suite einschließlich Fixture-Isolationsregression: **203 bestanden**, keine Fehler/Skips, 6,493 Sekunden. Der korrigierte anonyme native Fall zusätzlich einzeln bestanden, 2,759 Sekunden. Vollständige ausgewählte native Regression: **160 bestanden**, keine Fehler/Skips, 222,310 Sekunden. CI-safe nach der unten beschriebenen Fixturekorrektur: **1.594 bestanden, 0 Fehler, 8 externe Skips**, insgesamt 1.602 Tests, 1 Minute 21,804 Sekunden, Exit 0. Der sporadische C.8.0-Fehler trat in diesem zweiten vollständigen Lauf nicht auf; seine Ursache bleibt offen. Lokale Logs: `artifacts/git-generic-servers/gs03-*.log`; der erste fehlgeschlagene CI-Lauf bleibt in `gs03-ci-safe-before-isolation.log` erhalten.

Check-only-Formatprüfung der sieben neuen handgepflegten Dateien abschließend bestanden, Exit 0 (`gs03-format.log`). Die zusätzlich geprüfte bestehende `GitHttpsTransportTests.cs` hat bereits vorhandene Formatabweichungen in unveränderten Mitgliedern; der breite Check lieferte Exit 2 (`gs03-format-existing-fixture.log`). Zeilenenden dieser bearbeiteten Fixture auf LF gemäß `.editorconfig` normalisiert; übrige alte Formatabweichungen nicht umgeschrieben. Geänderte Fixturemitglieder manuell geprüft. `git diff --check`: Exit 0. Formatter nicht im Schreibmodus ausgeführt.

Im ersten gezielten Lauf wurde eine falsche Fehlerklassifikation für nicht angeschlossene Authentifizierungsarten festgestellt und der Vorabcheck getrennt. Im ersten nativen Lauf bestanden 159/160 Fälle; der neue anonyme Testserver behandelte einen fehlenden Authorizationheader fälschlich wie einen abweichenden Header. Die korrigierte Fixture verlangt ausdrücklich das Fehlen dieses Headers. Bestehende Git-/BPMN-Assertions und Fixtures wurden nicht gelockert.

Der erste CI-safe-Lauf hatte **1.475 Erfolge, 118 Fehler und 8 Skips**, insgesamt 1.601 Tests, Exit 2. 117 Fehler stammten vom gemeinsamen SQLite-Testaufbau (`UNIQUE constraint failed: EngineDeployments.Id`); zusätzlich trat der bereits zuvor beobachtete C.8.0-Fehler (`Vacation Approval` fehlt im FEEL-Kontext) auf. Ein neuer Regressionstest reproduziert deterministisch, dass zwei `SharedSqliteDbFixture`-Instanzen mit `:memory:;Mode=Memory;Cache=Shared` dieselbe Datenbank lesen. Jede Fixture erhält jetzt einen eigenen Namen, Sharing innerhalb derselben Fixture bleibt erhalten. Nur die Testinfrastruktur wurde korrigiert; Seed-Daten und fachliche Assertions bleiben unverändert. Regression vor Korrektur: erwartete Sichtbarkeit 0, tatsächlich 1; danach bestanden. C.8.0 und der zugehörige DMN-Fall bestanden im isolierten Lauf; die sporadische Konformitätsursache ist weiterhin nicht geklärt. Kein Test wurde ausgeschlossen oder umgeschrieben, um diesen Fehler zu verstecken.

Reproduzierbare Prüfungen (Editorassets unverändert, kein Restore):

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*GitHttpsNetworkBoundaryTests' -class '*SourceControlConnectionBindingTests' -class '*SourceControlConnectionProfilePersistenceTests' -class '*RepositoryConnectionProfileTests' -class '*SourceControlExecutionTargetTests' -class '*LegacyGitHubConnectionMappingTests' -class '*SourceControlSecurityTests' -class '*SourceControlOptionsTests' -class '*SharedSqliteDbFixtureIsolationTests'
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet tests/VertexBPMN.SourceControl.Tests/bin/Release/net10.0/VertexBPMN.SourceControl.Tests.dll -class '*CommitExecutionAcceptanceTests' -class '*GitHttpsTransportTests' -class '*NativeGitBoundaryTests' -class '*LeaseRunnerTests' -class '*BasicCredentialChannelTests' -class '*CredentialChannelAcceptanceTests' -class '*SourceControlContractTests' -class '*HttpsBoundaryTests' -class '*GitHubPullRequestTransportTests' -class '*GitHubPullRequestResponseTests' -class '*GitHubPullRequestReconciliationTests'
```

CI-safe: unveränderter Befehl und vollständige Ausschlussliste aus `.github/workflows/ci.yml`, Schritt **Test (CI-safe suite)**. Kein ungefilterter Gesamtlauf.

## Clean-Code-Review

Skill: `.agents/skills/dotnet-clean-code-review/SKILL.md`. Scope dieser Fortsetzung: zwölf C#-Dateien einschließlich Tests und der begrenzten Fixturekorrektur; neue Dateien vollständig, getrackte Dateien nur geänderte Mitglieder und betroffene Aufrufer. Basis: `02ebe35`, lokale Änderungen auf `codex/git-generic-servers`. Vorherige Änderungen wurden in den GS01/GS02-Berichten geprüft. Keine generierten Dateien in diesem neuen Scope. Abdeckung vollständig für diese Änderungen; bestehende unveränderte Mitglieder sind Kontext, kein erneuter Repositoryreview.

| Regel | Ergebnis und Evidenz |
| --- | --- |
| 1 Async/Cancellation | PASS: awaitable Verträge; DNS und native I/O erhalten Deadline/Caller-Token. Kein neues `async void`. |
| 2 Collection-Verträge | PASS: begrenzte materialisierte CIDR-/Adresslisten; genehmigte Adressen werden als Kopie zurückgegeben. |
| 3 Boolean-Modi | PASS: keine neuen Boolean-Modusschalter. Bestehende Fixtureoptionen unverändert; Authentifizierung über Enum/Lease. |
| 4 Verantwortungsnamen | PASS: Netzwerkboundary, genehmigtes Ziel und Profiled-Fetch sind getrennt; bestehender AuthHelper ist ein konkreter Credentialprozess. |
| 5 Regions | PASS: keine im Scope. |
| 6 Interfaces | PASS: keine neue Interfaceabstraktion; bestehender Credentialport bleibt die Grenze zwischen Git und Secretlease. |
| 7 Vererbung | PASS: keine neue Klassenhierarchie; Fixture implementiert den vorhandenen Disposalvertrag. |
| 8 Lesbarkeit | PASS: neue Dateien und geänderte Mitglieder manuell gelesen. Frühe Ablehnung, explizite Fehler, keine Null-Collections. |

D1–D7 durchgeführt, Ausgaben in `gs03-clean-code-D1.log` bis `D7.log`; D5/D6 Exit 1 bedeutet keine Treffer, die übrigen Exit 0. Die beiden ergänzten Fixturedateien gesondert mit D1–D7 und manuell geprüft (`gs03-isolation-clean-code-D*.log`): D1/D3/D7 Exit 0, übrige Exit 1 ohne Treffer. Kein Suchfehler in diesen Reviewpässen. Keine bestätigten verbleibenden Clean-Code-Befunde in den Änderungen dieser Fortsetzung. Review ersetzt keine externe Sicherheits- oder Providerabnahme.

## Offene nächste Schritte

1. Administrativen Netzwerk-/Trustprofilresolver mit unveränderlicher Identität und Deploymentkonfiguration anschließen; echte private Peers sowie Zertifikats-/DNS-/Rebinding-Negativfälle für Git und Hosting-HTTP qualifizieren.
2. Profileigene Credentialauflösung mit frischen Grants/Widerruf und Secretlifecycle anbinden. Keine GitHub-App-Lease an fremde Hosts weiterreichen.
3. Persistente Annahme/Idempotenz und Rekonstruktion samt Reconciliation vollständig auf den geschützten Zielvertrag umstellen. Vor Aktivierung neue Executor/Worker vollständig ausrollen und alte Worker drainen/stoppen.
4. Eigenen HTTPS-Server über den Produktpfad mit Lesen, Commit, Push, Konflikt und Recovery abnehmen, anschließend Hostingadapter und Studio gemäß Plan.

Keine Veröffentlichung, kein Push und keine Migration einer Benutzerdatenbank ausgeführt.
