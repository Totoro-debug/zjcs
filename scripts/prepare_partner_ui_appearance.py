"""Port selected UI appearance onto the already migrated current shared lifecycle."""
import json
import re
from pathlib import Path

from prepare_battle_scene_appearance_runtime import BASE, ROOT, SOURCE, DEST, common, identity, meta, write_changed
from prepare_unity_profession_player_appearance import binding, logs, strip


def ui(source):
    text = common(source)
    text = text.replace('private SkeletonGraphic mSkeletonGraphic;', 'private Component mSkeletonGraphic;')
    start = text.index('\t[Inject]')
    end = text.index('\tpublic AppearanceUIPartHandler PartHandler', start)
    text = text[:start] + '''    private IProfessionAppearanceUIBindings mUIBindings;
    private IProfessionAppearanceUIBindings UIBindings => mUIBindings ?? ProfessionAppearanceUIBindings.Required;
    private IProfessionAppearanceUIAssetManager AppearanceAssetManager => UIBindings.AssetManager;
    private IProfessionAppearanceUIGraphic GraphicGraph => (IProfessionAppearanceUIGraphic)mSkeletonGraphic;
    public void ConfigureUI(IProfessionAppearanceUIBindings bindings)
    { mUIBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

''' + text[end:]
    replacements = {
        'public AppearanceUIPartHandler PartHandler => (AppearanceUIPartHandler)mPartHandler;': 'public Component PartHandler => mPartHandler;',
        'public AnimationState AnimState': 'public IProfessionAppearanceAnimationState AnimState',
        'TrackEntry current2': 'IProfessionAppearanceTrack current2',
        'SkeletonDataAsset skeletonDataAsset': 'ScriptableObject skeletonDataAsset',
        'mSkeletonGraphic.': 'GraphicGraph.',
        '((Component)this).GetComponent<SkeletonGraphic>()': '((Component)this).GetComponent<IProfessionAppearanceUIGraphic>() as Component',
        'HandleOnCustomDemandLoading(SkeletonGraphic skeletonGraphic)': 'HandleOnCustomDemandLoading(IProfessionAppearanceUIGraphic skeletonGraphic)',
        'skeletonGraphic.MaterialsMultipleCanvasRenderers.Items': 'skeletonGraphic.MaterialsMultipleCanvasRenderers',
        'skeletonGraphic.TexturesMultipleCanvasRenderers.Items': 'skeletonGraphic.TexturesMultipleCanvasRenderers',
        'AppearanceAnimUtils.TryLazyLoadSpineAnimation((ISkeletonComponent)(object)mSkeletonGraphic, GraphicGraph.startingAnimation, out var _)': 'UIBindings.TryLazyLoadSpineAnimation(GraphicGraph, GraphicGraph.startingAnimation, out var _)',
        'AppearanceAnimUtils.PlayAnimation(mSkeletonGraphic, animationName, loop)': 'UIBindings.PlayAnimation(GraphicGraph, animationName, loop)',
        'IAppearanceAssetManager appearanceAssetManager': 'IProfessionAppearanceUIAssetManager appearanceAssetManager',
        '((OnDemandTextureLoader)yooAssetTextureLoader).RequestLoadMaterialTextures': 'yooAssetTextureLoader.RequestLoadMaterialTextures',
        'Object.op_Implicit((Object)(object)mTextureLoader)': 'mTextureLoader',
        '!Object.op_Implicit((Object)(object)mSkeletonGraphic)': '!mSkeletonGraphic',
    }
    for before, after in replacements.items():
        if before not in text:
            raise ValueError('Selected UI appearance changed: ' + before)
        text = text.replace(before, after)
    start = text.index('\t\t\tGLoggerErrorInterpolatedStringHandler')
    end = text.index('\n\t\t}\n\t}', start)
    text = text[:start] + '            Debug.LogError("AppearanceUI OnCheckMultipleMeshesValid: " + ex);' + text[end:]
    start = text.index('\t\tGLoggerErrorInterpolatedStringHandler')
    end = text.index('\n\t\tErrorStringBuilder.Clear();', start)
    text = text[:start] + '        Debug.LogError("AppearanceUI invalid mesh cleared: " + ErrorStringBuilder);' + text[end:]
    start = text.index('\t\t\tGLoggerInfoInterpolatedStringHandler')
    end = text.index('\n\t\t\treturn;', start)
    text = text[:start] + '            Debug.Log(name + " MaterialCallback but stencil still is 0");' + text[end:]
    return text


def animation(source):
    text = strip(source.read_text('utf-8-sig'))
    text = binding(text, '\t[Inject]', '\tpublic bool AutoResetAnimation', '')
    text = text.replace('AppearanceUtils.GetBaseProfession', 'PlayerBindings.GetBaseProfession')
    text = text.replace('AppearanceAnimUtils.GetAnimName(AppearanceAnimNameCombineRule.Value,',
        'ProfessionAppearanceAnimationNames.GetAnimName(PlayerBindings, PlayerBindings.AnimationRules,')
    text = text.replace('IAppearanceAnimComponent appearanceAnimComponent = default;', 'IAppearanceAnimComponent appearanceAnimComponent;')
    text = text.replace('TryGetComponent<IAppearanceAnimComponent>(ref appearanceAnimComponent)', 'TryGetComponent<IAppearanceAnimComponent>(out appearanceAnimComponent)')
    return 'namespace Zjcs.Official.CombatAppearance\n{\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.ProfessionUI;\n' + logs(text) + '\n}\n'


def main():
    receipt = BASE / 'code-manifest.json'
    data = json.loads(receipt.read_bytes())
    assembly = BASE / 'official-code/managed/decoded/Assembly-CSharp.dll'
    assert data['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == data['assemblies'][assembly.name]['decoded_sha256']
    inputs, outputs = [], []
    proof_path = ROOT / 'assets/reference/partner-ui-appearance-source-manifest.json'
    previous = json.loads(proof_path.read_bytes()) if proof_path.exists() else {}
    known = {row['path']: row['sha256'] for row in previous.get('outputs', [])}
    clip_receipt_path = ROOT / 'assets/reference/profession-ui/skin-clip-source-manifest.json'
    clip_receipt = json.loads(clip_receipt_path.read_bytes())
    clip, = clip_receipt['ports']
    current_clip = SOURCE / 'AppearanceSystem.Runtime/AppearanceSkinClip.cs'
    assert identity(current_clip)['sha256'] == clip['source']['sha256']
    assert identity(ROOT / clip['runtime']['path']) == clip['runtime']
    for relative, transform in [('AppearanceSystem.Runtime/AppearanceUI.cs', ui),
                                ('AppearanceSystem/PlayerAnimationAppearance.cs', animation)]:
        source = SOURCE / relative
        text = transform(source)
        if any(token in text for token in ('[Inject]', 'GLogger', 'using Spine', '.op_', 'AppearanceAnimUtils.', 'Framework.')):
            raise ValueError('Unresolved selected UI appearance dependency')
        if re.search(r'\b(?:SkeletonGraphic|SkeletonDataAsset|AnimationState|TrackEntry|AppearanceUIPartHandler)\s+\w', text):
            raise ValueError('Unbound animation SDK type')
        target = DEST / source.name
        if target.exists() and target.read_bytes().replace(b'\r\n', b'\n') != text.encode('utf8'):
            assert known.get(target.relative_to(ROOT).as_posix()) == identity(target)['sha256'], 'Preserve edited current UI source'
        write_changed(target, text)
        meta(target)
        inputs.append(identity(source))
        outputs.extend(identity(path) for path in (target, Path(str(target) + '.meta')))
    proof = dict(manifest='884_156692', code_receipt=identity(receipt), assembly=identity(assembly),
        application_sources=inputs + [identity(SOURCE / 'AppearanceSystem.Runtime/AppearanceAnimUtils.cs')],
        outputs=outputs, generator=identity(Path(__file__).resolve()),
        dependencies=[identity(ROOT / path) for path in (
            'scripts/prepare_battle_scene_appearance_runtime.py',
            'scripts/prepare_unity_profession_player_appearance.py',
            'unity/Assets/Game/Runtime/Official/CombatAppearance/AppearanceSpine.cs',
            'unity/Assets/Game/Runtime/Official/CombatAppearance/AppearanceCharacter.cs',
            'unity/Assets/Game/Runtime/Official/CombatAppearance/PlayerAppearance.cs',
            'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionAppearanceUIBindings.cs',
            'assets/reference/partner-ui-appearance-assets-source-manifest.json',
            'assets/reference/partner-ui-high-textures-source-manifest.json')],
        retained_skin_clip=dict(current_source=identity(current_clip), historical_receipt=identity(clip_receipt_path),
            runtime=clip['runtime'], operation='Only this application source file is byte-identical to the historical source recorded by the retained port; no whole-version equivalence is asserted.'),
        native_ports=[identity(ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI' / (name + suffix))
            for name in ('OfflineProfessionUiAnimationBindings', 'OfflineProfessionUiAppearanceBindings',
                         'ProfessionNativeUiCharacterPose', 'ProfessionNativeUiGraphic',
                         'OfflineProfessionCurrentUiSkinClipBindings') for suffix in ('.cs', '.cs.meta')],
        validation=[identity(ROOT / path) for path in ('scripts/prepare_partner_main_avatar_validation.py',
            'scripts/fixtures/partner_main_avatar_validation.cs')],
        operation='Copy complete selected application methods and fields into the current shared appearance lifecycle. Preserve pre-load animation selection, mask-delayed skins/dyes, texture/material reference ordering and OnReleaseBeforeSORelease; explicit native ports replace SDK/injection only.',
        limits=['UI pose production, complete avatar host integration and Windows Player validation remain required.'])
    write_changed(proof_path, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Migrated selected UI appearance and animation lifecycle onto current shared appearance classes.')


if __name__ == '__main__':
    main()
