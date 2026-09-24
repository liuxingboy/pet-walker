$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$petDist = Join-Path $PSScriptRoot 'dist'
$petStage = Join-Path $petDist ('stage-' + [Guid]::NewGuid().ToString('N'))
$petRelease = Join-Path $petStage 'HydrangeaWalker'
try {
    New-Item -ItemType Directory -Path (Join-Path $petRelease 'assets\local-ayaka-2d\gifs') -Force | Out-Null
    foreach ($petFile in @('HydrangeaWalker.exe', 'README.md')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $petFile) -Destination $petRelease
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'icons\app.ico') -Destination $petRelease
    foreach ($petGif in @('idle','running-left','running-right','waving','jumping','failed','waiting','running','review')) {
        $petName = $petGif + '.gif'
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "assets\local-ayaka-2d\gifs\$petName") -Destination (Join-Path $petRelease 'assets\local-ayaka-2d\gifs')
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\local-ayaka-2d\CREDITS.txt') -Destination (Join-Path $petRelease 'assets\local-ayaka-2d')
    $petZip = Join-Path $petDist 'HydrangeaWalker-portable.zip'
    Compress-Archive -LiteralPath $petRelease -DestinationPath $petZip -Force
    Get-FileHash -LiteralPath $petZip -Algorithm SHA256
} finally {
    $petResolvedStage = [IO.Path]::GetFullPath($petStage)
    $petResolvedDist = [IO.Path]::GetFullPath($petDist).TrimEnd('\') + '\'
    if (-not $petResolvedStage.StartsWith($petResolvedDist, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe staging path' }
    if (Test-Path -LiteralPath $petResolvedStage) { Remove-Item -LiteralPath $petResolvedStage -Recurse -Force }
}
