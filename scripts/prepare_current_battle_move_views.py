"""Bind the current official EC movement view to the shared native action clock."""
import argparse
import hashlib
import json

from official_source import OfficialSource, ROOT
from prepare_battle_scene_appearance_runtime import identity, write_changed
from prepare_battle_native_graph_sources import code_identity
from prepare_unity_entity_transform_views import method


def build():
    source = OfficialSource()
    code = source.prepared_path / 'official-code'
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    code_proof = code.parent / 'code-manifest.json'
    assert identity(assembly)['sha256'] == json.loads(code_proof.read_bytes())['assemblies'][assembly.name]['decoded_sha256']
    runtime = ROOT / 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables'
    original_timing = code / 'common-source/Common/FightActionSkillMoveComponent.cs'
    shared_timing = runtime / 'FightActionSkillMoveComponent.cs'
    comparisons = []
    for signature in ('public float GetDuration(', 'public bool IsTargetStart(', 'public float GetMoveSpeed('):
        original = method(original_timing.read_text('utf-8-sig'), signature)
        local = method(shared_timing.read_text('utf-8-sig'), signature)
        assert original == local, ('Current official movement timing changed', signature)
        comparisons.append(dict(signature=signature, sha256=hashlib.sha256(original.encode()).hexdigest()))
    signature = 'private float GetTotalMoveDuration('
    original = method(original_timing.read_text('utf-8-sig'), signature)
    log_start = original.index('\t\tILogger logger = Logger;')
    log_end = original.index('\t\treturn 0f;', log_start)
    adapted = original[:log_start] + (
        '\t\tDebug.LogError($"[Battle] ActionSkillMove {ClassId} MoveDuration:{Info.MoveDuration} MoveSpeed:{Info.MoveSpeed}");\n'
    ) + original[log_end:]
    assert adapted == method(shared_timing.read_text('utf-8-sig'), signature), 'Current total movement duration changed'
    comparisons.append(dict(signature=signature, sha256=hashlib.sha256(original.encode()).hexdigest(),
        adaptation='Replace the logging backend; preserve both duration branches and the invalid-configuration return.'))
    ports = [runtime / (name + '.cs') for name in ('ECFightActionSkillMoveView', 'IFightActionSkillMoveView')]
    adapters = [runtime / 'MyFightActionSkillMove.cs',
        ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMoveActionData.cs',
        ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMoveActionPlayback.cs']
    return dict(manifest=source.version, assembly=identity(assembly), code_source=identity(code_proof),
        originals=[identity(code / 'client-source' / (name + '.cs')) for name in ('ECFightActionSkillMoveView', 'MyFightActionSkillMove')],
        common_source=identity(original_timing), shared_timing=code_identity(shared_timing), timing_methods=comparisons,
        runtime=[code_identity(path) for path in ports],
        metadata=[identity(path.with_name(path.name + '.meta')) for path in ports],
        adapters=[code_identity(path) for path in adapters],
        tools=[identity(ROOT / 'scripts/current_battle_entity_views.py'), identity(ROOT / 'scripts/prepare_current_battle_move_views.py')],
        operation='Port original path sequence, movement and blocked playables, timers, direction, source/target binding and '
            'transform restoration. The shared presentation clock supplies each original update delta and action lifetime; '
            'local action snapshots, entity lookup and the existing entity-slot coordinates replace server component access. Current EC and older Mono views '
            'share one clock interface; the original Mono shell delegates when its EC view is present. '
            'A local BlockedAction parameter identifies existing collision receipts without changing authored direction.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    proof = build()
    target = ROOT / 'assets/reference/battle-animation/current-battle-move-views-source-manifest.json'
    if args.write:
        write_changed(target, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    else:
        assert json.loads(target.read_bytes()) == proof, 'Current battle movement source binding changed'
    print('Current official movement view and unchanged shared timing methods verified')
