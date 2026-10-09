using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using VertexBPMN.ServiceDefaults.Security;

namespace VertexBPMN.Studio.Services;

internal sealed class OidcSessionExpiredException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
