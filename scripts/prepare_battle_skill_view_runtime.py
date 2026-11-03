"""Port complete selected-client skill views; keep shared state as explicit input."""
import json
import re
import struct
from pathlib import Path
from hashlib import sha256
from Crypto.Hash import MD4
from official_apk import ROOT
from prepare_battle_status_playable_runtime import CODE, identity, write
from prepare_unity_story_sound_runtime import runtime_meta

OUTPUT = ROOT/'unity/Assets/Game/Runtime/Official/BattleSkillPlayables'
RECEIPT = ROOT/'assets/reference/battle-animation/skill-view-runtime-source-manifest.json'


def normalize(text):
    return re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', text))


def build():
    manifest = json.loads((CODE.parent/'code-manifest.json').read_bytes())
    assembly = CODE/'managed/decoded/Assembly-CSharp.dll'
    assert manifest['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == manifest['assemblies'][assembly.name]['decoded_sha256']
    outputs, originals = [], []
    for name in ('MyFightSkill', 'MyFightSkillAgent', 'IFightGetUsePlayable'):
        source = CODE/'client-source'/('Common/' if name.startswith('I') else '')/(name+'.cs')
        original = source.read_text(encoding='utf-8-sig')
        start = re.search(r'(?m)^(?:\[RequireComponent|public (?:class|interface))', original).start()
        body = original[start:]
        replacements = []
        if name == 'MyFightSkill':
            replacements = [('myContainer.GetEntity(', 'ViewServices.GetEntity('),
                ('myContainer.MyRootEntity.Entity.GetComponent<GridGroundComponent>().GridToPos(', 'ViewServices.GridToPos('),
                ('MySerializable userData', 'object userData'),
                ('\t[Inject]\n', ''), ('\t[Label("显示技能重播")]\n', '')]
            for before, after in replacements:
                assert before in body, before
                body = body.replace(before, after)
        restored = body
        for before, after in reversed(replacements):
            if after: restored = restored.replace(after, before)
        expected = original[start:].replace('\t[Inject]\n', '').replace('\t[Label("显示技能重播")]\n', '')
        assert normalize(restored) == normalize(expected), name
        if name == 'MyFightSkill':
            needle = 'public IMyPlayableManager myPlayableManager { get; set; }'
            assert body.count(needle) == 1
            body = body.replace(needle, needle+'\n\n\t// Explicit original entity lookup and root-grid query boundary.\n\tpublic IFightSkillViewServices ViewServices { get; set; }')
        body = re.sub(r'(?m)^\s*//IL_[^\n]*\n', '', body)
        text = ('// Complete selected official client source; see skill-view runtime receipt.\n'
            'using System;\nusing System.Collections.Generic;\nusing UnityEngine;\n'
            'using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityLifecycle;\n'
            'using Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.StoryPlayableManager;\n'
            'using Zjcs.Official.World.NativeTerrain.Common;\n'
            'namespace Zjcs.Official.BattleSkillPlayables\n{\n'+body+'\n}\n')
        target = OUTPUT/(name+'.cs')
        write(target, ('\n'.join(line.rstrip() for line in text.splitlines())+'\n').encode())
        runtime_meta(target)
        outputs += [target, Path(str(target)+'.meta')]
        entry = dict(original=identity(source), migrated=identity(target),
            whole_class_normalized_sha256=sha256(normalize(expected if name == 'MyFightSkill' else original[start:]).encode()).hexdigest(),
            replacements=[dict(original=a, migrated=b) for a,b in replacements],
            comparison='Complete class compared after reversing explicit query boundary and object payload adaptation; Inject/Label attributes removed. Serialized fields and every method retained. MyFightSkill adds only an explicit ViewServices injection property.')
        if not name.startswith('I'):
            entry['original_mono_script_file_id'] = struct.unpack('<i', MD4.new(('s\0\0\0'+name).encode()).digest()[:4])[0]
        originals.append(entry)
    asmdef = OUTPUT/'Zjcs.Official.BattleSkillPlayables.asmdef'
    write(asmdef, (json.dumps(dict(name='Zjcs.Official.BattleSkillPlayables', references=[
        'Zjcs.Official.EntityLifecycle', 'Zjcs.Official.EntityViews',
        'Zjcs.Official.StoryPlayables', 'Zjcs.Official.StoryPlayableManager',
        'Zjcs.Official.World.NativeTerrain']), indent=2)+'\n').encode())
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    boundary = OUTPUT/'FightSkillViewBindings.cs'; runtime_meta(boundary)
    outputs += [asmdef, Path(str(asmdef)+'.meta'), Path(str(OUTPUT)+'.meta'), boundary, Path(str(boundary)+'.meta')]
    write(RECEIPT, (json.dumps(dict(manifest=manifest['manifest'], assembly=identity(assembly),
        code_extraction=identity(CODE.parent/'code-manifest.json'), originals=originals,
        generator=identity(Path(__file__)), outputs=[identity(p) for p in outputs],
        verification_tools=[identity(ROOT/p) for p in (
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SkillViews.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.Casts.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/Zjcs.Editor.Combat.AnimatorPlayback.asmdef')],
        dependencies=[identity(ROOT/p) for p in (
            'unity/Assets/Game/Runtime/Official/EntityLifecycle/MyComponent.cs',
            'unity/Assets/Game/Runtime/Official/EntityLifecycle/MyEntity.cs',
            'unity/Assets/Game/Runtime/Official/EntityLifecycle/MyGridTransform.cs',
            'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleNativePlayableScope.cs',
            'unity/Assets/Game/Runtime/Official/StoryPlayableManager/IMyPlayableManager.cs')],
        consumed_shared_contracts=[identity(CODE/'common-source/Common'/name) for name in (
            'FightSkillComponent.cs', 'FightSkillComponentInfo.cs', 'FightSkillAgentComponent.cs')],
        boundary='Abstract shared component projections only. Entity lookup, root GridToPos, component info and interruption notifications must be supplied by an explicit host. No server lifecycle or combat rules are inferred.',
        limits=['Formal battle ECS/timeline installation and production sound backend remain separate integration requirements.']),
        ensure_ascii=False, indent=2)+'\n').encode())
    print('Complete original skill-view classes:', len(originals))


if __name__ == '__main__':
    build()
