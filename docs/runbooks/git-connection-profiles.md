# Profilgebundene Git-Verbindungen

Stand: 13. Oktober 2026, Entwicklungsbranch `codex/git-generic-servers`. Generisches HTTPS, Azure-Services-PR-Adapter und Export bestätigter Schreibbelege ins Audit sind implementiert. SSH hat einen vorbereiteten internen Read-Transport; öffentlicher Jobpfad, Passphrase-/Crash-Recovery und reale Serverabnahmen bleiben offen. Lieferstufen und Anbieter sind noch nicht vollständig abgenommen. Fortschritt und Grenzen: [Azure, SSH und Audit](../reviews/2026-10-13_Git-GS06-GS07_Azure-SSH-und-Audit.md).

### Azure DevOps Services: Hostingprofil

Provider-ID `azure-devops-services`, feste REST-Version 7.1. Git-URL: `https://dev.azure.com/{organisation}/{projektname}/_git/{repositoryname}`. `Hosting.RepositoryId`: `{organisation}/{projekt-guid}/{repository-guid}` mit kanonischen nichtleeren GUIDs. `Hosting.ApiEndpoint.BaseUri`: `https://dev.azure.com/{organisation}/{projektname}/_apis/git/repositories/{repository-guid}`. Git- und API-Netzwerkpolicy jeweils explizit freigeben; Capability `PullRequest` erst mit passenden Vertex-Grants verwenden. URLs, Namen und GUIDs müssen dieselbe Ressource bezeichnen.

API-Credentials separat referenzieren: `HostingAzureDevOpsPat` oder `HostingHttpBearer` mit `token` und `expiresAt` im invarianten Roundtripformat `O`; alternativ `HostingAzureDevOpsEntra` mit `tenantId`, `clientId`, `clientSecret`. Keine Secretwerte in Profile kopieren. PAT verwendet Standard-Basic mit festem Benutzernamen `VertexBPMN`; Entra verwendet die explizite Anwendung und die vorhandene Azure-DevOps-Audience. Leases sind auf zwei Minuten beziehungsweise tatsächlichen Ablauf begrenzt; mindestens 30 Sekunden Restgültigkeit sind erforderlich.

Der Adapter schreibt keine Policies und leitet aus Votes keine Releasefreigabe ab. Azure DevOps Server, alte Visual-Studio-Hostnamen und andere Entra-Clouds sind damit nicht qualifiziert.

### Auditrollout und SSH-Vorbereitung

Vor Aktivierung des Audit-Workers müssen beide neuen Migrationen (`BpmnDbContext`, `ProcessMiningEventDbContext`) im autorisierten vorhandenen Migrationsverfahren angewendet sein. Der zentrale Eventindex schützt Wiederanlauf und Mehrfachzustellung. Bei Exportausfall bleibt `AuditPending` gesetzt; Quelldetails werden noch nicht gelöscht. Keine nachträgliche Auditmarkierung alter Belege und kein automatischer Down-Rollback.

SSH-Konfiguration bleibt optional. Ein vorbereitetes SSH-Profil braucht `SshKey`, einen expliziten Port, `GitSshKey` mit `username`/`privateKey`, exakte Netzwerk-/Hostkeyreferenzen und einen administrativ freigegebenen absoluten `SshExecutablePath`. Hostkeys enthalten ausschließlich Algorithmus und Base64, keine freien known_hosts-Zeilen. Agent und interaktive Passwort-/Passphraseeingaben werden nicht übernommen. Der interne Transport enthält festen Commit-Fetch, Branch-Liste, Branch-Auflösung und begrenzten History-Fetch. Diese Konfiguration macht SSH noch nicht über API oder Studio nutzbar; Version 4 ist intern dauerhaft angenommen und claimbar; der produktive Worker, Ergebnisprojektion und Lebenszyklus sind zuerst vollständig anzuschließen. Siehe [SSH-Auftragsannahme und Claiming](../reviews/2026-10-13_Git-GS07_SSH-Auftragsannahme-und-Claiming.md).

## Voraussetzungen

- `SourceControl:Enabled`, absoluter `GitExecutablePath` und geschützter `WorkspaceRoot`; nativer Pfad benötigt Git mindestens 2.56. Windows lokal geprüft, Linux-Abnahme offen.
- Aktuelle Vertex-Identität und tenantgebundene Rollen-/Repository-Grants. Anonymes Remote-Lesen umgeht keine Vertex-Autorisierung.
- Relationale Persistenz mit den additiven Migrationen `20261011134324_SourceControlConnectionProfiles` und `20261011140107_SourceControlExecutionContracts`. Migrationen wurden im Produkt vorbereitet; Betriebsdatenbanken ausschließlich im freigegebenen Migrationsablauf aktualisieren.
- Dauerhafte, gesicherte Data-Protection-Schlüssel für Profil-/Auftragsbelege. Datenbank, Schlüssel und Credentials zusammen sichern; ohne passende Schlüssel sind geschützte Belege nicht wiederherstellbar.

## Einrichtung

1. In der Hostkonfiguration `SourceControl:HttpsNetworkPolicies` registrieren: `Id`, `TenantId`, `RepositoryScope`, `AllowedAddressRanges`, `DeniedAddressRanges`, `Revision`, `IsRevoked`. Umfang auf den konkreten HTTPS-Repositorypfad begrenzen. Leere Allowlist erlaubt nur öffentliche Adressen; private RFC1918-/ULA-Ziele benötigen explizite CIDRs. Loopback, Link-local und Metadatenziele bleiben gesperrt. Alle DNS-Antworten müssen zulässig sein; die native Verbindung wird auf genau diese Antworten begrenzt.
2. Bei privater CA unter `SourceControl:HttpsTrustProfiles` ein tenant-/scopegebundenes Profil mit `Id`, `TenantId`, `RepositoryScope`, `CertificateAuthorityPem`, `Revision`, `IsRevoked` registrieren. Nur öffentliche CA-Zertifikate, niemals private Schlüssel. Ohne private CA bleibt die systemseitige TLS-Prüfung aktiv; keine Abschaltung der Zertifikatsprüfung.
3. Für Basic/PAT ein geschütztes Credential vom Typ `GitHttpBasic` mit `username` und `password` erstellen. Ein PAT wird als Passwort verwendet; sein konkreter Anbieter muss dieses Verfahren unterstützen. Nur die Credential-Referenz ins Verbindungsprofil übernehmen. Keine Secrets in URL, Git-Konfiguration oder CLI-Argumenten.
4. Als Vertex-Admin über `POST /api/source-control/connection-profiles` ein `RepositoryConnectionProfile` mit `schemaVersion=1` erstellen. `GitEndpoint` bindet `BaseUri`, `NetworkPolicyReference` und optional `TrustProfileReference`. Aktiver generischer Transport: `Https`, Authentifizierung `HttpBasic`, `HttpBearer` oder `Anonymous`. Anonymous erlaubt ausschließlich `Read`. Hosting für den generischen Pfad nicht voraussetzen.
5. Repository mit Profilreferenz und Profilrevision erstellen oder über `PUT /api/source-control/repositories/{id}/connection-profile` zuordnen. `BindingRevision`, `ProfileId`, `ProfileRevision` sind Pflichtfelder. Der Server prüft Tenant, Scope, aktuelle Revision und Manage-Grant. Neue Bindungen erhalten Read/Manage-Grants; Commit/Push separat explizit vergeben.
6. `GET /api/source-control/repositories/{id}/availability` und den Studio-Ablauf prüfen. Die Antwort kombiniert implementierte Fähigkeit, Konfiguration und aktuelle Grants; sie ist noch kein erfolgreicher Server-Verbindungstest. Studio zeigt ohne Hostingadapter keine PR-Aktion.

## Rotation und Widerruf

### Profilgebundenes GitHub-Hosting: Übergabestand

Der Entwicklungsbranch ergänzt einen getrennten Hostingauftrag mit Ausführungsvertrag 3. Er ist lokal mit kontrollierten Persistenz-/Adapterfällen geprüft; echte Credential-/HTTP-/Audit-/Studio-Abnahmen bleiben offen. Der [DeepSeek-Übergabebericht](../reviews/2026-10-12_Git-Generische-Server_DeepSeek-Uebergabe.md) beschreibt die Fortsetzung und Prüfergebnisse.

Ein explizites `Hosting` im Verbindungsprofil verwendet derzeit ausschließlich `ProviderId=github`, die exakte `RepositoryId=owner/repository` und `ApiEndpoint.BaseUri=https://api.github.com/repos/owner/repository`. Diese Identität muss zum Git-Remote auf `github.com` passen. Die API besitzt eine separate administrative Netzwerkpolicy; der aktuelle Profiladapter nutzt Systemtrust am öffentlichen Standardendpunkt. Andere Anbieter und private API-CAs werden dadurch nicht aktiviert.

`Hosting.CredentialReference` verweist separat auf ein geschütztes Credential vom Typ `HostingHttpBearer` mit `token` und tatsächlichem `expiresAt` im Roundtripformat. Git-Credentials werden nicht automatisch an den API-Host weitergereicht. Das Profil benötigt die Hostingfähigkeit `PullRequest`; aktuelle Vertex-Rollen und Repository-Grants bleiben erforderlich. Historische Erstellungsbelege bleiben nach Profilwiderruf lesbar, während neue Ausführung und Liveabfrage gesperrt werden. Nach ungewissem Create erfolgt ausschließlich Abgleich, kein blinder zweiter Create. Keine automatische Release-/Deployfreigabe.

### Explizite Entra-Anwendung für Azure DevOps Services

Für `Https`/`HttpBearer` kann `GitCredentialReference` auf ein geschütztes Credential vom Typ `GitAzureDevOpsEntra` zeigen. Pflichtfelder sind `tenantId`, `clientId` (jeweils nichtleere GUID im Format `D`) und `clientSecret`. Die Anwendung muss zuvor in der vorgesehenen Azure-DevOps-Organisation berechtigt werden; Vertex legt keine Identität oder Organisation an. Die Entra-Tenant-ID ist nicht die Vertex-Tenant-ID; Vertex-Rechte und Credentials bleiben unabhängig tenantgebunden.

Dieser Pfad gilt ausschließlich für HTTPS auf `dev.azure.com` am Standardport und damit Azure DevOps Services, nicht Azure DevOps Server oder andere Hosts. Die bestehende Netzwerkfreigabe bindet weiterhin Organisation, Projekt und Repository. `Azure.Identity.ClientSecretCredential` fordert mit expliziter Identität am öffentlichen Entra-Endpunkt nur `499b84ac-1321-427f-aa17-267ca6975798/.default` an. Keine Default-Credential-Kette, Umgebungsidentität, CLI-Anmeldung oder Managed-Identity-Übernahme. Andere Entra-Clouds und Identitätsverfahren sind nicht angeschlossen.

Vor und nach Tokenbeschaffung prüft Vertex Rollen, Profil und Credentialmetadaten. Neue Zugriffe beschaffen ein Token, ohne es dauerhaft zu speichern; ausgegebene Bearer-Leases sind auf zwei Minuten und die tatsächliche Tokenablaufzeit begrenzt. Die Client-Secret-Rotation erfolgt über die geschützte Credentialverwaltung. Aufträge enthalten ausschließlich Referenzen und Zielbelege. Bei Fehlern gibt es keinen PAT-/Basic-Fallback. Lokale Tests ersetzen keine echte Entra-/Azure-Repos-Abnahme.

Microsoft-Grundlagen: [Azure-Repos-Authentifizierung](https://learn.microsoft.com/en-us/azure/devops/repos/git/auth-overview?view=azure-devops), [Service Principals und Managed Identities](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/service-principal-managed-identity?view=azure-devops).

### Extern ausgestellte Bearer-Tokens

Für `HttpBearer` ein geschütztes Credential vom Typ `GitHttpBearer` mit `token` und `expiresAt` verwenden. `expiresAt` ist die tatsächliche Ablaufzeit des dazugehörigen Tokens im Roundtripformat von `DateTimeOffset.ToString("O", CultureInfo.InvariantCulture)` mit UTC-Offset. Vertex beschafft oder erneuert dieses Token nicht selbst. Ablauf wird vor Tokenauflösung geprüft, die ausgegebene Lease auf höchstens zwei Minuten begrenzt; ein in weniger als 30 Sekunden ablaufendes Token wird abgewiesen. Eine frei erfundene spätere Ablaufzeit verlängert keine beim Anbieter abgelaufene Berechtigung.

Token und zugehörige Ablaufzeit gemeinsam aktualisieren, vorzugsweise durch ein neues vollständiges Credential und eine bewusst revisionierte Profiländerung. Profile/Grants/Metadaten werden während der Auflösung erneut geprüft. Der Helper gibt Bearer ausschließlich nach ausgehandelter Git-Capability `authtype` frei; fehlende Aushandlung bricht ohne Basic-Fallback ab. Native Windows-Prüfung liegt vor; Linux und echter Anbieter-/Entra-Lifecycle bleiben offene Abnahmen.

Credential über die geschützte Credentialverwaltung rotieren. Jeder neue native Zugriff prüft Metadaten und aktuelle Rechte erneut; kurzlebige Leases verwenden keine dauerhaft gespeicherten Secrets. Eine während der Auflösung erkannte Rotation gibt kein Credential frei.

Profiländerungen erfolgen über `PUT /api/source-control/connection-profiles/{id}?expectedRevision=...`; Widerruf über `DELETE` mit erwarteter Revision. Repositoryzuordnung anschließend bewusst aktualisieren. Änderungen an Netzwerk-/CA-Konfiguration erfordern eine neue Revision. Angenommene Aufträge behalten ihren ursprünglichen Zielkontext: Profil- oder Policyänderungen führen zum Konflikt, statt die Ausführung still auf ein anderes Ziel umzuleiten.

## Aufträge und Wiederaufnahme

Typisierte Leseaufträge: `POST /api/source-control/repositories/{id}/read-requests` mit `schemaVersion=1`, `kind`, Idempotenz-`key` und den zum jeweiligen Typ erforderlichen Feldern. Antwort ist `202` mit Operation-ID. Dauerhaftes Ergebnis: `GET /api/source-control/operations/{id}/read-result`; aktueller Actor, Tenant und Rechte werden bei Abruf erneut geprüft. Commit-ID, Pfad, Paging, Generation und Diff-Snapshot werden serverseitig validiert.

Read-Worker können abgelaufene Claims mit erhöhtem Fence wieder aufnehmen. Host-Shutdown beendet Lesen als wiederaufnehmbares unbekanntes Ergebnis; Benutzerabbruch bleibt Cancelled. Timeout, Quoten und signierte Workspace-Bereinigung bleiben aktiv. Bei CleanupPending die Ursache prüfen; keine fremden Verzeichnisse manuell löschen.

Commit und Push sind getrennte dauerhafte Aufträge. Push rekonstruiert den bestätigten lokalen Commit und verwendet erwarteten Remote-Ref als CAS-Bedingung. Ein verlorener Antwortstatus oder unbekannter Ausgang wird über den vorhandenen gespeicherten Intent und Remote-Ref abgeglichen. Niemals einen unbekannten Write durch ungeprüften zweiten Auftrag oder Force-Push ersetzen.

## Rollout und Abnahme

Version-2-Aufträge dürfen ausschließlich durch kompatible Worker verarbeitet werden. Vor einem Rollback neue Annahme stoppen und laufende Worker berücksichtigen; ein API-Rollback allein beendet keine Hintergrundarbeit. Bestehende Legacy-GitHub-Aufträge bleiben am ursprünglichen Pfad. Keine destruktive Downmigration bei neuen Belegen.

Vor Supportfreigabe fehlen insbesondere der kombinierte echte API-/Studio-/Serverlauf, Prozessverlust/Recovery, Browser-/Dirty-/Tenant-Wechselregressionen, Linux und die jeweils beanspruchten Anbieter-/Authentifizierungsverfahren. Lokaler nativer Testserver mit explizitem isolierten Peer ist kein Nachweis für die gesamte Produktions-DNS-/API-Kette.
