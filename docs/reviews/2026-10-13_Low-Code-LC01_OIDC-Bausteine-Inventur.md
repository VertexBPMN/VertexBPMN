# LC01 — OIDC-Bausteine-Inventur

Stand: 2026-10-13. Bestand laut Plan §5 LC01 „OIDC-Bausteine-Inventur". Zweck:
vorhandene Bausteine sauber zählen und benennen, damit Portal-BFF (LC06) und
instanzbasierte Permission-Hooks (LC10) auf vorhandene Teile zurückgreifen statt
duplizieren.

## Inventar (aus `src/` verifiziert)

| Bereich | Datei | Rolle |
| --- | --- | --- |
| Studio | `src/VertexBPMN.Studio/StudioAuthenticationRedirects.cs` | OIDC-Redirect-/Callback-Fluss für den Browser |
| Studio | `src/VertexBPMN.Studio/Components/Pages/OAuth2Callback.razor` | OAuth2/Callback-Seite |
| Studio | `src/VertexBPMN.Studio/Services/OAuth2ConnectConfig.cs` | Anbindung konfiguration für OAuth2-/Connectors |
| Studio | `src/VertexBPMN.Studio/Services/OAuth2CredentialFlowService.cs` | Credential-Flow (Authorization-Code) für angeschlossene Systeme |
| Studio | `src/VertexBPMN.Studio/Services/PersistentOidcSessionStore.cs` | persistente OIDC-Session-Ablage |
| Studio | `src/VertexBPMN.Studio/Services/OidcSessionStoreProvider.cs` | Session-Store-Provider |
| Studio | `src/VertexBPMN.Studio/Services/StoredOidcSession.cs`, `OidcSessionRecord.cs` | Session-Datensätze |
| Studio | `src/VertexBPMN.Studio/Services/OidcSessionExpiredException.cs` | Ablauf-Fehler |
| Studio | `src/VertexBPMN.Studio/Migrations/OidcSession/*` | EF-Migrationen des OIDC-Session-Stores |
| Infra | `src/VertexBPMN.Infrastructure/Operational/OAuth2FlowStateCleanupService.cs` | Aufräumen von OAuth2-Flow-Zuständen |
| Infra | `src/VertexBPMN.Infrastructure/Persistence/Services/OAuth2CredentialFlowService.cs` | Credential-Flow-Dienst (Backend) |
| Infra | `src/VertexBPMN.Infrastructure/Persistence/Migrations/Bpmn/20260906175904_AddOAuth2FlowStates.cs` u. Folge | Migration für OAuth2-Flow-States |
| AgentWorker | `src/VertexBPMN.AgentWorker/OAuthClientCredentialsTokenProvider.cs` | Client-Credentials-Token für maschinelle Aufrufe |
| ServiceDefaults | `src/VertexBPMN.ServiceDefaults/Security/VertexOidcClaims.cs` | OIDC-Claim-Namen |
| ServiceDefaults | `src/VertexBPMN.ServiceDefaults/Security/LocalOidcBackchannelHandler.cs` | lokaler OIDC-Backchannel (Test/Lokal) |

## Befunde

- Es gibt **keinen** zentralen Portal-/BFF-OIDC-Dienst; Browser-OIDC lebt heute im Studio.
- Client-Credentials (AgentWorker) und Authorization-Code (Studio) existieren als getrennte Pfade.
- Permission-/instanzbasierte Rechte (data-scoped) gibt es noch **nicht**; das ist Aufgabe von LC10.
- Ein Portal-BFF kann `VertexOidcClaims.cs` + `OAuth2CredentialFlowService.cs` wiederverwenden, ohne Studio zu berühren.

## Konsequenz für später

- LC06 (Portal-BFF) referenziert diese Inventur; LC10 (instanzbasierte Permission-Hooks) ergänzt neue Rechtepfade entlang `VertexOidcClaims.cs`, ohne bestehende Studio-OIDC-Flows zu brechen.
