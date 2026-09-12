$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$petDist = Join-Path $PSScriptRoot 'dist'
$petStage = Join-Path $petDist ('stage-' + [Guid]::NewGuid().ToString('N'))
$petRelease = Join-Path $petStage 'HydrangeaWalker'
try {
    New-Item -ItemType Directory -Path (Join-Path $petRelease 'assets\rig') -Force | Out-Null
    foreach ($petFile in @('HydrangeaWalker.exe', 'README.md')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $petFile) -Destination $petRelease
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'icons\app.ico') -Destination $petRelease
    foreach ($petAction in @(@('idle',6), @('wave',4), @('walk-left',8), @('walk-right',8))) {
        for ($petIndex = 0; $petIndex -lt $petAction[1]; $petIndex++) {
            $petName = '{0}-{1}.png' -f $petAction[0], $petIndex
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot "assets\$petName") -Destination (Join-Path $petRelease 'assets')
        }
    }
    foreach ($petFile in @('body.png', 'near-leg.png', 'far-leg.png', 'rig.json')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "assets\rig\$petFile") -Destination (Join-Path $petRelease 'assets\rig')
    }
    $petZip = Join-Path $petDist 'HydrangeaWalker-portable.zip'
    Compress-Archive -LiteralPath $petRelease -DestinationPath $petZip -Force
    Get-FileHash -LiteralPath $petZip -Algorithm SHA256
} finally {
    $petResolvedStage = [IO.Path]::GetFullPath($petStage)
    $petResolvedDist = [IO.Path]::GetFullPath($petDist).TrimEnd('\') + '\'
    if (-not $petResolvedStage.StartsWith($petResolvedDist, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe staging path' }
    if (Test-Path -LiteralPath $petResolvedStage) { Remove-Item -LiteralPath $petResolvedStage -Recurse -Force }
}
