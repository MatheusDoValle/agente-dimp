[CmdletBinding()]
param([switch]$All, [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$envPath = Join-Path $PSScriptRoot '.env'
if (-not (Test-Path -LiteralPath $envPath)) { throw 'Gerar infra/.env e iniciar o Compose antes dos testes.' }
$values = @{}
foreach ($line in [IO.File]::ReadAllLines($envPath)) {
    if ($line -match '^([A-Z_]+)=([a-f0-9]{64})$') { $values[$matches[1]] = $matches[2] }
}
foreach ($name in @('AGENT_DB_PASSWORD', 'RAG_DB_PASSWORD')) {
    if (-not $values.ContainsKey($name)) { throw 'Credencial local ausente ou formato inesperado.' }
}
$variables = @('DIMP_TEST_MODE', 'DIMP_TEST_AGENT_CONNECTION', 'DIMP_TEST_RAG_CONNECTION')
$previous = @{}
foreach ($name in $variables) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    [Environment]::SetEnvironmentVariable('DIMP_TEST_MODE', 'compose-local', 'Process')
    [Environment]::SetEnvironmentVariable('DIMP_TEST_AGENT_CONNECTION', 'Host=127.0.0.1;Port=55432;Database=agente;Username=agente_app;Password=' + $values['AGENT_DB_PASSWORD'], 'Process')
    [Environment]::SetEnvironmentVariable('DIMP_TEST_RAG_CONNECTION', 'Host=127.0.0.1;Port=55432;Database=rag;Username=rag_app;Password=' + $values['RAG_DB_PASSWORD'], 'Process')
    $filter = 'Category=Database'
    if ($All) { $filter = 'Category!=LiveProvider' }
    $arguments = @('test', (Join-Path $root 'AgenteDIMP.slnx'), '--filter', $filter, '--verbosity', 'minimal',
        '--logger', 'trx;LogFilePrefix=phase2', '--results-directory', (Join-Path $root 'artifacts/test-results'))
    if ($NoBuild) { $arguments += '--no-build' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes de fundacao; conferir resultados TRX locais.' }
} finally {
    foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
