namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Cache Keys Constants
/// </summary>
public static class CacheKeys
{
	private static readonly System.Text.CompositeFormat ProcessDefinitionFormat = System.Text.CompositeFormat.Parse(ProcessDefinition);
	private static readonly System.Text.CompositeFormat ProcessInstanceFormat = System.Text.CompositeFormat.Parse(ProcessInstance);
	private static readonly System.Text.CompositeFormat UserInfoFormat = System.Text.CompositeFormat.Parse(UserInfo);
	private static readonly System.Text.CompositeFormat TenantInfoFormat = System.Text.CompositeFormat.Parse(TenantInfo);
	public const string ProcessDefinition = "process_def_{0}";
	public const string ProcessInstance = "process_inst_{0}";
	public const string UserInfo = "user_{0}";
	public const string TenantInfo = "tenant_{0}";
	public const string SystemMetrics = "system_metrics";
	public const string WorkerNodes = "worker_nodes";
	public const string LoadBalancerStatus = "load_balancer_status";

	public static string ProcessDefinitionById(Guid id) => string.Format(System.Globalization.CultureInfo.CurrentCulture, ProcessDefinitionFormat, id);
	public static string ProcessInstanceById(Guid id) => string.Format(System.Globalization.CultureInfo.CurrentCulture, ProcessInstanceFormat, id);
	public static string UserById(string userId) => string.Format(System.Globalization.CultureInfo.CurrentCulture, UserInfoFormat, userId);
	public static string TenantById(string tenantId) => string.Format(System.Globalization.CultureInfo.CurrentCulture, TenantInfoFormat, tenantId);
}
