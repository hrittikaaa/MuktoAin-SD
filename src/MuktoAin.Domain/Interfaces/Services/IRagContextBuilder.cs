using MuktoAin.Domain.Models;

namespace MuktoAin.Domain.Interfaces.Services;

// Implemented in Application (vector-primary retrieval with FTS fallback).
public interface IRagContextBuilder
{
    Task<IEnumerable<RetrievedSection>> RetrieveContextAsync(string query, int topK = 8);

    /// <summary>
    /// Category-aware retrieval: filters results to prioritise sections from
    /// Acts relevant to the detected category. Falls back to unfiltered if
    /// categoryKey is null or has no mapping.
    /// </summary>
    Task<IEnumerable<RetrievedSection>> RetrieveContextAsync(string query, int topK, string? categoryKey)
        => RetrieveContextAsync(query, topK); // default: no-op for backwards compat
}
