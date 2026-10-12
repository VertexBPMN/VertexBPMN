# GS06: Explizite Entra-Tokenbeschaffung für Azure DevOps Services

Stand: 12. Oktober 2026, lokaler Branch `codex/git-generic-servers`. Implementierte Authentifizierungsergänzung, keine Gesamtfreigabe von GS06 oder generischer Anbieterunterstützung.

## Produktpfad

Der profilgebundene HTTPS-Broker akzeptiert bei `HttpBearer` zusätzlich ein geschütztes Credential vom Typ `GitAzureDevOpsEntra`. `tenantId`, `clientId` und `clientSecret` bestimmen die Anwendung ausdrücklich. Tenant und Client müssen nichtleere GUIDs sein. Die vorhandene Abhängigkeit Azure.Identity stellt mit `ClientSecretCredential` am öffentlichen Entra-Endpunkt Tokens für die feste Azure-DevOps-Audience `499b84ac-1321-427f-aa17-267ca6975798/.default` aus. Keine eigene OAuth-Implementierung, neue Abhängigkeit, implizite Identitätskette oder automatische Ressourcenanlage.

Die Auswahl erfolgt erst nach aktueller Vertex-Autorisierung und bestätigtem Git-Ziel. Ausgabe ist auf HTTPS mit Standardport und exakt `dev.azure.com` begrenzt. Die bestehende unveränderliche Profil-/Netzwerkbindung bestimmt das konkrete Repository. Azure DevOps Server, fremde Domains und abweichende Ports erhalten weder Client-Secret-Auflösung noch einen Token. Andere Cloud-Authorities, alte `visualstudio.com`-URLs, interaktive Identitäten, Managed Identity, Zertifikats- und Federationsverfahren sind in dieser Ergänzung nicht implementiert.

Vor der Tokenanforderung und nach ihrer Rückkehr werden Credentialtyp, Referenz, Vertex-Tenant, Metadatenrevision, Pflichtfelder sowie aktuelle Rollen/Grants und Profil erneut geprüft. Änderungen verhindern die Ausgabe. Der vorhandene repositorygebundene, capabilityverhandelte Bearer-IPC-Kanal bleibt unverändert; die Lease endet spätestens nach zwei Minuten beziehungsweise mit dem tatsächlichen Token. Secrets und Tokens werden weder in Auftragsbelegen noch in Argumenten, Gitkonfiguration oder Prozessumgebung gespeichert. Backendfehler werden bereinigt; Caller-Cancellation bleibt erhalten. Kein PAT-/Basic-Fallback.

Microsoft-Grundlagen: [Azure-Repos-Authentifizierung](https://learn.microsoft.com/en-us/azure/devops/repos/git/auth-overview?view=azure-devops), [Service-Principals und Managed Identities](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/service-principal-managed-identity?view=azure-devops), [Auswahl der Azure-DevOps-Authentifizierung](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/authentication-guidance?view=azure-devops).

## Geänderte Dateien

- `AzureDevOpsTokenIssuer.cs`, `IAzureDevOpsTokenIssuer.cs`: feste Audience und explizite SDK-Identität; interne Grenze für deterministische Tests ohne Cloudzugriff.
- `ProfiledGitCredentialBroker.Entra.cs`, `ProfiledGitCredentialBroker.Bearer.cs`, `ProfiledGitCredentialBroker.cs`: aktuelle Rechte, Metadaten- und Zielkontrollen, sichere Lease-Ausgabe.
- `InfrastructureModule.cs`: operationenbezogene Registrierung des Issuers.
- `SourceControlConnectionBindingTests.Entra.cs`: 14 neue Fälle für Erfolg, fremde Domains/Ports, ungültige Identitäten, Rotation vor und während Tokenanforderung, Rollen-/Profilwechsel, abgelaufene Tokens, bereinigte Fehler und Cancellation.
- `SourceControlConnectionBindingTests.cs`: gemeinsame SQLitefixture ermöglicht direkte Profilbindungen an zusätzliche Testendpunkte; bestehender Legacyaufbau bleibt Standard.
- Betriebsrunbook, Implementierungsplan sowie lokale Wiki-/Website-Dokumentation aktualisiert. Keine Veröffentlichung.

## Prüfung

- Release-Gesamtbuild mit `--no-restore`, `SkipBpmnIoAssetBuild=true`, `-m:1`, `--disable-build-servers`: bestanden, keine Warnungen/Fehler, 36,86 Sekunden. Vorherige Versuche deckten drei beziehungsweise einen Fehler im neuen Testcode auf; diese wurden korrigiert.
- Zentraler Testprojektbuild nach den letzten vier Regressionen: bestanden, keine Warnungen/Fehler, 34,05 Sekunden.
- Gezielte Profil-/Security-/Hosting-/Persistenzsuite: **406 bestanden**, keine Fehler/Skips, 38,077 Sekunden. Alle 14 Entra-Fälle in der aktuellen Assembly enthalten.
- Check-only-Formatprüfung des Broker-/Issuerumfangs und der neuen Testdatei: bestanden. Die bestehende gemeinsame Fixture wurde nur in den betroffenen Mitgliedern geprüft; keine Formatbereinigung anderer Tests.
- Identische CI-safe-Auswahl aus `.github/workflows/ci.yml`: **1.777 bestanden, keine Fehler, acht bestehende externe Skips**, 1.785 insgesamt, 1:39,759. Die nicht ausgeführten externen Prüfungen sind kein Erfolgsnachweis. Dieser grüne Lauf belegt keine Behebung des vorherigen sporadischen FEEL-Timeouts.
- `git diff --check` in Produkt, Wiki und Website: bestanden.
- Clean-Code-Skill: D1–D7 auf sieben Broker-/Issuer-/Testdateien ausgeführt, keine Suchfehler; alle acht Regeln und geänderte DI-Registrierung manuell geprüft. Die Issuer-Schnittstelle ist eine tatsächliche externe SDK-Grenze mit einem Produktionsadapter und Testsubstitution. Keine bestätigten offenen Wartbarkeitsbefunde im geprüften Umfang. Keine Aussage über den gesamten angesammelten Featurebranch.

Logs: ignorierter Ordner `artifacts/git-generic-servers/gs06-entra-*.log`.

## Noch erforderlicher Abschluss

Die Anwendung muss außerhalb dieser lokalen Tests in der vorgesehenen Azure-DevOps-Organisation mit minimalen Rechten eingerichtet und separat mit PAT und Entra geprüft werden. Es wurden keine Cloudidentität, Organisation oder Repository angelegt und keine externen Gitwrites ausgeführt. Der Issuer wurde in den Regressionen kontrolliert ersetzt; ein echter Entra-/Azure-Repos-End-to-End-Nachweis ist damit ausdrücklich nicht erbracht.

Profilgebundene Hostingannahme einschließlich unveränderlicher Provider-/API-/Repositorybelege und gemeinsame Antwortvalidierung, Azure-PR-Adapter, weitere Hostinganbieter, SSH, Linux sowie echte gemeinsame API-/Studio-/Recovery-Abnahme bleiben offen. Der zuvor diagnostizierte sporadische C.8.0-FEEL-Timeout wurde durch diese Authentifizierungsergänzung nicht behoben. Pflichtgates und Paketcheckboxen des Implementierungsplans bleiben unverändert offen.
