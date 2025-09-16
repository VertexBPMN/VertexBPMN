using VertexBPMN.Domain.Entities.Modeling;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Unified process execution engine interface supporting both simple and distributed execution modes.
/// Combines BPMN, CMMN, and DMN execution capabilities with backward compatibility.
/// </summary>
public interface IProcessEngine
{
    #region Core Execution Methods
    
    /// <summary>
    /// Executes a BPMN process model asynchronously.
    /// </summary>
    /// <param name="model">The BPMN model to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution trace for debugging and monitoring</returns>
    Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Synchronous execution wrapper for compatibility with existing code.
    /// Use ExecuteAsync for new code to benefit from better performance and cancellation support.
    /// </summary>
    /// <param name="model">The BPMN model to execute</param>
    /// <returns>Execution trace</returns>
    List<string> Execute(BpmnModel model);
    
    /// <summary>
    /// Executes a CMMN case model asynchronously.
    /// Simple engines may return informational messages about unsupported features.
    /// </summary>
    /// <param name="model">The CMMN case model to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution trace</returns>
    Task<List<string>> ExecuteCaseAsync(CaseModel model, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a process by its registered process ID.
    /// Requires process registry support in the engine.
    /// </summary>
    /// <param name="processId">The process definition ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution trace</returns>
    Task<List<string>> ExecuteProcessAsync(string processId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a node can be executed (considers capacity, workers available, etc.).
    /// Simple engines always return true; distributed engines check worker availability.
    /// </summary>
    /// <param name="nodeId">The node ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the node can be executed</returns>
    Task<bool> CanExecuteAsync(string nodeId, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Model Registration & Management
    
    /// <summary>
    /// Registers a BPMN process model for later execution.
    /// Simple engines may not support this and throw NotSupportedException.
    /// </summary>
    /// <param name="processId">Process definition ID</param>
    /// <param name="bpmnXml">BPMN XML content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RegisterBpmnModelAsync(string processId, string bpmnXml, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Registers a CMMN case model for later execution.
    /// Simple engines may not support this and throw NotSupportedException.
    /// </summary>
    /// <param name="caseId">Case definition ID</param>
    /// <param name="cmmnXml">CMMN XML content</param>
    Task RegisterCmmnModelAsync(string caseId, string cmmnXml);
    
    /// <summary>
    /// Registers a DMN decision model for business rule tasks.
    /// Simple engines may not support this and throw NotSupportedException.
    /// </summary>
    /// <param name="decisionId">Decision definition ID</param>
    /// <param name="dmnXml">DMN XML content</param>
    Task RegisterDmnModelAsync(string decisionId, string dmnXml);
    
    /// <summary>
    /// Retrieves a CMMN model by case ID.
    /// Simple engines may not support this and throw NotSupportedException.
    /// </summary>
    /// <param name="caseId">Case definition ID</param>
    /// <returns>The case model</returns>
    Task<CaseModel> GetCmmnModelAsync(string caseId);
    
    /// <summary>
    /// Gets historical case execution data for analytics and optimization.
    /// Simple engines may not support this and throw NotSupportedException.
    /// </summary>
    /// <param name="caseId">Case ID</param>
    /// <returns>Historical case data</returns>
    Task<List<HistoricalCaseData>> GetHistoricalCaseDataAsync(string caseId);
    
    #endregion
}