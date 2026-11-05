using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

internal static class TaskFormState
{
    public static bool IsBound(UserTask task) => !string.IsNullOrWhiteSpace(task.FormKey) || !string.IsNullOrWhiteSpace(task.FormSchema);
}
