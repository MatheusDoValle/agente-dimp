using System.Net;
using System.Text;
using Api.Agente.Application.Sofia;
using Api.Agente.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace Api.Agente.Endpoints;

internal static class LocalDimpDemoEndpoint
{
    internal const string Route = "/demo/dimp";
    internal const int MaxUploadBytes = 256 * 1024 * 1024;
    internal const int PreviewMaxUploadBytes = 4 * 1024 * 1024;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    public static void MapLocalDimpDemo(this WebApplication app, bool preview = false)
    {
        var bodyLimit = preview ? PreviewMaxUploadBytes : MaxUploadBytes;
        var limitDescription = $"Limite de {bodyLimit / 1024 / 1024} MiB por requisição.";

        app.UseWhen(context => context.Request.Path.StartsWithSegments(Route, StringComparison.OrdinalIgnoreCase),
            branch => branch.Use(async (context, next) =>
            {
                if (context.Request.ContentLength > bodyLimit)
                {
                    await Results.Json(new { mensagem = limitDescription },
                        statusCode: StatusCodes.Status413PayloadTooLarge).ExecuteAsync(context);
                    return;
                }
                ClassroomDemo.LimitRequestBody(context, bodyLimit);
                await next(context);
            }));

        app.MapPost(Route, AnalyzeAsync)
            .DisableAntiforgery()
            .WithTags("Demonstração DIMP")
            .WithSummary("Fazer uma pergunta ou analisar um DIMP")
            .WithDescription("Para consultar o Manual DIMP, preencha só pergunta e use uma dúvida geral, sem dados pessoais. Para validar um arquivo, anexe arquivo e escreva uma pergunta se quiser. " + limitDescription)
            .Produces<ResultadoAnaliseSofia>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status413PayloadTooLarge);
    }

    private static async Task<IResult> AnalyzeAsync(
        IFormFile? arquivo, [FromForm] string? pergunta, ISofiaAnalysisHandler handler,
        SofiaManualQuestionHandler manualQuestions, CancellationToken cancellationToken)
    {
        var question = pergunta?.Trim();
        if (arquivo is null)
        {
            if (string.IsNullOrWhiteSpace(question))
                return Results.BadRequest(new { mensagem = "Escreva uma pergunta ou selecione um arquivo DIMP." });

            return Results.Ok(await manualQuestions.AnswerAsync(question, cancellationToken));
        }

        var name = Path.GetFileName(arquivo.FileName.Replace('\\', '/'));
        if (arquivo.Length == 0 || string.IsNullOrWhiteSpace(name))
            return Results.BadRequest(new { mensagem = "Selecione um arquivo DIMP com conteúdo." });

        var request = new SolicitacaoAnaliseSofia(
            SolicitacaoAnaliseSofia.VersaoEsperada,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UsuarioSofia("Auditor local"),
            string.IsNullOrWhiteSpace(question) ? "Valide a estrutura e os registros do DIMP." : question,
            [new DocumentoSofia("dimp-local", name, "text/plain")]);

        if (arquivo.Length > ClassroomDemo.MaxBodyBytes)
        {
            await using var upload = arquivo.OpenReadStream();
            await using var validationStream = await PrepareValidationStreamAsync(upload, cancellationToken);
            return Results.Ok(await handler.HandleUploadAsync(request, validationStream, cancellationToken));
        }

        var text = await ReadTextAsync(arquivo, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
            return Results.BadRequest(new { mensagem = "O arquivo DIMP está vazio." });

        return Results.Ok(await handler.HandleAsync(
            request with { Documentos = [request.Documentos[0] with { Texto = text }] }, cancellationToken));
    }

    private static async Task<Stream> PrepareValidationStreamAsync(Stream upload, CancellationToken cancellationToken)
    {
        var prefix = new byte[4];
        var prefixLength = await upload.ReadAtLeastAsync(prefix.AsMemory(), prefix.Length, false, cancellationToken);
        upload.Position = 0;
        Encoding sourceEncoding;
        try
        {
            using var reader = new StreamReader(upload, StrictUtf8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var buffer = new char[16 * 1024];
            while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken) != 0) { }
            sourceEncoding = reader.CurrentEncoding;
        }
        catch (DecoderFallbackException)
        {
            upload.Position = 0;
            return upload;
        }

        var preamble = sourceEncoding.GetPreamble();
        upload.Position = prefix.AsSpan(0, prefixLength).StartsWith(preamble) ? preamble.Length : 0;
        return Encoding.CreateTranscodingStream(upload, sourceEncoding, SofiaAnalysisHandler.DimpEncoding, leaveOpen: true);
    }

    private static async Task<string> ReadTextAsync(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(file.OpenReadStream(), StrictUtf8);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.Latin1);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}
