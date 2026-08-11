# Studio Authentication

VertexBPMN Studio uses an external OpenID Connect provider. It does not provide
a local login, a development identity, or a default role.

Configure these values through deployment configuration or environment variables
(`StudioAuthentication__Authority`, `StudioAuthentication__ClientId`,
`StudioAuthentication__ClientSecret`, and optionally
`StudioAuthentication__ApiScope`):

- `Authority`: the issuer/authority URL of the OIDC provider.
- `ClientId`: the confidential client registered for Studio.
- `ClientSecret`: the client secret, stored outside the repository. Public OIDC
  clients may leave this empty when the provider supports PKCE without a secret.
- `ApiScope`: the provider scope that issues an access token accepted by the
  VertexBPMN API. Leave it empty only when the provider's default token already
  has the API audience.

Register the callback URL `{StudioBaseUrl}/signin-oidc` and the post-logout
redirect URL `{StudioBaseUrl}/` at the identity provider. The provider must
issue the `tenant_id` claim and the role claims expected by the API. Studio
stores the OIDC tokens in its encrypted cookie session and forwards the access
token to the API as a bearer token.

Studio fails during startup when `Authority` or `ClientId` is missing. This is
intentional: an unconfigured deployment must not silently become anonymous or
create a synthetic administrator.