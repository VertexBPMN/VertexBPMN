using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using VertexBPMN.Portal.Client;
using VertexBPMN.Portal.Client.Bff;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The Portal BFF is served from the same origin as the WASM client.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IBffAntiforgeryTokenProvider, BffAntiforgeryTokenProvider>();
builder.Services.AddScoped(sp => new PortalBffClient(
    sp.GetRequiredService<HttpClient>(),
    sp.GetRequiredService<IBffAntiforgeryTokenProvider>()));

await builder.Build().RunAsync();
