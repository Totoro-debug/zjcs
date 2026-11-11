"""Migrate original damage-entity presentation and its status-stack graph override."""
import json
import re
from pathlib import Path
from hashlib import sha256

from official_apk import ROOT
from prepare_battle_hit_view_runtime import clean, replace_once
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables'
RECEIPT = ROOT / 'assets/reference/battle-animation/damage-view-runtime-source-manifest.json'
HEADER = '''// Migrated original damage view; see damage-view-runtime source receipt.
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
namespace Zjcs.Official.BattleSkillPlayables
{
'''


def main():
    manifest = CODE.parent / 'code-manifest.json'
    proof = json.loads(manifest.read_bytes())
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    if (proof['manifest'] != '884_156692'
            or json.loads((ROOT / 'official-source.json').read_bytes())['active'] != '884_156692'
            or sha256(assembly.read_bytes()).hexdigest() != proof['assemblies'][assembly.name]['decoded_sha256']):
        raise ValueError('Review original damage presentation assembly/version.')
    inputs, outputs = [identity(manifest), identity(assembly)], []
    inputs.extend(identity(CODE / 'common-source/Common' / (name + '.cs')) for name in (
        'FightDamageComponent', 'DamageResultParameter', 'DamageTypeData'))

    def read(name):
        path = CODE / 'client-source' / (name + '.cs')
        inputs.append(identity(path))
        text = path.read_text(encoding='utf-8-sig')
        return text[text.index('[RequireComponent' if name == 'MyFightDamage' else 'public class '):]

    def emit(name, text):
        path = DEST / (name + '.cs')
        write_changed(path, HEADER + clean(text) + '\n}\n')
        meta(path)
        outputs.extend((identity(path), identity(Path(str(path) + '.meta'))))

    text = read('MyFightDamage')
    text = replace_once(text,
        '[Inject]\n\tprivate IMyPlayableManager mMyPlayableManager { get; set; }',
        'public IMyPlayableManager mMyPlayableManager { get; set; }\n'
        '\tpublic IFightDamageViewServices ViewServices { get; set; }')
    text = text.replace('myContainer.GetEntity(', 'ViewServices.GetEntity(')
    text = replace_once(text, 'Object.op_Implicit((Object)(object)myTriggerStatusPlayable)',
                        'myTriggerStatusPlayable')
    text = replace_once(text, '((Component)entity).GetComponent<MyFightRoleFight>().FireDamageEvent(this);',
        'ViewServices.FireDamageEvent(entity, this);')
    text = replace_once(text, '((Component)component.Source).GetComponent<MyFightRoleFight>().FireHitEvent(this);',
        'ViewServices.FireHitEvent(component.Source, this);')
    emit('MyFightDamage', text)

    text = read('FightDamagePlayableOverrideByStatusStack')
    text = text.replace('(Object)(object)mFightDamage.myContainer == (Object)null',
                        'mFightDamage.ViewServices == null')
    text = text.replace('mFightDamage.myContainer.GetEntity(', 'mFightDamage.ViewServices.GetEntity(')
    emit('FightDamagePlayableOverrideByStatusStack', text)

    text = read('MyFightDamagePetTransferEnergyFilter')
    if not re.fullmatch(r'public class MyFightDamagePetTransferEnergyFilter : '
                       r'MyComponent<FightDamagePetTransferEnergyFilterComponent>\s*\{\s*\}\s*', text):
        raise ValueError('Review original damage pet-energy filter view behavior.')
    emit('MyFightDamagePetTransferEnergyFilter', text.replace(
        'FightDamagePetTransferEnergyFilterComponent', 'IFightHitPassiveViewData'))
    bindings = DEST / 'FightDamageViewBindings.cs'
    meta(bindings)
    outputs.extend((identity(bindings), identity(Path(str(bindings) + '.meta'))))
    local_bindings = [identity(ROOT / path) for path in (
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleDamageEntityData.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleDamageEntityPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleNativeEntityLoader.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleDamagePlaybackAcceptance.cs',
        'scripts/inspect_battle_damage_graph_bindings.py',
        'scripts/prepare_battle_damage_playback_validation.py')]
    write_changed(RECEIPT, json.dumps(dict(manifest='884_156692', inputs=inputs, outputs=outputs,
        generator=identity(Path(__file__)), local_bindings=local_bindings, adaptations=[
            'Consumed shared damage state only; no server damage or status-trigger result is inferred.',
            'Explicit original entity lookups and target/source role notification boundary.',
            'Original common, block-before-crit, fallback and independent status-trigger graph order retained.',
            'Original status-stack override cache, lifecycle and five graph fields retained.',
            'Original pet-energy-filter client class has no view behavior.']),
        ensure_ascii=False, indent=2) + '\n')
    print('Migrated original damage entity presentation and status-stack override.')


if __name__ == '__main__':
    main()
