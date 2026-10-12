# Git-Plugin: generische Git-Server-Unterstützung – Implementierungsplan

Stand: 9. Oktober 2026. Geprüfte Basis: `6c3b88a0089b27ca15334ed62e7a1872098d340d`, Branch `codex/studio-strict-build`, mit vorhandenen lokalen Studio-Änderungen. Diese Änderungen bleiben erhalten. Vor Umsetzung den aktuellen Checkout erneut inventarisieren.

Status am 12. Oktober 2026: **Implementierung auf `codex/git-generic-servers`, Gesamtauftrag noch nicht abgeschlossen.** Profilpersistenz mit CAS/Widerruf, geschützte HTTPS-Ausführungsverträge, Netzwerk-/CA-Grenzen, Basic/PAT, extern ausgestellte Bearer-Tokens sowie profilgebundene Read-/Commit-/Push-Pfade mit API/Studio-Anschluss sind implementiert. Die feste Hostingregistry führt die bisherigen GitHub-Funktionen weiter. Zusätzlich ist eine explizite Entra-Anwendungsidentität für Azure DevOps Services am Bearerbroker angeschlossen. Aktueller Übergabestand: Release-Build ohne Warnungen/Fehler, 447 gezielte Erfolge, 31 bestehende GitHub-Adapterregressionen, CI-safe 1.818 Erfolge und acht externe Skips. Profilgebundene GitHub-Hostingannahme mit getrenntem Ausführungsvertrag v3 ist ergänzt; zusätzliche Broker-/HTTP-/Audit-/Produktpfadprüfungen bleiben erforderlich. **Noch fehlender Code:** Azure-PR- und weitere Hostingadapter sowie SSH. **Noch fehlende Abnahmen:** echte kombinierte Server-/API-/Studio-/Recoveryläufe, Linux und getrennte Anbieterqualifikation. Der frühere C.8.0-FEEL-Timeout ist diagnostiziert, nicht behoben. Keine Paket- oder Produktionsfreigabe, keine Veröffentlichung oder externen Testwrites. Die folgenden Fortsetzungsabsätze halten historische Zwischenstände fest; der aktuelle Einstieg für die Weiterarbeit steht unter [DeepSeek-Übergabe](2026-10-12_Git-Generische-Server_DeepSeek-Uebergabe.md).

Fortsetzung GS02: Eine interne profilgebundene HTTPS-Read-Annahme speichert Ziel-, Netzwerk- und CA-Kontext im getrennten geschützten Ausführungsvertrag Version 2. Idempotenz sowie Rekonstruktion gegen aktuelle Rechte und Profile sind lokal geprüft; der aktive Produktprovider und Legacyworker verwenden diesen Pfad noch nicht. Nachweis: [HTTPS-Auftragsannahme und Wiederaufnahme](2026-10-11_Git-GS02_HTTPS-Auftragsannahme-und-Wiederaufnahme.md). Claiming, Executor, echte Prozess-Recovery und externe Pflichtabnahmen bleiben offen.

Fortsetzung GS04: Ein getrennter interner Claimpfad übernimmt ausschließlich typisierte profilgebundene Readjobs mit vollständiger gespeicherter Commit-ID. Gemeinsame Jobquoten, Lease/Fence, aktuelle Autorisierung und ursprüngliche Netzwerk-/CA-Vorgaben werden geprüft; der profilgebundene Fetchtransport liest seine Revision aus dem Auftrag. API, Studio und Hintergrundrunner sind noch nicht angebunden. Nachweis: [Read-Claiming und Transport](2026-10-11_Git-GS04_Profilgebundenes-Read-Claiming-und-Transport.md). Ein kombinierter echter Server-/Prozess-Recovery-Nachweis bleibt offen; keine allgemeine Git-Server-Supportfreigabe.

Fortsetzung GS04: Der interne Read-Executor verbindet den bestehenden profilgebundenen Fetch mit signiertem Workspace, frischen Heartbeat-/Abschluss-/Cleanup-Scopes und einem gefenceten Erfolgsabschluss nach erneuter Autorisierung. Lokale kontrollierte Lebenszyklustests ergänzen die Nachweise; der generische Projektionswert ist noch kein dauerhaft abrufbares File-/History-/Diff-Ergebnis. API, Studio und Jobhost sind weiterhin nicht angebunden. Nachweis: [Read-Executor und Lebenszyklus](2026-10-11_Git-GS04_Read-Executor-und-Lifecycle.md). Kombinierte echte Server-/Prozess-Recovery-Abnahme und GS04 insgesamt bleiben offen.

### Aktueller Entwicklungsstand: 12. Oktober 2026

Die vorstehenden Fortsetzungsnotizen beschreiben historische Zwischenstände. Inzwischen sind typisierte profilgebundene HTTPS-Leseoperationen mit verschlüsselten dauerhaft abrufbaren Ergebnissen, der produktive Providerpfad, API und Hintergrundrunner angeschlossen. Branchauflösung, Dateiliste, Datei, Historie und Diff werden aus dem akzeptierten Auftrag rekonstruiert. API-Verwaltung und Studio unterstützen Profilreferenzen und Fähigkeiten pro Repository.

Commit und Push nutzen für Basic-/PAT-Profile ebenfalls den geschützten Ausführungsvertrag Version 2. Ziel, Profilrevision, administrative Netzwerk-/CA-Vorgaben und Requestdigest bleiben gebunden; aktuelle Rechte werden erneut geprüft. Native HTTPS-Schreiboperationen benötigen ausdrücklich eine Push-Freigabe. Bestehende persistente Commit-/Push-Intents und Reconciliation bleiben erhalten; ein Profilwechsel darf den angenommenen Zielkontext nicht umleiten.

**GS04 und Lieferstufe A sind weiterhin nicht vollständig abgenommen.** Native Testservernachweise und kontrollierte Persistenz-/Lifecycleprüfungen ersetzen noch keinen kombinierten realen API-/Studio-/Server-Recoverylauf. GS05–GS08 (Hostingregistry, Azure/PAT/Entra, SSH und weitere Hostingadapter) bleiben offen. GS09 ist teilweise angeschlossen, GS10 nicht abgeschlossen. Wiki und Website wurden lokal um diesen Entwicklungsstand ergänzt; keine Veröffentlichung oder Anbieter-Supportfreigabe.

Nachweis und konkrete Prüfresultate: [HTTPS-Leseergebnisse, API und Schreibpfad](2026-10-12_Git-GS04_HTTPS-Leseergebnisse-API-und-Schreibpfad.md).

Fortsetzung GS05 am 12. Oktober: Der vorhandene GitHub-Create-/Reconciliation-/Live-/Reviewpfad läuft über eine feste Hostingregistry mit operationslokalen Adaptersitzungen. Eine standardbasierte interne Bearer-IPC-Vorprüfung nutzt die moderne Git-Capability `authtype`; kein Token in Argumenten oder persistierter Konfiguration. 131 native Git-/IPC-/Hostingfälle unter Windows bestanden. Profilgebundene Hostingannahme, Entra-Tokenissuer und weitere Adapter bleiben offen. Zwei breite CI-Prüfungen meldeten einen C.8.0-FEEL-Fehler; isolierte Diagnose bestand. Der letzte Lauf mit ergänzter Testfehlerdiagnose bestand mit 1.753 Erfolgen und acht externen Skips. Ursache und Teststabilität bleiben ungeklärt; kein Nachweis einer behobenen Engineursache. Nachweis: [Hostingregistry und Bearer-IPC](2026-10-12_Git-GS05_Hostingregistry-und-Bearer-IPC.md). Keine Gesamtfreigabe aus dieser Teilumsetzung.

## 1. Ziel und Bedeutung von „beliebige Git-Server“

Aktuelle Ergänzung GS04/GS06-Authentifizierung am 12. Oktober: Eine explizite Entra-Anwendungsidentität (`GitAzureDevOpsEntra`) ist für HTTPS auf `dev.azure.com` am Standardport am profilgebundenen Bearerbroker angeschlossen. Das vorhandene Azure.Identity-SDK beschafft ausschließlich Tokens mit der Azure-DevOps-Audience; keine implizite Hostidentität und kein Basic-Fallback. Dieser Authentifizierungsbaustein ist keine Freigabe von GS06: profilgebundene Hostingaufträge, Azure-PR-Adapter und echte getrennte PAT-/Entra-Abnahmen bleiben erforderlich. Nachweis: [Explizite Entra-Tokenbeschaffung](2026-10-12_Git-GS06_Explizite-Entra-Tokenbeschaffung.md). Ältere Fortsetzungsabsätze darunter beschreiben den jeweiligen historischen Stand.

Fortsetzung GS04/Authentifizierung am 12. Oktober: `HttpBearer` ist nun am profilgebundenen Produktbroker und an der geschützten Read-/Commit-/Push-Annahme angeschlossen. Extern ausgestellte Tokens benötigen `GitHttpBearer` mit `token`/`expiresAt`; aktuelle Rechte und Metadaten werden während der Auflösung erneut geprüft. Zehn neue Bearerfälle, insgesamt 392 gezielte Erfolge. Automatische Entra-Tokenbeschaffung ist weiterhin offen. Der breite C.8.0-Fehler ist inzwischen als vorgeschalteter FEEL-Timeout diagnostiziert, nicht behoben. Nachweis: [Profilgebundene Bearer-Credentials](2026-10-12_Git-GS04_Profilgebundene-Bearer-Credentials.md). Alle ausstehenden Hosting-/SSH-/Anbieter- und Gesamtgates bleiben erhalten.

VertexBPMN soll BPMN-Modelle mit administrativ freigegebenen, standardkonformen Git-Servern über HTTPS und SSH versionieren können. Git-Grundfunktionen dürfen weder eine GitHub App noch eine Pull-Request-API voraussetzen. Eigene Server im Unternehmensnetz sind ein ausdrücklicher Bestandteil des Ziels.

Der verbindliche Grundumfang umfasst Branchauflösung, Dateiliste, revisionsgebundenes Öffnen, Historie, Diff, Entwurf, Commit auf einem Arbeitsbranch, konfliktgeschützten Push und dauerhafte Ergebnisabfrage. Ein Server ohne Hosting-API bietet diesen Umfang ebenfalls, soweit seine Transport- und Authentifizierungsmerkmale qualifiziert sind.

Pull Requests, Merge Requests, Reviews und Branch-Policies gehören zu separaten Hosting-Adaptern. Das Git-Protokoll allein stellt diese Funktionen nicht bereit. Für unbekannte Anbieter werden sie als nicht verfügbar angezeigt; Git-Lesen und -Schreiben bleiben nutzbar. „Beliebig“ bedeutet damit ein erweiterbares Protokoll-/Authentifizierungsmodell, keine ungeprüfte Universalunterstützung aller Serverversionen oder proprietären Loginverfahren.

### Geplanter Lieferumfang

| Ziel | Git-Grundfunktionen | Hosting-Funktionen | Lieferstufe |
| --- | --- | --- | --- |
| Eigener Git-Server ohne Webplattform | HTTPS mit geschützten Credentials oder anonymem Lesen; SSH mit Schlüssel | Keine vorausgesetzt | A: HTTPS; C: SSH |
| GitHub.com | Bestehender GitHub-App-Pfad erhalten; generischer Transport | PR-Erstellung, Status, Reviews erhalten | A: Regression |
| Azure DevOps Services / Azure Repos | HTTPS mit Entra-Token beziehungsweise begrenztem PAT; SSH | PR-Erstellung, Status, Reviewer-/Policy-Daten | B: HTTPS und Hosting; C: SSH |
| Azure DevOps Server | Eigene Basis-URL, Collection/Projekt, unterstützte API-Version und Authentifizierung | Separat qualifizierter Azure-Adapter | D |
| GitLab.com / GitLab Self-Managed | HTTPS mit passendem Token/Credential; SSH | Merge Requests, Status und Reviews | D |
| Gitea / Forgejo | Generischer HTTPS-/SSH-Pfad | Eigene, getrennt qualifizierte Adapter | D |
| GitHub Enterprise Server | Eigene Git-/API-Basis-URLs und passendes GitHub-App-Profil | Versionsgebundener Enterprise-Adapter | D |
| Weitere Anbieter, etwa Bitbucket oder Gerrit | Grundumfang bei qualifiziertem HTTPS-/SSH-Profil | Erweiterbarer Adaptervertrag; ohne Adapter nur Grundumfang | E |

Alle Zeilen sind Zielzustände, keine aktuellen Supportzusagen. Für jeden Anbieter werden getestete Versionen, Authentifizierungsverfahren und Fähigkeiten dokumentiert. Unbekannte Server werden über ein explizites Profil eingebunden, nicht durch automatische Erkennung beliebiger URLs.

Nicht Teil dieses Plans: weitere Modellfamilien, Git LFS, rekursive Submodule, automatische Merges, automatischer Force-Push, Ausführung von Repository-Hooks, dynamisches Laden fremder DLLs oder neue Git-Deploymentfreigaben. DMN/CMMN/Forms und das DLL-Framework bleiben G10/G11 des bestehenden Plans. Git-Deployment und Provenienz bleiben G08; die neue Integration darf dessen Freigabegrenzen nicht umgehen.

## 2. Verifizierte Ausgangslage

Der [ursprüngliche Git-Plan](2026-10-05_Git-Modellversionierung_Implementierungsplan.md) wurde gelesen. Seine historischen Checkboxen ersetzen keine aktuelle Inventur. Der [G07-Bericht](2026-10-08_Git-G07_PR-Antwortvalidierung.md) dokumentiert inzwischen angeschlossene PR-Erstellung, Status und Reviews, aber keine abgeschlossene Gesamtqualifikation.

| Aktueller Befund | Dateien | Änderung erforderlich |
| --- | --- | --- |
| Git- und Hosting-Verträge existieren getrennt | `src/VertexBPMN.SourceControl.Abstractions/IModelSourceControlProvider.cs`, `IRepositoryHostingProvider.cs` | Verträge wiederverwenden, Hosting-Auswahl tatsächlich anschließen |
| Lesen, Commit und Push sind an GitHub-Tokenbroker gekoppelt | `src/VertexBPMN.Infrastructure/SourceControl/NativeGitModelSourceControlProvider.cs`, `SourceControlCommitExecutor.cs`, `SourceControlPushExecutor.cs` | Anbieterneutrale Authentifizierung auch für Grundfunktionen |
| Tokenbroker akzeptiert nur `github.com` und Typ `GitHubApp` | `GitHubAppTokenBroker.cs` | Bestehenden Broker als einen Authentifizierungsadapter erhalten |
| Native Prozesse und IPC verwenden `GitHubTokenLease`; Helper liefert festen Benutzernamen `x-access-token` | `ControlledGitProcess*.cs`, `GitCredentialChannel.cs` | Transport-/Credentialvertrag mit explizitem Authentifizierungsschema |
| Netzwerkschutz erlaubt HTTPS auf Port 443, öffentliche IPs und feste GitHub-Pagingqueries | `SourceControlHttps.cs`, `SourceControlOptionsValidator.cs` | Allgemeine Zielpolicy plus spezifische API-Routen; interne Ziele und andere Ports gezielt zulassen |
| PR-Annahme, Abgleich, Links und Ausführung enthalten GitHub-Bindungen | `PersistentSourceControlStore.PullRequest.cs`, `GitHubPullRequest*.cs`, `SourceControlPullRequestExecutor.cs`, `SourceControlPullRequestStatusReader.cs` | Gemeinsame Invarianten von Anbietersemantik trennen |
| Aktive DI registriert konkreten GitHub-Broker und direkten PR-Pfad | `src/VertexBPMN.Infrastructure/InfrastructureModule.cs` | Explizite Providerregistry mit passenden Lifetimes |
| Bindung enthält Remote/Credential, aber kein Transport-/Hostingprofil | `RepositoryBinding.cs`, `PersistentSourceControlStore*.cs` | Versioniertes Profil und kompatibles Upgrade persistieren |
| PR-Beleg hat `ProviderId`, Nummer und Reviews; normalisierte Zustände sind noch GitHub-geprägt | `PullRequestReceipt.cs`, `PullRequestReview.cs`, `PullRequestState.cs` | Anbieteridentität, Nummernkontext und Zustandstreue qualifizieren |
| Studio enthält GitHub-App-Eingaben und GitHub-Statusbeschriftung | `src/VertexBPMN.Studio/Components/Modeling/SourceControlWorkspacePanel.razor*`, `Services/HttpSourceControlService.cs` | Anbieterwahl und ressourcenbezogene Fähigkeiten |

Die aktuelle Vertex-Autorisierung verwendet `KeycloakSourceControlActorResolver`, tenantgebundene Credentials und Repository-Grants. Eine Entra-Authentifizierung gegen Azure Repos ersetzt nicht die Vertex-Benutzer-/Tenant-Autorisierung. Ein Wechsel des Vertex-Identityproviders ist kein Bestandteil dieses Plans.

In dieser Planungsrunde wurden Code und Berichte statisch geprüft, keine Builds oder Runtime-Tests ausgeführt. Frühere Teilabnahmen gelten ausschließlich für ihre dokumentierten Kandidaten und Umgebungen.

## 3. Architekturentscheidungen

### 3.1 Drei getrennte Verantwortlichkeiten

1. **Git-Transport:** kontrollierte native Git-Operationen über HTTPS oder SSH; feste Revisionen, Originalbytes, Pfadschutz, Quoten, Cancellation und sichere Ref-Aktualisierung.
2. **Remote-Authentifizierung:** kurzlebige, zielgebundene Authentifizierung aus geschützten Credential-Referenzen. Anbieter kennt Ablauf/Rotation und benötigte Rechte; Transport erhält keine beliebig ausführbare Konfiguration.
3. **Hosting:** optionale API für PR/MR-Erstellung, Live-Status, Reviews und gesonderte Policy-Nachweise. Kein Hostingadapter bedeutet keine Hostingaktionen, aber weiterhin Git-Grundfunktionen.

`IModelSourceControlProvider` bleibt der fachliche Git-Einstieg. `IRepositoryHostingProvider` wird nach Prüfung seiner vorhandenen Semantik für die realen Hostingpfade verwendet. `RepositoryConnectionProfile` und die zugehörigen Endpoint-/Hosting-/Verfahrensverträge wurden am 11. Oktober 2026 im ersten GS01-Baustein eingeführt; die aktive Provideranbindung folgt später. Neue Namen wie `IGitRemoteAuthenticator`, `GitAuthenticationLease` und `SourceControlProviderRegistry` sind weiterhin **Vorschläge**, keine schon vorhandenen APIs. Vor Einführung vorhandene Typen/Lifetimes prüfen; keine parallele zweite Jobarchitektur schaffen.

Provider werden zunächst fest und typisiert im Host registriert. Unbekannte Provider-ID führt zu einem verständlichen Fehler. Kein freier Typname, Assemblypfad, Shellbefehl oder vom Browser geliefertem API-Endpunkt als Registry-Eingabe.

### 3.2 Verbindungsprofile und Fähigkeiten

Ein administrativ verwaltetes Verbindungsprofil beschreibt:

- Stabile Profil-ID und Revision; Transport `Https` oder `Ssh`; optionaler Hostingprovider und dessen Adapterversion.
- Exakte freigegebene Git-/API-Endpunkte mit Scheme, Host, Port und erlaubten Pfadbereichen; Anbieterparameter wie Azure-Organisation/Projekt/Repository oder GitLab-Projekt-ID.
- Credential-Referenzen für Git und Hosting separat. Geteilte Referenz nur bei explizit passendem Credentialtyp und Zielkontext; niemals ungeprüft das Git-Credential an einen API-Host weiterreichen.
- Netzwerkpolicy für öffentliche beziehungsweise gezielt freigegebene interne Ziele sowie Trustprofil für CA oder SSH-Hostkeys.
- Transport-/Hostingfähigkeiten, Grenzen und Versionsverträglichkeit. Vorhandene `SourceControlOptions` werden kompatibel weiterentwickelt; Schlüssel im neuen Plan sind zunächst Designvorschläge.

Die Repositorybindung verweist auf ein Profil und behält Tenant, Remote, Modellroots, Branchregeln und Revision. Remote-URL und Hosting-Repository-ID müssen nachweislich dasselbe Repository bezeichnen. Keine automatische Umleitung von Anbieter A zu B allein durch URLänderung.

Fähigkeiten werden pro Repository und angemeldetem Actor ermittelt: konfigurierte Adapterfähigkeit, verfügbare Authentifizierung, aktuelle Rolle/Grants und Servervoraussetzungen. Ein allgemeines „Git verfügbar“ darf kein PR-Recht suggerieren. Read-only-Server oder Credentials dürfen Lesen erlauben, während Push gesperrt bleibt.

### 3.3 Authentifizierungsverfahren

| Verfahren | Geplante Verwendung und Regeln |
| --- | --- |
| Anonymes HTTPS | Explizites Profil nur für Lesen; keine Credentialauflösung, keine Umgehung der Vertex-ACL |
| HTTPS Basic / PAT | Benutzernamen und Secret aus typisiertem, tenantgebundenem Credential; ziel-/pfadgebundener IPC-Helper statt festem `x-access-token` |
| GitHub App | Bestehenden installations-/repositorygebundenen Broker erhalten, einschließlich frischer Rechteprüfung und Tokenablauf |
| HTTPS Bearer / Entra | Eigener Broker für passende Audience, Ablauf und Rechte; konkrete Identität explizit konfigurieren. Ein auf dem Host verfügbares Konto oder Managed Identity ist keine automatische Repositoryberechtigung |
| SSH-Schlüssel | Dienstgebundener Schlüssel, kontrollierter Zugriff und Ablauf der Sitzung; kein Benutzer-SSH-Agent oder globales SSH-Profil übernehmen |

Ein Git-Credentialhelper für Benutzername/Passwort ist kein allgemeiner Bearer-Headerkanal. Für Entra/Bearer wird ein gesonderter sicherer Übertragungsweg prototypisiert und abgenommen. Geheimnisse dürfen nicht in Argumentlisten, Remote-URLs, persistenter Git-Konfiguration, Logs oder Crash-/Testartefakten landen. Bevorzugt ist eine eng begrenzte, pro Operation abgesicherte lokale Vermittlung mit zielgebundener Headerinjektion; ihre TLS-, DNS- und Lebensdauergrenzen müssen unter Windows und Linux real geprüft werden. Scheitert dieser Nachweis, bleibt Bearer-Unterstützung offen. Kein stiller Rückfall auf Token in Kommandozeile oder globale Umgebungsvariablen.

Passwortgeschützte SSH-Schlüssel benötigen einen nicht interaktiven, dienstgebundenen Agent-/IPC-Pfad; bis zu dessen Abnahme werden sie als nicht unterstützt ausgewiesen. NTLM/Kerberos, Clientzertifikate und herstellerspezifisches SSO sind zusätzliche Authentifizierungsadapter mit eigener Qualifikation, kein pauschaler Fallback.

### 3.4 Interne Server, TLS und SSH

Der öffentliche Standard bleibt restriktiv. Für Self-Hosting wird eine eigene administrative Policy eingeführt: exakter Host und Port, erlaubte interne IP-/CIDR-Bereiche und Repositorypfade. Kein globales `AllowPrivateNetworks=true` ohne weitere Bindung. IP-literal-Ziele benötigen ein ausdrücklich dafür eingerichtetes Profil und passende Zertifikats-/Hostkeyprüfung.

Nach DNS-Auflösung werden alle nutzbaren Ziele gegen die Profilpolicy geprüft; Verbindung nur zu geprüften Adressen ohne zweiten unkontrollierten DNS-Lookup. API-, Git- und SSH-Prozesspfade müssen denselben Schutz nachweisen. Cloud-Metadata-/Link-local-, Loopback- und Managementziele bleiben gesperrt; lokale Testausnahmen sind isolierte Testadapter. IPv4, IPv6 und DNS-Rebinding ausdrücklich testen. Remote-Anwender dürfen weder neue Netzwerkfreigaben noch Trustanker anlegen.

TLS-Zertifikat und Hostname werden immer validiert. Unternehmens-CAs werden über ein administrativ bereitgestelltes Trustprofil unterstützt; `sslVerify=false` ist kein Lösungsweg. Redirects standardmäßig aus; notwendige Anbieterweiterleitungen nur nach eigener Ziel-/Pfadprüfung und ohne unkontrollierte Credentialweitergabe. Proxy-Unterstützung wird separat administrativ profiliert und getestet; keine Umgebungsproxy-Übernahme.

SSH verwendet feste ausführbare Datei, strukturiert erzeugte Argumente, `BatchMode`, strikte Hostkeyprüfung und ein geprüftes, dienstgebundenes Known-Hosts-Profil. Keine automatische Erstvertrauensannahme, kein `StrictHostKeyChecking=no`, kein `ProxyCommand`, Forwarding, Benutzerconfig oder unkontrollierter Agent. Hostkeywechsel bricht ab und erfordert administratives Trustupdate. SSH-Hostnamen/Ports/Benutzernamen und scp-artige URLs werden in ein geprüftes strukturiertes Ziel normalisiert. Schutz gegen Optionsinjection und DNS-Rebinding muss auch für den tatsächlichen SSH-Verbindungsaufbau greifen.

### 3.5 Hostingsemantik und unbekannte Ergebnisse

Anbieteradapter prüfen ihren jeweiligen Repositorykontext, Head-/Basebranch, angenommenen Headcommit, Anbieter-ID, URL und Status. PR-Nummern sind nur innerhalb ihres Anbieter-/Repositorykontexts eindeutig. Noch nicht abgefragte Reviews, leer abgefragte Reviews und unbekannter Zustand bleiben unterscheidbar; Azure-Reviewer-Votes oder GitLab-Approvals werden nicht blind als GitHub-Reviewzustände gespeichert.

Historischer Erstellungsbeleg bleibt unverändert. Livezustand erhält Abfragezeit, Quelle und Commitbezug. Unbekannt oder nicht vollständig paginiert bedeutet keine Freigabe. Squash/Rebase/Mergeverfahren können unterschiedliche Commitbezüge erzeugen; tatsächlicher Zielcommit und Nachweis sind anbieterspezifisch zu prüfen. Ein Approve, erfülltes Vote oder sichtbarer Merge ersetzt keine verifizierte Releasepolicy.

PR/MR-Erstellung behält persistente Write-Intents und einen providergeeigneten Operationsmarker. Nach Antwortverlust werden vorhandene Requests mit begrenztem vollständigem Paging und Detailabgleich gesucht. Kein blinder zweiter Create-Aufruf. Mehrdeutigkeit bleibt `ResultUnknown`. Derselbe Idempotenzschlüssel mit verändertem Provider, Repository, Head/Base oder Inhalt ergibt Konflikt.

### 3.6 Persistenz, Upgrade und Wiederanlauf

Bestehende Bindungen werden eindeutig auf ein Legacy-GitHub-Profil abgebildet. Fehlende neue Metadaten sind bei Altbelegen nur über diese dokumentierte Migrationsregel auflösbar, bei neuen Bindungen niemals als impliziter GitHub-Default. Bestehende Credentialtypen, IDs, ACLs, Belege und Operationen bleiben erhalten.

Angenommene Aufträge speichern unveränderlich Provider-/Profilidentität, fachliche Zielidentität und Vertragsversion zusätzlich zu Actor, Tenant, Revision und Inhaltsbindung. Secretwerte werden nicht eingefroren. Ausführung und Reconciliation prüfen aktuelle Grants, Profilwiderruf und Credentialstatus; der ursprüngliche externe Zielkontext darf durch zwischenzeitliche Profiländerung nicht wechseln. Ungültig gewordene Profile liefern einen eindeutigen Status.

Neue Worker unterstützen ausdrücklich qualifizierte alte Jobversionen. Alte Worker dürfen neue Auftragstypen nicht claimen. Rollout erst nach Drain beziehungsweise kompatibler Versionssteuerung; ein API-Rollback allein rollt laufende Hintergrundjobs nicht zurück. Keine destruktive Downmigration bei neu geschriebenen Belegen.

## 4. Arbeitspakete und Reihenfolge

Aufwand: S/M/L/XL relativ einschließlich Tests und Review; keine zugesagte Kalenderdauer. Alle Implementierungspakete sind offen. Nach jedem Paket Bericht mit Kandidatencommit, Dateien, Befehlen, tatsächlichen Ergebnissen und offenen Abnahmen ergänzen.

| ID | Arbeitspaket | Abhängigkeit | Aufwand | Pflichtabnahme |
| --- | --- | --- | --- | --- |
| GS00 | Aktuellen Stand, Runner, Git-/SSH-Versionen und bestehende Regression inventarisieren | Keine | S | Baseline und Alt-/Neuvertragsliste; keine fremden Änderungen einschließen |
| GS01 | Profil-, Credential-, Capability- und Hostingverträge festlegen | GS00 | M | Vertrags-/Securityreview mit Legacy-Mapping und Anbieterzuständen |
| GS02 | Persistenzupgrade und versionierte Aufträge | GS01 | L | Reale Upgrade-/Restart-/Claimtests, unveränderte Altbelege |
| GS03 | HTTPS-/interne Netzwerkpolicy und Trustprofile | GS01 | L | Echte Netzwerk-/TLS-/Rebinding-Negativfälle für API und Git |
| GS04 | GitHub-Kopplung aus Git-Transport und Credentialkanal lösen | GS02, GS03 | L | Eigener HTTPS-Server: Lesen, Commit, Push, Konflikt, Recovery; GitHub-Regression |
| GS05 | Hostingregistry und gemeinsamer persistenter PR/MR-Pfad | GS02, GS04 | L | Bestehender GitHub-Ablauf über Registry, Unknown-Reconciliation und Audit |
| GS06 | Azure DevOps Services: Authentifizierung und Hostingadapter | GS05; Bearer-Sicherheitsnachweis | XL | Echte isolierte Azure-Repos-Abnahme mit PAT und Entra getrennt |
| GS07 | SSH-Transport und Schlüssellifecycle | GS02, GS03, GS04 | XL | Echter eigener SSH-Server, Hostkey-/DNS-/Cancel-/Secretprüfungen auf Windows/Linux |
| GS08 | GitLab, Gitea, Forgejo und Enterprise-/Serverprofile | GS05; SSH-Anteile nach GS07 | XL | Anbieterweise getrennte HTTPS-/Hostingabnahme und Versionsmatrix |
| GS09 | Anbieterunabhängige API und Studio-Bedienung | Grundpfad ab GS04; Hosting nach GS05–GS08 | L | Reale Browser-/API-Abläufe und Capability-/Tenant-/Dirty-Regressionsfälle |
| GS10 | Gesamtqualifikation, Betrieb, Wiki und Website | Je Lieferstufe deren Pflichtpakete | L | Sauberer Kandidat, Build/CI-safe, reale Transport-/UI-Abnahme, Supportgrenzen |

Umsetzungsreihenfolge: **GS00 → GS01 → GS02 → GS03 → GS04 → GS05 → GS06 → GS07 → GS08 → GS10**. GS09 wird mit jedem verfügbaren Backendpfad inkrementell geliefert und muss vor dessen GS10-Abnahme abgeschlossen sein. Keine Backendstufe wird allein wegen einer sichtbaren Oberfläche als abgenommen markiert.

### GS00–GS04: generischer HTTPS-Grundumfang (Lieferstufe A)

- [ ] Baseline aus aktuellem Checkout; Änderungen an Studio zuerst von diesem Auftrag trennen. Ist-Stand von G02–G09 erfassen, offene Kriterien nicht als erledigt übernehmen.
- [ ] Bestehende `SourceControlOptions`, `RepositoryBinding`, `SourceControlCapability`, Credentials und Fehlercodes gezielt erweitern; öffentliche Verträge und OpenAPI kompatibel halten beziehungsweise Änderung versionieren.
- [ ] EF-Modell/Migrationen für Profile und Jobversionen ergänzen; aktuelle Source-Control-Entitäten und `PersistentSourceControlStore` verwenden.
- [ ] Broker aus `NativeGitModelSourceControlProvider`, Commit-/Push-Executor und `ControlledGitProcess` abstrahieren; Prozesssicherheit, Helper-IPC, Ref-CAS und Gitobjektformat erhalten.
- [ ] Geschützte Basic-/PAT-Credentials mit frei konfigurierbarem geprüftem Benutzernamen sowie explizites anonymes Lesen implementieren. Keine interaktiven Prompts oder globalen Helper.
- [ ] Eigenen internen HTTPS-Gitserver mit Unternehmens-CA und Portfreigabe real testen. API-/Git-Adressschutz unabhängig prüfen.
- [ ] GitHub-App-Pfad einschließlich aktueller ACL-Prüfung vor/nach Writes qualifizieren. Kein Produktions-PR und keine Tokenrechte zur Planbearbeitung verändern.

Abnahme A: Studio/API arbeitet mit einem eigenen HTTPS-Server **ohne GitHub-Credential und ohne Hostingadapter**. Zweiter unabhängiger Clone bestätigt Bytes/Commits/Refs. Leseberechtigung, Pushkonflikt, Crash-/Unknown-Recovery und deaktivierter Modus sind nachgewiesen.

### GS05–GS06: Hosting und Azure DevOps Services (Lieferstufe B)

- [ ] GitHub-PR-Executor/Statusreader hinter der Hostingregistry anschließen; gemeinsame Actor-/Tenant-/Lease-/Idempotenzinvarianten erhalten. GitHub-spezifische Decoder bleiben im GitHub-Adapter.
- [ ] PR-Annahme und Belege von festem `github.com` lösen; Provider-ID, Repositoryidentität, Profilrevision und Audit absichern. Audit trennt Vertex-Anwender, Gitautor und Hostingidentität.
- [ ] Azure-Organisation, Projekt, Repository-ID, zulässige API-Version und Remotezuordnung serverseitig binden. Query-Policy unterstützt nur die pro Adapter erlaubten Parameter, nicht beliebige Querystrings.
- [ ] Azure-PAT über geschützte Credentials sowie Entra-Broker mit Ablauf/Rotation und expliziter Identität implementieren; Basic- und Bearerpfad getrennt abnehmen.
- [ ] Azure PR erstellen, gezielt lesen und nach ungewissem Create abgleichen. Status `active/completed/abandoned`, Reviewer-Votes, Iterationen und Mergeergebnis mit qualifizierten Commitbezügen abbilden.
- [ ] Policyinformationen separat darstellen. Eine tatsächlich verifizierte Releasefreigabe erst im G08-Vertrag auswerten; unvollständige oder versionsbedingt nicht verfügbare Policy liefert keine Freigabe.

Abnahme B: tatsächlicher Azure-Repos-Gitpfad und PR-Erstellung/Liveabfrage im autorisierten Testprojekt; Entra und PAT separat; Rollenentzug, Tokenrotation, Paging/Rate-Limit, falsche Repositoryzuordnung und Antwortverlust geprüft. Azure DevOps Server gilt dadurch noch nicht als abgenommen.

### GS07: eigene SSH-Server (Lieferstufe C)

- [ ] Neue Transportauswahl einschließlich URLnormalisierung und fester SSH-/Known-Hosts-/Credentialreferenzen.
- [ ] Kontrollierten SSH-Prozess-/Verbindungspfad mit tatsächlich durchgesetzter Peerpolicy entwickeln; eine bloße DNS-Vorprüfung reicht nicht.
- [ ] Privaten Schlüssel nur im dienstgebundenen sicheren Zugriffspfad bereitstellen, gegebenenfalls temporäre Datei mit minimalen ACLs und garantiertem Cleanup; nie Repository-/Shared-Workspace-Datei. Restexposition dokumentieren.
- [ ] Hostkeyprüfung, Rotation, passphrasegeschützten Schlüsselpfad, Windows/Linux und Timeout-/Prozessbaumbeendigung separat qualifizieren.
- [ ] HTTPS und SSH verwenden dieselben fachlichen Commit-/Push-/Reconciliation-Verträge. Kein Shellzugriff, Portforwarding oder frei gewählter Remote-Befehl als Produktfunktion.

Abnahme C: eigener interner SSH-Gitserver mit positiven Read/Commit/Push-/Recoveryfällen; unbekannter/geänderter Hostkey, fremder Host/Port, Agentmissbrauch und Optionsinjection zuverlässig abgelehnt. SSH zu Azure/GitLab zusätzlich anbieterspezifisch prüfen, bevor es als unterstützt dokumentiert wird.

### GS08: weitere Hostingprofile (Lieferstufe D)

- [ ] GitLab: Cloud/Self-Managed-Basis-URL, Projekt-ID und MR-IID binden; MR/Create/Livezustand/Reviews/Abgleich sowie verfügbare Approval-APIs versions-/lizenzabhängig prüfen.
- [ ] Gitea und Forgejo: getrennte Registry-IDs und qualifizierte Versionen. Gemeinsamen Code nur bei belegtem gleichem Vertrag wiederverwenden; API-Kompatibilität nicht aus Produktverwandtschaft ableiten.
- [ ] GitHub Enterprise Server: konfigurierbare geprüfte Git-/API-Basis einschließlich Pfadpräfix; Tokenbroker, Antwort-URLprüfung und Paging ebenfalls auf dieses Profil binden.
- [ ] Azure DevOps Server: Collection-/Projektpfade, API-/Serverversion und verfügbares Authentifizierungsverfahren qualifizieren. Cloud-Entra-Verhalten nicht ungeprüft übertragen; fehlender Windows-Integrated-Auth-Adapter bleibt ausdrücklich offen.
- [ ] Einheitliche Anbieter-Contractsuite und echte isolierte Referenzinstanz pro beanspruchtem Hostingumfang. Kein Adapter wird wegen bestandener Git-Grundfunktionen als vollständig qualifiziert bezeichnet.

### GS09–GS10: API, Studio und Betrieb

- [ ] `SourceControlController`, DTOs und Studio-Dienste mit Profil-/Providerwahl und ressourcenbezogenen Fähigkeiten ergänzen. Server bestätigt Konfiguration; API nimmt keine freien Gitkommandos, Headers oder Trustdateien an.
- [ ] GitHub-spezifische Beschriftung durch Anbietername und passende PR/MR-Begriffe ersetzen. Credential-Formular fordert nur Referenzen und zulässige Metadaten; vorhandene geschützte Credentialverwaltung nutzen.
- [ ] Ohne Hostingadapter PR-/Reviewaktionen ausblenden und „Git-Versionierung verfügbar; Review erfolgt extern“ verständlich anzeigen. Fehlende Credentials, gesperrte Zielpolicy und unbekannter Hostkey sind unterscheidbare Fehler.
- [ ] Eigene PR-/MR-Belege anhand Provider und Repository wieder öffnen; Tenant-/Repositorywechsel entfernen alten Kontext. Dirty-Warnung, Export, Undo und Editoraktionen erhalten.
- [ ] Runbook für Git-/SSH-Versionen, Profile, private Netze/CA/Hostkeys, Rechte, Rotation, Quoten, Recovery, Backup und kompatiblen Rollout erstellen.
- [ ] Wiki `Plug-ins`/`Git-Plugin`, Website `plugins.html` und Supportmatrix nach **belegter** Stufe aktualisieren. Geplant, implementiert, lokal getestet und extern abgenommen getrennt ausweisen.

## 5. Verbindliche Abnahmefälle

Die IDs sind geplante Kriterien, keine vorhandenen Testmethoden. Zu jedem Fall konkrete Methode, Kandidat, Infrastruktur und Ergebnis eintragen. Bestehende GT01–GT19 bleiben für ihren jeweiligen Umfang gültig; dieser Plan ersetzt sie nicht.

| ID | Fall | Pflichtnachweis |
| --- | --- | --- |
| GS-T01 | Eigener HTTPS-Server ohne GitHub/Hosting | Branch/File/History/Diff/Commit/Push über echte API und Studio; zweiter Clone bestätigt Daten |
| GS-T02 | Anonymes Lesen / begrenztes Basic-/PAT-Credential | Vertex-ACL bleibt aktiv; fehlendes Pushrecht blockiert Write; keine Secretleckage |
| GS-T03 | Interner Host, eigene CA und anderer freigegebener Port | Verbindung funktioniert ausschließlich innerhalb exakter Profilpolicy; fremde Ziele abgelehnt |
| GS-T04 | DNS-Rebinding, IPv4/IPv6, Redirect, Metadataziel | Kein unerlaubter Socketkontakt und keine Credentialweitergabe über Git-/API-/SSH-Pfad |
| GS-T05 | Tenant-/Actor-/Grant-/Profilwechsel nach Annahme | Aktuelle Rechte vor Write; ursprünglicher Zielkontext unverändert; Fremdtenant ohne Zugang |
| GS-T06 | Credentialrotation/Ablauf/Widerruf | Frische Auflösung, kein globaler Cache/Fallback; sichere Fehler und Wiederanlauf |
| GS-T07 | Gleichzeitiger Push / Remote-Reset / Antwortverlust | Ref-CAS, kein Überschreiben fremder Commits, idempotenter dauerhafter Abgleich |
| GS-T08 | PR/MR-Create schreibt, Antwort geht verloren | Nach Neustart genau zugehörigen Request abgleichen; kein doppeltes Create, Mehrdeutigkeit unbekannt |
| GS-T09 | Azure Services mit PAT und Entra | Beide Authpfade real getrennt geprüft, richtige Org/Projekt/Repo und minimale Rechte |
| GS-T10 | Azure-/GitLab-/Gitea-/Forgejo-/Enterprisezustände | Provider-/Repositorybindung, vollständiges begrenztes Paging, Head-/Mergebezug und Reviews korrekt |
| GS-T11 | Keine Hosting-API oder keine Policy-/Reviewrechte | Git-Grundumfang bleibt verfügbar; kein erfundener Review-/Releaseerfolg |
| GS-T12 | SSH unbekannter/geänderter Key, fremder Port, Injection | Keine Verbindung mit unbestätigtem Trust, kein Fremdprozess/Agent/Forwarding |
| GS-T13 | Upgrade mit aktiven alten Jobs und Belegen | Legacy-GitHub-Zuordnung, kompatibles Claiming, Restart und keine Daten-/Zieländerung |
| GS-T14 | Windows/Linux, Cleanup, Quoten, Cancel | Reale Transport-/Prozess-/IPC-Pfade und sichere temporäre Ressourcen; keine fremden Dateien gelöscht |
| GS-T15 | GitHub-Regressionspfad | Bestehendes Lesen/Commit/Push/PR/Status/Review bleibt im isolierten Repository funktional |
| GS-T16 | Studio mit Provider-/Tenantwechsel und verspäteter Antwort | Neuer Kontext nicht überschrieben, Dirty-Inhalt erhalten, Export/Undo/Normalbetrieb intakt |
| GS-T17 | Deaktivierter Modus und unbekannter Provider | Kein Git-/SSH-/Credentialzwang; gespeicherte Bindungen/Jobs bleiben; vorhandene Engine nutzbar |
| GS-T18 | Squash/Rebase/Merge und nicht prüfbare Policies | Tatsächliche Commitbezüge beziehungsweise Unbekannt; kein automatischer Merge/Deploy |

## 6. Teststrategie und Freigabe

Reine Vertrags-/Policy-/DTO-/Zustandstests bleiben ohne Git, Browser oder externe Dienste CI-safe. Neue native HTTPS-/SSH-, DB-/Recovery-, Hosting- und Browserabnahmen bleiben explizite Opt-in-Suiten außerhalb des schnellen Workflows. Aktuelle Tests in `tests/VertexBPMN.SourceControl.Tests`, `tests/VertexBPMN.Tests` und `tests/VertexBPMN.Studio.UiTests` wiederverwenden; Runneroptionen aus `--help` und vorhandenen Scripts ableiten.

Für externe Writes sind konkrete Testrepositorys, Identitäten, minimal benötigte Berechtigungen und Cleanup vorab festzulegen. Keine Ressourcenbereitstellung oder Zugriffserweiterung aus dieser Planung ableiten. Native lokale Transporttests dürfen Testadapter verwenden, aber keinen Produktionsschutz abschalten. SQL-Server-Abnahme war im bisherigen Auftrag ausdrücklich ausgenommen; nicht eigenmächtig Infrastruktur dafür einrichten, sondern unveränderten Ausschluss und verbleibende Lücke berichten.

Bei Umsetzung gemäß `global.json` und aktueller CI:

```powershell
dotnet restore VertexBPMN.sln
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

Asset-Skip nur bei unveränderten oder separat korrekt gebauten Editorassets. Betroffene Tests zuerst, danach anwendbare CI-safe Suite mit der vollständigen aktuellen Ausschlussliste aus `.github/workflows/ci.yml`. `--no-build` verlangt einen aktuellen erfolgreichen Build. Diagnosebuild ist kein strenger Buildnachweis. Nach C#-Änderungen scoped Clean-Code-Prüfung gemäß `AGENTS.md`.

Eine Stufe ist erst abgenommen, wenn ihre Pflichtfälle real nachgewiesen sind. Fehlende Infrastruktur, Pflicht-Skips oder null entdeckte Tests bleiben offen. Alte G07-Lücken werden durch neue Provider nicht geschlossen. Der generische Git-Grundumfang kann separat freigegeben werden; Hosting, SSH und Releaseverifikation besitzen jeweils eigene Gates.

## 7. Erweiterbarkeit und weiterer Startauftrag

Nach A–D kann ein neuer Anbieter über einen fest registrierten Adapter hinzugefügt werden. Pflichtvertrag: konfigurierte Zielidentität, unterstützte Authentifizierung, explizite Fähigkeiten, redigierte Fehler, Cancellation/Quoten, sichere Credentials, PR/MR-Abgleich nach Unknown und gemeinsame Acceptance-Suite. Fehlt dessen Hostingadapter, darf ein qualifiziertes generisches Git-Profil trotzdem verwendet werden.

**Nächster konkreter Schritt bei einem späteren Implementierungsauftrag: GS00 und GS01.** Zuerst Ist-Stand/Baseline erfassen und Profile, sicheren Bearerkanal, interne Zielpolicy und Legacy-Jobmigration konkret reviewen; danach GS02–GS04 für den eigenen HTTPS-Server liefern. Keine PR-/SSH-Umsetzung vor ihren notwendigen Sicherheitsverträgen beginnen.

Kopierbarer Startauftrag:

> Lies `AGENTS.md`, diesen Plan, den ursprünglichen Git-Plan und den aktuellen G07-Bericht vollständig. Prüfe Checkout und vorhandene Änderungen. Bearbeite nach ausdrücklichem Implementierungsauftrag zuerst GS00/GS01, dann dependency-ordered GS02–GS04. Erhalte GitHub-App-Pfad, Tenant-/Grant-/Credentialschutz, dauerhafte Jobs, Editorfunktionen und lokale/WSLC-Profile. Liefere einen echten eigenen HTTPS-Gitpfad ohne Hostingzwang. Kein automatischer Merge/Deployment, keine dynamische Fremd-DLL und keine externe Ressourcen-/Rechteanlage. Berichte tatsächliche Build-/Testergebnisse und offene Pflichtabnahmen pro Paket; Commit/Push/Remote-Schreibtests nur im autorisierten Umfang.

## 8. Technische Quellen

Am 9. Oktober 2026 zur Planung geprüft; Anbieter- und Versionsdetails vor Implementierung erneut prüfen.

- [Git: Credential-Vertrag und Credential-Kontext](https://git-scm.com/docs/gitcredentials): Grundlage des zielgebundenen HTTPS-Helperpfads.
- [Microsoft: Authentifizierung für Azure Repos](https://learn.microsoft.com/en-us/azure/devops/repos/git/auth-overview?view=azure-devops): Entra-Token, PAT und SSH als unterschiedliche Authentifizierungswege. Die dortigen Beispielkommandos werden wegen der strengeren Vertex-Secretpolicy nicht unverändert übernommen.
- [Microsoft: Azure DevOps Pull Request Create, REST 7.1](https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-requests/create?view=azure-devops-rest-7.1): organisations-/projekt-/repositorygebundene Hosting-API mit expliziter API-Version.
- [GitLab: Merge Requests API](https://docs.gitlab.com/api/merge_requests/): eigener MR-Vertrag; keine Gleichsetzung mit GitHub-PR-Semantik.

Für SSH, Gitea, Forgejo, Enterprise Server und Azure DevOps Server werden die offiziellen Versions-/Sicherheits-/API-Quellen im jeweiligen Paket verbindlich ergänzt. Hierfür wurde keine externe Laufzeitabnahme durchgeführt.
