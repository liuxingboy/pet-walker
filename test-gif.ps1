$ErrorActionPreference = 'Stop'
$petCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $petCompiler /nologo /target:exe /platform:x64 /utf8output /out:"$PSScriptRoot\GifPlaybackTests.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\GifPlaybackTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'GIF test compilation failed' }
& "$PSScriptRoot\GifPlaybackTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'GIF playback tests failed' }
