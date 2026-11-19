param([switch]$Desktop)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$src = Join-Path $root 'src/ClementiaPet'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$resource = Join-Path $root 'content/conversation.zh-CN.json'
$resources = @("/resource:$resource,conversation.zh-CN.json")
if ($Desktop) {
    $suite = 'ChatDesktopTests'
    $sources = @(Get-ChildItem -LiteralPath $src -Filter '*.cs' | ForEach-Object FullName)
    $resources += @(Get-ChildItem -LiteralPath (Join-Path $src 'Assets') -File | ForEach-Object { "/resource:$($_.FullName),$($_.Name)" })
    $baseContent = Join-Path $root 'content/zh-CN.json'
    $resources += "/resource:$baseContent,zh-CN.json"
} else {
    $suite = 'SpeechContractTests'
    $sources = @('Conversation.cs','PetSettings.cs','SpeechScheduler.cs') | ForEach-Object { Join-Path $src $_ }
}
$probe = Join-Path $env:TEMP ("Clementia-" + $suite + "-" + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /utf8output /codepage:65001 /target:exe /platform:x64 "/main:$suite" "/out:$probe" "/win32manifest:$src/app.manifest" /r:System.Runtime.Serialization.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll @resources @sources (Join-Path $PSScriptRoot "$suite.cs")
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
    & $probe
    if ($LASTEXITCODE -ne 0) { throw "$suite failed ($LASTEXITCODE)" }
} finally {
    if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe }
}
