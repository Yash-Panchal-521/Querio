using Mediator;

namespace Querio.Application.Documents.CreateDownloadLink;

public sealed record CreateDownloadLinkCommand(Guid TenantId, Guid DocumentId) : ICommand<DownloadLink>;

/// <param name="ExpiresAt">So the interface can say when the link stops working.</param>
public sealed record DownloadLink(Uri Url, DateTimeOffset ExpiresAt);
