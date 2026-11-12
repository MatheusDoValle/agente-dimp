[CmdletBinding()]
param(
    [ValidateSet('calibration', 'acceptance')]
    [string]$Split = 'calibration',
    [ValidateSet('text-embedding-3-small-1536', 'local-fake-sha256-v1')]
    [string]$EmbeddingProfileId = 'text-embedding-3-small-1536',
    [uri]$BaseUrl = 'http://127.0.0.1:5081',
    [Nullable[double]]$MaximumCosineDistance
)

$ErrorActionPreference = 'Stop'
if (-not $BaseUrl.IsLoopback -or $BaseUrl.Scheme -notin @('http', 'https')) {
    throw 'Use a URL local da Api.RAG.'
}
if ($Split -eq 'acceptance' -and
    ($null -eq $MaximumCosineDistance -or -not [double]::IsFinite($MaximumCosineDistance) -or
        $MaximumCosineDistance -lt 0 -or $MaximumCosineDistance -gt 2)) {
    throw 'Para aceite, informe a distância máxima de cosseno configurada na API.'
}

$fixture = Join-Path $PSScriptRoot '../../tests/fixtures/rag/evaluation.json'
$settings = (Get-Content -LiteralPath $fixture -Raw | ConvertFrom-Json).semantic
$endpoint = $BaseUrl.GetLeftPart([UriPartial]::Authority) + '/mcp'
function Invoke-RagTool($Id, $Name, $Arguments) {
    $request = @{
        jsonrpc = '2.0'
        id = $Id
        method = 'tools/call'
        params = @{ name = $Name; arguments = $Arguments }
    }
    $body = [Text.Encoding]::UTF8.GetBytes(($request | ConvertTo-Json -Depth 6 -Compress))
    $response = Invoke-RestMethod $endpoint -Method Post -ContentType 'application/json; charset=utf-8' -Body $body -TimeoutSec 45
    if ($response.error -or -not $response.result.content[0].text) {
        throw "Resposta MCP inválida para $Id."
    }

    $response.result.content[0].text | ConvertFrom-Json
}

$results = foreach ($item in $settings.$Split) {
    $revision = if ($item.revision) { $item.revision } else { $settings.revision }
    $profile = if ($item.profile) { $item.profile } else { $EmbeddingProfileId }
    $result = Invoke-RagTool $item.id 'search_manual' @{
        query = $item.query
        layoutVersion = $settings.layoutVersion
        documentRevisionSha256 = $revision
        embeddingProfileId = $profile
    }
    $hits = @($result.hits | Where-Object { $null -ne $_ })
    $invalidHits = @($hits | Where-Object {
        $_.documentRevisionSha256 -cne $revision -or $_.embeddingProfileId -cne $profile -or
        $_.layoutVersion -cne $settings.layoutVersion -or @($_.pages).Count -eq 0 -or
        ($_.matchType -and $_.matchType -notin @('lexical', 'semantic')) -or
        ($Split -eq 'acceptance' -and $_.matchType -ne 'lexical' -and $_.rankingMetric.value -gt $MaximumCosineDistance)
    })
    $requiredMatches = @(foreach ($evidence in $item.evidence) {
        $match = $hits | Where-Object {
            $evidence.page -in $_.pages -and
            (-not $evidence.matchType -or $_.matchType -eq $evidence.matchType) -and
            ($_.text -replace '\s+', ' ').IndexOf($evidence.keyword, [StringComparison]::OrdinalIgnoreCase) -ge 0
        } | Select-Object -First 1
        if ($match) {
            $citation = Invoke-RagTool "$($item.id)-$($match.chunkId)" 'get_manual_chunk' @{
                chunkId = $match.chunkId
                documentRevisionSha256 = $revision
            }
            if ($citation.chunk.chunkId -eq $match.chunkId -and
                $citation.chunk.documentRevisionSha256 -ceq $revision -and
                $evidence.page -in $citation.chunk.pages -and $citation.chunk.text -ceq $match.text) {
                $match
            }
        }
    })
    $semanticDistances = @($requiredMatches | Where-Object matchType -NE 'lexical' |
        ForEach-Object { [double]$_.rankingMetric.value })
    $requiredCount = $requiredMatches.Count
    $positive = $item.evidence.Count -gt 0
    $passed = if ($item.expectedError) {
        $result.error.code -eq $item.expectedError -and $hits.Count -eq 0
    } elseif ($positive) {
        -not $result.error -and $requiredCount -eq $item.evidence.Count
    } else {
        -not $result.error -and $hits.Count -eq 0 -and 'NO_EVIDENCE_FOUND' -in $result.limitationCodes
    }

    [pscustomobject]@{
        Id = $item.id
        Passed = $passed -and $invalidHits.Count -eq 0 -and $hits.Count -le $settings.topK
        RequiredEvidence = $item.evidence.Count
        FoundEvidence = $requiredCount
        RequiredLexicalMatches = @($requiredMatches | Where-Object matchType -EQ 'lexical').Count
        MaxRequiredSemanticDistance = if ($semanticDistances.Count -gt 0) { ($semanticDistances | Measure-Object -Maximum).Maximum }
        NearestDistance = if ($hits.Count -gt 0) { $hits[0].rankingMetric.value }
        Pages = @($hits | ForEach-Object { $_.pages } | Sort-Object -Unique)
        Error = $result.error.code
        Limitations = $result.limitationCodes
    }
}

$passedCount = @($results | Where-Object Passed).Count
$gatePassed = $Split -eq 'acceptance' -and $EmbeddingProfileId -eq 'text-embedding-3-small-1536' -and
    $MaximumCosineDistance -lt 2 -and $passedCount -eq @($results).Count
$suggestedDistance = $null
if ($Split -eq 'calibration') {
    $positives = @($results | Where-Object { $_.RequiredEvidence -gt 0 })
    $negativeDistances = @($results | Where-Object { $_.RequiredEvidence -eq 0 -and $null -ne $_.NearestDistance } |
        ForEach-Object { $_.NearestDistance })
    $semanticPositives = @($positives | Where-Object { $null -ne $_.MaxRequiredSemanticDistance })
    if ($semanticPositives.Count -gt 0 -and @($positives | Where-Object { -not $_.Passed }).Count -eq 0 -and
        $negativeDistances.Count -gt 0) {
        $lower = ($semanticPositives | Measure-Object MaxRequiredSemanticDistance -Maximum).Maximum
        $upper = ($negativeDistances | Measure-Object -Minimum).Minimum
        if ($lower -lt $upper) { $suggestedDistance = ($lower + $upper) / 2 }
    }
}

[pscustomobject]@{
    Split = $Split
    Profile = $EmbeddingProfileId
    Revision = $settings.revision
    Passed = $passedCount
    Total = @($results).Count
    SemanticGatePassed = $gatePassed
    SuggestedMaximumCosineDistance = $suggestedDistance
    Results = $results
} | ConvertTo-Json -Depth 6
