# Git-Modellversionierung – G01: Verträge, Rechte und Sicherheitsgrenzen

Stand: 2026-10-05. Basiscommit: `cb88bee0f83807cd0d7906a4d092807a56b49969`.
Branch: `codex/git-source-control-phase-1`.

**Status: Vertragsbibliothek implementiert und isoliert getestet; Produktprovider/API/UI noch nicht implementiert.**
Projektweite lokale Baseline ist nach OpenAPI-/Profilerkorrektur erfolgreich: 1.279 Tests bestanden, kein Fehler, acht externe Fälle übersprungen; Details und Grenzen in [G00](2026-10-05_Git-G00_Inventur-und-Baseline.md). Dieses Dokument definiert den Implementierungsvertrag für G02–G08; eine definierte Schutzregel ist noch kein laufzeitbewiesener Schutz.

## 1. Gelieferte Dateien und Schichtgrenze

- `src/VertexBPMN.SourceControl.Abstractions/`: packagefreie .NET-10-Bibliothek mit Context/Commit-ID/Idempotenz/Ref-Erwartung, Snapshot, DTOs, Capability-/Permission-/Fehlercodes, Options/Limits und zwei Providerinterfaces.
- `src/VertexBPMN.Application/VertexBPMN.Application.csproj`: Referenz auf diese Abstraktionen; keine Providerregistrierung oder neue Startupbedingung.
- `tests/VertexBPMN.SourceControl.Tests/`: isolierter MTP/xUnit-v3-Vertragstesthost, unabhängig von API, Browser, Git, DB und Netzwerk.
- `tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj`: derselbe reine Testquellcode wird verlinkt und im bestehenden CI-safe Testprojekt mitkompiliert. Keine zweite Testimplementierung und kein neuer Workflowjob.
- Solution registriert die beiden Projekte. Infrastructure und Studio erhalten ihre direkten Referenzen erst mit konkreten G02–G06-Verbrauchern; Plugin/API-Referenzen werden nicht als Vertragsbasis verwendet.

Die produktiven HTTP-DTOs brauchen in G05 explizite Mapper. Hostkontext und `ReleaseEvidence` dürfen nicht aus Browser-JSON als vertrauenswürdige Beweise deserialisiert werden. Die Konstruktoren prüfen nur grundlegende Invarianten; Pfad-/Netzwerk-/Content-/ACL-Prüfungen folgen im jeweiligen Anwendungsfall.

## 2. Daten- und Operationsvertrag

| Vertrag | Pflichtinhalt / Regel |
| --- | --- |
| `SourceControlContext` | Expliziter kanonischer Tenant (max. 64), authentifizierter Actor (max. 512); kein null/global-Fallback oder Controlchar. Actor issuerqualifiziert; für API-Keys stabile geprüfte Keyidentität, nie Keywert |
| `RepositoryBinding` | Server-ID/Tenant/Remote/Credential-ID/Default-/Releasebranch/Modellroots; Remote ohne eingebettete Zugangsdaten. Nicht ungeprüft aus Request übernehmen |
| `RepositoryGrant` | Stabile Actor-ID + unabhängige Permissionflags; Actor kann nicht über Anzeigenamen oder Commitautor nachgeahmt werden |
| `GitCommitId` | Vollständige 40- oder 64-stellige hexadezimale Object-ID; Canonical lowercase. Kein Branch, Kurz-SHA oder Revisionausdruck. Provider muss Existenz und Commit-Objekttyp nachweisen |
| `EditSession` | Tenant/Actor/Repository/Workbranch/Basiscommit/Dokumentgeneration/lokale und committed Revision/Ablauf; nur Besitzer bearbeiten, Job-/Sessionreferenzen serverseitig verifizieren |
| `ModelSnapshot` | Originalbytes, Modellart, Pfad, Generation, lokale Revision, SHA-256 über Bytes. Defensive Kopien verhindern Input-/Outputmutation. Kein XML im Diagnose-ToString |
| Read/Tree/History/Diff | Feste Commit-ID bzw. Basiscommit+Snapshot; serverseitig validierte Modellpfade; Paging; Diff darf begrenzt sein, Commitinhalt nicht still gekürzt |
| `CommitCommand` | Operation-/Idempotenz-ID, Session/Basiscommit/Workbranch/Nachricht/explizite Snapshots. G03 kopiert Liste und Bytes beim dauerhaften Annehmen, bevor asynchron gearbeitet wird |
| `CommitReceipt` | Exakter Commit, Session/Branch und pro Datei Generation/Revision/Hash; verspätete Antwort darf neuere Editrevision nicht clean markieren |
| `PushCommand` | Exakter Commit, eigener Arbeitsbranch und zwingendes `ExpectedRemoteRef`: erwartete ID oder MUSS fehlen. Null ist keine Erlaubnis zum ungeprüften Überschreiben |
| PR-Vertrag | Operation-ID, Headcommit/Headbranch/Basebranch, Titel/Beschreibung; keine Merge-/Approval-Funktion im MVP |
| `ReleaseEvidence` | Repository-ID/Provider/PR/Basebranch/Head-/Mergecommit/Mergezeit/Verifikationszeit/Protectionfingerprint; ausschließlich trusted Hostingadapter |
| `SourceControlOperation` | Tenant/Actor/Repository/Art/Status/Updatezeit/öffentlicher Fehlercode; kein Raw-stderr, Token, Connectionstring oder Hostpfad |
| Deployment | Commit/Pfad/Contenthash/Ziel/Name + Idempotenz; dauerhafte Provenienz mit Definitions-ID/-Version und Freigabenachweis |

Jede I/O-Methode verlangt einen ausdrücklichen `CancellationToken`, keine Defaultparameter. Provider prüft Context/Binding-Tenantgleichheit zusätzlich zur Application-Autorisierung. Methoden liefern nicht automatisch eine Freigabe, nur weil sie aufrufbar sind.

## 3. Rollen und Repository-ACLs

**Rolle ist eine Obergrenze, nicht die Repositoryberechtigung.** Zulässig ist nur die Schnittmenge aus authentifizierter Rollenobergrenze, explizitem Repositorygrant und Tenant-/Session-/Zielprüfung. Standard ohne Grant: kein Zugriff. Credentials bleiben ausschließlich intern auflösbar.

| Operation | ReadOnly | ProcessManager | Admin | Pflichtgrant / weitere Grenze |
| --- | --- | --- | --- | --- |
| Repository/Datei/History/Diff lesen | möglich | möglich | möglich | `Read`, richtiger Tenant |
| Session öffnen, Modellcommit | nein | möglich | möglich | `Read + Commit`, eigene Session |
| Arbeitsbranch pushen | nein | möglich | möglich | `Read + Push`, servergebundener Sessionbranch/Commit |
| PR erstellen/Status lesen | nur lesender Status | möglich | möglich | Status: Read; Erstellen: `Read + PullRequest`, eigener verifizierter Head |
| Freigegebenen Commit deployen | nein | möglich | möglich | `Read + Deploy`, bestehende ProcessManager-Policy, Zielrecht und Releaseevidence |
| Bindung/Credentialreferenz/ACL ändern | nein | nein | möglich | `Read + Manage`, AdminOnly, tenantgebundene Administration |

- `ExternalTaskWorker` und unbekannte Rollen erhalten keine Source-Control-Rechte.
- `Manage` beinhaltet nicht automatisch `Commit`, `Push` oder `Deploy`.
- Auch Admin erhält keinen impliziten `$global`-/Cross-Tenant-Bypass. Tenantlose Identität ist zunächst abzuweisen; spätere explizite administrative Tenantdelegation braucht einen separaten geprüften Vertrag.
- Erstellen einer Bindung ist ein tenantgebundener Admin-Bootstrap ohne schon bestehende Repo-ACL. Der Ersteller erhält zunächst Read+Manage; weitere Rechte explizit vergeben und auditieren.
- ACL-/Credentialreferenzänderungen erhöhen eine Bindungsrevision. Bereits gequeue-te Writes prüfen vor Ausführung erneut die aktuellen Rechte; fehlende Rechte beenden den Write, nicht nur seine UI-Anzeige.
- Jobs liest ausschließlich der Ersteller oder ein berechtigter Administrator derselben Bindung. Listen filtern unsichtbare Ressourcen; Fremdtenant-ID liefert 404, bekannte sichtbare Ressource mit fehlender Aktionsberechtigung 403.
- Wirksame Rechte vor dem externen Write nachprüfen; technisch nicht rückgängig machbare, bereits begonnene externe Writes bei gleichzeitigem Widerruf als solche auditieren. Keine atomare Widerrufsgarantie über Git/Hosting versprechen.

Diese Rechteprüfung ist **noch nicht implementiert**. Negative echte HTTP-/Job-/Credentialfälle sind G02/G05/G09; der Flagtest belegt ausschließlich die unabhängige Vertragsrepräsentation.

## 4. Branch-, Konkurrenz- und Freigaberegeln

### Arbeitsbranch und Compare-and-Swap

- Defaultbranch beim Verbinden ermitteln, nicht `main` oder `master` hartcodieren. Releasebranch wird ausdrücklich adminseitig konfiguriert.
- Neue Sessionbranches serverseitig unter `vertex/` erzeugen, beispielsweise `vertex/{sessionId:N}`. Keine frei übernommenen Benutzerrefspecs. A bietet nur BPMN-Erstellen/Ändern; Dateilöschen/-rename bleiben capabilitybedingt deaktiviert.
- Keine direkten Default-/Releasebranch-/Tagwrites, keine automatische Merge-/Rebase-/Reset-Funktion. Read darf andere erlaubte Branches öffnen; Write verwendet den Sessionbranch.
- Ein Commit basiert auf dem beim Öffnen festgehaltenen Basiscommit und ist dessen Nachfolger. Ein Push muss relativ zum erwarteten Remotecommit nachweislich fast-forward sein; neuer Branch erwartet ausdrückliche Abwesenheit.
- Ein vorheriger Fetch/Headvergleich genügt nicht. G04 muss einen exact-expected Compare-and-Swap am Remote nachweisen, einschließlich Remote-Reset und Änderung zwischen Fetch/Push.
- Falls der native Adapter eine exakte `--force-with-lease=<ref>:<expected>`-Bedingung verwendet, ist dies ausschließlich CAS nach zusätzlich geprüfter Abstammung. Niemals `--force`, allgemeine Lease ohne erwartete ID, History-Rewrite oder Freigabe einer non-fast-forward-Änderung. Für neuen Branch muss die Ref fehlen. Provider darf hierzu keinen frei setzbaren Forceparameter exponieren.
- Konnte das Pushresultat nicht gelesen werden, Zustand `ResultUnknown`; erwarteten Commit/Ref über autorisierten Read abgleichen, bevor erneut geschrieben wird. Remoteabweichung bleibt Konflikt, eigener Snapshot erhalten.

Die Abstammungs-/CAS-Kombination ist ein projektspezifischer Implementierungsvertrag, noch kein bestandenes Git-Transportverhalten. [Git Push](https://git-scm.com/docs/git-push), [Git Ref-Format](https://git-scm.com/docs/git-check-ref-format).

### Freigegebener Produktionsstand

Für A müssen alle Bedingungen erfüllt sein:

1. Hostingbindung und Repository-/Releasebranch-Zuordnung stimmen zum Tenant/Ziel.
2. PR ist tatsächlich gemergt, Base ist der konfigurierte Releasebranch; Auswahl entspricht dem verifizierten Merge-/Squashcommit und ist im aktuellen Releasebranch erreichbar. A qualifiziert Merge/Squash; Rebase-Merges zunächst nicht anbieten, bis die Commitzuordnung separat bewiesen ist.
3. Releasebranch ist geschützt: mindestens eine zustimmende Review, stale Reviews werden verworfen, der letzte reviewbare Push braucht Zustimmung, Force-Push/Delete sind gesperrt und Admin-Enforcement ist aktiv; keine unkontrollierten Bypass-Ausnahmen. In G07 konkrete Classic-Protection-/Ruleset-Auflösung testen, unbekannte Konfiguration fail-closed.
4. Server holt den Nachweis frisch und bindet Repo/PR/Commit/Protectionfingerprint an den Auftrag. Nachweisalter maximal 60 Sekunden beim Start der Deploymentwirkung; späterer Retry verifiziert erneut. Deploy des ausgewählten früheren freigegebenen Commits darf bewusst eine neue Definition erzeugen.
5. Inhalts-Hash stimmt exakt, aktuelle Rechte/Tenant/Ziel sind gültig und vorhandene Validatoren erfolgreich. Hostingausfall oder unvollständiger Nachweis: kein Productiondeploy.

Ein Merge allein behauptet keine vollständige Produktionskonformität. Review-/Protection-Vertrag ist die zusätzliche Source-Control-Freigabe, nicht Ersatz für bestehende fachliche/technische Releasegates. Anbietergrenzen am konkreten Testrepo nachweisen; die Integration konfiguriert fremden Branchschutz nicht automatisch. [GitHub Branch Protection](https://docs.github.com/en/rest/branches/branch-protection), [GitHub Pull Requests](https://docs.github.com/en/rest/pulls/pulls).

Generisches/offline Git ohne Hostingnachweis bleibt in A für Lesen/Commit nutzbar. Kein manueller UI-Freigabeknopf als Umgehung. GitHub-App-Testrepository und echte PR-Writes sind bislang nicht freigegeben/eingerichtet.

## 5. Idempotenz, Snapshot und persistente Zustände

- Eindeutiger Schlüssel: `(Tenant, RepositoryId, OperationKind, IdempotencyKey)`. API bindet ihn zusätzlich an den Ersteller und einen kanonischen Requestdigest. Anderer Actor darf vorhandene Operation nicht auslesen/wiederverwenden.
- Digest umfasst Session/Generation/Revision, Basis-/Zielcommit/-branch, sortierte explizite Datei-/Bytehashes, Nachricht, Ziel und relevante Bindungsrevision. Gleicher Schlüssel/gleicher Request liefert dieselbe Operation; anderes Payload/Ziel: 409.
- Identische Inhalte mit **neuem** Schlüssel sind eine neue ausdrückliche Operation. Kein globales Contenthash-Deduplizieren, das gewünschte spätere Deployments verhindert.
- Jobclaims: DB-Lease, Fencingtoken und bedingte Statusupdates. Vor endgültigem Commit/Push/PR/Deploy Requestsnapshot und Rechte erneut verifizieren; keine mutable DTO-Liste aus fremder Sitzung übernehmen.
- `Queued → Running → CommittedLocal/Pushed/Succeeded` sind operationstypische Erfolge. Konflikt/Fehler/Cancel bleiben sichtbar. `ResultUnknown → Reconciling` ist kein Erfolg und darf keine blinde externe Wiederholung auslösen.
- Commit bindet genau N; Receipt markiert nur N committed. N+1 bleibt dirty. Leseresponses werden nach Dokument-/Tenant-/Sessionwechsel verworfen.
- Eigene Snapshotdaten bleiben bei Writefehler erhalten. Keine garantierte Recovery unbestätigter Browseränderungen; erst dauerhaft angenommener Snapshot ist rekonstruierbar.
- Deployprovenienz und Definitionsanlage müssen crashsicher gekoppelt werden; Triggerreconciliation ist G08. Ein gemeinsamer Transaktionsnachweis darf nicht aus mehreren getrennten Saves abgeleitet werden.

## 6. Konfiguration, Quoten und Performanceziele

Ursprünglicher G01-Stand: `SourceControlOptions` war ausschließlich ein C#-Vertrag. Seit dem [G02-Teilstand](2026-10-05_Git-G02_Sicherheitsbasis.md) sind Hostbinding und Konfigurationsvalidation implementiert. G03/G04 müssen Limits weiterhin an tatsächlichen Speicher-/Transportgrenzen erzwingen; daraus folgt noch keine benutzbare Git-Integration.

Standard: `Enabled=false`, kein automatisch gewählter Git-Pfad/Workspace, leere Hostallowlist. Aktivierung verlangt absoluten validierten Git-Pfad, private dedizierte Workspace-Root und explizite HTTPS-Hostfreigaben. Linuxcontainer installieren Git nur für aktivierte Profile. Qualifizierungsbaseline: Git 2.56.0 oder neuer, gewarteter Sicherheitsstand; hier ausschließlich Windowsversion 2.56.0 geprüft, kein Linuxnachweis.

| Option in `Limits` | Initialwert |
| --- | --- |
| MaxRepositoryBytes | 256 MiB einschließlich Gitdaten/Checkout pro Arbeitsbereich; keine Vorausallokation |
| MaxModelBytes / MaxModelFiles | 2 MiB / 1.000 |
| MaxDiffBytes / MaxPageSize | 1 MiB / 100 |
| MaxCommitFiles / MaxCommitMessageCharacters | 20 / 2.000 |
| MaxConcurrentJobsPerTenant / MaxConcurrentJobsTotal | 2 / 8 |
| MaxWorkspaceBytesTotal | 5 GiB Obergrenze, nicht reservierter Speicher |
| ReadTimeout / WriteTimeout | 15 Sekunden / 5 Minuten, jeweils total inklusive Retry |
| SessionIdleRetention | 24 Stunden; aktive geleaste Jobs nicht durch Idle-Cleanup löschen |
| CompletedJobRetention | 30 Tage für abgeschlossene Jobdetails |

Weitere verbindliche Regeln:

- HTTP-Modellupload über explizites Byte-/Base64-Transportmapping: maximal 3 MiB Requestbody, dekodierter Inhalt weiterhin 2 MiB. Keine stille Normalisierung, JSON/body/prozessstdout nicht loggen.
- Kein neuer Clone ohne ausreichende Workspacequote. Repositorygrößen auch während Transfer begrenzen; nicht erst nach komplettem Download abbrechen. Überlimit: kein stiller Partialcommit.
- Modelroots adminseitig gesetzt; HTTPS Port 443, private/Loopback/Metadata-Ziele standardmäßig verboten. Lokaler Testadapter hat getrennte Vertrauensgrenze, nicht einen Production-AllowPrivateNetworks-Schalter.
- Pfadprüfung zusätzlich gegen reales FS/Reparsepoints/Case-Kollisionen; Refprüfung gegen [Gitregeln](https://git-scm.com/docs/git-check-ref-format) und engere Vertex-Arbeitsbranchpolicy. Vertragsstrings allein sind keine geprüften Pfade/Refs.
- Clone/Sessiondaten nur unter privater Serviceaccount-Root, nicht WWW-/Shared-Root. Persistente Snapshotbytes in G03 über vorhandene Data Protection schützen; Keyring entsprechend Bestandsprofil. Host-Festplattenverschlüsselung/ACLs für Cloneinhalte als Betriebsanforderung dokumentieren.
- Erfolgreich gepushte, nicht mehr benötigte Clones früh freigeben; ungesicherte angenommene Snapshots erst nach festgelegter Retention löschen. Nach Ablauf ehrlich „Session abgelaufen“, nicht unsichtbar wiederhergestellt anzeigen.
- Provenienz und notwendige Audit-/Idempotenzbelege leben unabhängig vom 30-Tage-Jobdetail-Cleanup. Nicht löschen, solange zugehörige Definitionen/Operationen noch wiederholbar sind; Löschkonzept in G03 konkretisieren.

Vor Messung festgelegte G09-Ziele, keine jetzigen Ergebnisse: isolierter lokaler Testhost (mindestens 4 CPU-Kerne/16 GiB), 100 BPMN-Dateien mit höchstens 500 Elementen und 2 parallele Sessions. P95 warmed API-Dateiliste/Read ≤ 2 s; sichtbares Öffnen ≤ 5 s; lokaler Commit ≤ 5 s; lokaler Transport-Push ≤ 10 s. HTTPS/GitHub-Latenz separat messen, kein Internet-SLO daraus ableiten. Mindestens 20 Wiederholungen, Hardware/Repozustand dokumentieren. Zielverfehlung melden, nicht nachträglich Grenzwerte passend ändern.

## 7. HTTP-Fehler und Beispiele für G05

Noch keine Routen implementiert. Geplant: `/api/source-control`, Bindungs-/Session-ID statt frei übergebener Remote-URL bei Modelloperationen.

| Vertrag | HTTP / Verhalten |
| --- | --- |
| Unauthenticated | 401, vorhandener Authhandler |
| Forbidden / NotFound | 403 für bekannte sichtbare Ressourcen mit fehlendem Aktionsrecht; 404 für fremde/unsichtbare IDs |
| RevisionConflict / IdempotencyConflict | 409; nur autorisiert sichtbarer erwarteter/aktueller Commit, kein Rohgitstderr |
| InvalidInput / ContentUnsafe / ReleaseNotApproved | 400 / 422 / 422 mit stabilen Codes; keine Secretwerte/Providerfehlermeldungen |
| PayloadTooLarge / QuotaExceeded | 413 / 429; Retry-After nur bei belegtem retryfähigem Zustand |
| Disabled / GitUnavailable / ProviderUnavailable / CredentialUnavailable | 503; internes Detail redigiert. Availability-Read kann 200 + available=false liefern |
| TimedOut | synchroner Read 504; Writejobstatus Timeout/ResultUnknown nach Nebenwirkungsprüfung |
| Cancelled / ResultUnknown | sichtbarer Jobzustand; nach Clientdisconnect keine falsche Garantie „Write nicht passiert“ |

Beispiele:

- Erfolg: Commitrequest mit Scope aus Auth, Session S, Basiscommit A, Snapshot N und neuem Schlüssel → `202` + autorisierte Operation-ID → `CommittedLocal` + Commit B/Revision N. Keine neue Runtime-Definition.
- Konflikt: erwarteter Remote-Head A, tatsächlicher Head C → `409 RevisionConflict`, Snapshot N bleibt. Kein automatischer Retry auf C.
- Zugriff: ReadOnly mit Readgrant sendet Commit → `403 Forbidden`, kein Job/Clone/Secretresolve mit Schreibwirkung.
- Ungültig: `HEAD~1` als feste Commit-ID → `400 InvalidInput`; Kontext/Tenant aus Request wird nicht als Hostkontext akzeptiert.
- Ausfall: Git fehlt oder Token widerrufen → redigiertes `503`; regulärer Editor/Enginepfad bleibt verfügbar.
- Deployment: Mergecommit B + Pfad/Bytehash/Ziel/Schlüssel → frische Hosting-/ACL-Prüfung → dauerhafter Job; bei gleicher Wiederholung dieselbe Definitions-ID/Provenienz. Ein UI-Deployrequest kann keinen `ReleaseEvidence`-Nachweis mitliefern.

## 8. Tests und Abschlussgrenze

Tatsächlich ausgeführt:

```powershell
dotnet restore tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-restore
dotnet build src/VertexBPMN.Application/VertexBPMN.Application.csproj --configuration Release -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

Der erste Contractbuild fand eine mehrdeutige Record-Konstruktorwahl bei `ExpectedRemoteRef.Absent`; im Quellcode mit explizitem nullable GitCommitId behoben. Finaler Lauf nach allen C#-Änderungen: **44 erfolgreich, 0 fehlgeschlagen, 0 übersprungen**, Exit 0, Dauer 2,735 Sekunden. Application-Teilbuild anschließend erneut mit `--no-restore`: Exit 0, keine Warnungen/Fehler, 4,18 Sekunden.

Abgedeckt: Commit-ID-Invarianten, Tenant-/Actor-Invarianten, Idempotenzschlüssel, zwingende Abwesenheits-/Head-Erwartung, bytegetreuer unveränderlicher Snapshot, no-payload Diagnose, I/O-Cancellation, Trennung Git/Hosting, unabhängige Rechteflags, packagefreie Architektur und disabled-default Limits.

Nicht abgedeckt: echte ACL-/JWT-Handler, DB-Leases, Git-CAS/HTTPS, Hook-/SSRF-/Dateisystemschutz, Providercredentials, GitHub-Review, Definition-/Trigger-Recovery und Studio. Diese Nachweise bleiben G02–G09; keine Produktabnahme durch Vertragstests ersetzen.

Testkategorien: reine Verträge CI-safe ohne externen Trait; zukünftige native Git-/DB-Tests `GitLocalAcceptance`, Remote/GitHub `GitExternalAcceptance`, reale GUI `LocalStudioE2E`. G05 ergänzt gezielte Ausschlüsse erst beim Hinzufügen echter lokaler Fälle; kein Workflow geändert.

**Nächster Schritt:** mit entsprechendem Auftrag Phase 2 mit G02/G03. Phase 1 ist mit der dokumentierten lokalen Baseline abgeschlossen, nicht als Produktabnahme. Keine sicherheitsrelevante Entscheidung dieses Vertrags bleibt als stiller „TODO“-Default offen. GitHub-Installation/Testrepo, echte externe Berechtigungen und zusätzliche manuelle Freigabe bleiben ausdrücklich eigene Nutzer-/Betriebsentscheidungen, bevor deren Aktionen erfolgen.
