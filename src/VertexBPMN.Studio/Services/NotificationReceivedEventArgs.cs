namespace VertexBPMN.Studio.Services;

internal sealed class NotificationReceivedEventArgs(UserNotificationDto value) : EventArgs
{
	public UserNotificationDto Notification { get; } = value;
}
