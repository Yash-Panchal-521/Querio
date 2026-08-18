namespace Querio.Domain.Common;

/// <summary>
/// Timestamps maintained by the persistence layer rather than by callers, so no handler can
/// forget to set them and no two rows disagree about what "now" was.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset UpdatedAt { get; set; }
}
