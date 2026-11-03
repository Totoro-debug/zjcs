"""Restore original status animation pause and verify its controller dependency."""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_battle_status_playable_runtime import CODE, identity, write, method_body
from prepare_battle_status_material_runtime import method_fingerprints
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta

RECEIPT = ROOT/'assets/reference/battle-animation/status-pause-runtime-source-manifest.json'
OUTPUT = ROOT/'unity/Assets/Game/Runtime/Official/BattleAnimationPlayables'


def build():
    manifest = json.loads((CODE.parent/'code-manifest.json').read_bytes())
    assembly = CODE/'managed/decoded/Assembly-CSharp.dll'
    assert manifest['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == manifest['assemblies'][assembly.name]['decoded_sha256']
    source = CODE/'client-source/MyPlayableAnimationControllerPause.cs'
    original = source.read_text(encoding='utf-8-sig')
    text, diagnostics = logging_adapter(original); assert diagnostics == 1
    text = re.sub(r'(?m)^using (?:GameFramework|Framework(?:\.[\w.]+)*);\n', '', text)
    start = text.index('[MyPlayableApply')
    text = ('// Migrated current official application source; see status pause runtime receipt.\n'
        'using Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\n'
        'using Zjcs.Official.StoryAnimation;\n'+text[:start]
        +'namespace Zjcs.Official.BattleAnimationPlayables\n{\n'+text[start:]+'\n}\n')
    old_methods, new_methods = method_fingerprints(original), method_fingerprints(text)
    assert len(old_methods) == 5 and old_methods.keys() == new_methods.keys()
    assert all(old_methods[key] == new_methods[key] for key in old_methods if 'PlayImpl(' not in key)
    controller_source = CODE/'client-source/AnimationController.cs'
    controller_target = ROOT/'unity/Assets/Game/Runtime/Official/StoryAnimation/AnimationController.cs'
    normalize = lambda body: re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', body))
    for name in ('Pause', 'Resume', 'ForceResumeAllPause'):
        assert normalize(method_body(controller_source.read_text(encoding='utf-8-sig'), name)) == normalize(
            method_body(controller_target.read_text(encoding='utf-8-sig'), name)), name
    target = OUTPUT/'MyPlayableAnimationControllerPause.cs'
    write(target, text.encode()); runtime_meta(target)
    asmdef = OUTPUT/'Zjcs.Official.BattleAnimationPlayables.asmdef'
    write(asmdef, (json.dumps(dict(name='Zjcs.Official.BattleAnimationPlayables', references=[
        'Zjcs.Official.StoryPlayables', 'Zjcs.Official.StoryAnimation']), indent=2)+'\n').encode())
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    outputs = [target, Path(str(target)+'.meta'), asmdef, Path(str(asmdef)+'.meta'), Path(str(OUTPUT)+'.meta')]
    write(RECEIPT, (json.dumps(dict(manifest=manifest['manifest'], code_extraction=identity(CODE.parent/'code-manifest.json'),
        assembly=identity(assembly), original=identity(source), original_mono_script_file_id=498949265,
        original_method_bodies=old_methods, adapted_method_bodies=new_methods,
        controller_source=identity(controller_source), controller_target=identity(controller_target),
        verified_unchanged_controller_methods=['Pause', 'Resume', 'ForceResumeAllPause'],
        outputs=[identity(p) for p in outputs], generator=identity(Path(__file__)),
        validation=[identity(ROOT/'unity/Assets/Game/Editor/Combat/AnimatorPlayback'/name) for name in (
            'OfficialBattleAnimatorPlaybackAcceptance.StatusPause.cs', 'OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAnimator.cs',
            'OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance.cs',
            'OfficialBattleAnimatorPlaybackAcceptance.cs', 'OfficialBattleAnimatorPlaybackAcceptance.StatusMaterials.cs',
            'Zjcs.Editor.Combat.AnimatorPlayback.asmdef')],
        reused_adapters=[identity(ROOT/path) for path in ('scripts/prepare_battle_status_material_runtime.py',
            'scripts/prepare_unity_story_playable_runtime.py', 'scripts/prepare_battle_status_pause_assets.py')],
        adaptation='Only namespace imports and one framework diagnostic are adapted. Preserve the original AnimationController cast, pause call, explicit Stop versus StopImpl resume timing, ResumeOnDestroy and one-shot mResumed guard. Controller pause/ref-count/restoration methods match the selected current source.',
        limits=['Other freeze graphs and formal status actor/timeline installation remain separate integration work.']),
        ensure_ascii=False, indent=2)+'\n').encode())
    print('Original status pause migrated: five methods and three unchanged controller methods.')


if __name__ == '__main__':
    build()
