$ErrorActionPreference = 'Stop'
$petCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $petCompiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /out:"$PSScriptRoot\HydrangeaWalker.exe" /win32icon:"$PSScriptRoot\icons\app.ico" /resource:"$PSScriptRoot\icons\app.ico,HydrangeaWalker.Icon" /reference:System.Security.dll /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\PetWalker.cs" "$PSScriptRoot\Reminder.cs" "$PSScriptRoot\Todo.cs" "$PSScriptRoot\RightWalkRig.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
