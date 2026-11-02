param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts\win-x64' }
Push-Location $root
try {
    dotnet restore TBRKLabelStudio.sln
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    dotnet build TBRKLabelStudio.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet test tests/EtiketStudio.Tests -c Release --no-build --logger 'trx;LogFileName=core-tests.trx' --results-directory test-results
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
    dotnet run --project tools/EtiketStudio.Fixtures -c Release --no-build -- examples
    if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed.' }
    dotnet publish src/EtiketStudio.Windows/EtiketStudio.Windows.csproj -c Release -r win-x64 --self-contained true -m:1 -p:UseSharedCompilation=false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugType=None -o $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    $testScripts = Join-Path $OutputDirectory 'scripts'
    New-Item -ItemType Directory -Path $testScripts -Force | Out-Null
    Copy-Item scripts\Test-Windows.ps1,scripts\Test-Printer.ps1 $testScripts -Force
    Copy-Item README.md $OutputDirectory -Force
    Get-FileHash (Join-Path $OutputDirectory 'TBRKLabelStudio.exe') -Algorithm SHA256 | Format-List
    Write-Host "Published to $OutputDirectory. Run Test-Windows.ps1, then perform physical acceptance."
}
finally { Pop-Location }
