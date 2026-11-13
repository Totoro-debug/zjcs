"""Migrate the original hit presentation and its shared trajectory formula."""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
CLIENT = CODE / 'client-source'
COMMON = CODE / 'common-source/Common'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables'
RECEIPT = ROOT / 'assets/reference/battle-animation/hit-view-runtime-source-manifest.json'
HEADER = '''// Migrated official hit presentation; see hit-view-runtime source receipt.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.BattleTrackPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.BattleSkillPlayables
{
'''


def block(text, declaration):
    start = text.index(declaration)
    opening = text.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


def replace_once(text, original, replacement):
    if text.count(original) != 1:
        raise ValueError('Review original hit source adaptation: ' + original)
    return text.replace(original, replacement)


def clean(text):
    text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
    return text.replace('MySerializable userData', 'object userData').replace('object> { \n', 'object> {\n')


def main():
    selection = ROOT / 'official-source.json'
    if json.loads(selection.read_text(encoding='utf-8'))['active'] != '884_156692':
        raise ValueError('Review the selected official hit view version.')
    inputs = []
    outputs = []
    code_manifest = CODE.parent / 'code-manifest.json'
    manifest = json.loads(code_manifest.read_bytes())
    from hashlib import sha256
    if manifest['manifest'] != '884_156692':
        raise ValueError('Review the official hit source assembly version.')
    inputs.append(identity(code_manifest))
    for name in ('Assembly-CSharp.dll', 'Console.dll', 'Framework.dll', 'Framework.FirstPass.Runtime.dll', 'GameFramework.Runtime.dll'):
        assembly = CODE / 'managed/decoded' / name
        if sha256(assembly.read_bytes()).hexdigest() != manifest['assemblies'][name]['decoded_sha256']:
            raise ValueError('Official hit source assembly changed: ' + name)
        inputs.append(identity(assembly))

    loader_receipt = ROOT / 'assets/reference/official-map/map14-entity-loading-source-manifest.json'
    previous_loader = json.loads(loader_receipt.read_bytes())
    loader_compatibility = []
    for previous in previous_loader['inputs'][:6]:
        current = ROOT / previous['path'].replace('/871_156598/', '/884_156692/')
        record = identity(current)
        if record['sha256'] != previous['sha256']:
            raise ValueError('Review selected official entity loading implementation: ' + current.name)
        loader_compatibility.append(record)
    inputs.extend(loader_compatibility)
    inputs.append(identity(loader_receipt))

    # Parse the selected build's complete table using its generated constructors.
    # Do not inherit another map/version's loader flag.
    from official_tables import Constructors
    settings_input = CODE.parent / 'official-data/game_settings.g.bytes'
    from official_apk import Apk
    apk = Apk(manifest='884_156692')
    matches = [(path, pointer) for path, pointer in apk.container('config_luban')
               if path.endswith('/game_settings.g.bytes')]
    if len(matches) != 1:
        raise ValueError('Selected game settings asset is not unique.')
    settings_asset = matches[0][1].deref()
    original_bytes = settings_asset.read().m_Script.encode('utf-8', 'surrogateescape')
    if original_bytes != settings_input.read_bytes():
        raise ValueError('Selected hit loading settings differ from their official bundle.')
    constructors = Constructors(CODE)
    settings = constructors.table(settings_input.read_bytes(), 'TbGameSettings')
    inputs.extend((identity(CODE / 'global-metadata.dat'),
                   identity(ROOT / 'scripts/official_tables.py')))
    for name in sorted(constructors.sources):
        source = COMMON / (name + '.cs')
        if not source.exists() or name in ('Pos2d', 'Vector2', 'Vector3'):
            source = COMMON.parent / 'Common.LubanGen' / (name + '.cs')
        if source.exists():
            inputs.append(identity(source))
    flag = settings['ECIsUseGameObjectPool']
    if not isinstance(flag, bool):
        raise ValueError('Review original entity pool flag type.')
    settings_output = ROOT / 'unity/Assets/Game/Resources/Combat/hit-native-settings.json'
    write_changed(settings_output, json.dumps(dict(manifest='884_156692',
        ECIsUseGameObjectPool=flag), indent=2) + '\n')
    meta(settings_output)
    inputs.append(dict(**identity(settings_input), official_bundle=apk.provenance(settings_asset)))
    outputs.extend((identity(settings_output), identity(Path(str(settings_output) + '.meta'))))

    def read(path):
        inputs.append(identity(path))
        return path.read_text(encoding='utf-8-sig')

    def emit(name, body):
        path = DEST / name
        write_changed(path, HEADER + clean(body) + '\n}\n')
        meta(path)
        outputs.extend((identity(path), identity(Path(str(path) + '.meta'))))

    from prepare_unity_map14_entity_loading import diagnostics
    battle_loader = read(CLIENT / 'MyFightEntityLoadHandler.cs')
    battle_loader, _ = diagnostics(battle_loader)
    battle_loader = battle_loader[battle_loader.index('public class MyFightEntityLoadHandler'):]
    battle_loader = replace_once(battle_loader, 'MyContainer container', 'IEntityLoadContainer container')
    battle_loader_output = ROOT / 'unity/Assets/Game/Runtime/Official/EntityLoading/MyFightEntityLoadHandler.cs'
    write_changed(battle_loader_output, '// Original battle entity loading specialization.\n'
        'using System;\nnamespace Zjcs.Official.EntityLoading\n{\n' + clean(battle_loader) + '\n}\n')
    meta(battle_loader_output)
    outputs.extend((identity(battle_loader_output), identity(Path(str(battle_loader_output) + '.meta'))))
    lifecycle = read(CLIENT / 'FightLifecycleHandler.cs')
    if 'CreateInstance<MyFightEntityLoadHandler>(new object[2] { mServiceProvider, container })' not in lifecycle:
        raise ValueError('Review original battle loader construction and return-position flag.')

    for name in ('MyPtStopPlayable', 'MyPtSubPlayable'):
        trigger = read(CODE / 'game-framework-source/GameFramework' / (name + '.cs'))
        trigger = trigger[trigger.index('public class ' + name):]
        if name == 'MyPtSubPlayable':
            trigger = replace_once(trigger,
                '[Inject]\n\tprivate IMyPlayableManager mMyPlayableManager { get; set; }',
                'public IMyPlayableManager mMyPlayableManager { get; set; }')
        trigger_output = ROOT / 'unity/Assets/Game/Runtime/Official/StoryPlayableManager' / (name + '.cs')
        write_changed(trigger_output, '// Original GameFramework playable trigger.\n'
            'using UnityEngine;\nusing Zjcs.Official.StoryPlayables;\n'
            'namespace Zjcs.Official.StoryPlayableManager\n{\n' + clean(trigger) + '\n}\n')
        meta(trigger_output)
        outputs.extend((identity(trigger_output), identity(Path(str(trigger_output) + '.meta'))))

    formula = read(COMMON / 'BattleFormulaHandler.cs')
    provider = block(read(COMMON / 'IBattleFormulaHandler.cs'),
                     'public struct SkillHitBaseDelayInfoProvider')
    begin = formula.index('\tpublic float CalcSkillHitDelay(')
    end = formula.index('\tpublic bool CalcFlipX(', begin)
    methods = formula[begin:end]
    methods = methods.replace('IBattleFormulaHandler.SkillHitBaseDelayInfoProvider',
                              'SkillHitBaseDelayInfoProvider')
    methods = methods.replace('GridGroundComponent ground', 'IFightSkillViewServices ground')
    methods = methods.replace('public float CalcSkillHitDelay', 'public static float CalcSkillHitDelay')
    methods = methods.replace('public Vector3 ', 'public static Vector3 ')
    emit('FightHitViewFormula.cs', 'public static class FightHitViewFormula\n{\n'
         + provider + '\n' + methods + '}')

    text = read(CLIENT / 'MyFightHitBase.cs')
    text = text[text.index('[RequireComponent'):]
    text = replace_once(text, ': MyComponent<T, TInfo>, IFightGetUsePlayable where',
                        ': MyComponent<T, TInfo>, IFightGetUsePlayable, IFightHitViewBinding where')
    text = replace_once(text, '\t[Inject]\n\tprotected IMyPlayableManager mMyPlayableManager { get; set; }',
        '\tpublic IMyPlayableManager mMyPlayableManager { get; set; }\n'
        '\tpublic IFightHitViewServices ViewServices { get; set; }')
    text = text.replace('IBattleFormulaHandler.SkillHitBaseDelayInfoProvider',
                        'FightHitViewFormula.SkillHitBaseDelayInfoProvider')
    text = replace_once(text,
        '\tpublic FightHitViewFormula.SkillHitBaseDelayInfoProvider DelayInfoProvider;',
        '\tpublic FightHitViewFormula.SkillHitBaseDelayInfoProvider DelayInfoProvider;\n'
        '\tFightHitViewFormula.SkillHitBaseDelayInfoProvider IFightHitViewBinding.DelayInfoProvider => DelayInfoProvider;')
    text = text.replace('MySerializable userData', 'object userData')
    text = text.replace('GridGroundComponent gridGround = myGridTransform.GridGround;',
                        'IFightSkillViewServices gridGround = ViewServices;')
    text = text.replace('BattleFormula.Instance.', 'FightHitViewFormula.')
    text = text.replace('TryGetComponent<MyFightHitExtDelayInfoOverriderByStatusStack>(ref ',
                        'TryGetComponent<MyFightHitExtDelayInfoOverriderByStatusStack>(out ')
    text = replace_once(text, 'Component.GridTransform.GridGround.GridToPos(grid)',
                        'ViewServices.GridToPos(grid)')
    text = replace_once(text, 'myContainer.GetEntity(Component.SkillId)',
                        'ViewServices.GetEntity(Component.SkillId)')
    logging = block(text, '\t\telse\n\t\t{\n\t\t\tILogger logger = Logger;')
    text = text.replace(logging, '\t\telse\n\t\t{\n'
        '\t\t\tDebug.LogError($"[Fight] MyFightHitBase: start a inactive gameobject coroutine, classId:{mGridTransform.myEntity.ClassId}");\n\t\t}')
    emit('MyFightHitBase.cs', text)

    for name in ('Fixed', 'Summon', 'RandomTarget', 'CustomTarget', 'TriggerSkill', 'Chained'):
        text = read(CLIENT / ('MyFightHit' + name + '.cs'))
        text = text[text.index('public class '):]
        if name == 'Chained':
            text = replace_once(text,
                'GridGroundComponent component = myContainer.MyRootEntity.Entity.GetComponent<GridGroundComponent>();',
                'IFightSkillViewServices component = ViewServices;')
            text = replace_once(text, 'myContainer.ToWorldPos(', 'ViewServices.ToWorldPos(')
            text = text.replace('BattleFormula.Instance.', 'FightHitViewFormula.')
        emit('MyFightHit' + name + '.cs', text)

    # These client classes have no fields or method bodies. Their shared combat
    # rules remain in the executor; retain the original native prefab scripts.
    for name in ('MyFightHitExtHemophagia', 'MyFightHitTransientPropDiff',
                 'MyFightHitTransientPropUpByCharacterType', 'MyFightDamageLimit',
                 'MyFightHitExtApplyDamage'):
        text = read(CLIENT / (name + '.cs'))
        text = text[text.index('public class '):]
        if not re.fullmatch(r'public class \w+ : MyComponent<\w+, \w+>\s*\{\s*\}\s*', text):
            raise ValueError('Passive hit client class acquired behaviour: ' + name)
        text = re.sub(r'MyComponent<\w+, \w+>', 'MyComponent<IFightHitPassiveViewData>', text)
        emit(name + '.cs', text)

    for name in ('ApplySkillHitOffsetForPlayableTrackOnInit',
                 'OverridePlayableTrackByHitOnInit', 'OverridePlayableAttachOffsetByHitOnInit'):
        text = read(CLIENT / (name + '.cs'))
        text = text[text.index('public class '):]
        text = text.replace('TryGetComponent<MyFightHitFixed>(ref ',
                            'TryGetComponent<MyFightHitFixed>(out ')
        text = text.replace('\t\tGLoggerWarnInterpolatedStringHandler message;\n', '')
        text = text.replace('\t\tbool enabled;\n', '')
        text, warnings = re.subn(
            r'(?ms)^\s*(?:GLoggerWarnInterpolatedStringHandler )?message = new GLoggerWarnInterpolatedStringHandler.*?^\s*Log.Warn\([^\n]+\);',
            '\n\t\t\tDebug.LogWarning("' + name + ': invalid original playable target or type.");', text)
        expected_warnings = 2 if name.startswith('ApplySkillHitOffset') else 1
        if warnings != expected_warnings:
            raise ValueError('Review hit initializer warning diagnostics: ' + name)
        if name.startswith('ApplySkillHitOffset'):
            text, debug = re.subn(
                r'(?ms)^\s*GLoggerDebugInterpolatedStringHandler message2 = .*?^\s*Log.Debug\([^\n]+\);',
                '', text)
            if debug != 1:
                raise ValueError('Review hit initializer debug diagnostics: ' + name)
        if 'InterpolatedStringHandler' in text or 'Log.Warn(' in text or 'Log.Debug(' in text:
            raise ValueError('Unconverted original hit initializer diagnostics: ' + name)
        emit(name + '.cs', text)

    text = read(CLIENT / 'MyFightHitExtDelayInfoOverriderByStatusStack.cs')
    text = text[text.index('[RequireComponent'):]
    original_query = block(text, '\tpublic int GetOverriderIndex()')
    query = '''\tpublic int GetOverriderIndex()
\t{
\t\tMyFightSkill myFightSkill = default;
\t\tif (!TryGetComponent<MyFightSkill>(out myFightSkill)) return -1;
\t\tvar statusAgent = StatusServices.GetSourceStatusAgent(myFightSkill);
\t\tif (statusAgent == null) return -1;
\t\treturn GetStatusCount(statusAgent);
\t}'''
    text = replace_once(text, original_query, query)
    opening = text.index('{') + 1
    text = text[:opening] + '\n\tpublic IFightHitSourceStatusServices StatusServices { get; set; }\n' + text[opening:]
    text = text.replace('FightStatusAgentComponent', 'FightHitSourceStatusAgent')
    text = text.replace('FightStatusComponent', 'FightHitSourceStatus')
    emit('MyFightHitExtDelayInfoOverriderByStatusStack.cs', text)

    for name in ('FightHitBaseComponent', 'FightHitBaseComponentInfo', 'HitCfg', 'HitScopCfg',
                 'Pos2d', 'FightHitFixedComponentInfo',
                 'FightHitChainedComponent', 'FightHitChainedComponentInfo',
                 'FightHitSummonComponent', 'FightHitSummonComponentInfo',
                 'FightHitExtDelayInfoOverriderByStatusStackComponent',
                 'FightHitExtDelayInfoOverriderByStatusStackComponentInfo'):
        read(COMMON / (name + '.cs'))
    read(CLIENT / 'EFightHitSourceToMeAnchor.cs')
    emit('EFightHitSourceToMeAnchor.cs', block(
        (CLIENT / 'EFightHitSourceToMeAnchor.cs').read_text(encoding='utf-8-sig'),
        'public enum EFightHitSourceToMeAnchor'))

    # Shared data projections and the source-status query are explicit host bindings.
    bindings = DEST / 'FightHitViewBindings.cs'
    outputs.extend((identity(bindings), identity(Path(str(bindings) + '.meta'))))
    asmdef = DEST / 'Zjcs.Official.BattleSkillPlayables.asmdef'
    assembly_definition = json.loads(asmdef.read_bytes())
    for reference in ('Zjcs.Official.StoryFollowBone', 'Zjcs.Official.BattleTrackPlayables'):
        if reference not in assembly_definition['references']:
            assembly_definition['references'].append(reference)
    write_changed(asmdef, json.dumps(assembly_definition, indent=2) + '\n')
    outputs.extend((identity(asmdef), identity(Path(str(asmdef) + '.meta'))))
    local_bindings = [identity(ROOT / path) for path in (
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHitEntityPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHitReceiptPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleNativeEntityLoader.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHitEntityData.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillEntityPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/Zjcs.Presentation.Combat.asmdef',
        'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleNativePlayableScope.cs',
        'unity/Assets/Game/Runtime/Official/Combat/FightBattleState.cs',
        'unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleHitPlaybackAcceptance.cs')]
    # Existing leaf receipts also name the shared scope. Refresh only records
    # for these changed binding files, preserving unrelated session changes.
    updated_binding_hashes = {
        path: identity(ROOT / path)['sha256'] for path in (
            'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleNativePlayableScope.cs',
            'unity/Assets/Game/Runtime/Official/BattleSkillPlayables/Zjcs.Official.BattleSkillPlayables.asmdef')}

    def refresh_binding_records(value):
        if isinstance(value, dict):
            if value.get('path') in updated_binding_hashes and 'sha256' in value:
                value['sha256'] = updated_binding_hashes[value['path']]
            for child in value.values():
                refresh_binding_records(child)
        elif isinstance(value, list):
            for child in value:
                refresh_binding_records(child)

    for receipt_name in ('cast-assets-source-manifest.json',
                         'skill-afterimage-runtime-source-manifest.json',
                         'skill-camera-performance-runtime-source-manifest.json',
                         'skill-master-override-runtime-source-manifest.json',
                         'skill-view-runtime-source-manifest.json',
                         'spine-clone-runtime-source-manifest.json'):
        existing_receipt = RECEIPT.parent / receipt_name
        existing_proof = json.loads(existing_receipt.read_bytes())
        refresh_binding_records(existing_proof)
        write_changed(existing_receipt, json.dumps(existing_proof, ensure_ascii=False, indent=2) + '\n')
    write_changed(RECEIPT, json.dumps(dict(manifest='884_156692',
        official_source=identity(selection), inputs=inputs, outputs=outputs,
        selected_entity_loader_compatibility=loader_compatibility,
        local_resource_and_identity_bindings=local_bindings,
        generator=identity(Path(__file__)),
        adaptations=['Namespace and existing Unity entity/Playable types.',
                     'Explicit existing entity, local grid and world position queries.',
                     'The explicit source-status query preserves absent source/skill/status agent checks.',
                     'Original trajectory operations and branch/order retained.',
                     'Inactive coroutine error uses Unity logging.']),
        ensure_ascii=False, indent=2) + '\n')
    print('Migrated original hit presentation classes and shared trajectory formula.')


if __name__ == '__main__':
    main()
