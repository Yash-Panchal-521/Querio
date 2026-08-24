namespace Querio.Infrastructure.Ingestion;

/// <summary>
/// How the ingestion worker paces itself.
/// </summary>
public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>
    /// How long to wait before asking for work again when the queue is empty. Short enough
    /// that an upload feels immediate, long enough that an idle instance is not holding a
    /// database awake — which on a plan that suspends when idle is the whole month's compute.
    /// </summary>
    public int IdlePollSeconds { get; set; } = 5;

    /// <summary>
    /// How long a claim is held before another worker may take it. Long enough to cover a slow
    /// batch, short enough that a killed container's work resumes without an operator.
    /// </summary>
    public int LeaseSeconds { get; set; } = 120;

    /// <summary>Set false to run the API without ingesting — useful when debugging.</summary>
    public bool Enabled { get; set; } = true;
}
