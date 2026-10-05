<#
  Runs Knock Knock straight from the source code instead of the .exe.

  Use this if Windows Smart App Control blocks the unsigned KnockKnock.exe: Windows' own PowerShell
  is trusted, and it compiles the same code in memory when it starts. Run .\build.ps1 once first
  (it downloads the WebView2 files and builds the UI), then:

    powershell -ExecutionPolicy Bypass -File .\run-from-source.ps1

  Turning on "Start with Windows" from here makes Windows start it this way at sign-in.
#>
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$wv = Get-ChildItem "$root\lib\webview2" -Directory -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
if (-not $wv -or -not (Test-Path "$root\ui\dist\index.html")) { throw 'Run .\build.ps1 once first.' }

$core = "$($wv.FullName)\Microsoft.Web.WebView2.Core.dll"
$winforms = "$($wv.FullName)\Microsoft.Web.WebView2.WinForms.dll"
Add-Type -Path $core, $winforms
Add-Type -Path (Get-ChildItem "$root\src\*.cs").FullName -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.Web.Extensions, System.Core, $core, $winforms

[KnockKnock.Program]::ResourceDir = $wv.FullName
[KnockKnock.Program]::UiFile = "$root\ui\dist\index.html"
[KnockKnock.Program]::Version = (Get-Content "$root\VERSION" -Raw).Trim()
[KnockKnock.Program]::StartCommand = 'powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $PSCommandPath + '"'
[KnockKnock.Program]::Main([string[]]$args)
