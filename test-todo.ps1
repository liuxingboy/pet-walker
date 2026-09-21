$ErrorActionPreference = 'Stop'
$petCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $petCompiler /nologo /target:exe /resource:"$PSScriptRoot\icons\app.ico,HydrangeaWalker.Icon" /main:TodoTests /out:"$PSScriptRoot\TodoTests.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Security.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\PetWalker.cs" "$PSScriptRoot\Todo.cs" "$PSScriptRoot\Reminder.cs" "$PSScriptRoot\RightWalkRig.cs" "$PSScriptRoot\TodoTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Todo test compilation failed' }
& "$PSScriptRoot\TodoTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Todo tests failed' }
