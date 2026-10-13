# Git GS03/GS04: Adminprofile und einmalige Netzwerkfreigabe

Stand: 11. Oktober 2026. Branch `codex/git-generic-servers`, Basis `02ebe35`, lokale Änderungen. Fortsetzung des [generischen Git-Plans](2026-10-09_Git-Generische-Server-Unterstuetzung_Implementierungsplan.md) und der [profilgebundenen Credentialauflösung](2026-10-11_Git-GS04_Profilgebundene-Credentialaufloesung.md).

## Ergebnis und Status

Administrative HTTPS-Netzwerk- und CA-Profile werden jetzt aus `SourceControlOptions` gebunden und validiert. Der registrierte `GitHttpsPolicyRegistry` löst ausschließlich mandantengebundene Referenzen gegen die aktuelle Serverkonfiguration auf. Der registrierte `ProfiledGitHttpsTransport` verbindet aktuelle Autorisierung, eine DNS-Freigabe, den Profilcredentialbroker und den kontrollierten Git-Fetch. Git verwendet dieselben geprüften Adressen; nach Credentialauflösung erfolgt kein zweiter DNS-Lookup.

Dieser Ablauf ist ein **vorbereiteter interner Transport**, noch nicht der aktive Studio-/API-/Workerpfad. Neue Jobverträge werden weiterhin nicht aktiviert. GS03 und GS04 bleiben offen, bis insbesondere ein echter privater Server und die dauerhaften Produktabläufe qualifiziert sind. Die lokalen TLS-Tests ersetzen keine Azure-DevOps-, SSH-, Linux- oder Hosting-API-Abnahme.

## Dateien

| Bereich | Dateien und Änderung |
| --- | --- |
| Konfigurationsverträge | `GitHttpsNetworkPolicy.cs`: Revision/Widerruf; neu `GitHttpsTrustProfile.cs`: öffentliche CA-Bündel; `SourceControlOptions.cs`: `HttpsNetworkPolicies` und `HttpsTrustProfiles` |
| Validierung | Neu `Application/SourceControl/GitHttpsConfigurationPolicy.cs`; `SourceControlOptionsValidator.cs` erweitert; `RepositoryConnectionProfilePolicy.cs` ergänzt um gemeinsame HTTPS-Scopeprüfung |
| Auflösung | Neu `Infrastructure/SourceControl/GitHttpsPolicyRegistry.cs` und `ResolvedGitHttpsPolicy.cs`; `ApprovedGitHttpsTarget.cs` enthält den festen administrativen Snapshot |
| Transport | Neu `ProfiledGitHttpsTransport.cs` und `ControlledGitProcess.ApprovedHttps.cs`; `GitHttpsNetworkBoundary.cs` kopiert/validiert die Policy vor DNS; `InfrastructureModule.cs` registriert die Dienste |
| CI-safe Tests | Neu `GitHttpsPolicyRegistryTests.cs` und `SourceControlConnectionBindingTests.HttpsPreparation.cs` |
| Native Tests | Neu `GitHttpsTransportTests.ApprovedProfiles.cs`; bestehende TLS-Fixture in `GitHttpsTransportTests.cs` unterstützt einen synthetischen Testhostnamen |

Pfade ohne vollständiges Präfix stehen unter `src/VertexBPMN.SourceControl.Abstractions`, `src/VertexBPMN.Application`, `src/VertexBPMN.Infrastructure` beziehungsweise den bisherigen Unit-/SourceControl-Testprojekten. Insgesamt 17 C#-Dateien in dieser Fortsetzung; generierte Dateien unverändert.

## Administrative Konfiguration

Neue Schlüssel unter `SourceControl`:

| Schlüssel | Semantik |
| --- | --- |
| `HttpsNetworkPolicies` | Bis zu 128 eindeutige `(TenantId, Id)`-Profile mit `Revision`, `IsRevoked`, HTTPS-`RepositoryScope`, `AllowedAddressRanges`, `DeniedAddressRanges` |
| `HttpsTrustProfiles` | Bis zu 128 eindeutige `(TenantId, Id)`-Profile mit `Revision`, `IsRevoked`, HTTPS-`RepositoryScope`, `CertificateAuthorityPem` |

`Revision` beginnt bei 1. Änderungen sollen die Revision erhöhen. Der Vergleich prüft zusätzlich den ganzen Inhalt, sodass unveränderte IDs/Revisionen bei verändertem CIDR oder CA-Bündel keine bereits vorbereitete Freigabe wiederverwenden können. Dies ist ein operationslokaler Vergleich, noch kein persistierter Policybeleg bei Jobannahme.

Beispiel einer zusätzlichen administrativen Netzwerkpolicy innerhalb einer ansonsten gültigen bestehenden `SourceControl`-Konfiguration:

```json
{
  "HttpsNetworkPolicies": [
    {
      "Id": "corporate-git",
      "TenantId": "company-a",
      "Revision": 1,
      "IsRevoked": false,
      "RepositoryScope": "https://git.example.internal:8443/team",
      "AllowedAddressRanges": ["10.40.0.0/16"],
      "DeniedAddressRanges": ["10.40.1.0/24"]
    }
  ],
  "HttpsTrustProfiles": []
}
```

Ein Verbindungsprofil verweist darauf über `GitEndpoint.NetworkPolicyReference = "corporate-git"`. Eine null `TrustProfileReference` verwendet die normalen TLS-Vertrauensanker des Gitprozesses. Für eigene CA-Vertrauensanker liefert der Administrator ein Trustprofil mit passendem Tenant und Scope und dem öffentlichen PEM-Bündel; das Verbindungsprofil enthält nur dessen ID. Kein Request darf CIDRs, CA-Inhalt, Zertifikatsdateipfade oder ein `sslVerify=false` vorgeben. Die bestehende `AllowedHosts`-Konfiguration des Legacy-Pfads bleibt erforderlich und unverändert.

CIDR-Listen sind jeweils auf 64 Einträge begrenzt. Die öffentliche Standardpolicy bleibt restriktiv; eine private Zieladresse benötigt explizite Freigabe. Die bestehenden Infrastruktur-/Metadata-/Loopback-Sperren gelten auch bei breiten CIDRs. Scopes müssen HTTPS mit kanonischem Host, Port und Repositorypfad sein; Rootscope, Userinfo, Query, Fragment und Traversal werden abgelehnt.

CA-Bündel enthalten höchstens 16 X.509-CA-Zertifikate und 64 KiB Text. Zulässig sind ausschließlich `CERTIFICATE`-PEM-Blöcke, mit CA-Basic-Constraints und, sofern KeyUsage vorhanden, `KeyCertSign`. Andere PEM-Blöcke, private Schlüssel, Leaf-Zertifikate und Fremdtext werden abgelehnt. Zertifikatskette, Ablauf und Hostname werden weiterhin durch den tatsächlichen TLS-Stack geprüft. Es werden keine Zertifikate in Betriebssystem-Stores installiert und keine globalen Gitoptionen geändert.

## Ausführung und Lebensdauer

1. Aktuelle Rolle und Repositorygrants sowie der eingefrorene Zielvertrag werden im Store geprüft.
2. Aktuelle Netzwerk-/Trustreferenzen werden gegen Serverkonfiguration aufgelöst; fehlende, widerrufene oder fremde Profile werden abgelehnt.
3. DNS wird einmal aufgelöst. Alle Antworten müssen zur Netzwerkpolicy passen; Adressen und CIDR-Listen werden kopiert.
4. Nach DNS aktuelle Profile, Grants und administrative Konfiguration erneut prüfen. Erst anschließend löst der Credentialbroker die Secrets auf.
5. Vor Start des Gitprozesses Autorisierung und administrative Konfiguration nochmals prüfen. Der Prozess erhält ausschließlich die freigegebenen IP-Adressen über den vorhandenen kontrollierten Resolve-Kanal.
6. Bei eigenem CA-Profil dessen festen öffentlichen PEM-Inhalt mit `CreateNew` in eine eindeutige Datei im kontrollierten Arbeitsbereich schreiben. Git verwendet die vorhandenen TLS-Optionen `http.sslCAInfo` und unter Windows `http.schannelUseSSLCAInfo`; Zertifikatsprüfung bleibt eingeschaltet.
7. Die Datei nach Erfolg, Fehler oder Cancellation entfernen. Cleanupfehler nach einem erfolgreichen Fetch liefern einen sicheren Fehler; bei schon fehlgeschlagener Ausführung bleibt deren ursprünglicher Fehler erhalten. Ein eventuell verbliebenes öffentliches CA-Bündel fällt zusätzlich unter den bestehenden Workspace-Cleanup.

Die Git-TLS-Optionen und die Windows-CA-Bündelsemantik entsprechen der [offiziellen Git-Konfiguration](https://git-scm.com/docs/git-config). Die Zertifikatsvalidierung verwendet den vorhandenen [.NET-X509CertificateLoader](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificateloader?view=net-10.0); keine eigene TLS- oder Kryptografieimplementierung.

Konfigurations- und Berechtigungsprüfungen sind keine atomare Transaktion mit externen Sockets. Änderungen unmittelbar nach der letzten Prüfung widerrufen eine bereits laufende Verbindung nicht sofort. Die im vorherigen Bericht dokumentierten Credential-Snapshotgrenzen bleiben bestehen. Persistierte Policyrevisionen, Executor-/Workeraktivierung und Recovery müssen im nächsten Paket zusammenhängend qualifiziert werden.

## Prüfung

| Prüfung | Ergebnis |
| --- | --- |
| Strenger Release-Build der gesamten Lösung | Erfolgreich, 0 Warnungen, 0 Fehler; 17,62 s |
| Gezielte Konfigurations-/Profil-/Ziel-/Securitytests | 254 bestanden, 0 Fehler, 0 übersprungen; 11,717 s |
| Native Git-/TLS-/Helper-/GitHub-Auswahl | 165 bestanden, 0 Fehler, 0 übersprungen; 229,481 s |
| CI-safe gemäß unverändertem Workflow | 1.645 bestanden, 0 Fehler, 8 externe Tests übersprungen; 1.653 gesamt, 1 min 33,219 s |
| Formatprüfung der neun neuen Dateien plus Netzwerkvertrag | Erfolgreich, Exit 0; ausschließlich Checkmodus |

Die acht externen Skips sind nicht ausgeführte Abnahmen. Der zuvor dokumentierte sporadische C8-FEEL-Fehler trat in diesem CI-Lauf nicht auf; seine Ursache ist außerhalb dieser Fortsetzung weiterhin ungeklärt.

36 neue CI-safe Fälle prüfen reales Configuration-Binding einschließlich fehlender/leer gebundener CIDR-Listen, ungültige Identitäten/Revisionen/Scopes/CIDRs, beschränkte CA-Bündel, fremde/widerrufene Referenzen, unveränderliche Snapshots und Änderungen ohne Revisionserhöhung. Vorbereitungstests prüfen genau eine DNS-Auflösung ohne Credentiallookup, aktuelle Rollen/Profile nach DNS, Konfigurationswiderruf und Cancellation.

Fünf neue native Fälle verwenden echten Git und den echten Helper gegen einen isolierten TLS-Gitserver. Der synthetische `.invalid`-Hostname hat keine reguläre DNS-Auflösung; der Test injiziert explizit den Loopbackpeer, während die Produktionsboundary Loopback unverändert ablehnt. Geprüft werden Basic und anonymes Lesen mit Originalbytes, falsche CA, falscher Hostname, kein Credentialkontakt bei TLS-Ablehnung und CA-Cleanup nach Cancellation. Dies qualifiziert den internen Git-/TLS-Pfad mit Testpeer, nicht den gesamten Produktionsresolver oder eine private Unternehmensinstanz.

Build:

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

Editorassets unverändert. Gezielte Auswahl ergänzt die vorherigen Klassen um `*GitHttpsPolicyRegistryTests`; die neuen Preparationtests liegen in der vorhandenen `*SourceControlConnectionBindingTests`-Klasse. Native Auswahl entspricht dem GS03-Bericht; alle neuen Fälle liegen unter `*GitHttpsTransportTests`. CI-safe nutzt exakt den Befehl samt Ausschlüssen aus `.github/workflows/ci.yml`. Logs unter `artifacts/git-generic-servers/gs03-registry-*`.

## Clean-Code-Prüfung

Skill `.agents/skills/dotnet-clean-code-review/SKILL.md`; Scope: die 17 C#-Dateien dieser Fortsetzung. Neue Dateien vollständig; vorhandene Dateien nur geänderte Mitglieder und betroffene Aufrufer. Basis `02ebe35` und lokale Änderungen. Generierte Dateien ausgeschlossen. D1–D7 mit `rg`, Kandidaten in `gs03-registry-clean-code.log`, kein Suchfehler in diesen Reviewpässen; neue Dateien/geänderte Mitglieder manuell gegen alle zwölf Lesbarkeitskategorien geprüft.

| Regel | Ergebnis / Evidenz |
| --- | --- |
| 1 Async/Cancellation | PASS: DNS, Store, Credentials, Dateischreiben und Git erhalten den Operationstoken; keine neue async-void-Methode. Cancellationtest wartet den Fetch auch bei Assertionfehler im finally ab. |
| 2 Collection-Verträge | PASS: begrenzte materialisierte Profile/CIDRs; unveränderliche CIDR-Kopien und kopierte IP-Adressen; fehlende Konfigurationslisten werden sichere leere Listen. |
| 3 Boolean-Modi | PASS: IsRevoked ist Profildatum; keine neuen bool-Modusschalter. Bestehende Fixtureflags bleiben unverändert. |
| 4 Verantwortungsnamen | PASS: Konfigurationspolicy, Registry, Transportorchestrierung und native Prozessgrenze sind getrennt. |
| 5 Regions | PASS: keine in den Änderungen. |
| 6 Interfaces | PASS: keine neue Interfaceabstraktion; bestehende Actor-/Credential-/Optionsports erhalten. |
| 7 Vererbung | PASS: keine neue eigene Klassenhierarchie; Partial-Klassen erweitern vorhandene Typen. |
| 8 Lesbarkeit | PASS: geänderte Mitglieder manuell gelesen, frühe Ablehnung und sichere Fehler; Formatcheck erfolgreich. |

Keine bestätigten verbleibenden Clean-Code-Befunde in diesem Scope. Die methodenbezogene CA2025-Ausnahme im Cancellationtest folgt der bereits vorhandenen Testkonvention: Der Fetch wird nach Cancellation im finally vor Lease-/Token-Disposal abgewartet; der Analyzer modelliert diesen Kontrollfluss nicht. Keine Produktassertion wurde abgeschwächt. Die ersten Builds meldeten Compile-/Analyzerbefunde im neuen Code; sie wurden vor dem erfolgreichen strengen Gesamtbuild behoben. Die erste Formatprüfung fand eine Whitespacestelle im neuen Cleanupblock; sie wurde manuell korrigiert. Der Review ist keine externe Sicherheitsabnahme.

## Offene Pflichtabnahmen und nächste Arbeit

1. Profilgebundene Annahme, Idempotenz und persistierter Netzwerk-/Trustbeleg mit Executor, Claiming und Reconciliation verbinden. Alte Worker vor Aktivierung drainen/stoppen; neue Jobversionen bleiben bis dahin gesperrt.
2. Generische Branchauflösung, History/Diff, Commit und Ref-CAS-Push ebenfalls über denselben kontrollierten Profilpfad führen.
3. Eigenen echten privaten HTTPS-Server samt Unternehmens-CA und nichtstandardisiertem freigegebenem Port durch API und Studio abnehmen, einschließlich Konflikt, Widerruf, Crash-/Unknown-Recovery und unabhängiger Cloneprüfung.
4. Hosting-HTTP mit gleicher Sicherheitsgrenze gesondert qualifizieren; danach Azure/GitLab/SSH und Studio gemäß Abhängigkeiten.
5. Wiki und Website nur um belegten Supportumfang erweitern. Keine neue Supportzusage aus bloßer Registrierung oder isolierten TLS-Tests ableiten.

Keine Veröffentlichung, kein Commit/Push, keine externe Testschreiboperation und keine Migration einer Benutzerdatenbank ausgeführt.
