$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Assert-CustomActionFloat($condition, $message) {
    if (-not $condition) { throw $message }
}

$runtimePath = Join-Path $root 'unity/Assets/Game/Resources/Combat/battle-custom-action-world-runtime.json'
$manifestPath = Join-Path $root 'unity/Assets/Game/Resources/Combat/battle-custom-action-world-runtime-source-manifest.json'
$runtime = Get-Content $runtimePath -Raw | ConvertFrom-Json
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$pinnedHud = Get-Content (Join-Path $root 'assets/reference/battle-hud/source-manifest.json') -Raw | ConvertFrom-Json
$hudCatalog = Get-Content (Join-Path $root 'unity/Assets/Game/Resources/Combat/OfficialBattleHudCatalog.asset') -Raw
$fontAsset = Get-Content (Join-Path $root 'unity/Assets/Game/Resources/Combat/FZY4K-Dynamic.asset') -Raw
$fontMeta = Get-Content (Join-Path $root 'unity/Assets/Official/Commerce/Art/fonts/FZY4K.ttf.meta') -Raw
$fontPath = Join-Path $root 'unity/Assets/Official/Commerce/Art/fonts/FZY4K.ttf'
$actualHash = (Get-FileHash $runtimePath -Algorithm SHA256).Hash.ToLowerInvariant()
$fontHash = (Get-FileHash $fontPath -Algorithm SHA256).Hash.ToLowerInvariant()
$worldHash = $pinnedHud.output_sha256.'world-ui.json'

Assert-CustomActionFloat ($runtime.manifest -eq '854_156383' -and $manifest.manifest -eq '854_156383') `
    'Custom action float data is not pinned to the official battle manifest.'
Assert-CustomActionFloat ($manifest.source_sha256 -eq $worldHash -and $runtime.sourceSha256 -eq $worldHash) `
    'Custom action float data did not preserve the pinned official world UI source hash.'
Assert-CustomActionFloat ($manifest.source_path -eq 'Assets/Official/Combat/HUD/world-ui.json' `
    -and $manifest.output_sha256 -eq $actualHash) `
    'Custom action float runtime manifest does not match its generated payload.'
$font = $manifest.font_binding
Assert-CustomActionFloat ($font.official_name -eq 'FZY4K SDF - dynamic' `
    -and $font.source_bundle -eq 'defaultpackage_assets_textmesh_pro_assets_fonts_&_materials.bundle' `
    -and $font.source_bundle_hash -eq 'bf62732f37a5a423ddbac02681e054a4' `
    -and $font.source_bundle_sha256 -eq 'ebee921d1efee60b02445c5d09faebdef159fdd6c7284658d2c05cf9e3826434' `
    -and $font.serialized_file -eq 'CAB-43221b4d126447a2b398f3477e814a2c' `
    -and $font.path_id -eq '-1054575886599457342') `
    'The prefab TMP_FontAsset PPtr is not linked to the pinned official font bundle.'
Assert-CustomActionFloat ($font.runtime_asset_path -eq 'Assets/Game/Resources/Combat/FZY4K-Dynamic.asset' `
    -and $font.runtime_asset_guid -eq 'e9b69727f25b00e4c82f8bc68bd859c5' `
    -and $hudCatalog.Contains($font.runtime_asset_guid)) `
    'The battle HUD catalog is not bound to the official custom-action font asset.'
Assert-CustomActionFloat ($font.source_ttf_guid -eq '6318d84275f8ea84bb687a21c99efcf7' `
    -and $fontMeta -match ('(?m)^guid:\s*' + [regex]::Escape($font.source_ttf_guid) + '$') `
    -and $fontAsset -match ('(?m)^  m_SourceFontFileGUID:\s*' + [regex]::Escape($font.source_ttf_guid) + '$') `
    -and $fontHash -eq $font.source_ttf_sha256 `
    -and $fontHash -eq '90023872dffe6a58503de655bd0631bf3334ce3b6b72cc75a6625b685c94c386') `
    'The runtime TMP_FontAsset does not use the pinned official FZY4K source font bytes.'
Assert-CustomActionFloat ($runtime.sourceBundle -eq 'defaultpackage_assets_prefabs_ui_fight_damagescene_battlecustomdamagefloateffect_scene.bundle' `
    -and $runtime.sourceBundleHash -eq '14e38fc9585d8e00567ae2f9f4b0e8bb' `
    -and $runtime.sourcePathId -eq '-2976502348647371213') `
    'Custom action float source bundle identity changed.'
Assert-CustomActionFloat ($runtime.nodes.Count -eq 5 -and $runtime.effectType -eq 11 `
    -and [Math]::Abs($runtime.showTime - 1.0) -lt 0.0001 `
    -and [Math]::Abs($runtime.height - 0.268) -lt 0.0001) `
    'Custom action float hierarchy or CharacterDamageUI settings changed.'

$number = $runtime.nodes | Where-Object { $_.name -eq 'NumberText' } | Select-Object -First 1
$icon = $runtime.nodes | Where-Object { $_.name -eq 'Icon' } | Select-Object -First 1
$animationNode = $runtime.nodes | Where-Object { $_.name -eq 'Ani' } | Select-Object -First 1
$actionText = $runtime.actions | Where-Object { $_.action -eq 3 } | Select-Object -First 1
$unflinchingText = $runtime.actions | Where-Object { $_.action -eq 2 } | Select-Object -First 1
Assert-CustomActionFloat ($number.uses_rect_transform -and ($number.size_delta -join ',') -eq '10,10' `
    -and $number.text_value -eq '免疫') `
    'The official TMP RectTransform or source text was not retained.'
Assert-CustomActionFloat ($icon.has_sprite_renderer -and $icon.image_order -eq 3 `
    -and $icon.image_sprite_file -eq 'res://assets/official/battle-hud/sprites/ImgArrow-676497dfd9.png') `
    'The official CDModify arrow SpriteRenderer binding was not retained.'
Assert-CustomActionFloat ($animationNode.animation_play_automatically `
    -and $animationNode.animations.Count -eq 1 `
    -and $animationNode.animations[0].name -eq 'GameCanvas_FlyText_Immunity_Scene' `
    -and $animationNode.animations[0].loop `
    -and [Math]::Abs($animationNode.animations[0].duration - 1.0) -lt 0.0001) `
    'The official scene float animation binding changed.'
Assert-CustomActionFloat ($null -ne $actionText -and $actionText.key -eq 'EStatusCustomAction.CDModify' `
    -and $actionText.text -eq '减少冷却') `
    'The official EStatusCustomAction.CDModify localization is missing.'
Assert-CustomActionFloat ($null -ne $unflinchingText `
    -and $unflinchingText.key -eq 'EStatusCustomAction.Unflinching' `
    -and $unflinchingText.text -eq '霸体') `
    'The official EStatusCustomAction.Unflinching localization is missing.'
Assert-CustomActionFloat (Test-Path (Join-Path $root 'unity/Assets/Official/Combat/HUD/Sprites/ImgArrow-676497dfd9.png')) `
    'The official CDModify action icon was not extracted into the Unity project.'

Write-Output 'Official CDModify and Unflinching world float source, font, localization, transforms, sprite, animation, and hashes passed.'
