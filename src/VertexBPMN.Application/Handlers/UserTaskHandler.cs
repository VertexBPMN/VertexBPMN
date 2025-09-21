using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using System.Text.Json;
using VertexBPMN.Application.Extensions;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application.Handlers;

public class UserTaskHandler
{
    private readonly IProcessInstanceRepository _processRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly ITaskService _taskService;
    private readonly ILogger _logger;

    public UserTaskHandler(
        IProcessInstanceRepository processRepository,
        IUserRepository userRepository,
        INotificationService notificationService,
        ITaskService taskService,
        ILogger logger)
    {
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserTaskResult> HandleUserTaskAsync(UserTaskContext context)
    {
        try
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.TaskId == Guid.Empty || context.ProcessInstanceId == Guid.Empty)
                throw new ArgumentException("TaskId and ProcessInstanceId must be provided");

            var processInstance = await _processRepository.GetByIdAsync(context.ProcessInstanceId);
            if (processInstance == null)
                throw new ServiceTaskExecutionException($"Process instance {context.ProcessInstanceId} not found");

            var userTask = await _taskService.GetByIdAsync(context.TaskId);
            if (userTask == null)
                throw new ServiceTaskExecutionException($"User task {context.TaskId} not found");

            if (!await HasUserPermissionAsync(context.UserId, userTask))
            {
                _logger.LogWarning("User {UserId} has no permission for task {TaskId}", context.UserId, context.TaskId);
                throw new UnauthorizedAccessException("User has no permission to perform this task");
            }

            if (userTask.Status != UserTaskStatus.Pending)
            {
                _logger.LogWarning("Task {TaskId} is in invalid state: {Status}", context.TaskId, userTask.Status);
                throw new InvalidOperationException($"Task is in {userTask.Status} state");
            }

            var validationResult = await ValidateTaskDataAsync(userTask, context.TaskData);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Task data validation failed: {Errors}", string.Join("; ", validationResult.Errors));
                throw new BpmnValidationException("Task data validation failed", validationResult.Errors.ToList());
            }
                
            var result = await ExecuteUserTaskAsync(userTask, context);

            userTask.Status = context.Action switch
            {
                UserTaskAction.Complete => UserTaskStatus.Completed,
                UserTaskAction.Delegate => UserTaskStatus.Delegated,
                UserTaskAction.Reject => UserTaskStatus.Rejected,
                _ => throw new NotSupportedException($"Unsupported task action: {context.Action}")
            };

            userTask.LastModified = DateTime.UtcNow;
            userTask.ModifiedBy = context.UserId;

            await _processRepository.UpdateAsync(processInstance);

            await NotifyStakeholdersAsync(userTask, context);
            await LogTaskEventAsync(userTask, context);

            return new UserTaskResult
            {
                Success = true,
                TaskId = userTask.Id,
                NewStatus = userTask.Status,
                ResultData = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling user task {TaskId}", context?.TaskId);
            return new UserTaskResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<bool> HasUserPermissionAsync(string userId, UserTask userTask)
    {
        var user = await _userRepository.GetUserAsync(userId); // resolves User type via repository
        return userTask.Assignee == userId ||
               userTask.CandidateUsers.Contains(userId) ||
               await _userRepository.IsInRoleAsync(userId, userTask.CandidateRole);
    }

    private async Task<UserTaskValidationResult> ValidateTaskDataAsync(UserTask userTask, Dictionary<string, object> taskData)
    {
        var errors = new List<string>();

        foreach (var requiredField in userTask.RequiredFields)
        {
            if (!taskData.TryGetValue(requiredField, out var value) || value is null)
            {
                errors.Add($"Missing or invalid required field: {requiredField}");
            }
        }

        return errors.Count == 0
            ? UserTaskValidationResult.Success()
            : UserTaskValidationResult.Failure(errors);
    }

    private async ValueTask<object> ExecuteUserTaskAsync(UserTask userTask, UserTaskContext context) =>
        context.Action switch
        {
            UserTaskAction.Complete => await _taskService.CompleteAsync(userTask.Id, context.TaskData),
            UserTaskAction.Delegate => await _taskService.DelegateAsync(userTask.Id, context.DelegatedUserId),
            UserTaskAction.Reject => await _taskService.RejectAsync(userTask.Id, context.RejectionReason),
            _ => throw new NotSupportedException($"Unsupported task action: {context.Action}")
        };

    private async Task NotifyStakeholdersAsync(UserTask userTask, UserTaskContext context)
    {
        var notifications = new List<Notification>();

        if (context.Action == UserTaskAction.Delegate && !string.IsNullOrWhiteSpace(context.DelegatedUserId))
        {
            notifications.Add(new Notification(
                context.DelegatedUserId,
                $"Task {userTask.Name} has been delegated to you"));
        }

        if (!string.IsNullOrWhiteSpace(userTask.Assignee))
        {
            notifications.Add(new Notification(
                userTask.Assignee!,
                $"Task {userTask.Name} has been {context.Action.ToString().ToLowerInvariant()}"));
        }

        await _notificationService.SendNotificationsAsync(notifications);
    }

    private async Task LogTaskEventAsync(UserTask userTask, UserTaskContext context)
    {
        var processMiningEvent = new ProcessMiningEvent
        {
            TaskId = userTask.Id.ToString(),
            ProcessInstanceId = context.ProcessInstanceId.ToString(),
            UserId = context.UserId,
            TenantId = userTask.TenantId,
            EventType = context.Action.ToString(),
            Timestamp = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(context.TaskData)
        };

        _logger.LogInformation("{@ProcessMiningEvent}", processMiningEvent);
    }
}
