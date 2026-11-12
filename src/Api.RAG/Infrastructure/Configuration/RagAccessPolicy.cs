using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Api.RAG.Infrastructure.Configuration;

public sealed class RagAccessPolicy
{
    private readonly byte[]? queryToken;
    private readonly byte[]? adminToken;

    public RagAccessPolicy(IConfiguration configuration, bool queryOnly)
    {
        QueryOnly = queryOnly;
        queryToken = ReadToken(configuration, "QueryToken");
        adminToken = ReadToken(configuration, "AdminToken");
        if (queryOnly && queryToken is null)
            throw new InvalidOperationException("Configure RagAccess__QueryToken para a Api.RAG hospedada.");
        if (queryOnly && adminToken is not null)
            throw new InvalidOperationException("Configure a credencial de ingestão apenas no processo administrativo.");
        if (queryToken is not null && adminToken is not null &&
            CryptographicOperations.FixedTimeEquals(queryToken, adminToken))
            throw new InvalidOperationException("Use credenciais distintas para consulta e ingestão.");
    }

    public bool QueryOnly { get; }

    public bool AllowsQuery(HttpRequest request) => Matches(request, queryToken);

    public bool AllowsAdministration(HttpRequest request) => !QueryOnly && Matches(request, adminToken);

    public static IResult Challenge(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Unauthorized();
    }

    private static byte[]? ReadToken(IConfiguration configuration, string name)
    {
        var value = configuration[$"RagAccess:{name}"];
        if (value is null)
            return null;
        if (value.Length < 32 || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            throw new InvalidOperationException($"Configure RagAccess__{name} com uma credencial de pelo menos 32 caracteres sem espaços.");
        return Encoding.UTF8.GetBytes(value);
    }

    private static bool Matches(HttpRequest request, byte[]? token) => token is null ||
        (AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header) &&
         string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
         header.Parameter is { } value &&
         CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(value), token));
}
