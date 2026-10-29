using System.Security.Cryptography;
using System.Text;

namespace Api.RAG.Infrastructure.Development;

// Deterministic test vector: carries no semantic quality and cannot share a real profile.
public sealed class OfflineEmbeddingProvider
{
    public const string ProfileId = "local-fake-sha256-v1";
    public const int Dimensions = 1536;

    public Task<OfflineEmbeddingResult> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var values = new float[Dimensions];
        double squaredNorm = 0;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (hash[i % hash.Length] - 127.5f) / 127.5f;
            squaredNorm += values[i] * values[i];
        }

        var norm = (float)Math.Sqrt(squaredNorm);
        for (var i = 0; i < values.Length; i++)
        {
            values[i] /= norm;
        }

        return Task.FromResult(new OfflineEmbeddingResult(ProfileId, values, IsSynthetic: true));
    }
}

public sealed record OfflineEmbeddingResult(string ProfileId, float[] Values, bool IsSynthetic);
