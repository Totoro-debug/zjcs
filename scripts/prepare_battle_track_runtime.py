"""Migrate the complete current application track player and all three track types."""
import json
import re
import struct
from pathlib import Path
from hashlib import sha256
from Crypto.Hash import MD4
from official_apk import ROOT
from prepare_battle_status_playable_runtime import CODE, identity, write
from prepare_battle_status_material_runtime import method_fingerprints
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_battle_movement_effects import method_body

OUTPUT = ROOT/'unity/Assets/Game/Runtime/Official/BattleTrackPlayables'
RECEIPT = ROOT/'assets/reference/battle-animation/track-runtime-source-manifest.json'
NAMES = ('MyTrack', 'MyCurveTrack', 'AnimationTrack', 'ParabolicTrack', 'MyPlayableTrack')


def normalize(text):
    return re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', text))


def build():
    manifest = json.loads((CODE.parent/'code-manifest.json').read_bytes())
    assembly = CODE/'managed/decoded/Assembly-CSharp.dll'
    assert manifest['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == manifest['assemblies'][assembly.name]['decoded_sha256']
    outputs, originals = [], []
    container_source=CODE/'client-source/MyContainer.cs'
    container_target=ROOT/'unity/Assets/Game/Runtime/Official/EntityContainers/MyContainer.cs'
    coordinate_body=normalize(method_body(container_source.read_text(encoding='utf-8-sig'),'ToLocalPos'))
    assert coordinate_body==normalize(method_body(container_target.read_text(encoding='utf-8-sig'),'ToLocalPos'))
    for name in NAMES:
        source = CODE/'client-source'/(name+'.cs')
        original = source.read_text(encoding='utf-8-sig')
        start = re.search(r'(?m)^(?:\[MyPlayableApply|public (?:abstract )?class)', original).start()
        body = original[start:]
        body = re.sub(r'(?m)^\s*//IL_[^\n]*\n', '', body)
        old = 'mEcTransform.myContainer.ToLocalPos('
        new = '((IEntityContainerLocalPosition)mEcTransform.myContainer).ToLocalPos('
        if name == 'MyPlayableTrack':
            assert body.count(old) == 2
            body = body.replace(old, new)
        # Compare the whole class, including nested calculators and fields,
        # after reversing only the explicit container boundary cast.
        assert normalize(body.replace(new, old)) == normalize(original[start:])
        text = ('// Complete current original application source; see track runtime receipt.\n'
            'using Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\n'
            'using Zjcs.Official.StoryAttachment;\nusing Zjcs.Official.StoryFollowBone;\n'
            'using Zjcs.Official.EntityLifecycle;\n'
            +re.sub(r'(?m)^using GameFramework;\n', '', original[:start])
            +'namespace Zjcs.Official.BattleTrackPlayables\n{\n'+body+'\n}\n')
        text = '\n'.join(line.rstrip() for line in text.splitlines())+'\n'
        target = OUTPUT/(name+'.cs');write(target,text.encode());runtime_meta(target)
        originals.append(dict(original=identity(source), migrated=identity(target),
            original_mono_script_file_id=struct.unpack('<i',MD4.new(('s\0\0\0'+name).encode()).digest()[:4])[0],
            whole_class_normalized_sha256=sha256(normalize(original[start:]).encode()).hexdigest(),
            original_method_bodies=method_fingerprints(original),
            adaptation='Namespaced and decompiler comments removed. The track player explicitly casts the existing container to the coordinate interface; all complete class bodies, nested calculators and serialized fields compared after reversing that cast.'))
        outputs += [target,Path(str(target)+'.meta')]
    asmdef=OUTPUT/'Zjcs.Official.BattleTrackPlayables.asmdef'
    write(asmdef,(json.dumps(dict(name='Zjcs.Official.BattleTrackPlayables',references=[
        'Zjcs.Official.StoryPlayables','Zjcs.Official.StoryAttachment',
        'Zjcs.Official.StoryFollowBone','Zjcs.Official.EntityLifecycle']),indent=2)+'\n').encode())
    runtime_meta(asmdef);runtime_meta(OUTPUT)
    outputs += [asmdef,Path(str(asmdef)+'.meta'),Path(str(OUTPUT)+'.meta')]
    write(RECEIPT,(json.dumps(dict(manifest=manifest['manifest'],assembly=identity(assembly),
        code_extraction=identity(CODE.parent/'code-manifest.json'),originals=originals,
        generator=identity(Path(__file__)),outputs=[identity(p) for p in outputs],
        coordinate_method=dict(original=identity(container_source),migrated=identity(container_target),
            method='ToLocalPos',normalized_body_sha256=sha256(coordinate_body.encode()).hexdigest()),
        verification_tools=[identity(ROOT/p) for p in (
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.Tracks.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/Zjcs.Editor.Combat.AnimatorPlayback.asmdef')],
        dependencies=[identity(ROOT/p) for p in (
            'unity/Assets/Game/Runtime/Official/EntityLifecycle/EntityLifecycleBindings.cs',
            'unity/Assets/Game/Runtime/Official/EntityContainers/MyContainer.cs',
            'unity/Assets/Game/Runtime/Official/StoryAttachment/AttachPosCfg.cs',
            'unity/Assets/Game/Runtime/Official/StoryFollowBone/IPlayableOverrideAttachOffset.cs')],
        limits=['Full native graph installation, runtime verification and production battle integration are separate requirements.']),ensure_ascii=False,indent=2)+'\n').encode())
    print('Complete original track classes:',len(originals))


if __name__=='__main__':build()
