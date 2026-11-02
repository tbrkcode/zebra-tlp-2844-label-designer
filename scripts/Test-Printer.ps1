[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
param(
    [Parameter(Mandatory=$true)][string]$PrinterName,
    [string]$Exe = (Join-Path (Split-Path -Parent $PSScriptRoot) 'TBRKLabelStudio.exe'),
    [string]$EplFile = (Join-Path (Split-Path -Parent $PSScriptRoot) 'examples\epl2\01-native-code128-50x30.epl'),
    [string]$OutputDirectory = (Join-Path $env:TEMP 'EtiketStudio-hardware-test'),
    [switch]$Print
)
$ErrorActionPreference = 'Stop'
if (-not $Print) { throw 'No output sent. Add -Print to request a real print; use -WhatIf to review.' }
$Exe = (Resolve-Path $Exe).Path
$EplFile = (Resolve-Path $EplFile).Path
$printer = Get-Printer -Name $PrinterName
if ($printer.DriverName -notmatch 'TLP\s*2844\b' -or ($printer.DriverName + ' ' + $printer.Name) -match '2844\s*-?\s*Z\b|ZPL') { throw 'Select an installed TLP 2844 EPL driver, not a 2844-Z/ZPL driver.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$result = Join-Path ([System.IO.Path]::GetFullPath($OutputDirectory)) ('submission-' + [guid]::NewGuid().ToString('N') + '.json')
if ($PSCmdlet.ShouldProcess($PrinterName, "Send actual binary EPL2 file $EplFile")) {
    $arguments = @('--send-epl', ('"' + $EplFile + '"'), '--printer', ('"' + $PrinterName + '"'), '--result', ('"' + $result + '"'), '--confirm-print')
    $process = Start-Process -FilePath $Exe -ArgumentList $arguments -Wait -PassThru
    Get-Content $result
    if ($process.ExitCode -ne 0) { throw 'Submission failed or its physical outcome is uncertain. Check the queue before retrying.' }
    Write-Host 'Windows accepted the job. This is NOT confirmation of physical printing.'
    Write-Host 'Read the label and scan the barcode. Record actual results in docs/HARDWARE_ACCEPTANCE.md.'
}
