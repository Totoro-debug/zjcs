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
    if (proof['manifest'] != '884_156692'
            or json.loads((ROOT / 'official-source.json').read_bytes())['active'] != '884_156692'):
        raise ValueError('Review original damage presentation assembly/version.')
    inputs, outputs = [identity(manifest)], []
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        assembly = CODE / 'managed/decoded' / name
        if sha256(assembly.read_bytes()).hexdigest() != proof['assemblies'][name]['decoded_sha256']:
            raise ValueError('Review original damage presentation assembly: ' + name)
        inputs.append(identity(assembly))
    inputs.extend(identity(CODE / 'common-source/Common' / (name + '.cs')) for name in (
        'FightDamageComponent', 'DamageResultParameter', 'DamageTypeData', 'FightRoleFightComponent'))

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

    # Keep the complete original result value, enum values and helper formula.
    # Wire serialization is not used by the local view projection.
    for name, declaration in (
            ('DamageResultParameter', 'public struct '), ('DamageTypeData', 'public struct '),
            ('DamageResultType', 'public enum '), ('DamageResultValueType', 'public enum '),
            ('ElementType', 'public enum ')):
        path = CODE / 'common-source/Common' / (name + '.cs')
        if not any(row['path'] == path.relative_to(ROOT).as_posix() for row in inputs):
            inputs.append(identity(path))
        text = path.read_text(encoding='utf-8-sig')
        text = text[text.index(declaration):]
        text = re.sub(r'^\s*\[(?:Key\(\d+\)|IgnoreMember|JsonIgnore)\]\s*\n', '', text, flags=re.M)
        if name == 'DamageResultParameter':
            text = replace_once(text, 'public Pos2d Pos;',
                'public Zjcs.Official.World.NativeTerrain.Common.Pos2d Pos;')
        text = '\n'.join(line.rstrip() for line in text.splitlines()) + '\n'
        emit(name, text)

    text = read('MyFightDamage')
    text = replace_once(text,
        '[Inject]\n\tprivate IMyPlayableManager mMyPlayableManager { get; set; }',
        'public IMyPlayableManager mMyPlayableManager { get; set; }\n'
        '\tpublic IFightSkillViewServices ViewServices { get; set; }')
    text = text.replace('myContainer.GetEntity(', 'ViewServices.GetEntity(')
    text = replace_once(text, 'Object.op_Implicit((Object)(object)myTriggerStatusPlayable)',
                        'myTriggerStatusPlayable')
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

    text = read('MyFightRoleFight')
    text = replace_once(text, 'MyComponent<FightRoleFightComponent>', 'MyComponent<BattleRoleFightViewData>')
    # This client view reads no shared role fields. Retain its actual events,
    # multicast invocation and catch boundaries; replace only structured logging.
    text = text.replace('Action<', 'System.Action<').replace('Exception ex', 'System.Exception ex')
    text, count = re.subn(r'catch \(System.Exception ex\)\s*\{.*?\n\t\t\}',
        'catch (System.Exception ex)\n\t\t{\n\t\t\tDebug.LogException(ex);\n\t\t}', text, flags=re.S)
    if count != 2:
        raise ValueError('Review original role damage/hit exception boundaries.')
    emit('MyFightRoleFight', text)
    bindings = DEST / 'FightDamageViewBindings.cs'
    meta(bindings)
    outputs.extend((identity(bindings), identity(Path(str(bindings) + '.meta'))))
    local_bindings = [identity(ROOT / path) for path in (
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleDamageEntityData.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleDamageEntityPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleDamageReceiptPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleActorMaterialBinding.cs',
        'assets/reference/battle-animation/actor-material-bindings-source-manifest.json',
        'unity/Assets/Game/Resources/Combat/actor-material-bindings.json',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillEntityPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleNativeEntityLoader.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleDamagePlaybackAcceptance.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleDamageReceiptAcceptance.cs',
        'scripts/inspect_battle_damage_graph_bindings.py',
        'scripts/prepare_battle_damage_playback_validation.py')]
    write_changed(RECEIPT, json.dumps(dict(manifest='884_156692', inputs=inputs, outputs=outputs,
        generator=identity(Path(__file__)), local_bindings=local_bindings, adaptations=[
            'Consumed shared damage state only; no server damage or status-trigger result is inferred.',
            'Shared skill-view entity lookup; original target/source role notifications called directly.',
            'Original common, block-before-crit, fallback and independent status-trigger graph order retained.',
            'Original status-stack override cache, lifecycle and five graph fields retained.',
            'Original role damage/hit events, subscriber order and exception isolation retained; Unity logging port.',
            'Complete original damage result, flags, enum values and GetDamageByType; wire annotations omitted.',
            'Original pet-energy-filter client class has no view behavior.']),
        ensure_ascii=False, indent=2) + '\n')
    # Native prefabs and their script GUIDs are unchanged. Refresh the migrated
    # code identities and this linked proof, retaining all original/asset evidence.
    graph_receipt = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    if graph_receipt.is_file():
        graph = json.loads(graph_receipt.read_bytes())
        if graph['manifest'] != '884_156692':
            raise ValueError('Review damage code binding in the native graph receipt.')
        emitted = {row['path'] for row in outputs if row['path'].endswith('.cs')}
        originals = {row['path']: row['sha256'] for row in inputs}
        for row in graph['application']:
            if row.get('migrated', {}).get('path') not in emitted:
                continue
            original = row['original']
            if originals.get(original['path']) != original['sha256']:
                raise ValueError('Original damage script identity differs from its native binding.')
            sidecar = row['migrated_meta']
            if identity(ROOT / sidecar['path'])['sha256'] != sidecar['sha256']:
                raise ValueError('Damage script GUID binding changed; regenerate native assets explicitly.')
            row['migrated'] = identity(ROOT / row['migrated']['path'])
        for row in graph['runtime_receipts']:
            if row['path'] == RECEIPT.relative_to(ROOT).as_posix():
                row.update(identity(RECEIPT))
        write_changed(graph_receipt, json.dumps(graph, ensure_ascii=False, indent=2) + '\n')
    print('Migrated original damage views, status override and role notifications.')


if __name__ == '__main__':
    main()
