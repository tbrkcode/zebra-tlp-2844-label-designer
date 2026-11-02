param([string]$Exe = (Join-Path (Split-Path -Parent $PSScriptRoot) 'TBRKLabelStudio.exe'), [string]$OutputDirectory = (Join-Path $env:TEMP 'EtiketStudio-Windows-tests'))
$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$process = Start-Process -FilePath $Exe -ArgumentList @('--self-test', '--output', ('"' + $OutputDirectory + '"')) -Wait -PassThru
Get-Content (Join-Path $OutputDirectory 'result.txt')
if ($process.ExitCode -ne 0) { throw 'Windows rendering smoke test failed.' }
Write-Host "Inspect the PNGs in $OutputDirectory. No printer was accessed by this test."
