[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$IncludeNugetCache
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

$targets = @(
    (Join-Path $repoRoot ".dotnet")
    (Join-Path $repoRoot ".dotnet_cli")
    (Join-Path $repoRoot ".sfdx")
    (Join-Path $repoRoot "artifacts")
)

$searchRoots = @(
    (Join-Path $repoRoot "src")
    (Join-Path $repoRoot "tests")
)

foreach ($root in $searchRoots) {
    if (Test-Path -LiteralPath $root) {
        $targets += Get-ChildItem -LiteralPath $root -Directory -Recurse -Force |
            Where-Object { $_.Name -in @("bin", "obj") } |
            Select-Object -ExpandProperty FullName
    }
}

if ($IncludeNugetCache) {
    $targets += (Join-Path $repoRoot ".nuget")
}

$existingTargets = $targets |
    Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
    Sort-Object -Unique

if (-not $existingTargets) {
    Write-Host "Nenhum diretorio de limpeza encontrado."
    exit 0
}

foreach ($target in $existingTargets) {
    if ($PSCmdlet.ShouldProcess($target, "Remover diretorio")) {
        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host "Removido: $target"
    }
}

Write-Host "Limpeza concluida. Itens removidos: $($existingTargets.Count)"
