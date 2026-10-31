$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$partnerRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspace = New-ZjcsValidationWorkspace -Name 'partner-gifts'
$previousWorkspace = $env:ZJCS_VALIDATION_DIR
$previousRepository = $env:ZJCS_REPOSITORY_ROOT
try {
    $env:ZJCS_VALIDATION_DIR = $workspace
    $env:ZJCS_REPOSITORY_ROOT = $partnerRepository
    Push-Location $partnerRepository
    try {
        python -B scripts/test_partner_gift_rules.py
        if ($LASTEXITCODE -ne 0) { throw 'Partner gift rule tests failed.' }
        python -B scripts/prepare_partner_gift_validation.py
        if ($LASTEXITCODE -ne 0) { throw 'Partner validation preparation failed.' }
        $log = Join-Path $workspace 'editor.log'
        $editor = (Get-Content unity-tools.json -Raw | ConvertFrom-Json).editorExecutable
        $process = Start-UnityOnInternalDisplay -Editor $editor -ArgumentList @(
            '-batchmode', '-projectPath', ('"' + (Join-Path $workspace 'unity') + '"'),
            '-executeMethod', 'PartnerGiftTransactionValidation.Run', '-logFile', ('"' + $log + '"'))
        if (-not $process.WaitForExit(180000)) {
            # This invocation owns this process and workspace.
            Stop-Process -Id $process.Id -Force
            throw 'Partner validation Editor did not exit within three minutes.'
        }
        $result = Get-Content -LiteralPath $log -Raw
        if ($process.ExitCode -ne 0 -or $result -notmatch 'PARTNER_GIFT_TRANSACTIONS_PASSED') {
            Write-Output (($result -split "`n" | Select-String -Pattern 'Exception:|error CS|at Partner|Failed' -Context 0,3) -join "`n")
            throw 'Partner gift transaction validation failed.'
        }
        Write-Output 'Partner gift/roster rules, Unity compilation and isolated save transactions passed.'
    } finally { Pop-Location }
} finally {
    $env:ZJCS_VALIDATION_DIR = $previousWorkspace
    $env:ZJCS_REPOSITORY_ROOT = $previousRepository
    # Unity's helper processes can briefly retain the project working directory
    # after the Editor exits. Every attempt repeats the repository ownership checks.
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        try { Remove-ZjcsValidationWorkspace -Path $workspace; break }
        catch [System.IO.IOException] {
            if ($attempt -eq 9) { throw }
            Start-Sleep -Seconds 2
        }
    }
}
