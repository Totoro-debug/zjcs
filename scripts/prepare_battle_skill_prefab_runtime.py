"""Migrate original additional skill-prefab roles and status-stack overrides."""
import json
import re
from hashlib import sha256
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_hit_view_runtime import replace_once

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables'
RECEIPT = ROOT / 'assets/reference/battle-animation/skill-prefab-runtime-source-manifest.json'
PASSIVE = ('MyFightSkillExtSkill', 'MyFightSkillHitCfgOverrideByStatusStack',
           'MyFightSkillAIRandomTargetFallbackSpawner', 'MyFightSkillBehAttachOperate',
           'MyFightSkillBehAutoRemove', 'MyFightSkillCombo', 'MyFightSkillReplay',
           'MyFightSkillLogic')
HEADER = '''// Migrated original skill-prefab role; see skill-prefab-runtime source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.BattleSkillPlayables
{
'''


def main():
    receipt_paths = sorted(path for folder in ('battle-animation', 'battle-hud')
        for path in (RECEIPT.parent.parent / folder).glob('*.json'))
    receipt_hashes = {path.relative_to(ROOT).as_posix(): identity(path)['sha256']
        for path in receipt_paths}
    receipt_versions = {path: {digest} for path, digest in receipt_hashes.items()}
    selection = ROOT / 'official-source.json'
    if json.loads(selection.read_bytes())['active'] != '884_156692':
        raise ValueError('Review the selected original skill prefab version.')
    code_manifest = CODE.parent / 'code-manifest.json'
    manifest = json.loads(code_manifest.read_bytes())
    if manifest['manifest'] != '884_156692':
        raise ValueError('Review the original skill source assembly version.')
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    if sha256(assembly.read_bytes()).hexdigest() != manifest['assemblies'][assembly.name]['decoded_sha256']:
        raise ValueError('Original skill source assembly changed.')
    inputs = [identity(code_manifest), identity(assembly)]
    outputs = []

    def read(name):
        source = CODE / 'client-source' / (name + '.cs')
        inputs.append(identity(source))
        return source.read_text(encoding='utf-8-sig')

    def emit(name, body):
        path = DEST / (name + '.cs')
        write_changed(path, HEADER + body + '\n}\n')
        meta(path)
        outputs.extend((identity(path), identity(Path(str(path) + '.meta'))))

    for name in PASSIVE:
        source = read(name)
        match = re.search(r'public class ' + name + r' : MyComponent<[^>]+>\s*\{\s*\}', source)
        if match is None or source[match.end():].strip():
            raise ValueError('Review additional original skill role behavior: ' + name)
        body = re.sub(r'MyComponent<[^>]+>', 'MyComponent<IFightSkillPassiveViewData>', match[0])
        emit(name, body)

    for name, declaration in (
            ('MyPlayableOverrider', 'public class MyPlayableOverrider'),
            ('PlayableOverrideInfo', '[Serializable]'),
            ('IPlyableOverridSetter', 'public interface IPlyableOverridSetter'),
            ('FightSkillPlayableOverrideByStatusStack', 'public class FightSkillPlayableOverrideByStatusStack')):
        source = read(name)
        emit(name, source[source.index(declaration):])

    source = read('PlayableOverrideByStatusStack')
    source = source[source.index('public abstract class PlayableOverrideByStatusStack'):]
    opening = source.index('{') + 1
    source = source[:opening] + '\n\tpublic IFightPlayableOverrideStatusServices StatusServices { get; set; }\n' + source[opening:]
    source = replace_once(source,
        'MyFightStatusAgent component = ((Component)source).GetComponent<MyFightStatusAgent>();',
        'FightHitSourceStatusAgent component = StatusServices.GetSourceStatusAgent(source);')
    source = replace_once(source, 'if ((Object)(object)component == (Object)null)', 'if (component == null)')
    source = replace_once(source, 'component.Component.ApplyStatusDict', 'component.ApplyStatusDict')
    source = source.replace('FightStatusComponent', 'FightHitSourceStatus')
    emit('PlayableOverrideByStatusStack', source)

    bindings = DEST / 'FightSkillPrefabViewBindings.cs'
    if not bindings.is_file():
        raise FileNotFoundError('Bind original source-status queries explicitly: ' + str(bindings))
    outputs.extend((identity(bindings), identity(Path(str(bindings) + '.meta'))))
    # Shared presentation bindings are also named by the existing leaf/HUD
    # receipts. Refresh only these local files, leaving official inputs intact.
    binding_paths = (
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillEntityPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/Zjcs.Presentation.Combat.asmdef',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/Zjcs.Editor.Combat.AnimatorPlayback.asmdef',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleHitPlaybackAcceptance.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SkillSlotLifecycle.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.ProductionSkillViews.cs',
        'unity/Assets/Game/Editor/OfficialBattlePetHudAcceptance.cs')
    hashes = {path: identity(ROOT / path)['sha256'] for path in binding_paths}

    def refresh(value):
        changed = False
        if isinstance(value, dict):
            expected = hashes.get(value.get('path'))
            if expected is not None and 'sha256' in value and value['sha256'] != expected:
                value['sha256'] = expected
                changed = True
            for child in value.values():
                changed = refresh(child) or changed
        elif isinstance(value, list):
            for child in value:
                changed = refresh(child) or changed
        return changed

    for folder in ('battle-animation', 'battle-hud'):
        for path in sorted((RECEIPT.parent.parent / folder).glob('*.json')):
            if path == RECEIPT or path.name == 'skill-native-graph-assets-source-manifest.json':
                continue
            proof = json.loads(path.read_bytes())
            if refresh(proof):
                write_changed(path, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')

    write_changed(RECEIPT, json.dumps(dict(
        manifest='884_156692', official_source=identity(selection), inputs=inputs,
        outputs=outputs, generator=identity(Path(__file__)),
        local_resource_and_identity_bindings=[identity(ROOT / path) for path in (
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillEntityPlayback.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleNativeEntityLoader.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillPrefabData.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillSourceStatusSnapshot.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/Zjcs.Presentation.Combat.asmdef',
            'unity/Assets/Game/Runtime/Official/Combat/FightBattleSkillCastPresentation.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleSkillPrefabPlaybackAcceptance.cs')],
        adaptations=['Existing entity lifecycle namespaces and Unity types.',
            'Empty original client roles retain their original absence of view behavior.',
            'Explicit original source-status-agent query; absent source and agent checks retained.',
            'Original stack lookup, cached override, enable/disable restoration and direction refresh order retained.']),
        ensure_ascii=False, indent=2) + '\n')
    # Keep the dependent receipt links consistent with these local binding
    # changes. A link must name the identity captured before this run; official
    # input identities and unrelated pre-existing discrepancies stay untouched.
    for unused in range(len(receipt_paths) + 1):
        changes = {}
        for path, versions in receipt_versions.items():
            current = identity(ROOT / path)['sha256']
            versions.add(current)
            if current != receipt_hashes[path] or len(versions) > 1:
                changes[path] = (versions, current)
        def refresh_links(value):
            changed = False
            if isinstance(value, dict):
                pair = changes.get(value.get('path'))
                if pair and value.get('sha256') in pair[0] and value['sha256'] != pair[1]:
                    value['sha256'] = pair[1]
                    changed = True
                for child in value.values():
                    changed = refresh_links(child) or changed
            elif isinstance(value, list):
                for child in value:
                    changed = refresh_links(child) or changed
            return changed
        updated = False
        for path in receipt_paths:
            proof = json.loads(path.read_bytes())
            if refresh_links(proof):
                write_changed(path, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
                updated = True
        if not updated:
            break
    else:
        raise ValueError('Cyclic battle receipt dependencies require review.')
    print('Migrated eight original passive skill roles and original status-stack playable override classes.')


if __name__ == '__main__':
    main()
