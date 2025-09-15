# AIServiceTaskHandler Usage Examples

## 1. Basic AI Service Task in BPMN

```xml
<serviceTask id="ai-task-1" name="AI Analysis Task">
  <extensionElements>
    <zeebe:taskDefinition type="aiServiceTask" />
    <zeebe:taskHeaders>
      <zeebe:header key="ai:provider" value="openai" />
      <zeebe:header key="ai:model" value="gpt-4" />
      <zeebe:header key="ai:prompt" value="Analyze customer sentiment" />
      <zeebe:header key="ai:contextEnrichment" value="true" />
      <zeebe:header key="ai:mcpIntegration" value="true" />
    </zeebe:taskHeaders>
  </extensionElements>
</serviceTask>
```

## 2. Advanced Configuration

```xml
<serviceTask id="ai-task-advanced" name="Advanced AI Processing">
  <extensionElements>
    <zeebe:taskDefinition type="aiServiceTask" />
    <zeebe:taskHeaders>
      <zeebe:header key="ai:provider" value="anthropic" />
      <zeebe:header key="ai:model" value="claude-3-sonnet-20240229" />
      <zeebe:header key="ai:taskType" value="analysis" />
      <zeebe:header key="ai:prompt" value="Perform detailed risk assessment for loan application" />
      <zeebe:header key="ai:systemMessage" value="You are a financial risk analyst with 20 years of experience." />
      <zeebe:header key="ai:temperature" value="0.3" />
      <zeebe:header key="ai:maxTokens" value="2000" />
      <zeebe:header key="ai:resultVariable" value="riskAssessment" />
      <zeebe:header key="ai:inputVariables" value="customerData,financialHistory,creditScore" />
      <zeebe:header key="ai:contextEnrichment" value="true" />
      <zeebe:header key="ai:includeMetadata" value="true" />
      <zeebe:header key="ai:timeout" value="120" />
      <zeebe:header key="ai:retryCount" value="3" />
    </zeebe:taskHeaders>
  </extensionElements>
</serviceTask>
```

## 3. Multi-Step AI Workflow

```xml
<!-- Step 1: Data Enrichment -->
<serviceTask id="enrich-context" name="Enrich Customer Context">
  <extensionElements>
    <zeebe:taskDefinition type="contextEnrichment" />
    <zeebe:taskHeaders>
      <zeebe:header key="context:sourceType" value="api" />
      <zeebe:header key="context:sourceUrl" value="https://api.crm.company.com/customer/{customerId}" />
    </zeebe:taskHeaders>
  </extensionElements>
</serviceTask>

<!-- Step 2: AI Analysis -->
<serviceTask id="ai-analysis" name="AI Customer Analysis">
  <extensionElements>
    <zeebe:taskDefinition type="aiServiceTask" />
    <zeebe:taskHeaders>
      <zeebe:header key="ai:provider" value="openai" />
      <zeebe:header key="ai:model" value="gpt-4" />
      <zeebe:header key="ai:prompt" value="Based on the enriched customer data, provide a comprehensive analysis including sentiment, risk level, and recommendations." />
      <zeebe:header key="ai:inputVariables" value="enrichedContext,customerData,transactionHistory" />
      <zeebe:header key="ai:resultVariable" value="customerAnalysis" />
      <zeebe:header key="ai:mcpIntegration" value="true" />
      <zeebe:header key="ai:mcpMethod" value="store_analysis_result" />
    </zeebe:taskHeaders>
  </extensionElements>
</serviceTask>

<!-- Step 3: Decision Based on AI Result -->
<exclusiveGateway id="decision-gateway" name="Analysis Result?">
  <incoming>Flow_from_ai</incoming>
  <outgoing>Flow_positive</outgoing>
  <outgoing>Flow_negative</outgoing>
</exclusiveGateway>

<sequenceFlow id="Flow_positive" sourceRef="decision-gateway" targetRef="approve-task">
  <conditionExpression xsi:type="tFormalExpression">
    #{customerAnalysis != null and customerAnalysis.contains("low risk")}
  </conditionExpression>
</sequenceFlow>
```

## 4. Provider-Specific Examples

### OpenAI GPT-4 Example
```xml
<zeebe:header key="ai:provider" value="openai" />
<zeebe:header key="ai:model" value="gpt-4" />
<zeebe:header key="ai:temperature" value="0.7" />
```

### Anthropic Claude Example
```xml
<zeebe:header key="ai:provider" value="anthropic" />
<zeebe:header key="ai:model" value="claude-3-opus-20240229" />
<zeebe:header key="ai:temperature" value="0.5" />
```

### Google Gemini Example
```xml
<zeebe:header key="ai:provider" value="gemini" />
<zeebe:header key="ai:model" value="gemini-pro" />
<zeebe:header key="ai:temperature" value="0.8" />
```

## 5. Error Handling

The handler automatically sets error variables that can be used in BPMN error handling:

```xml
<boundaryEvent id="ai-error" attachedToRef="ai-task-1">
  <errorEventDefinition id="ErrorEventDefinition_ai" />
</boundaryEvent>

<serviceTask id="handle-ai-error" name="Handle AI Error">
  <extensionElements>
    <zeebe:taskDefinition type="logError" />
    <zeebe:taskHeaders>
      <zeebe:header key="errorMessage" value="#{aiTask_error}" />
      <zeebe:header key="taskFailed" value="#{aiTask_failed}" />
    </zeebe:taskHeaders>
  </extensionElements>
</serviceTask>
```

## 6. Environment Variables Required

```bash
# For OpenAI
OPENAI_API_KEY=sk-your-openai-api-key

# For Anthropic
ANTHROPIC_API_KEY=your-anthropic-api-key

# For Gemini
GEMINI_API_KEY=your-gemini-api-key
# OR
GOOGLE_API_KEY=your-google-api-key
```

## 7. Process Variables Usage

### Input Variables
- `customerId`: Customer identifier for context enrichment
- `customerData`: Customer information object
- `transactionHistory`: Array of customer transactions
- `riskFactors`: Risk assessment parameters

### Output Variables
- `aiResult`: Main AI response (default variable name)
- `aiResult_metadata`: Token usage and model information (if `ai:includeMetadata=true`)
- `aiTask_error`: Error message (set on failure)
- `aiTask_failed`: Boolean indicating task failure

## 8. MCP Integration

When `ai:mcpIntegration=true`, the handler will call the configured MCP server after AI processing:

```xml
<zeebe:header key="ai:mcpIntegration" value="true" />
<zeebe:header key="ai:mcpServerUrl" value="http://mcp-server:8080/api/mcp" />
<zeebe:header key="ai:mcpMethod" value="process_ai_result" />
```

The MCP server receives:
- `aiResult`: The AI response
- `aiMetadata`: Token usage and model info
- `processVariables`: All process variables
- `provider`: AI provider used
- `model`: AI model used

This enables sophisticated AI workflows with external processing and storage of AI results.