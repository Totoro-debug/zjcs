"""Record original typed entity ports and the local status-graph binding adaptation."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(),
                sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    selected = json.loads((ROOT / 'official-source.json').read_text(encoding='utf-8-sig'))
    assert selected['active'] == '884_156692', 'Review a changed official source version.'
    sources = ROOT / '.tools/source-packages/current/884_156692/official-code/common-source/Common'
    names = ('EFightBlackBoardKeyValue.cs', 'FightHandleCfgTriggerSkills.cs',
             'FightHandleTriggerSkillsByStatus.cs', 'FightHandleCfgTriggerStatuss.cs',
             'FightHandleTriggerStatussByStatus.cs', 'FightHandleRemoveStatusCfg.cs',
             'FightHandleRemoveStatus.cs')
    original = [identity(sources / name) for name in names]
    runtime = ROOT / 'unity/Assets/Game/Runtime/Official/Combat/FightStatusLogicRuntime.cs'
    receipt = dict(manifest=selected['active'], official_sources=original,
                   configuration_input=identity(ROOT / 'unity/Assets/Official/Combat/battle-rules.json'),
                   migrated_output=identity(runtime),
                   adaptation='Preserve MainEntity as a unit binding. MainStatus and DamageEntity retain '
                              'their original distinct types; absent or unbound event/graph ports '
                              'cannot be reinterpreted as a unit. Unsupported removal conditions '
                              'stop the flow before mutation.',
                   implementation_document='docs/features/skills.md', generator=identity(Path(__file__)))
    target = ROOT / 'assets/reference/battle-rules/status-entity-ports-source-manifest.json'
    target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    shared_path = ROOT / 'assets/reference/battle-rules/shared-stack-source-manifest.json'
    shared = json.loads(shared_path.read_text(encoding='utf-8-sig'))
    matches = [row for row in shared['bindings'] if row['path'] == runtime.relative_to(ROOT).as_posix()]
    assert len(matches) == 1
    matches[0].update(identity(runtime))
    shared_path.write_text(json.dumps(shared, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Recorded seven selected-version port/handler sources and the status runtime binding hash.')


if __name__ == '__main__':
    main()
