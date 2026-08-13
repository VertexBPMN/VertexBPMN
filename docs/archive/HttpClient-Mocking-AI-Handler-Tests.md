# HttpClient Mocking für AI Service Task Handler Tests

## ?? **Übersicht**

Die AI Service Task Handler Tests verwenden jetzt **HttpClient Mocking** mit **Moq.Protected**, um offline Tests zu ermöglichen. Dadurch können die Tests ohne echte API-Aufrufe an OpenAI, Anthropic oder Gemini ausgeführt werden.

## ?? **Vorteile des HttpClient Mocking**

- ? **Offline-Tests:** Keine Internet-Verbindung erforderlich
- ? **Keine API-Kosten:** Vermeidung von API-Gebühren während Tests
- ? **Deterministische Tests:** Vorhersagbare Antworten
- ? **Schnelle Ausführung:** Keine Netzwerk-Latenzen
- ? **Isolation:** Tests sind unabhängig von externen Services

---

## ?? **Test-Architektur**

### **Mock-Setup mit Moq.Protected**

```csharp
private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
private readonly HttpClient _httpClient;

public OpenAiServiceTaskHandlerMockedTests()
{
    _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
    _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
    _handler = new OpenAiServiceTaskHandler(_httpClient, _loggerMock.Object, null);
}
```

### **Mock-Response Setup**

```csharp
// OpenAI Response Mock
var openAiResponse = new
{
    choices = new[]
    {
        new
        {
            message = new { content = "Mocked OpenAI response" },
            finish_reason = "stop"
        }
    },
    usage = new { prompt_tokens = 25, completion_tokens = 15, total_tokens = 40 }
};

var responseJson = JsonSerializer.Serialize(openAiResponse, JsonOptions);
var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
{
    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
};

_httpMessageHandlerMock
    .Protected()
    .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.Is<HttpRequestMessage>(req =>
            req.Method == HttpMethod.Post &&
            req.RequestUri!.ToString().Contains("openai.com")),
        ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync(httpResponse);
```

---

## ?? **Provider-spezifische Mocks**

### **1. OpenAI API Mock**

```csharp
[Fact]
public async Task ExecuteAsync_WithMockedHttpClient_ShouldHandleOpenAIResponse()
{
    // Arrange
    var openAiResponse = new
    {
        choices = new[]
        {
            new
            {
                message = new { content = "Mocked OpenAI response" },
                finish_reason = "stop"
            }
        },
        usage = new { prompt_tokens = 25, completion_tokens = 15, total_tokens = 40 },
        model = "gpt-4"
    };

    SetupHttpMock(openAiResponse, "openai.com");

    // Act & Assert
    await ExecuteAndVerifyResult();
}
```

### **2. Anthropic Claude API Mock**

```csharp
[Fact]
public async Task ExecuteAsync_WithMockedHttpClient_ShouldHandleClaudeResponse()
{
    // Arrange
    var claudeResponse = new
    {
        content = new[]
        {
            new { type = "text", text = "Mocked Claude response" }
        },
        model = "claude-3-sonnet-20240229",
        stop_reason = "end_turn",
        usage = new { input_tokens = 15, output_tokens = 18 }
    };

    SetupHttpMock(claudeResponse, "anthropic.com");

    // Act & Assert
    await ExecuteAndVerifyResult();
}
```

### **3. Google Gemini API Mock**

```csharp
[Fact]
public async Task ExecuteAsync_WithMockedHttpClient_ShouldHandleGeminiResponse()
{
    // Arrange
    var geminiResponse = new
    {
        candidates = new[]
        {
            new
            {
                content = new
                {
                    parts = new[] { new { text = "Mocked Gemini response" } }
                },
                finishReason = "STOP"
            }
        },
        usageMetadata = new
        {
            promptTokenCount = 20,
            candidatesTokenCount = 12,
            totalTokenCount = 32
        }
    };

    SetupHttpMock(geminiResponse, "generativelanguage.googleapis.com");

    // Act & Assert
    await ExecuteAndVerifyResult();
}
```

---

## ?? **Handler-Implementierung mit Mock-Unterstützung**

### **Mock-Mode für Tests**

Die AI Handler unterstützen einen `UseMockMode` Parameter für einfachere Tests:

```csharp
// In Handler-Implementierung
if (config.UseMockMode)
{
    var mockResult = $"Gemini {config.Model} processed: {config.Prompt}";
    variables[config.ResultVariable] = mockResult;
    return;
}
```

### **BPMN-Attribute für Mock-Mode**

```xml
<serviceTask id="ai-task" name="AI Task">
  <extensionElements>
    <zeebe:taskDefinition type="aiServiceTask" />
    <zeebe:taskHeaders>
      <zeebe:header key="ai:provider" value="openai" />
      <zeebe:header key="ai:model" value="gpt-4" />
      <zeebe:header key="ai:useMockMode" value="true" />
      <zeebe:header key="ai:prompt" value="Test prompt" />
    </zeebe:taskHeaders>
  </extensionElements>
</serviceTask>
```

---

## ?? **Request-Verifikation**

### **HTTP-Header Verifikation**

```csharp
// Verify OpenAI Authorization Header
_httpMessageHandlerMock.Protected().Verify(
    "SendAsync",
    Times.Once(),
    ItExpr.Is<HttpRequestMessage>(req =>
        req.Headers.Authorization!.Scheme == "Bearer" &&
        req.Headers.Authorization.Parameter == "test-api-key"),
    ItExpr.IsAny<CancellationToken>());

// Verify Anthropic Custom Headers
_httpMessageHandlerMock.Protected().Verify(
    "SendAsync",
    Times.Once(),
    ItExpr.Is<HttpRequestMessage>(req =>
        req.Headers.Contains("x-api-key") &&
        req.Headers.Contains("anthropic-version")),
    ItExpr.IsAny<CancellationToken>());
```

### **Request Body Verifikation**

```csharp
string? capturedRequestBody = null;
_httpMessageHandlerMock
    .Protected()
    .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
    .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
    {
        capturedRequestBody = await req.Content!.ReadAsStringAsync();
    })
    .ReturnsAsync(httpResponse);

// Nach dem Test
capturedRequestBody.ShouldContain($"\"model\":\"{model}\"");
capturedRequestBody.ShouldContain(prompt);
```

---

## ?? **Test-Szenarien**

### **1. Erfolgreiche API-Antworten**
- ? Standard-Konfiguration
- ? Verschiedene Modelle
- ? Custom-Parameter
- ? Token-Usage Tracking

### **2. Fehlerbehandlung**
- ? HTTP-Fehler (401, 429, 500)
- ? Ungültige API-Keys
- ? Malformed Response
- ? Timeout-Verhalten

### **3. Provider-spezifische Tests**
- ? OpenAI: GPT-3.5, GPT-4, GPT-4-Turbo
- ? Anthropic: Claude-3 Haiku, Sonnet, Opus
- ? Gemini: Pro, Pro Vision, Multimodal

---

## ?? **Test-Ausführung**

### **Einzelne Tests**

```bash
# Alle AI Handler Tests
dotnet test --filter "FullyQualifiedName~Mocked" --logger console

# OpenAI Tests
dotnet test --filter "ClassName~OpenAiServiceTaskHandlerMockedTests" --logger console

# Anthropic Tests
dotnet test --filter "ClassName~AnthropicServiceTaskHandlerMockedTests" --logger console

# Gemini Tests
dotnet test --filter "ClassName~GeminiServiceTaskHandlerMockedTests" --logger console
```

### **CI/CD Integration**

```yaml
# GitHub Actions / Azure DevOps
- name: Run AI Handler Tests
  run: dotnet test --filter "FullyQualifiedName~AI" --no-build --verbosity normal
```

---

## ?? **Best Practices**

### **1. Test-Isolation**
- Jeder Test reinigt Environment-Variablen nach sich auf
- HttpClient wird pro Test-Klasse erstellt
- Mock-Setup ist spezifisch für jeden Test

### **2. Realistische Mock-Daten**
- Mock-Antworten spiegeln echte API-Strukturen wider
- Token-Counts sind realistisch
- Error-Responses enthalten echte Fehlermeldungen

### **3. Verifikation**
- HTTP-Requests werden auf korrekte Headers geprüft
- Request-Bodies werden auf erwartete Inhalte getestet
- Response-Processing wird vollständig validiert

---

## ? **Fazit**

Das HttpClient Mocking ermöglicht:
- **Offline-Entwicklung** ohne API-Dependencies
- **Kosteneinsparung** durch Vermeidung echter API-Aufrufe
- **Zuverlässige Tests** mit deterministischen Ergebnissen
- **Vollständige Abdeckung** aller Fehler-Szenarien

Die Tests sind jetzt **produktionstauglich** und können sicher in CI/CD-Pipelines ausgeführt werden! ??
