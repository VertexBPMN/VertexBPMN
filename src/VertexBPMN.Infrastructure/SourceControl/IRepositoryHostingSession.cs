using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal interface IRepositoryHostingSession : IDisposable
{
	Task<PullRequestReceipt> CreateAsync(PullRequestCommand command, CancellationToken cancellationToken);
	Task<PullRequestReceipt> ReconcileAsync(PullRequestCommand command, CancellationToken cancellationToken);
	Task<PullRequestReceipt> ReadAsync(PullRequestCommand command, long number, CancellationToken cancellationToken);
	Task<IReadOnlyList<PullRequestReview>> ReadReviewsAsync(long number, CancellationToken cancellationToken);
}
