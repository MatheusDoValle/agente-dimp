[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$target = Join-Path $PSScriptRoot '.env'
if (Test-Path -LiteralPath $target) { throw 'infra/.env ja existe; nao sera sobrescrito.' }
$random = [Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $lines = foreach ($name in @('POSTGRES_BOOTSTRAP_PASSWORD', 'AGENT_DB_PASSWORD', 'RAG_DB_PASSWORD', 'MONGO_BOOTSTRAP_PASSWORD', 'AGENT_MEMORY_PASSWORD')) {
        $bytes = New-Object byte[] 32
        $random.GetBytes($bytes)
        $name + '=' + ([BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant())
    }
    [IO.File]::WriteAllLines($target, $lines, (New-Object Text.UTF8Encoding($false)))
} finally {
    $random.Dispose()
}
Write-Output 'infra/.env criado com credenciais locais aleatorias; valores nao exibidos.'
