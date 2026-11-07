"""Record the exact official sources and explicit ports for skill clone leaves."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, write_changed


def main():
    base = ROOT / '.tools/source-packages/current/884_156692'
    code = base / 'official-code'
    receipt = base / 'code-manifest.json'
    record = json.loads(receipt.read_bytes())
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    assert record['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == record['assemblies']['Assembly-CSharp.dll']['decoded_sha256']
    sources = [code / 'client-source' / name for name in (
        'MyPlayableSpineClone.cs', 'PlayableSpineCloneInit.cs', 'PlayableSpinePlayerCloneInit.cs',
        'ApplyOnStopCheckSourceDie.cs', 'ECFightRoleCharacterView.cs',
        'AppearanceSystem.Runtime/AppearanceAnimation.cs',
        'AppearanceSystem.Runtime/AppearanceAnimUtils.cs',
        'AppearanceSystem.Runtime/AppearanceCharacter.cs', 'AppearanceAnimationPartHandler.cs')]
    sources.append(code / 'framework-source/PreviewTool.cs')
    runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
    outputs = [runtime / 'BattleSkillPlayables' / (name + '.cs') for name in (
        'MyPlayableSpineClone', 'PlayableSpineCloneInit', 'PlayableSpinePlayerCloneInit',
        'AppearanceAnimation', 'BattleSpineCloneAnimation', 'PreviewTool')]
    outputs.extend(runtime / name for name in (
        'StoryFollowBone/ApplyOnStopCheckSourceDie.cs',
        'StoryPlayables/IBattleSourceDeathView.cs',
        'CombatAppearance/AppearanceCharacter.cs', 'ProfessionUI/AppearanceAnimationPartHandler.cs'))
    outputs.extend(ROOT / 'unity/Assets/Game' / name for name in (
        'Runtime/Presentation/Combat/OfficialBattleSpineCloneAnimation.cs',
        'Runtime/Presentation/Combat/OfficialBattlePlayerClonePrefab.cs',
        'Runtime/Presentation/Combat/OfficialBattlePlayerCloneValidation.cs',
        'Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs',
        'Runtime/Presentation/Combat/OfficialBattleAnimatorPlayback.cs',
        'Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs',
        'Runtime/Presentation/Combat/OfficialBattleSceneView.Players.cs',
        'Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
        'Runtime/Presentation/Home/SkillSummonPlayerValidation.cs',
        'Runtime/Presentation/Combat/OfficialBattleSceneView.SourceDeath.cs',
        'Runtime/Offline/BattleNativePlayables/OfflineBattleNativePlayableScope.cs',
        'Runtime/Offline/ProfessionUI/OfflineProfessionBattlePlayerResources.cs',
        'Runtime/Offline/ProfessionUI/OfflineProfessionSceneCloneBindings.cs',
        'Runtime/Offline/ProfessionUI/ProfessionNativeSceneRenderer.cs',
        'Runtime/Offline/ProfessionUI/ProfessionNativeSceneRenderer.Parts.cs',
        'Runtime/Offline/ProfessionUI/ProfessionNativeSceneRenderer.Effects.cs',
        'Runtime/Offline/ProfessionUI/ProfessionNativeSceneRendererSettings.cs',
        'Editor/Combat/AnimatorPlayback/OfficialBattleSpineCloneAnimationValidation.cs',
        'Editor/Combat/AnimatorPlayback/OfficialBattleSourceDeathValidation.cs'))
    proof = dict(manifest=record['manifest'], code_receipt=identity(receipt), assembly=identity(assembly),
        official_sources=[identity(p) for p in sources],
        migrated_components_and_ports=[identity(p) for p in outputs],
        metadata=[identity(Path(str(p) + '.meta')) for p in outputs],
        validation_tools=[identity(ROOT / name) for name in (
            'scripts/test_battle_skill_graph_dispatch.ps1',
            'scripts/fixtures/battle_skill_graph_dispatch_harness.cs')],
        clone_settings_receipt=identity(ROOT / 'assets/reference/battle-animation/spine-clone-settings-source-manifest.json'),
        generator=identity(Path(__file__).resolve()),
        operation='Retain clone init/activate/play and clear/deactivate/stop order, original duration, '
            'player appearance source and empty PreviewTool marker. Bind source renderer selection '
            'through IProfessionFollowerRenderer and serialized targets through Component, so current '
            'native characters can participate without an SDK renderer. Expose the original track-zero, '
            'zero-mix, immediate-pose animation request and read-only state through an independent native '
            'track and physical clock. Load all renderer/clock settings from five verified original clone '
            'targets; replace EC view lookup with a scene dependency injected before Awake. Restart the '
            'still-present dead source through the shared scene, retaining original stop-then-replay behavior. '
            'Adapt each inactive original clone target in place, bind its existing appearance/character/part '
            'components to the battle resource owner, exchange only the renderer endpoint, then activate '
            'through the original playable. Retain ungrouped clone renderer orders and slot depths, without '
            'promoting fragment or weapon insertion keys to global sorting priorities. Preserve the '
            'actual initialized controller parameter/layer contract while the original graph hides '
            'its source model; reject unknown parameters without reactivating that model.',
        limits=['Physics inheritance of world movement requires a separate binding and validation. '
            'Attached player death motions and the complete original death playable binding remain incomplete. '
            'Source mapping and successful prefab loading alone do not prove those effects work.',
            'The public diagnostic covers the first 13100 segment only. Server combo sequencing and '
            'AIRandomTargetFallbackSpawner behavior remain unimplemented pending an offline decision.'])
    target = ROOT / 'assets/reference/battle-animation/spine-clone-runtime-source-manifest.json'
    write_changed(target, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    # The graph receipt references this runtime proof and the migrated leaf.
    # Refresh only those code identities; asset conversion/output evidence stays
    # untouched because this operation does not regenerate any prefab or asset.
    graph_receipt = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    if graph_receipt.is_file():
        graph = json.loads(graph_receipt.read_bytes())
        for row in graph['application']:
            if row.get('migrated', {}).get('path') == 'unity/Assets/Game/Runtime/Official/StoryFollowBone/ApplyOnStopCheckSourceDie.cs':
                row['migrated'] = identity(ROOT / row['migrated']['path'])
        for row in graph['runtime_receipts']:
            if row['path'] == target.relative_to(ROOT).as_posix():
                row.update(identity(target))
        write_changed(graph_receipt, json.dumps(graph, ensure_ascii=False, indent=2) + '\n')
    # Shared Animator playback is also identified by existing consumer receipts.
    # Refresh this code identity only; preserve their original inputs and assets.
    playback = identity(ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAnimatorPlayback.cs')
    def refresh_playback(value):
        if isinstance(value, dict):
            if value.get('path') == playback['path']:
                value.update(playback)
            for child in value.values():
                refresh_playback(child)
        elif isinstance(value, list):
            for child in value:
                refresh_playback(child)
    for relative in ('battle-animation/scene-skill-motions-source-manifest.json',
                     'partner-native-playables-source-manifest.json',
                     'partner-native-role-views-source-manifest.json'):
        consumer = ROOT / 'assets/reference' / relative
        data = json.loads(consumer.read_bytes())
        refresh_playback(data)
        write_changed(consumer, json.dumps(data, ensure_ascii=False, indent=2) + '\n')
    print('Recorded original skill clone sources and explicit dependency boundaries.')


if __name__ == '__main__':
    main()
