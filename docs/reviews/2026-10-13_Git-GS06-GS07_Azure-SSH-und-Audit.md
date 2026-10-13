# Git-Plugin: Azure-PR-Adapter, SSH und Audit

Stand: 13. Oktober 2026. Entwicklungsbranch: `codex/git-generic-servers`; Ausgangscommit: `44aa932bd64f45a790e4ae68d159b2c5b27f3590`. Die DeepSeek-Übergabe bleibt verschoben. GS05, GS06 und GS07 bleiben als Gesamtpakete offen.

## Azure DevOps Services

Der fest registrierte `AzureDevOpsHostingAdapter` verwendet den bestehenden geschützten Hostingvertrag v3, die persistente PR-Annahme, Claims/Fences, Current-Authority-Prüfungen und Unknown-Reconciliation. Kein neuer paralleler Jobpfad.

- `AzureDevOpsHostingTargetPolicy` bindet den HTTPS-Git-Endpunkt, Organisation, Projekt-GUID, Repository-GUID und den API-Repositoryendpunkt. Projekt-/Repositoryname, GUIDs und Git-URL werden auch gegen die Detailantwort geprüft. Ein unter gleichem Namen neu angelegtes Repository wird abgewiesen.
- Provider-ID: `azure-devops-services`. API-Version 7.1 ist im Hostingziel gespeichert; der unveränderliche Ausführungsvertrag und die aktuelle Zielprüfung umfassen sie. Azure DevOps Server und `*.visualstudio.com` sind nicht angeschlossen.
- Eigene HTTP-Policy erlaubt ausschließlich PR-Create, gefilterte vollständige begrenzte Listen, Detailabfrage und Iterationen mit den fest erlaubten Queryparametern. DNS-Adressen werden wie beim GitHub-Profil direkt an den Socket gebunden; Proxy, Redirects, Cookies und Umgebungs-Credentials bleiben aus.
- `ProfiledHostingCredentialBroker.Azure` trennt `HostingAzureDevOpsPat`, extern ausgestellte `HostingHttpBearer` und explizite `HostingAzureDevOpsEntra`-Anwendungsidentität. Git- und API-Referenzen bleiben getrennt. Keine implizite Hostidentität, kein Auth-Fallback. Rotation wird auch vor Entra-Tokenbeschaffung geprüft.
- Ein Create ist ein einzelner POST. Nach Antwortverlust wird ausschließlich gelesen. Azure kürzt Beschreibungen in Listen; deshalb werden zugehörige Detailantworten vor dem Operationsmarkerabgleich gelesen. Doppelte Marker, nicht vollständiges Paging und widersprüchliche Daten bleiben unbekannt beziehungsweise unsicher.
- `active`, `completed` und `abandoned` werden getrennt normalisiert. Ein Merge benötigt tatsächliches erfolgreiches Mergeergebnis und Commit-ID. Aktuelle UUID-bezogene Votes stehen in `CurrentVotes`; sie werden nicht als GitHub-Reviewhistorie oder commitbezogene Releasefreigabe ausgegeben. Iterationen enthalten Source-/Target-Commitbezüge und müssen zum angenommenen Head passen.
- Studio-Beschriftungen sind providerneutral und zeigen aktuelle Votes getrennt. Razor kompiliert; eine Browserabnahme ist damit nicht nachgewiesen.

Offiziell nachgeprüfte Grundlagen: [PR Create, REST 7.1](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-requests/create?view=azure-devops-rest-7.1), [PR List mit gekürzten Beschreibungen](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-requests/get-pull-requests?view=azure-devops-rest-7.1), [Iterations](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-iterations/list?view=azure-devops-rest-7.1), [Repositoryidentität](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/repositories/get-repository?view=azure-devops-rest-7.1).

## Dauerhafter Audit-Export

Bestätigte Commit-/Push-/PR-Abschlüsse markieren den Export innerhalb derselben BPMN-Datenbankänderung als offen. Geschützte Auftrags-/Ergebnisbelege bleiben bis zur Auditübernahme erhalten. Der separate Hintergrundexport schreibt in die bestehende zentrale `AuditLogs`-Tabelle und quittiert danach in der Quelldatenbank.

`AuditLog.SourceControlEventId` hat einen eindeutigen gefilterten Index. Ein Neustart nach zentralem Commit vor Quittierung führt zur Prüfung desselben unveränderten Eintrags, nicht zu einem Duplikat. Widersprüchliche vorhandene Einträge werden nicht überschrieben. Fehler bleiben offen, werden ohne Exception-/Secretinhalt protokolliert und verzögert erneut versucht; andere Einträge können weiterlaufen.

Der Auditdatensatz enthält ursprünglichen Vertex-Actor/Tenant, Repository, Operation, Fence, Commit, gegebenenfalls Pushbezug sowie Profil-/Credentialreferenzen und den festen Gitautor. `HostingIdentityReference` ist eine Credentialreferenz, kein unabhängig nachgewiesener Hostingprincipal. Token, Schlüssel, Modellbytes, PR-Titel und Beschreibung werden nicht exportiert. Langfristige Audits aller Annahmen/Fehlversuche und eine genauer aufgelöste Hostingidentität bleiben offen.

Schemaänderungen: `SourceControlAuditExport` für `BpmnDbContext`; `SourceControlAuditIdempotency` für `ProcessMiningEventDbContext`. Typen sind providerneutral; Actor-ID-Länge wird auf 512 erweitert. Lokale Migrationsprüfungen verwenden isolierte SQLite-Datenbanken. Es wurden keine bestehenden oder produktiven Datenbanken migriert. Kein automatischer Down-Rollback nach neuen Auditdaten.

## SSH: vorbereiteter interner Read-Pfad

Neue administrative `SshNetworkPolicies`, `SshTrustProfiles` und `SshExecutablePath` bleiben optional. `GitSshNetworkBoundary` prüft exakte Repository-/Tenant-/Profilbezüge, alle DNS-Antworten und explizite private CIDRs. `ProfiledSshCredentialBroker` prüft aktuelle Rechte, Profil, Netzwerk-/Hostkeyrevisionen und Credentialmetadaten vor Ausgabe einer kurzlebigen Lease.

`ControlledGitProcess.FetchApprovedSshRevisionAsync` bereitet einen kontrollierten OpenSSH-Fetch auf einen festen Commit vor. `HostName` ist die freigegebene numerische IP, Hostkeys sind an einen festen Host-/Portalias gebunden. Globale SSH-Konfiguration, Agent, Proxy, Forwarding, Passwortdialoge und Verbindungswiederverwendung werden ausgeschlossen. Native Git-Quoten, Ausgabegrenzen und Prozessbaumbeendigung bleiben aktiv. Interner Timeout und Aufruferabbruch werden getrennt behandelt.

Schlüsseldateien liegen außerhalb Repository und Shared Workspace in einem zufälligen privaten Verzeichnis: Windows-DACL nur für das Dienstkonto, Unix-Verzeichnis 0700/Datei 0600. Normales Ende, Fehler und Cancellation räumen die eigenen Dateien auf. Administrativer Hostzugriff und Dateiexposition während der Sitzung bleiben zu berücksichtigen; nach abruptem Host-/Prozessabbruch ist die Orphan-Recovery noch nicht implementiert.

**SSH ist weiterhin nicht am öffentlichen Job-/API-/Studio-Pfad verfügbar.** Offen sind geschützter SSH-Ausführungsvertrag und Workerdispatch, Branch-/Historypfad, Commit/Push samt Ref-CAS/Reconciliation, Passphrase-/Schlüsselrotation-/Crash-Recovery und echte Windows-/Linux-Serverabnahmen. Die vorhandenen Tests nutzen synthetische Schlüsseldaten; sie belegen keine SSH-Anmeldung. Auf diesem Windows-Host ist der Client vorhanden, `sshd` wurde nicht gefunden; WSL/WSLC wurde für einen SSH-Serverlauf nicht verwendet. Profile verwenden explizite SSH-Ports und derzeit einfache sichere Repositorypfade.

Grundlagen: [OpenSSH-Konfiguration](https://man.openbsd.org/ssh_config), [OpenSSH-Schlüsselformat](https://github.com/openssh/openssh-portable/blob/master/PROTOCOL.key). Kein selbst entwickeltes kryptografisches Verfahren.

## Prüfungen und nächste Schritte

Vollständiger Release-Build mit dem bestehenden Asset-Skip-Schalter; Editorassets unverändert. Gezielte Klassen: `AzureDevOpsHostingTests`, `GitSshBoundaryTests`, `SourceControlConnectionBindingTests`, `SourceControlPersistenceTests`, `SourceControlOptionsTests`, `RepositoryHostingTargetTests`, `RepositoryHostingReceiptTests`, `GitHubHostingHttpPolicyTests`. Bestehende GitHub-Transport-/Response-/Reconciliationklassen zusätzlich geprüft. CI-safe verwendet exakt die Ausschlüsse aus `.github/workflows/ci.yml`.

Lokale Logs: `artifacts/git-generic-servers/azure-ssh-audit-*.log` (nicht versioniert). Vollständiger Release-Build: null Warnungen/Fehler (31,84 Sekunden). 325 gezielte Erfolge, keine Skips (45,775 Sekunden); 31 GitHub-Regressionsfälle, keine Skips (0,418 Sekunden). CI-safe: 1.904 Erfolge, acht bestehende externe Skips, keine Fehler (1:53,900). Beide EF-Modelchecks melden keine unversionierten Modelländerungen. Die zentrale Migration ist auch im isolierten SQLite-Audittest ausgeführt; keine Abnahme von SQL Server/PostgreSQL daraus ableiten. Bestehende externe Skips bleiben unperformierte Abnahmen. Der historische C.8.0-FEEL-Timeout ist durch diesen grünen Lauf nicht als behoben nachgewiesen.

Neue C#-Dateien sind check-only formatgeprüft; die gemeldeten Leerraumkorrekturen wurden nur in eigenen Dateien angewendet. D1–D7 und manuelle Prüfung aller acht Clean-Code-Regeln gemäß `.agents/skills/dotnet-clean-code-review/SKILL.md`: 41 eigene C#-Dateien beziehungsweise geänderte Mitglieder, keine verbleibenden Maintainability-Befunde in diesem Umfang. Generierte Designer/Snapshots sind aus dem Kandidatensweep ausgeschlossen; die beiden Migrationen sind zusätzlich manuell geprüft. Razoränderung separat auf Escaping und providerneutrale Darstellung gelesen; Browser nicht ausgeführt.

| Regel | Ergebnis im geprüften Umfang |
| --- | --- |
| Async/Cancellation | Awaitbare Verträge und Tokenweitergabe; SSH-Deadline gegenüber Aufruferabbruch durch zwei Regressionen getrennt; Lease-/Client-/Dateilebensdauer geprüft |
| Collections | Materialisierte begrenzte Listen; nullable Receipt-Properties bedeuten absichtlich „nicht geprüft“, nicht „keine Einträge“ |
| Boolean-Modi | Keine neuen Produktions-Modusschalter; AuditPending ist Zustand, callerCancels ein Testfallparameter |
| Namen | Transport, Policy, Broker und Exporter nach Verantwortung getrennt; Session nur innerhalb des Azure-Adapters |
| Regions/Struktur | Keine neuen Regions; kohäsive eigene Teildateien |
| Interfaces | Bestehende Hostingports mit festen Implementierungen erhalten; keine unnötigen neuen Interfaces |
| Vererbung | Nur vorhandene Partial-Typen und Framework-Basen für Handler/Hosted Service/Migration |
| Lesbarkeit | Eigene Methoden, Parameter, Guards, Grenzen und Nullzustände manuell geprüft; fremde Formatierungsaltlasten erhalten |

Nächster implementierbarer Schritt: SSH dauerhaft und versionsgetrennt anschließen, ohne HTTPS-/Legacyjobs falsch zu claimen. Danach native Windows-/Linux- und kombinierte API-/Studio-/Recoveryabnahmen. Azure benötigt reale getrennte PAT-/Entra-Abnahmen, Policyinformationen und Providerqualifikation. Audit benötigt zusätzliche Recovery-/Mehrreplikat-/Langzeitabnahmen. Keine Paket-, Anbieter- oder Produktionsfreigabe aus den lokalen Tests ableiten.
