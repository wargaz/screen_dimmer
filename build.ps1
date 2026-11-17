# Bygger Dimma.exe med C#-kompilatorn som redan finns i Windows (.NET Framework 4.8).
# Inget behöver installeras.
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$src = Join-Path $PSScriptRoot 'src'
$out = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $out | Out-Null

$files = Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName }
& $csc /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 `
    "/out:$out\Dimma.exe" "/win32manifest:$src\app.manifest" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll $files
if ($LASTEXITCODE -ne 0) { throw "Bygget misslyckades" }
Write-Host "Klart: $out\Dimma.exe"
