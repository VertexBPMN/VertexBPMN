# GitHub Copilot Anweisungen für das Projekt "BPMN-NextGen Engine"

Willkommen im Team! Du hilfst uns beim Bau einer BPMN 2.0 Engine der nächsten Generation für .NET. Deine Aufgabe ist es, Code von höchster Qualität zu generieren, der unseren strengen Architektur-, Stil- und Testrichtlinien entspricht. Bitte befolge diese Anweisungen sorgfältig.

## 1. Die Mission (Das 'Warum')

Wir bauen eine BPMN-Engine, die in puncto Performance, Konformität und Developer Experience neue Maßstäbe im .NET-Ökosystem setzt. Das Ziel ist funktionale Parität mit Camunda, kombiniert mit nativer .NET-Performance und innovativen Features. Denk immer daran: **Qualität, Testbarkeit und Lesbarkeit sind nicht verhandelbar.**

## 2. Technische Kernspezifikationen (Das 'Was')

Dein Code MUSS auf dem folgenden Tech-Stack basieren:

- **Framework:** .NET 9
- **Sprache:** C# 13 (nutze moderne Features wie Primary Constructors, `record struct`, Collection Literals etc.)
- **Persistenz:** Entity Framework Core 9 (EF Core) mit dem Repository- und Unit-of-Work-Pattern.
- **Testing:**
    - **Unit-Tests:** xUnit
    - **Mocks/Fakes:** Moq (oder NSubstitute, falls bevorzugt, aber sei konsistent)
    - **Assertions:** FluentAssertions
- **API:** ASP.NET Core Minimal APIs für REST; gRPC for .NET für RPC-Schnittstellen.
- **Observability:** OpenTelemetry für Tracing, Metrics und Logging.
- **Datenbanken:** PostgreSQL (primär) und SQL Server (sekundär). Schreibe portablen EF Core Code.

## 3. Architektonische Leitsätze (Das 'Wie')

Unsere Architektur ist modular und serviceorientiert.

- **Service-Orientierung:** Die Engine ist in klare Services aufgeteilt (`RepositoryService`, `RuntimeService`, `TaskService` etc.), die über Interfaces (`IRepositoryService` etc.) angesprochen werden. Implementiere immer gegen diese Interfaces.
- **Persistenz-Abstraktion:** **NIEMALS** direkten `DbContext` in der Business-Logik verwenden. Nutze immer ein Repository-Interface (z.B. `IProcessDefinitionRepository`). Die Implementierung dieses Interfaces verwendet EF Core.
- **Zustandslosigkeit:** Die API- und Worker-Knoten müssen zustandslos (stateless) sein. Jeder Zustand wird in der Datenbank (oder einem verteilten Cache) persistiert.
- **Asynchronität:** Alle I/O-lastigen Operationen (Datenbank, Netzwerk) MÜSSEN asynchron sein. Verwende `async`/`await` konsequent und nutze `ValueTask`, wo sinnvoll.

## 4. Code-Qualität und Stil (Unsere Regeln)

- **Sprachfeatures:** Nutze die neuesten C# 13 Features, wo sie die Lesbarkeit und Performance verbessern. `record` und `record struct` sind für DTOs und unveränderliche Datenmodelle zu bevorzugen.
- **Nullability:** Das gesamte Projekt verwendet `#nullable enable`. Behandle Warnungen zu Null-Referenzen als Fehler. Vermeide den `!`-Operator (null-forgiving) so weit wie möglich.
- **Namenskonventionen:**
    - Methoden: `PascalCase`. Asynchrone Methoden enden **immer** auf `Async` (z.B. `StartProcessInstanceAsync`).
    - Interfaces: `IPascalCase` (z.B. `IRuntimeService`).
    - Private Felder: `_camelCase`.
- **XML-Dokumentation:** Alle `public` und `internal` Typen und Member MÜSSEN eine vollständige XML-Dokumentation haben (`<summary>`, `<param>`, `<returns>`).

  ```csharp
  /// <summary>
  /// Starts a new process instance for the definition with the given key.
  /// </summary>
  /// <param name="processDefinitionKey">The unique key of the process definition.</param>
  /// <param name="variables">A dictionary of variables to start the process with.</param>
  /// <param name="cancellationToken">A token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation, which returns the newly created process instance.</returns>
  Task<ProcessInstance> StartProcessByKeyAsync(string processDefinitionKey, IDictionary<string, object> variables, CancellationToken cancellationToken);
````

  - **Fehlerbehandlung:** Wir verwenden spezifische, benutzerdefinierte Exceptions (z.B. `ProcessNotFoundException`, `TaskAlreadyClaimedException`). Werfe keine generischen `Exception` oder `SystemException`.
  - **Logging:** Nutze `Microsoft.Extensions.Logging.ILogger` für strukturiertes Logging. **KEIN** `Console.WriteLine` oder `Debug.WriteLine` in der Anwendungslogik.

## 5\. Test-Philosophie (Immer testen\!)

  - **Hohe Abdeckung:** Wir streben eine Testabdeckung von \>=95% für die Kernlogik an. Jede neue Funktion muss von Unit- und/oder Integrationstests begleitet werden.
  - **Arrange-Act-Assert (AAA):** Strukturiere alle Tests klar nach dem AAA-Muster.
  - **Fokus auf Unit-Tests:** Die Geschäftslogik der einzelnen BPMN-Elemente und Services sollte primär mit Unit-Tests validiert werden.
  - **Integrationstests für Abläufe:** End-to-End-Szenarien, die die Interaktion mehrerer Komponenten (z.B. API -\> Service -\> Datenbank) testen, werden als Integrationstests implementiert.
  - **Konformitätstests:** Denke daran, dass der generierte Code die BPMN MIWG und DMN TCK Test-Suiten bestehen muss.

## 6\. Domänenspezifische Regeln (BPMN-Semantik)

  - **Token-Fluss:** Die Engine ist Token-basiert. Denke in Tokens, die durch den Graphen fließen, konsumiert und erzeugt werden.
  - **Unveränderlichkeit (Immutability):** Historische Daten (`HistoryEvent`) sind nach ihrer Erstellung unveränderlich.
  - **Transaktionsgrenzen:** Zustandsänderungen müssen atomar innerhalb von Datenbanktransaktionen stattfinden. Eine "Arbeitseinheit" (z.B. das Bewegen eines Tokens von einem Knoten zum nächsten) sollte in einer einzigen Transaktion erfolgen.

## 7\. Absolute No-Gos (Was du vermeiden musst)

  - **Veraltete Muster:** Keine `for`-Schleifen, wenn `LINQ` lesbarer ist. Keine manuellen Thread-Erstellungen.
  - **Direkte DB-Abfragen:** Kein `DbContext` außerhalb der Repository-Implementierungen.
  - **Magische Strings:** Verwende `nameof()` für Typen- und Member-Namen und `const` oder `static readonly` Felder für wiederkehrende Strings (z.B. Variablennamen, BPMN-Fehlercodes).
  - **Verschluckte Exceptions:** Fange niemals eine Exception, ohne sie zu loggen oder weiterzuwerfen. Leere `catch`-Blöcke sind verboten.
  - **Abhängigkeiten von konkreten Klassen:** Programmiere immer gegen Interfaces (`IEnumerable` statt `List`, `IDictionary` statt `Dictionary` in öffentlichen Signaturen).

Wir zählen auf deine Unterstützung, um dieses Projekt zu einem herausragenden Erfolg zu machen. Lass uns sauberen, performanten und robusten Code schreiben\!
