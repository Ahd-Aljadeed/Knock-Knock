# Regenerates assets\knockknock.ico (and a preview PNG) from src\AppIcon.cs.
$root = Split-Path $PSScriptRoot
Add-Type -Path "$root\src\AppIcon.cs" -ReferencedAssemblies System.Drawing
[KnockKnock.AppIcon]::WriteIco("$root\assets\knockknock.ico")
$bmp = [KnockKnock.AppIcon]::Draw(256, $true)
$bmp.Save("$root\assets\icon-256.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Wrote assets\knockknock.ico and assets\icon-256.png"
