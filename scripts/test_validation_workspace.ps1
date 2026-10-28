$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')

function Assert-Workspace($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-Refused([scriptblock]$Action) {
    $refused = $false
    try { & $Action } catch { $refused = $true }
    Assert-Workspace $refused 'Unsafe cleanup was not refused.'
}

$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Assert-Refused { Remove-ZjcsValidationWorkspace -Path $repository }
Assert-Refused { Remove-ZjcsValidationWorkspace -Path (Get-ZjcsValidationRoot) }

$savedWorkspace = $env:ZJCS_VALIDATION_DIR
$savedRepository = $env:ZJCS_REPOSITORY_ROOT
$observed = [System.Collections.Generic.List[string]]::new()
Invoke-ZjcsValidation -Name cleanup-test -ScriptBlock {
    param($work)
    $observed.Add($work)
    Assert-Workspace ($env:ZJCS_VALIDATION_DIR -eq $work) 'Missing session output environment.'
    Assert-Workspace ($env:ZJCS_REPOSITORY_ROOT -eq $repository) 'Missing repository environment.'
    New-Item -ItemType Directory -Path (Join-Path $work 'Player/Data') | Out-Null
    Set-Content -LiteralPath (Join-Path $work 'Player/test.exe') -Value 'fixture'
}
Assert-Workspace (-not (Test-Path -LiteralPath $observed[0])) 'Successful validation left its outputs.'
try {
    Invoke-ZjcsValidation -Name failure-test -ScriptBlock {
        param($work)
        $observed.Add($work)
        throw 'Intentional validation failure'
    }
    throw 'Expected validation failure did not propagate'
} catch {
    Assert-Workspace ($_.Exception.Message -eq 'Intentional validation failure') 'Original validation failure was lost.'
}
Assert-Workspace (-not (Test-Path -LiteralPath $observed[1])) 'Failed validation left its outputs.'
Assert-Workspace ($env:ZJCS_VALIDATION_DIR -eq $savedWorkspace) 'Session environment was not restored.'
Assert-Workspace ($env:ZJCS_REPOSITORY_ROOT -eq $savedRepository) 'Repository environment was not restored.'

$one = New-ZjcsValidationWorkspace -Name link-test
$two = New-ZjcsValidationWorkspace -Name independent-test
$link = Join-Path $one 'other-session'
try {
    Set-Content -LiteralPath (Join-Path $two 'keep.txt') -Value 'preserve other session'
    New-Item -ItemType Junction -Path $link -Target $two | Out-Null
    Assert-Refused { Remove-ZjcsValidationWorkspace -Path $one }
    Assert-Workspace (Test-Path -LiteralPath (Join-Path $two 'keep.txt')) 'Cleanup touched another session.'
} finally {
    # Remove only the junction itself; never recurse into its target.
    if (Test-Path -LiteralPath $link) { [System.IO.Directory]::Delete($link) }
    Remove-ZjcsValidationWorkspace -Path $one
    Remove-ZjcsValidationWorkspace -Path $two
}
Write-Output 'Validation workspaces: success/failure cleanup, environment restore, scope and junction guards passed.'
