# Git-Modellversionierung – G02: Sicherheitsbasis (Teilstand)

Stand: 2026-10-05. Basiscommit: `4c30df17d7d819ab50c776cf326f12556d54e77d`.
Branch: `codex/git-source-control-phase-1`. Neue Änderungen uncommittet.

## Status und Umfang

**G02 in Arbeit, nicht abgenommen. G03 nicht begonnen.** Dieser Teilstand ist keine Zusage funktionierender Git-/Credential-/Hostingoperationen oder bereits geschützter neuer HTTP-Endpunkte.

## Implementiert

- `src/VertexBPMN.Application/SourceControl/RepositoryAccessPolicy.cs`: Schnittmenge aus vertrauenswürdigen Rollen, expliziten Repositorygrants und Tenant. Admin ohne Grant erhält keinen Bypass; Manage beinhaltet kein Commit/Deploy. Unsichtbare Ressourcen liefern NotFound, sichtbare ohne Aktionsrecht Forbidden. Sessionbesitzer, Repository, Tenant und Ablauf werden geprüft. Policies vertrauen nur serverseitigem Kontext und frisch geladenen Grants; Auth-/Storeadapter müssen diese in G03/G05 liefern.
- `SourceControlInputPolicy.cs`: portable relative Modellpfade mit Rootgrenze, Verbot von Traversal, UNC/absoluten Pfaden, Alternate Data Streams, `.git`, Gitmodules und Windowsgerätenamen. Reine Stringprüfung, keine Aussage über reale Symlinks/Reparsepoints oder Case-Kollisionen. G03/G04 müssen Dateisystem/Objektmodus prüfen.
- Branch-/Refsyntax nach Gitregeln; Arbeitsbranch ist ausschließlich `vertex/{sessionId:N}`, passend zur servergebundenen Session und nicht Default-/Releasebranch. Kein Benutzerrefspec, kein History-/CAS-Nachweis durch diese Prüfung.
- BPMN-Bytegrenze vor Kopie; XML mit DTD-Verbot, deaktivierter externer Auflösung und Zeichenlimit. Unsichere strukturierte Inline-Secrets sowie redigierte Exportartefakte werden blockiert, nicht geändert. Bestehendes `ModelExportRedaction` wird nur als Detektor benutzt. Originalbytes bleiben exakt erhalten; fachlich noch nicht ausführbare Entwürfe bleiben zulässig. Opaque Skriptsecrets sind nicht vollständig erkennbar.
- `SourceControlSecurityException.cs`: stabiler Code ohne Eingabewert, XML, Secret oder Rohproviderfehler.
- `SourceControlOptionsValidator.cs` und `ApplicationModule.cs`: Binding der Section `SourceControl` und Startupvalidation. Default deaktiviert: keine Git-/Workspace-/Hostanforderung. Aktiviert: absolute lokale Pfade, dedizierter Root (kein Dateisystemroot), feste Branchpolicy, explizite DNS-Hostnamen und positive/konsistente Limits erforderlich. Validierung erstellt keine Verzeichnisse und startet keinen Prozess.
- `SourceControlOptions.cs`: Dokumentation an tatsächlich vorhandenes Binding angepasst; Quoten sind noch nicht an echten Speicher-/Transportgrenzen durchgesetzt.

## Gezielt geprüfte Fälle

`tests/VertexBPMN.Tests/Unit/Application/SourceControlSecurityTests.cs` und `SourceControlOptionsTests.cs`: **55 bestanden, 0 fehlgeschlagen, 0 übersprungen**, Exit 0; 10,938 s.

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --no-build --no-restore --filter-class 'VertexBPMN.Tests.Unit.Application.SourceControl*' --max-parallel-test-modules 1
```

Finaler Build nach allen C#-Änderungen: Exit 0, 0 Fehler, 67 Warnungen, 40,26 s. Keine Warnung in den neuen Source-Control-Dateien gefunden. Kein Editorasset geändert. Tests brauchen keine native Gitinstallation, keinen Browser und keinen externen Dienst. AuthenticatedRoles und Grants sind Testeingaben der Policy, keine echte JWT-/DB-/HTTP-Abnahme.

Danach die vollständige CI-safe Suite mit **unveränderten** Filtern aus `.github/workflows/ci.yml`, Schritt `Test (CI-safe suite)`, ausgeführt: **1.342 gesamt, 1.334 bestanden, 0 fehlgeschlagen, 8 übersprungen**, Exit 0, 80,442 s. Die acht bestehenden Opt-in-Fälle brauchen Azure-Data-Protection-/PostgreSQL-/Live-TypeSafe-Voraussetzungen; daraus keine externe Abnahme ableiten. Kein neuer Workflow-Ausschluss.

## Offene Pflichtgrenzen / nächste Reihenfolge

Aktualisierung nach Fortsetzung: Die unten beschriebenen Ausgangslücken 1/2/4 sind mittlerweile teilweise implementiert: `SourceControlCredentialResolver`, `GitHubAppTokenBroker`, `SourceControlHttps`, `PersistentSourceControlStore` und die neue Migration existieren. Token-Contracttests: 4 bestanden; SQLite-Persistenz: 5 bestanden; echte PostgreSQL-Abnahme unter WSLC: 1 bestanden. Der neue AuthHelper und Workspace-Schutz wurden gebaut, aber noch nicht end-to-end abgenommen. Die ursprüngliche Liste bleibt als Ausgangsbefund lesbar und ist kein aktueller Vollständigkeitsstatus. Aktueller Infrastruktur-Nachweis und verbleibende Grenzen: [G03-WSLC-Abnahme](2026-10-05_Git-G03_WSLC-Abnahme.md). Keine produktive Git-End-to-End-Abnahme behauptet.

1. Vorhandenen persistenten Credentialdienst tenantgeprüft anschließen; an die aktuell autorisierte Bindung/Revision binden. GitHub-App-Installationstoken bevorzugen; kein verpflichtender globaler PAT. Rotation/Widerruf, Tokenablauf und redigierte Fehler prüfen.
2. HTTPS-URL und **tatsächlich verbundenes Ziel** prüfen: DNS/private/link-local/Metadata/Rebinding und Redirects. Die Hostnamen-Optionsprüfung allein verhindert kein SSRF. Kein Netzwerktransport in diesem Teilstand.
3. Prozessargumente/Umgebung, Hooks/Filter/Submodule/LFS, kurzlebiger interner Tokenkanal, Cancellation/Timeout und Prozessbaumbeendigung implementieren; echte Adaptertests ohne Secret in URL/Args/Config/Logs.
4. G03-Bindungen/ACLs/Sessions/Operations, Data-Protection-Snapshots, Unique-Idempotenz, Claims/Leases/Fencing und Workspacequoten/Cleanup persistent anschließen; keine neue InMemory-Produktimplementierung.
5. HTTP-Identität/ACL-/Job-/Credential-Negativtests in G05 bzw. den zugehörigen Paketen nachweisen. Bei jedem Write frische Berechtigungen prüfen.

Keine externen Ressourcen erzeugt, kein Repositorytoken aufgelöst, kein Remote-Write, kein Commit/Push/PR ausgeführt. Die Integration ist weiterhin unbenutzbar als Gitprodukt, bis die offenen Adapter-/API-/UI-Pakete umgesetzt sind.

Primärquelle für Refsyntax: [Git check-ref-format](https://git-scm.com/docs/git-check-ref-format).
