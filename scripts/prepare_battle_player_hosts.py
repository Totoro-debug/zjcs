"""Record the explicit native-player bindings supplied by battle hosts."""
from hashlib import sha256
from pathlib import Path
import argparse
import json

ROOT = Path(__file__).resolve().parents[1]
PRESENTATION = 'unity/Assets/Game/Runtime/Presentation/'
HOSTS = [PRESENTATION + 'Home/' + name + 'BattleRunner.cs' for name in (
    'Arena', 'DailyDungeon', 'FantasyTower', 'SkillDamageTest', 'WeeklyChallenge')]
HOSTS += [PRESENTATION + 'World/Mainline/OfficialMainlineMapRuntime.' + suffix + '.cs'
          for suffix in ('Battle', 'BattleScene')]
VALIDATION = ['unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance' + suffix + '.cs'
              for suffix in ('', '.Presentation')]
VALIDATION += [PRESENTATION + 'Home/SkillDamageTestValidation.cs']
VALIDATION += ['unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs']
RUNTIME = [PRESENTATION + 'Combat/' + name + '.cs' for name in (
    'OfficialBattleSceneView', 'OfficialBattleRuntimeController')]
RUNTIME += ['unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionAppearanceBindings.cs']


def identity(name, root):
    return dict(path=name, sha256=sha256((root / name).read_bytes()).hexdigest())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--runtime-root', type=Path, default=ROOT,
                        help='Exact source projection being committed.')
    args = parser.parse_args()
    for name in HOSTS:
        source = (args.runtime_root / name).read_text(encoding='utf-8')
        if name.endswith('.BattleScene.cs'):
            if 'child == avatar' in source:
                raise ValueError('The map avatar must be suspended with the world: ' + name)
        elif 'new OfficialBattlePlayerAppearanceSource(1, 1,' not in source:
            raise ValueError('Missing explicit native player profile binding: ' + name)
        elif 'AttachUnitTransform(1,' in source:
            raise ValueError('The host still borrows its map avatar: ' + name)
    proof = dict(manifest='884_156692',
        inputs=[identity('assets/reference/battle-animation/' + name + '-source-manifest.json', ROOT)
                for name in ('player-scene-dispatch', 'player-native-view')],
        hosts=[identity(name, args.runtime_root) for name in HOSTS],
        runtime=[identity(name, args.runtime_root) for name in RUNTIME],
        validation=[identity(name, args.runtime_root) for name in VALIDATION],
        generator=identity(Path(__file__).relative_to(ROOT).as_posix(), ROOT),
        operation='The five home battle hosts and mainline host explicitly supply the selected '
            'local player profile and official item-type lookup to the shared native scene. '
            'The scene owns a separate original 3100 actor, resources, animation and HP lifecycle. '
            'Home/map avatars stay in their original hierarchy and are suspended/restored with '
            'the world. Tower foreground/background changes do not reparent the map avatar '
            'or play its home portal animation.',
        limits=['This binding does not complete original player HUD/fade or every skill/status graph.',
            'Arena opponent appearance remains a separate host binding.',
            'Runtime validation and remaining presentation gaps are recorded in battle-execution.md.',
            'No Windows Player release is implied by this provenance record.'])
    target = ROOT / 'assets/reference/battle-animation/player-hosts-source-manifest.json'
    target.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()
