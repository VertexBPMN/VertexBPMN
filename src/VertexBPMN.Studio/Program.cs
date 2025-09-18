using VertexBPMN.Studio.Components;
using VertexBPMN.Studio.Services;
using MudBlazor.Services;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddMudServices();

// Configure HTTP clients
builder.Services.AddHttpClient("VertexBPMN.Api", client => 
{
    client.BaseAddress = new Uri("http://localhost:5074/"); // Your API URL
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(GetRetryPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

// Register HTTP-based services
builder.Services.AddScoped<IBpmnEngineService, HttpBpmnEngineService>();
builder.Services.AddScoped<IHistoryService, HttpHistoryService>();
builder.Services.AddSingleton<ActiveEngineService>();

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();
app.Run();
