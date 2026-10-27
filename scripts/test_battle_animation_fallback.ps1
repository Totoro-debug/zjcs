. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
$fallback = Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAnimationFallback.cs'
$timeline = Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAnimationTimeline.cs'
$parameterStack = Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAnimatorParameterStack.cs'
Add-Type -Path (@($sources) + $fallback + $timeline + $parameterStack)

$stack = [Zjcs.Presentation.Combat.OfficialBattleAnimatorParameterStack[bool]]::new($false)
$stack.Push(10, $true)
$stack.Push(20, $false)
if ($stack.Current -ne $false -or -not $stack.Remove(10) -or $stack.Current -ne $false) {
    throw 'Removing an older Animator Playable changed the newest merged parameter value.'
}
if (-not $stack.Remove(20) -or $stack.Current -ne $false -or $stack.Count -ne 0) {
    throw 'Removing the final Animator Playable did not restore the initial parameter value.'
}
$stack.Push(30, $true)
$stack.Push(40, $false)
if (-not $stack.Remove(40) -or $stack.Current -ne $true -or
    -not $stack.Remove(30) -or $stack.Current -ne $false) {
    throw 'Animator Playable parameters did not resume in stack order.'
}

$unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
$unit.Id = 4

$step = [Zjcs.Combat.Official.FightBattleStepEvent]::new()
$cast = [Zjcs.Combat.Official.FightBattleSkillCastEvent]::new()
$cast.ActorId = $unit.Id
$cast.SkillEntityId = 123
$step.SkillCasts.Add($cast)
if ($null -ne [Zjcs.Presentation.Combat.OfficialBattleAnimationFallback]::Resolve($unit, $step)) {
    throw 'A skill without a dedicated animation mapping interrupted the current action.'
}

$step = [Zjcs.Combat.Official.FightBattleStepEvent]::new()
$damage = [Zjcs.Combat.Official.FightBattleDamageEvent]::new()
$damage.TargetId = $unit.Id
$damage.Damage = 10
$step.DamageEvents.Add($damage)
if ([Zjcs.Presentation.Combat.OfficialBattleAnimationFallback]::Resolve($unit, $step) -ne 'hit_01') {
    throw 'Ordinary damage did not select the official hit animation.'
}
$damage.Block = $true
if ([Zjcs.Presentation.Combat.OfficialBattleAnimationFallback]::Resolve($unit, $step) -ne 'block_01') {
    throw 'Blocked damage did not select the official block animation.'
}

$step = [Zjcs.Combat.Official.FightBattleStepEvent]::new()
$movement = [Zjcs.Combat.Official.FightBattleMovementEvent]::new()
$movement.UnitId = $unit.Id
$movement.Path.Add([Zjcs.Combat.Official.FightGridPoint]::new(0, 0))
$movement.Path.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
$step.Movements.Add($movement)
if ([Zjcs.Presentation.Combat.OfficialBattleAnimationFallback]::Resolve($unit, $step) -ne 'run_01') {
    throw 'Movement did not select the official run animation.'
}

$movement.Delay = 0.8
$damage.Block = $false
$damage.Delay = 0.4
$step.DamageEvents.Add($damage)
$step.SkillCasts.Add($cast)
$cues = [Zjcs.Presentation.Combat.OfficialBattleAnimationTimeline]::Build($step)
if ($cues.Count -ne 2 -or $cues[0].Animation -ne 'hit_01' -or
    [Math]::Abs([double]$cues[0].Delay - 0.4) -gt 0.001 -or
    $cues[1].Animation -ne 'run_01' -or [Math]::Abs([double]$cues[1].Delay - 0.8) -gt 0.001) {
    throw 'Hit and movement animation cues were not ordered by their configured delays.'
}

$movement.Delay = 0.4
$damage.Block = $true
$sameFrame = [Zjcs.Presentation.Combat.OfficialBattleAnimationTimeline]::Build($step)
if ($sameFrame.Count -ne 2 -or $sameFrame[0].Animation -ne 'run_01' -or
    $sameFrame[1].Animation -ne 'block_01') {
    throw 'A same-frame hit reaction did not take priority over movement animation.'
}

$damage.IsSkillCost = $true
if ([Zjcs.Presentation.Combat.OfficialBattleAnimationTimeline]::Build($step).Count -ne 1) {
    throw 'Skill HP cost incorrectly generated a target hit animation.'
}

$step = [Zjcs.Combat.Official.FightBattleStepEvent]::new()
$cast = [Zjcs.Combat.Official.FightBattleSkillCastEvent]::new()
$cast.ActorId = $unit.Id
$cast.SkillEntityId = 123
$step.SkillCasts.Add($cast)
$skillCatalog = [Zjcs.Presentation.Combat.OfficialBattleSkillPlayableCatalogData]::new()
$skillCatalog.manifest = '854_156383'
$skill = [Zjcs.Presentation.Combat.OfficialBattleSkillPlayableRecord]::new()
$skill.skill_entity_id = 123
$playable = [Zjcs.Presentation.Combat.OfficialBattleSkillPlayableCue]::new()
$playable.kind = 1
$playable.time = 0.25
$playable.animation = '200/skill_01'
$skill.playables.Add($playable)
$skillCatalog.skills.Add($skill)
$mapped = [Zjcs.Presentation.Combat.OfficialBattleAnimationTimeline]::Build($step, $skillCatalog)
if ($mapped.Count -ne 1 -or $mapped[0].SkillPlayable -ne $playable -or
    $mapped[0].Animation -ne '200/skill_01' -or [Math]::Abs([double]$mapped[0].Delay - 0.25) -gt 0.001) {
    throw 'Official skill Playable mapping was not scheduled with its configured delay.'
}

$runtimeCatalogPath = Join-Path $root 'unity/Assets/Game/Resources/Combat/skill-playables.json'
$runtimeCatalog = Get-Content $runtimeCatalogPath -Raw | ConvertFrom-Json
if ($runtimeCatalog.manifest -ne '854_156383' -or $runtimeCatalog.skills.Count -ne 1451) {
    throw 'Packaged skill Playable catalog does not match the fixed official manifest.'
}
$animatorCues = @($runtimeCatalog.skills | ForEach-Object { $_.playables } | Where-Object { $_.kind -eq 0 })
if ($animatorCues.Count -ne 1191 -or
    @($animatorCues | Where-Object { $_.end_resume_param }).Count -ne 0 -or
    @($animatorCues | Where-Object { $_.my_duration -ne 0 }).Count -ne 16) {
    throw 'Fixed official Animator Playable lifecycle fields changed unexpectedly.'
}
$sourceManifestPath = Join-Path $root 'assets/reference/battle-animation/skill-playables-source-manifest.json'
$sourceManifest = Get-Content $sourceManifestPath -Raw | ConvertFrom-Json
$clipAssets = @{}
foreach ($property in $sourceManifest.clip_assets.PSObject.Properties) {
    $clipAssets[$property.Name] = $property.Value
}
if ($sourceManifest.manifest -ne '854_156383' -or @($sourceManifest.clip_assets.PSObject.Properties).Count -ne 132 -or
    $sourceManifest.resolved_clip_source_names.Count -ne 8 -or
    $sourceManifest.unavailable_clip_names.Count -ne 0) {
    throw 'Fixed official skill clip source resolution changed unexpectedly.'
}
if ((Get-FileHash $runtimeCatalogPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sourceManifest.output_sha256) {
    throw 'Packaged skill Playable data hash does not match its source manifest.'
}
$resolvedSources = @{}
foreach ($property in $sourceManifest.resolved_clip_sources.PSObject.Properties) {
    $resolvedSources[$property.Name] = $property.Value
}
foreach ($name in $sourceManifest.resolved_clip_source_names) {
    $references = @($resolvedSources[$name].references)
    if ($references.Count -eq 0 -or $references[0].clip.type -ne 'AnimationClip' -or
        $references[0].clip.name -ne $name -or $references[0].clip.source.manifest -ne '854_156383') {
        throw "Official source object is incomplete or mismatched: $name"
    }
}
$overrideCues = @($runtimeCatalog.skills | ForEach-Object { $_.playables } | Where-Object { $_.kind -eq 1 })
if ($overrideCues.Count -ne 289) { throw 'Packaged skill override count changed unexpectedly.' }
foreach ($cue in $overrideCues | Where-Object { $_.clip_resource }) {
    $clipPath = Join-Path $root ('unity/Assets/Game/Resources/Combat/SkillAnimations/' + $cue.clip_resource + '.anim')
    if (-not (Test-Path $clipPath)) { throw "Packaged official skill clip is missing: $($cue.clip_resource)" }
    $asset = $clipAssets[$cue.animation]
    if ($null -eq $asset -or (Get-FileHash $clipPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $asset.output_sha256) {
        throw "Packaged official skill clip hash does not match its source manifest: $($cue.animation)"
    }
}
foreach ($name in $sourceManifest.resolved_clip_source_names) {
    $cues = @($overrideCues | Where-Object { $_.animation -eq $name })
    if ($cues.Count -eq 0) { throw "Resolved official skill clip has no Playable cue: $name" }
    if (@($cues | Where-Object { $_.clip_resource -ne $name.Replace('/', '_') }).Count -ne 0) {
        throw "Resolved official skill clip was not packaged: $name"
    }
}

Write-Output 'Battle animation checks passed: unmapped skills preserve action; mapped skill Playables retain official timing and clips.'
