$ErrorActionPreference = 'Stop'
$src = Join-Path $PSScriptRoot 'src/ClementiaPet'
$outDir = Join-Path $PSScriptRoot 'dist'
$null = New-Item -ItemType Directory -Force -Path $outDir
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources = @(Get-ChildItem -LiteralPath $src -Filter '*.cs' | ForEach-Object FullName)
$resources = @('Relax.png','Move.png','Interact.png','Lift.png','RayMotion.png','RayTiming.txt','ray.png') | ForEach-Object { "/resource:$src/Assets/$_,$_" }
$content = Join-Path $PSScriptRoot 'content/zh-CN.json'
$resources += "/resource:$content,zh-CN.json"
$resources += "/resource:$PSScriptRoot/content/conversation.zh-CN.json,conversation.zh-CN.json"
& $compiler /nologo /utf8output /codepage:65001 /optimize+ /target:winexe /platform:x64 "/win32manifest:$src/app.manifest" "/out:$outDir/ClementiaPet.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll @resources @sources
if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed.' }
Get-Item -LiteralPath (Join-Path $outDir 'ClementiaPet.exe') | Select-Object FullName,Length
