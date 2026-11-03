"""Migrate the original material bounds provider used by global frozen graphs."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_status_playable_runtime import CODE, identity, write
from prepare_battle_status_material_runtime import adapt, method_fingerprints
from prepare_unity_story_sound_runtime import runtime_meta

RECEIPT = ROOT/'assets/reference/battle-animation/status-frozen-runtime-source-manifest.json'


def build():
    manifest = json.loads((CODE.parent/'code-manifest.json').read_bytes())
    assembly = CODE/'managed/decoded/Assembly-CSharp.dll'
    assert manifest['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == manifest['assemblies'][assembly.name]['decoded_sha256']
    source = CODE/'client-source/PlayableMaterialBoundingBoxProvider.cs'
    original = source.read_text(encoding='utf-8-sig')
    text, diagnostics = adapt(original)
    assert diagnostics == 0
    text = text.replace('Vector4.op_Implicit(meshMaterialProperty.RendererBoundsSize)', '(Vector4)meshMaterialProperty.RendererBoundsSize')
    before, after = method_fingerprints(original), method_fingerprints(text)
    assert len(before) == len(after) == 1 and before.keys() == after.keys()
    assert original.replace('Vector4.op_Implicit(meshMaterialProperty.RendererBoundsSize)', '(Vector4)meshMaterialProperty.RendererBoundsSize').split('public class', 1)[1].strip() == text.split('public class', 1)[1].rsplit('}', 1)[0].strip()
    target = ROOT/'unity/Assets/Game/Runtime/Official/BattleMaterialPlayables/PlayableMaterialBoundingBoxProvider.cs'
    write(target, text.encode()); runtime_meta(target)
    write(RECEIPT, (json.dumps(dict(manifest=manifest['manifest'], code_extraction=identity(CODE.parent/'code-manifest.json'),
        assembly=identity(assembly), original=identity(source), original_mono_script_file_id=-425368815,
        original_method_bodies=before, adapted_method_bodies=after,
        outputs=[identity(target), identity(Path(str(target)+'.meta'))], generator=identity(Path(__file__)),
        reused_adapter=identity(ROOT/'scripts/prepare_battle_status_material_runtime.py'),
        adaptation='Namespace imports and ILSpy Vector4 implicit conversion syntax only. Preserve the serialized Key and one-time Play callback; the original provider has no ProcessFrame or Resume override.',
        validation=[identity(ROOT/'unity/Assets/Game/Editor/Combat/AnimatorPlayback'/name) for name in (
            'OfficialBattleAnimatorPlaybackAcceptance.StatusFrozen.cs', 'OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance.cs',
            'OfficialBattleAnimatorPlaybackAcceptance.cs')]), ensure_ascii=False, indent=2)+'\n').encode())
    print('Original material bounding-box provider migrated.')


if __name__ == '__main__':
    build()
