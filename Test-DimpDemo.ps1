param(
    [uri] $BaseUrl = 'https://agente-dimp-demo.vercel.app',
    [switch] $UsarTokenLocal,
    [ValidateNotNullOrEmpty()]
    [string] $ArquivoDimp,
    [switch] $SelecionarDimp,
    [ValidateNotNullOrEmpty()]
    [string] $Pergunta
)

$ErrorActionPreference = 'Stop'
$localDemo = 'http://127.0.0.1:5098'
$enviarArquivo = $SelecionarDimp -or $PSBoundParameters.ContainsKey('ArquivoDimp')
if ($Pergunta -and -not $enviarArquivo) { throw 'Selecione um arquivo DIMP para perguntar sobre ele.' }
if ($enviarArquivo) {
    if ($PSBoundParameters.ContainsKey('BaseUrl') -and $BaseUrl.GetLeftPart([UriPartial]::Authority) -ne $localDemo) {
        throw 'O envio de arquivos proprios usa apenas a API local na porta 5098.'
    }
    $BaseUrl = $localDemo
    $UsarTokenLocal = $true
}
if ($BaseUrl.Scheme -ne 'https' -and -not ($BaseUrl.Scheme -eq 'http' -and $BaseUrl.IsLoopback)) {
    throw 'Use HTTPS ou um endereco HTTP local para testar.'
}
$base = $BaseUrl.GetLeftPart([UriPartial]::Authority)
$rpcUrl = "$base/a2a"

# 1. Resolver token
$token = $env:SOFIA_A2A_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) { $token = $env:ClassroomDemo__Token }
if ([string]::IsNullOrWhiteSpace($token) -and $UsarTokenLocal) {
    $tokenPath = Join-Path $PSScriptRoot '.local/classroom-demo.token.dpapi'
    if (Test-Path $tokenPath) {
        try {
            $secureToken = Get-Content -LiteralPath $tokenPath -Raw | ConvertTo-SecureString
            $token = [System.Net.NetworkCredential]::new('', $secureToken).Password
        } catch { }
    }
}
$publicDemo = 'https://agente-dimp-demo.vercel.app'
if ([string]::IsNullOrWhiteSpace($token) -and ($base -eq $publicDemo -or $base -eq $localDemo)) {
    $secrets = Join-Path $env:APPDATA 'Microsoft/UserSecrets/agentedimp-api-agente-local/secrets.json'
    if (Test-Path -LiteralPath $secrets) {
        $configuration = Get-Content -LiteralPath $secrets -Raw | ConvertFrom-Json
        $token = $configuration.SOFIA_A2A_TOKEN
    }
}
if ([string]::IsNullOrWhiteSpace($token)) {
    throw 'Configure SOFIA_A2A_TOKEN antes de testar a demo.'
}

$headers = @{ Authorization = "Bearer $token" }

# 2. Validar Health Check
$health = Invoke-RestMethod -Uri "$base/health" -MaximumRedirection 0 -TimeoutSec 30
if ($health.status -ne 'alive') {
    throw 'A API nao confirmou o status alive.'
}
Write-Host "[OK] Health check: $($health.status) (service: $($health.service), profile: $($health.profile))" -ForegroundColor Green

# 3. Validar Swagger público
$swagger = try {
    (Invoke-WebRequest -Uri "$base/swagger/v1/swagger.json" -Method Get `
        -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 30).StatusCode
} catch {
    if ($null -eq $_.Exception.Response) { throw }
    [int] $_.Exception.Response.StatusCode
}
if ($swagger -eq 200) {
    Write-Host "[OK] Swagger OpenAPI v1 disponivel publicamente em $base/swagger" -ForegroundColor Green
} else {
    Write-Warning "Swagger retornou status $swagger em $base/swagger"
}

# 4. Validar protecao Bearer no Agent Card (sem credencial e com credencial invalida)
$cardSemTokenStatus = try {
    (Invoke-WebRequest -Uri "$base/.well-known/agent-card.json" -Method Get `
        -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 30).StatusCode
} catch {
    if ($null -eq $_.Exception.Response) { throw }
    [int] $_.Exception.Response.StatusCode
}
if ($cardSemTokenStatus -ne 401) { throw "Esperava HTTP 401 no Agent Card sem credencial. Recebido: $cardSemTokenStatus." }
Write-Host "[OK] Agent Card sem credencial rejeitado com HTTP 401" -ForegroundColor Green

$cardTokenInvalidoStatus = try {
    (Invoke-WebRequest -Uri "$base/.well-known/agent-card.json" -Method Get `
        -Headers @{ Authorization = "Bearer token-invalido" } `
        -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 30).StatusCode
} catch {
    if ($null -eq $_.Exception.Response) { throw }
    [int] $_.Exception.Response.StatusCode
}
if ($cardTokenInvalidoStatus -ne 401) { throw "Esperava HTTP 401 no Agent Card com credencial invalida. Recebido: $cardTokenInvalidoStatus." }
Write-Host "[OK] Agent Card com credencial invalida rejeitado com HTTP 401" -ForegroundColor Green

# 5. Validar Agent Card autenticado
$card = Invoke-RestMethod -Uri "$base/.well-known/agent-card.json" -Headers $headers -MaximumRedirection 0 -TimeoutSec 30
if ($card.protocolVersion -ne '0.3.0' -or $card.preferredTransport -ne 'JSONRPC' -or
    $card.skills.id -notcontains 'analisar_documentos' -or $null -eq $card.securitySchemes.sofiaService) {
    throw "Agent Card autenticado nao cumpre a especificacao A2A 0.3.0 da SOFIA."
}
Write-Host "[OK] Agent Card autenticado valido: $($card.name) (A2A $($card.protocolVersion) / $($card.preferredTransport))" -ForegroundColor Green

# 6. Validar protecao Bearer no endpoint RPC /a2a
if (-not $enviarArquivo) {
    foreach ($path in @('/a2a', '/a2a/')) {
        $status = try {
            (Invoke-WebRequest -Uri "$base$path" -Method Post -ContentType 'application/json' -Body '{}' `
                -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 30).StatusCode
        } catch {
            if ($null -eq $_.Exception.Response) { throw }
            [int] $_.Exception.Response.StatusCode
        }
        if ($status -ne 401) { throw "Esperava HTTP 401 em $path sem credencial. Recebido: $status." }
    }
    Write-Host "[OK] Endpoint RPC /a2a sem credencial rejeitado com HTTP 401" -ForegroundColor Green
}

# 7. Executar chamada message/send
if ($SelecionarDimp) {
    Add-Type -AssemblyName System.Windows.Forms
    $seletor = New-Object System.Windows.Forms.OpenFileDialog
    try {
        $seletor.Title = 'Escolha o DIMP para enviar ao agente local'
        $seletor.Filter = 'Arquivos DIMP (*.dimp;*.txt)|*.dimp;*.txt|Todos os arquivos (*.*)|*.*'
        if ($seletor.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
        $ArquivoDimp = $seletor.FileName
    } finally {
        $seletor.Dispose()
    }
}

if ($enviarArquivo) {
    $arquivo = Get-Item -LiteralPath $ArquivoDimp
    if ($arquivo.PSIsContainer -or $arquivo.Length -eq 0) { throw 'Selecione um arquivo DIMP com conteudo.' }
    if ($arquivo.Length -ge 1MB) { throw 'O arquivo excede o limite de 1 MiB da demo local.' }
    $nomeDocumento = $arquivo.Name
    try {
        $textoDocumento = [System.IO.File]::ReadAllText($arquivo.FullName, [System.Text.UTF8Encoding]::new($false, $true))
    } catch {
        if ($_.Exception.InnerException -isnot [System.Text.DecoderFallbackException]) { throw }
        $textoDocumento = [System.IO.File]::ReadAllText($arquivo.FullName, [System.Text.Encoding]::GetEncoding(28591))
    }
} else {
    $nomeDocumento = 'dimp-sintetico.dimp'
    $fixturePath = Join-Path $PSScriptRoot 'tests/fixtures/dimp-v10/zerado-sintetico.dimp'
    $fixture = [System.IO.File]::ReadAllText($fixturePath, [System.Text.Encoding]::GetEncoding(28591))
    if (-not $fixture.Contains('|9999|19|')) { throw 'Fixture sintetica inesperada.' }
    $textoDocumento = $fixture.Replace('|9999|19|', '|9999|0|')
}

$executionId = [Guid]::NewGuid().ToString()
$requestId = [Guid]::NewGuid().ToString()
$payload = @{
    versaoContrato = '1.0'
    idExecucao = $executionId
    idSolicitacao = $requestId
    usuario = @{ nome = 'Auditor de demonstracao' }
    pergunta = if ($Pergunta) { $Pergunta } else { 'Valide a estrutura e os registros do DIMP.' }
    documentos = @(@{ idDocumento = 'dimp-demo'; nome = $nomeDocumento; tipo = 'text/plain'; texto = $textoDocumento })
} | ConvertTo-Json -Depth 8 -Compress

$rpc = @{
    jsonrpc = '2.0'
    id = 'dimp-demo-send'
    method = 'message/send'
    params = @{ message = @{
        kind = 'message'
        role = 'user'
        messageId = [Guid]::NewGuid().ToString()
        parts = @(@{ kind = 'text'; text = $payload })
    } }
} | ConvertTo-Json -Depth 10 -Compress
$body = [System.Text.Encoding]::UTF8.GetBytes($rpc)

$response = Invoke-RestMethod -Uri $rpcUrl -Method Post -Headers $headers `
    -ContentType 'application/json; charset=utf-8' -Body $body `
    -MaximumRedirection 0 -TimeoutSec 45

if ($response.result.kind -ne 'task' -or $response.result.status.state -ne 'completed') {
    throw 'A resposta A2A nao trouxe uma tarefa concluida.'
}
$taskId = $response.result.id
$result = $response.result.artifacts[0].parts[0].text | ConvertFrom-Json
if ($result.versaoContrato -ne '1.0' -or $result.agente.idAgente -ne 'agente-dimp' -or
    $result.idExecucao -ne $executionId -or $result.idSolicitacao -ne $requestId) {
    throw 'A resposta do agente nao corresponde aos IDs da solicitacao.'
}
Write-Host "[OK] Chamada message/send concluida com sucesso (task: $taskId)" -ForegroundColor Green

# 8. Validar tasks/get
$rpcGet = @{
    jsonrpc = '2.0'
    id = 'dimp-demo-get'
    method = 'tasks/get'
    params = @{ id = $taskId }
} | ConvertTo-Json -Depth 6 -Compress

$responseGet = Invoke-RestMethod -Uri $rpcUrl -Method Post -Headers $headers `
    -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($rpcGet)) `
    -MaximumRedirection 0 -TimeoutSec 30

if ($responseGet.result.id -ne $taskId -or $responseGet.result.contextId -ne $response.result.contextId) {
    throw "tasks/get retornou tarefa inconsistente."
}
Write-Host "[OK] Chamada tasks/get recuperou a mesma tarefa com sucesso" -ForegroundColor Green

# 9. Validar tasks/cancel (espera 400 por ja estar completed)
$rpcCancel = @{
    jsonrpc = '2.0'
    id = 'dimp-demo-cancel'
    method = 'tasks/cancel'
    params = @{ id = $taskId }
} | ConvertTo-Json -Depth 6 -Compress

$cancelStatus = try {
    (Invoke-WebRequest -Uri $rpcUrl -Method Post -Headers $headers `
        -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($rpcCancel)) `
        -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 30).StatusCode
} catch {
    if ($null -eq $_.Exception.Response) { throw }
    [int] $_.Exception.Response.StatusCode
}
if ($cancelStatus -notin @(400, 404)) { throw "Esperava HTTP 400 ou 404 em tasks/cancel. Recebido: $cancelStatus." }
Write-Host "[OK] Chamada tasks/cancel rejeitada adequadamente (HTTP $cancelStatus)" -ForegroundColor Green

# 10. Validar achado esperado no DIMP sintetico
if (-not $enviarArquivo) {
    $finding = $result.achados | Where-Object {
        $_.idDocumentos -contains 'dimp-demo' -and $_.descricao -match 'DIMP10\.9999\.F02\.TABLE'
    } | Select-Object -First 1
    if ($result.status -ne 'concluido' -or $null -eq $finding) {
        throw 'A analise sintetica nao devolveu o achado esperado no registro 9999.'
    }
    Write-Host "[OK] Achado esperado no registro 9999 identificado com precisao" -ForegroundColor Green
}

Write-Host "`n=== RESULTADO DA ANALISE SOFIA V1 ===" -ForegroundColor Cyan
$result | ConvertTo-Json -Depth 10
