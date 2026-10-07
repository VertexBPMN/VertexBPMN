namespace VertexBPMN.Domain.Entities;

public enum ExternalTaskState { Ready, Leased, RetryScheduled, Completed, Failed, Cancelled, TimedOut }
