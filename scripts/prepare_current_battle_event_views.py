"""Record current official fight-view event sources and their local runtime adapters."""
import argparse
import json

from official_source import OfficialSource, ROOT
from prepare_battle_scene_appearance_runtime import identity, write_changed
from prepare_battle_native_graph_sources import code_identity


ORIGINALS = (
    'ECFightViewEventSourceBase', 'ECStatusInvokedEventSourceView', 'FightViewHandler',
    'FightViewHandlerBase', 'FightViewArgsConverter', 'FightViewEventArgs', 'IFightViewHandlerHost',
    'PlayPlayableWithSourceAndTargetHandler', 'PlayPlayableWithSourceAndTargetData',
    'StatusInvokedArgs', 'StatusInvokedArgsToPlayPlayableWithSourceAndTargetDataConverter',
)
PORTS = (
    'BattleSkillPlayables/FightViewEvents.cs',
    'BattleSkillPlayables/PlayPlayableWithSourceAndTargetHandler.cs',
    'BattleStatusPlayables/ECStatusInvokedEventSourceView.cs',
    'BattleStatusPlayables/StatusInvokedArgsToPlayPlayableWithSourceAndTargetDataConverter.cs',
)
ADAPTERS = (
    'unity/Assets/Game/Runtime/Offline/Combat/FightBattleStatusViewEvent.cs',
    'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleStatusPlayback.cs',
    'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAirComboPlayback.cs',
    'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs',
)


def build():
    source = OfficialSource()
    code = source.prepared_path / 'official-code'
    code_proof = code.parent / 'code-manifest.json'
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    expected = json.loads(code_proof.read_bytes())['assemblies'][assembly.name]['decoded_sha256']
    assert identity(assembly)['sha256'] == expected, 'Changed decoded official client assembly'
    originals = [identity(code / 'client-source' / (name + '.cs')) for name in ORIGINALS]
    ports = [ROOT / 'unity/Assets/Game/Runtime/Official' / relative for relative in PORTS]
    return dict(manifest=source.version, assembly=identity(assembly), code_source=identity(code_proof),
        originals=originals, runtime=[code_identity(path) for path in ports],
        metadata=[identity(path.with_name(path.name + '.meta')) for path in ports],
        adapters=[code_identity(ROOT / relative) for relative in ADAPTERS],
        dependencies=[identity(ROOT / 'assets/reference/battle-animation/current-status-ec-view-source-manifest.json'),
            identity(ROOT / 'assets/reference/battle-animation/current-battle-ec-views-source-manifest.json')],
        tools=[identity(ROOT / 'scripts/current_battle_entity_views.py'), identity(ROOT / 'scripts/prepare_current_battle_event_views.py')],
        operation='Port original pooled-argument event dispatch, converter validation, ordered handlers, error boundaries, '
            'unsubscribe and playable source/target lookup. Adapt status component access and fight-root services to local '
            'battle views; preserve original managed-reference IDs, nested handlers and source-specific airborne catalogs.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    proof = build()
    target = ROOT / 'assets/reference/battle-animation/current-battle-event-views-source-manifest.json'
    if args.write:
        write_changed(target, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    else:
        assert json.loads(target.read_bytes()) == proof, 'Current battle event source binding changed'
    print('Current official battle events verified:', len(proof['originals']), 'sources;', len(proof['runtime']), 'ports')
