using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace Api.Agente.Infrastructure.Configuration;

internal static class ClassroomDemo
{
    internal const int MaxBodyBytes = 1024 * 1024;

    public static IReadOnlyList<byte[]>? Configure(WebApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue<bool>("ClassroomDemo:Enabled")) return null;

        var token = Environment.GetEnvironmentVariable("SOFIA_A2A_TOKEN")
            ?? builder.Configuration["SOFIA_A2A_TOKEN"]
            ?? builder.Configuration["Sofia:A2aToken"]
            ?? builder.Configuration["A2A:Token"]
            ?? builder.Configuration["ClassroomDemo:Token"];

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Configure a variável de ambiente SOFIA_A2A_TOKEN.");

        var publicUrl = Environment.GetEnvironmentVariable("A2A__PublicUrl")
            ?? builder.Configuration["A2A:PublicUrl"];

        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            var vercelHost = Environment.GetEnvironmentVariable("VERCEL_ENV") == "production"
                ? Environment.GetEnvironmentVariable("VERCEL_PROJECT_PRODUCTION_URL")
                : Environment.GetEnvironmentVariable("VERCEL_URL");
            publicUrl = !string.IsNullOrWhiteSpace(vercelHost) ? $"https://{vercelHost}/a2a" : null;
        }

        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            publicUrl = "https://agente-dimp-demo.vercel.app/a2a";
        }

        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https") || endpoint.AbsolutePath != "/a2a" ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0)
            throw new InvalidOperationException("Configure A2A__PublicUrl com a URL completa do endpoint /a2a.");

        builder.Configuration["A2A:PublicUrl"] = publicUrl;

        var tokens = new List<byte[]> { Encoding.UTF8.GetBytes(token) };
        var legacyToken = builder.Configuration["ClassroomDemo:Token"];
        if (!string.IsNullOrWhiteSpace(legacyToken) && !string.Equals(legacyToken, token, StringComparison.Ordinal))
        {
            tokens.Add(Encoding.UTF8.GetBytes(legacyToken));
        }

        return tokens;
    }

    public static void RequireToken(WebApplication app, IReadOnlyList<byte[]> tokens, string? extraProtectedRoute = null)
    {
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            var path = request.Path;

            var isCard = path.StartsWithSegments("/.well-known/agent-card.json", StringComparison.OrdinalIgnoreCase);
            var isA2A = path.StartsWithSegments("/a2a", StringComparison.OrdinalIgnoreCase);
            var isExtra = extraProtectedRoute is not null &&
                          path.StartsWithSegments(extraProtectedRoute, StringComparison.OrdinalIgnoreCase);

            if (isCard || isA2A || isExtra)
            {
                if (HttpMethods.IsOptions(request.Method))
                {
                    await next(context);
                    return;
                }

                if (tokens.Count == 0 ||
                    !AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var auth) ||
                    !auth.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
                    auth.Parameter is not { } supplied)
                {
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
                var isAuthorized = false;
                foreach (var expectedToken in tokens)
                {
                    if (CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedToken))
                    {
                        isAuthorized = true;
                        break;
                    }
                }

                if (!isAuthorized)
                {
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                if (isA2A) LimitRequestBody(context);
            }

            await next(context);
        });
    }

    internal static void LimitRequestBody(HttpContext context, int maxBodyBytes = MaxBodyBytes)
    {
        var bodyLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = maxBodyBytes;
    }
}
