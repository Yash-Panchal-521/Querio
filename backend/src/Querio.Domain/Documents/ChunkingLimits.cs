namespace Querio.Domain.Documents;

/// <summary>
/// How big a passage should be.
///
/// Expressed in characters rather than tokens on purpose. The embedding model accepts 2,048
/// tokens, and an exact count would mean either shipping a vocabulary or spending an API call
/// per chunk against a metered daily allowance. Sizing well below the ceiling makes the
/// approximation safe: at roughly four characters per token, 2,000 characters is about 500
/// tokens, and even a pathological ratio of two characters per token stays under half the limit.
/// </summary>
public static class ChunkingLimits
{
    public const int MaxChunkCharacters = 2_000;

    /// <summary>
    /// Carried from the end of one passage into the start of the next, so a sentence split
    /// across a boundary is still wholly present in one of them. Without it, the answer to a
    /// question that straddles a break is in neither chunk.
    /// </summary>
    public const int OverlapCharacters = 200;

    /// <summary>
    /// A trailing fragment shorter than this is absorbed into the previous passage instead of
    /// standing alone. A twenty-character chunk embeds to a vector that matches almost
    /// anything, which is worse than not existing.
    /// </summary>
    public const int MinChunkCharacters = 250;

    /// <summary>Rough characters per token, used only for the count shown in the interface.</summary>
    public const int CharactersPerToken = 4;
}
