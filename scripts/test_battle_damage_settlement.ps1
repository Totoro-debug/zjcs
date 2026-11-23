$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Add-Type -Path (Get-FightCombatSources)

$settlement = [Zjcs.Combat.Official.FightDamageSettlement]::new()
$limit = [long]::MaxValue
$cases = @(
    @{ Name = 'capped cure after overflow'; Before = $limit - 2; Max = $limit; Amount = 10; After = $limit; Applied = 2; Killed = $false }
    @{ Name = 'cure at full health'; Before = $limit; Max = $limit; Amount = 1; After = $limit; Applied = 0; Killed = $false }
    @{ Name = 'ordinary capped cure'; Before = 98; Max = 100; Amount = 10; After = 100; Applied = 2; Killed = $false }
    @{ Name = 'uncapped cure'; Before = 50; Max = 100; Amount = 10; After = 60; Applied = 10; Killed = $false }
    @{ Name = 'lethal damage'; Before = 5; Max = 100; Amount = -10; After = 0; Applied = -5; Killed = $true }
)
foreach ($case in $cases) {
    $target = [Zjcs.Combat.Official.FightEntityState]::new(1, $case.Before, $case.Max)
    $result = $settlement.Apply(1, $null, $target, $case.Amount)
    if ($target.Hp -ne $case.After -or $result.RealDamageValue -ne $case.Applied -or
        $result.DamageValue -ne $case.Amount -or $result.KilledTarget -ne $case.Killed) {
        throw "Unexpected settlement for $($case.Name): HP=$($target.Hp), applied=$($result.RealDamageValue), killed=$($result.KilledTarget)"
    }
}

$schema = [Zjcs.Combat.Official.FightDamagePropertySchema]::new()
foreach ($name in @('BaseCureAdd', 'BaseBeCureAdd')) {
    $property = [Zjcs.Combat.Official.FightPropertyName]::new()
    $property.Id = $schema.Names.Count + 1
    $property.Name = $name
    $schema.Names.Add($property)
}
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    $schema, [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$executor = [Zjcs.Combat.Official.FightBattleHitExecutor]::new($formula, $settlement)
$actor = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$actor.Id = 1
$actor.Hp = 100
$actor.MaxHp = 100
$target = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$target.Id = 2
$target.Hp = 40
$target.MaxHp = 100
$baseCure = $formula.CalculateCure($null, $null, 0, 0, 20)
if ($baseCure.Cure -ne 20 -or $baseCure.AbsoluteCure -ne 20) {
    throw 'Missing optional healing bonuses changed the base cure.'
}
$sourceCureBase = [Zjcs.Combat.Official.FightPropertyValue]::new()
$sourceCureBase.Id = 1
$sourceCureBase.Value = 1
$actor.Properties.Add($sourceCureBase)
$targetCureBase = [Zjcs.Combat.Official.FightPropertyValue]::new()
$targetCureBase.Id = 2
$targetCureBase.Value = 1
$target.Properties.Add($targetCureBase)
$hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$hit.ActionType = 5
$hit.HasCustomDamage = $true
$hit.CustomDamage = 20
$random = [Zjcs.Combat.Official.FightRandomStream]::new(1)
$result = $executor.Apply($random, 1, $actor, $target, $null, $hit, 0, $false)
if (-not $result.IsCure -or $target.Hp -ne 60 -or $result.Settlement.RealDamageValue -ne 20) {
    throw 'A healing hit without a skill did not apply its configured amount.'
}

$hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
$absorbed = 0L
$remaining = [Zjcs.Combat.Official.FightBattleShieldRuntime]::AbsorbDamage(
    $random, $target, $hit, [long]::MinValue, [ref]$absorbed)
if ($remaining -ne [long]::MinValue -or $absorbed -ne 0) {
    throw 'Shield resolution changed unabsorbed damage.'
}
$shield = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$shield.ShieldKind = [Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
$shield.ShieldValue = $limit
$target.Statuses.Add($shield)
$remaining = [Zjcs.Combat.Official.FightBattleShieldRuntime]::AbsorbDamage(
    $random, $target, $hit, [long]::MinValue, [ref]$absorbed)
if ($remaining -ne -1 -or $absorbed -ne $limit -or $target.Statuses.Count -ne 0) {
    throw 'A finite shield absorbed more damage than its capacity.'
}
$shield = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$shield.ShieldKind = [Zjcs.Combat.Official.FightBattleShieldKind]::Invincible
$shield.ShieldRemainCount = 1
$target.Statuses.Add($shield)
$remaining = [Zjcs.Combat.Official.FightBattleShieldRuntime]::AbsorbDamage(
    $random, $target, $hit, [long]::MinValue, [ref]$absorbed)
if ($remaining -ne 0 -or $absorbed -ne $limit -or $target.Statuses.Count -ne 0) {
    throw 'An invincible shield failed to absorb the full hit with a capped receipt.'
}
$shield = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$shield.ShieldKind = [Zjcs.Combat.Official.FightBattleShieldKind]::Counted
$shield.ShieldValue = 100
$shield.ShieldRemainCount = 2
$target.Statuses.Add($shield)
foreach ($count in @(1, 0)) {
    $remaining = [Zjcs.Combat.Official.FightBattleShieldRuntime]::AbsorbDamage(
        $random, $target, $hit, -30, [ref]$absorbed)
    if ($remaining -ne 0 -or $absorbed -ne 30 -or
        $shield.ShieldRemainCount -ne $count -or $target.Statuses.Count -ne $count) {
        throw 'A counted shield lost its per-hit capacity before its last charge.'
    }
}
Write-Output 'Damage settlement passed: healing, HP bounds, shield absorption and shield charges.'
