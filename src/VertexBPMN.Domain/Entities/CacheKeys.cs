using System;

namespace VertexBPMN.Domain;

/// <summary>
/// Cache Keys Constants
/// </summary>
public static class CacheKeys
{
    public const string ProcessDefinition = "process_def_{0}";
    public const string ProcessInstance = "process_inst_{0}";
    public const string UserInfo = "user_{0}";
    public const string TenantInfo = "tenant_{0}";
    public const string SystemMetrics = "system_metrics";
    public const string WorkerNodes = "worker_nodes";
    public const string LoadBalancerStatus = "load_balancer_status";

    public static string ProcessDefinitionById(Guid id) => string.Format(ProcessDefinition, id);
    public static string ProcessInstanceById(Guid id) => string.Format(ProcessInstance, id);
    public static string UserById(string userId) => string.Format(UserInfo, userId);
    public static string TenantById(string tenantId) => string.Format(TenantInfo, tenantId);
}