<#
  Builds dist\KnockKnock.exe: a single file with the React UI and the WebView2 SDK embedded.
  Needs only Windows (its built-in .NET Framework 4.8 C# compiler) and Node.js for the UI.

    .\build.ps1             # full build
    .\build.ps1 -SkipUi     # reuse the last UI build
#>
param([switch]$SkipUi)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$version = (Get-Content "$root\VERSION" -Raw).Trim()
$webview2 = '1.0.4258.31'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }

Write-Host "Knock Knock $version" -ForegroundColor Cyan

# 1. WebView2 SDK (downloaded once from nuget.org into lib\)
$lib = "$root\lib\webview2\$webview2"
if (-not (Test-Path "$lib\Microsoft.Web.WebView2.Core.dll")) {
    Write-Host "Downloading WebView2 SDK $webview2..."
    New-Item -ItemType Directory -Force $lib | Out-Null
    $zip = "$lib\pkg.zip"
    Invoke-WebRequest "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$webview2" -OutFile $zip -UseBasicParsing
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $z = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        foreach ($e in $z.Entries) {
            $target = $null
            if ($e.FullName -match '^lib/net462/(Microsoft\.Web\.WebView2\.(Core|WinForms)\.dll)$') { $target = "$lib\$($Matches[1])" }
            elseif ($e.FullName -match '^runtimes/(win-(x64|x86|arm64))/native/WebView2Loader\.dll$') {
                New-Item -ItemType Directory -Force "$lib\runtimes\$($Matches[1])" | Out-Null
                $target = "$lib\runtimes\$($Matches[1])\WebView2Loader.dll"
            }
            if ($target) { [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $target, $true) }
        }
    } finally { $z.Dispose() }
    Remove-Item $zip
}

# 2. React UI -> ui\dist\index.html (one self-contained file)
if (-not $SkipUi) {
    Push-Location "$root\ui"
    try {
        if (-not (Test-Path node_modules)) { npm ci --no-audit --no-fund; if ($LASTEXITCODE) { throw 'npm ci failed' } }
        npm run build --silent; if ($LASTEXITCODE) { throw 'UI build failed' }
    } finally { Pop-Location }
}
if (-not (Test-Path "$root\ui\dist\index.html")) { throw 'ui\dist\index.html is missing; run without -SkipUi' }

# 3. Version info
New-Item -ItemType Directory -Force "$root\obj", "$root\dist" | Out-Null
@"
using System.Reflection;
[assembly: AssemblyTitle("Knock Knock")]
[assembly: AssemblyProduct("Knock Knock")]
[assembly: AssemblyDescription("Knock on your desk to take screenshots and run actions")]
[assembly: AssemblyCopyright("Copyright (c) $((Get-Date).Year) Knock Knock contributors. MIT License.")]
[assembly: AssemblyVersion("$version.0")]
[assembly: AssemblyFileVersion("$version.0")]
[assembly: AssemblyInformationalVersion("$version")]
"@ | Set-Content "$root\obj\AssemblyInfo.cs" -Encoding utf8

# 4. Compile
$sources = @(Get-ChildItem "$root\src\*.cs" | ForEach-Object FullName) + "$root\obj\AssemblyInfo.cs"
$refs = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll',
          "$lib\Microsoft.Web.WebView2.Core.dll", "$lib\Microsoft.Web.WebView2.WinForms.dll") | ForEach-Object { "/r:$_" }
$resources = @(
    "/resource:$root\ui\dist\index.html,ui.html",
    "/resource:$lib\Microsoft.Web.WebView2.Core.dll,Microsoft.Web.WebView2.Core.dll",
    "/resource:$lib\Microsoft.Web.WebView2.WinForms.dll,Microsoft.Web.WebView2.WinForms.dll"
) + (@('win-x64', 'win-x86', 'win-arm64') | ForEach-Object { "/resource:$lib\runtimes\$_\WebView2Loader.dll,WebView2Loader.$_.dll" })
$common = @('/nologo', '/optimize+', '/platform:anycpu', '/warn:4', '/nowarn:1591') + $refs

& $csc @common /target:winexe "/win32icon:$root\assets\knockknock.ico" "/win32manifest:$root\assets\app.manifest" `
    "/out:$root\dist\KnockKnock.exe" @resources @sources
if ($LASTEXITCODE) { throw 'Compile failed' }


$exe = Get-Item "$root\dist\KnockKnock.exe"
$hash = (Get-FileHash $exe.FullName -Algorithm SHA256).Hash.ToLower()
"$hash  KnockKnock.exe" | Set-Content "$root\dist\KnockKnock.exe.sha256" -Encoding ascii
Write-Host ("Built dist\KnockKnock.exe ({0:N0} KB)  sha256 {1}" -f ($exe.Length / 1KB), $hash) -ForegroundColor Green
