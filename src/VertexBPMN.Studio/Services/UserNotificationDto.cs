using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace VertexBPMN.Studio.Services;

internal sealed record UserNotificationDto(
    string Recipient,
    string Message,
    string Category,
    DateTime Timestamp);
