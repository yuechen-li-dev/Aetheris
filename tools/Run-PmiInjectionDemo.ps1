$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "..\demos\Aetheris.PmiInjectionDemo\Run-PmiInjectionDemo.ps1"
& $scriptPath @args
exit $LASTEXITCODE
