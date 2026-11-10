"""Port the original movement client and its consumed shared timing calculations."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from prepare_unity_entity_transform_views import method
from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed

SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code'
NAMESPACE = 'Zjcs.Official.BattleSkillPlayables'


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise ValueError(f'Review a changed original movement branch: {old}')
    return text.replace(old, new)


def wrap(text):
    return 'namespace ' + NAMESPACE + '\n{\n' + text + '\n}\n'


def generate(destination, receipt):
    selected = json.loads((ROOT / 'official-source.json').read_text(encoding='utf-8-sig'))
    if selected['active'] != '884_156692':
        raise ValueError('Review a changed original movement source version.')
    rules_path = ROOT / 'unity/Assets/Official/Combat/battle-rules.json'
    audit_path = ROOT / 'assets/reference/battle-rules/active-entity-configuration-source-manifest.json'
    rules = json.loads(rules_path.read_bytes())['entities']
    audit = json.loads(audit_path.read_bytes())
    if audit['manifest'] != selected['active']:
        raise ValueError('Movement configuration audit must use the selected original version.')
    audited = {str(row['entity_id']): row for row in audit['rows']}
    consumed_actions = []
    for key, value in rules.items():
        if 'FightActionSkillMoveComponent' not in value:
            continue
        digest = hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True,
            separators=(',', ':')).encode('utf-8')).hexdigest()
        row = audited.get(key)
        if row is None or row['changed'] or row['installed_components_sha256'] != digest \
                or row['selected_components_sha256'] != digest:
            raise ValueError('Review movement configuration against the selected source: ' + key)
        consumed_actions.append(int(key))
    if len(consumed_actions) != 222:
        raise ValueError('Review the selected movement action membership.')
    client = SOURCE / 'client-source'
    common = SOURCE / 'common-source/Common'
    inputs = [client / 'MyFightActionSkillMove.cs', client / 'MyActionBase.cs', client / 'MyAction.cs',
              common / 'FightActionSkillMoveComponent.cs',
              common / 'FightActionSkillMoveComponentInfo.cs',
              common / 'FightMoveUtilsHandler.cs', common / 'BattleFormulaHandler.cs',
              common / 'Pos2d.cs', common / 'GridTransformComponent.cs', common / 'Direction.cs',
              common / 'ActionComponent.cs',
              common / 'ActionComponentImplement.cs', common / 'ActionComponentImplementBase.cs']
    original = {path.name: path.read_text(encoding='utf-8-sig') for path in inputs}
    position = ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeTerrain/Common/Pos2d.cs'
    position_source = position.read_text(encoding='utf-8-sig')
    for signature in ('public static float RealDistance(', 'public float RealMagnitude('):
        if method(original['Pos2d.cs'], signature) != method(position_source, signature):
            raise ValueError('Review a changed original movement distance calculation: ' + signature)

    move = original['MyFightActionSkillMove.cs']
    move = move.replace('using Common;\n', '').replace('using Framework.DependencyInjection;\n', '')
    move = move.replace('using GameFramework;\n', '')
    move = replace_once(move, '[Inject]\n\tprivate IMyPlayableManager mMyPlayableManager { get; set; }',
        'public IMyPlayableManager mMyPlayableManager { get; set; }\n'
        '\tpublic Transform EntitySlot { get; set; }\n\n'
        '\t// The battle host advances this same original timer at presentation boundaries.\n'
        '\tpublic bool ManualTiming { get; set; }\n'
        '\tpublic float RemainingMoveTime => mMoving ? Mathf.Max(0f, mMoveDuration - mMoveTimer) : float.PositiveInfinity;')
    move = replace_once(move, 'private void Update()\n\t{',
        'private void Update() { if (!ManualTiming) Advance(Time.deltaTime); }\n\n'
        '\tpublic void Advance(float deltaTime)\n\t{')
    move = replace_once(move, 'mMoveTimer += Time.deltaTime;', 'mMoveTimer += deltaTime;')
    move = move.replace('MySerializable userData', 'object userData')
    move = replace_once(move, 'BattleFormula.Instance.CalcFlipX(', 'FightMovementViewMath.CalcFlipX(')
    move = replace_once(move, 'myContainer.myEntitySlot', 'EntitySlot')
    imports = ('using Object = UnityEngine.Object;\n'
               'using Zjcs.Official.EntityLifecycle;\n'
               'using Zjcs.Official.StoryPlayables;\n'
               'using Zjcs.Official.StoryPlayableManager;\n'
               'using Zjcs.Official.World.NativeTerrain.Common;\n')
    outputs = {'MyFightActionSkillMove.cs': imports + wrap(move)}

    base = original['MyActionBase.cs'].replace('using Common;\n', '')
    base = base.replace('EcComponentInfoEmpty', 'object')
    base = base.replace('where T : EcComponent', 'where T : IEntityComponentInfoSource')
    base = replace_once(base, ' where TInfo : EcComponentInfo', '')
    outputs['MyActionBase.cs'] = ('using Object = UnityEngine.Object;\n'
        'using Zjcs.Official.EntityLifecycle;\n'
        'using Zjcs.Official.EntityActions;\n' + wrap(base))

    info = original['FightActionSkillMoveComponentInfo.cs']
    info = info.replace('using MessagePack;\n', '').replace('namespace Common;\n', '')
    info = re.sub(r'(?m)^\s*\[(?:MessagePackObject\([^\n]*|Key\([^\n]*|CheckEntityExist)\]\n', '', info)
    info = replace_once(info, ' : EcComponentInfo', '')
    outputs['FightActionSkillMoveComponentInfo.cs'] = wrap(info)

    component = original['FightActionSkillMoveComponent.cs']
    methods = '\n\n'.join(method(component, prefix) for prefix in (
        'public float GetDuration(', 'public bool IsTargetStart(', 'public float GetMoveSpeed('))
    duration = method(component, 'private float GetTotalMoveDuration(')
    # Preserve the invalid-configuration return and replace only the logging backend.
    log_start = duration.index('\t\tILogger logger = Logger;')
    log_end = duration.index('\t\treturn 0f;', log_start)
    duration = duration[:log_start] + (
        '\t\tDebug.LogError($"[Battle] ActionSkillMove {ClassId} MoveDuration:{Info.MoveDuration} MoveSpeed:{Info.MoveSpeed}");\n'
    ) + duration[log_end:]
    outputs['FightActionSkillMoveComponent.cs'] = '''using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.World.NativeTerrain.Common;
''' + wrap('''// Consumed action snapshot; original shared timing methods are copied below.
// The host owns entity creation, authoritative positions and destruction scheduling.
public sealed class FightActionSkillMoveComponent : IEntityComponentInfoSource
{
    public int ClassId;
    public List<Pos2d> Path;
    public Direction Direction;
    public Pos2d? BlockedPos;
    public FightActionSkillMoveComponentInfo Info;
    object IEntityComponentInfoSource.ComponentInfo => Info;
    private Pos2d mSourcePos;
    private float mMoveDistance, mMoveDuration;
    public float TotalMoveDuration => mMoveDuration;

    public void InitializeMovement(Pos2d source)
    {
        mSourcePos = source;
        mMoveDistance = FightMovementViewMath.CalcRealDistance(mSourcePos, Path) * 1f;
        mMoveDuration = GetTotalMoveDuration();
    }
''' + methods + '\n\n' + duration + '\n}\n')

    distance = method(original['FightMoveUtilsHandler.cs'], 'public float CalcRealDistance(')
    flip = method(original['BattleFormulaHandler.cs'], 'public bool CalcFlipX(')
    distance = replace_once(distance, 'public float ', 'public static float ')
    flip = replace_once(flip, 'public bool ', 'public static bool ')
    outputs['FightMovementViewMath.cs'] = ('''using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;
''' + wrap('internal static class FightMovementViewMath\n{\n' + distance + '\n\n' + flip + '\n}\n'))

    destination.mkdir(parents=True, exist_ok=True)
    paths = []
    for name, text in outputs.items():
        path = destination / name
        write_changed(path, '// Migrated selected original source; see skill movement runtime receipt.\n' + text)
        paths.append(path)
        if path.is_relative_to(ROOT / 'unity/Assets/Game'):
            meta(path)
            paths.append(path.with_name(path.name + '.meta'))
    if destination == ROOT / 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables':
        assembly = destination / 'Zjcs.Official.BattleSkillPlayables.asmdef'
        definition = json.loads(assembly.read_bytes())
        if 'Zjcs.Official.EntityActions' not in definition['references']:
            definition['references'].append('Zjcs.Official.EntityActions')
            write_changed(assembly, json.dumps(definition, ensure_ascii=False, indent=2) + '\n')
        paths.extend((assembly, assembly.with_name(assembly.name + '.meta')))
    bindings = []
    if destination == ROOT / 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables':
        bindings = [identity(ROOT / path) for path in (
            'unity/Assets/Game/Runtime/Official/EntityActions/MyAction.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightBattleState.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.Movement.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/IBattleMovementActionView.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMoveActionData.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMoveActionPlayback.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillEntityPlayback.Movement.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleNativeEntityLoader.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillEntityPlayback.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMovementEffects.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGraphCapabilityValidation.cs',
            'scripts/test_battle_skill_moves.ps1',
            'scripts/test_battle_skill_combo.ps1',
            'scripts/test_battle_hit_move.ps1',
            'scripts/test_battle_skill_graph_dispatch.ps1',
            'scripts/fixtures/battle_skill_graph_dispatch_harness.cs',
            'scripts/prepare_unity_map14_visibility_handlers.py',
            'scripts/inspect_battle_action_graph_bindings.py',
            'scripts/prepare_battle_skill_graph_assets.py')]
    receipt.parent.mkdir(parents=True, exist_ok=True)
    write_changed(receipt, json.dumps(dict(manifest=selected['active'],
        official_sources=[identity(path) for path in inputs],
        outputs=[identity(path) for path in paths],
        dependencies=[identity(position), identity(rules_path), identity(audit_path)],
        selected_configuration_actions=sorted(consumed_actions),
        local_action_bindings=bindings,
        generator=identity(Path(__file__)),
        conversion=['Original client lifecycle and per-path timer; explicit manager injection and optional host timing.',
            'Original shared duration, speed, distance and flip calculations; Unity logging replaces the shared logger.',
            'Unit grid rotation retains an existing native grid direction or the original new-grid Up default. Cast and action direction parameters are independent; original movement StartImpl changes FlipX without changing grid Direction.'],
        confirmed_offline_rules=[
            'Self actions snapshot the settled skill direction before path settlement, preserving configured fixed direction; ordinary approach/retreat and forced target actions snapshot the moved unit direction at action creation. FlipX is independent; legacy receipts without direction keep their fallback.',
            'For these settled paths without collision data, action lifetime is original total movement duration plus DelayDestroyTime, then original End/Remove and pooling. Path eligibility, probability, resistance and landing retain their existing configured rules; missing paths or collision information are not inferred.'],
        implementation_document='docs/features/skills.md'), ensure_ascii=False, indent=2) + '\n')
    print('Prepared original movement client, action base, configuration and timing methods:', len(outputs))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-root', type=Path, required=True)
    parser.add_argument('--receipt', type=Path, required=True)
    args = parser.parse_args()
    generate(args.output_root.resolve(), args.receipt.resolve())
