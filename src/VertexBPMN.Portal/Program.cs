var builder = WebApplication.CreateBuilder(args);

// Portal-BFF: Cookie-basierte Authentifizierung (server-side token holding)
// und CSRF-Schutz werden vom gemeinsamen OIDC-Baustein (LC06) genutzt.
builder.Services.AddAntiforgery();
builder.Services.AddHttpClient();
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapControllers();

// Blazor-WebAssembly-Client (Portal.Client) als Fallback ausliefern.
app.MapFallbackToFile("index.html");

await app.RunAsync();
