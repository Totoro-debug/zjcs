"""Record the application/shared sources of battle movement facing (no SDK inputs)."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, write_changed


def main():
    version = '884_156692'
    original = ROOT / '.tools/source-packages/current' / version / 'official-code'
    rules_path = ROOT / 'unity/Assets/Official/Combat/battle-rules.json'
    entities = json.loads(rules_path.read_bytes())['entities']
    skills = {str(value[field]) for entity in entities.values()
              if (value := entity.get('FightRoleCharacterComponent'))
              for field in ('MoveSkillId', 'BackMoveSkillId', 'FixSpeedMoveSkillId')
              if value.get(field)}
    actions = {str(move['MoveBehaviourId']) for key in skills
               for move in entities[key]['FightSkillComponent']['SourceMoveList']}
    actions.add('61771')  # Existing reverse-facing action used by the regression fixture.
    receipt = dict(
        manifest=version,
        application=[identity(original / 'client-source' / name)
                     for name in ('MyFightActionSkillMove.cs', 'MyGridTransform.cs')],
        shared=[identity(original / 'common-source/Common' / name)
                for name in ('FightActionSkillMoveComponent.cs', 'BattleFormulaHandler.cs')],
        rule_input=identity(rules_path),
        rule_version='854_156383',
        actions=[dict(id=int(key), **{field: entities[key]['FightActionSkillMoveComponent'][field]
                                     for field in ('UpdateFlipX', 'ReverseFlipX')})
                 for key in sorted(actions, key=int)],
        outputs=[identity(ROOT / path) for path in (
            'unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.Movement.cs',
            'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.GridMotion.cs')],
        integrations=[
            'unity/Assets/Game/Runtime/Official/Combat/FightBattleState.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs'],
        generator=identity(Path(__file__).resolve()),
        binding='StartImpl commits the final FlipX before action-start listeners. CalcFlipX uses grid X, preserving the previous value on vertical moves. ReverseFlipX reverses the last segment. The client temporarily faces intermediate cells toward travel, and restores authoritative FlipX before the final cell. Visual sign changes preserve the existing transform magnitudes.',
        limits=[
            'Historical rule values keep their original 854_156383 version; current application/shared code is 884_156692.',
            'Legacy checkpoints without explicit FlipX retain the existing Direction-based initial facing until a configured action commits it.',
            'Unconfigured offline retreats do not invent original facing flags.',
            'This migration does not complete native body physics, other animations or blocked effects.'])
    target = ROOT / 'assets/reference/battle-animation/movement-facing-source-manifest.json'
    write_changed(target, json.dumps(receipt, ensure_ascii=False, indent=2) + '\n')
    print(f'Recorded original movement facing: {len(actions)} configured actions.')


if __name__ == '__main__':
    main()
