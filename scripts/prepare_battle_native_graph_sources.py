"""Bind native graph sources to their existing, independently verified conversion receipts."""
import argparse
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    directory = ROOT / 'unity/Assets/Game/Resources/Combat'
    receipts = {
        'skill-native-graphs': 'skill-native-graph-assets-source-manifest',
        'hit-native-settings': 'hit-view-runtime-source-manifest',
        'skill-entity-views': 'skill-entity-views-source-manifest',
        'conqueror-current-skill-graphs': 'conqueror-current-skill-graphs-source-manifest',
        'conqueror-passive-current-skill-graphs': 'conqueror-passive-current-skill-graphs-source-manifest',
    }
    inputs, outputs = [], []
    for name, receipt in receipts.items():
        path = directory / (name + '.json')
        proof_path = ROOT / 'assets/reference/battle-animation' / (receipt + '.json')
        proof = json.loads(proof_path.read_bytes())
        inputs.extend((identity(path), identity(proof_path)))
        assert identity(path) in proof['outputs'], 'Native catalog differs from its conversion receipt: ' + name
        data = json.loads(path.read_bytes())
        assert data['manifest'] == proof['manifest'], 'Mixed native catalog source: ' + name
        expected = dict(manifest=data['manifest'], output_sha256=identity(path)['sha256'])
        target = directory / (name + '-source-manifest.json')
        if args.write:
            write_changed(target, json.dumps(expected, indent=2) + '\n')
            meta(target)
        else:
            assert json.loads(target.read_bytes()) == expected, 'Native catalog source metadata changed: ' + name
        outputs.extend((identity(target), identity(Path(str(target) + '.meta'))))
    index = dict(resources=['Combat/skill-native-graphs', 'Combat/conqueror-current-skill-graphs',
                            'Combat/conqueror-passive-current-skill-graphs'])
    path = directory / 'skill-native-graph-sources.json'
    if args.write:
        write_changed(path, json.dumps(index, indent=2) + '\n')
        meta(path)
    else:
        assert json.loads(path.read_bytes()) == index, 'Native catalog source index changed'
    outputs.extend((identity(path), identity(Path(str(path) + '.meta'))))
    runtime = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat'
    record = dict(inputs=inputs, outputs=outputs, generator=identity(Path(__file__)),
                  runtime=[identity(runtime / (name + '.cs')) for name in
                           ('OfficialBattleNativeGraphSources', 'OfficialBattleNativeEntityLoader',
                            'OfficialBattleSkillGraphCatalog', 'OfficialBattleSkillEntityViewCatalog')],
                  operation='Verify each original native payload against its existing conversion receipt; '
                            'load source-consistent SHA256-verified catalogs and replace only declared migrated entities. '
                            'Reject duplicate sources and overlapping migrations without a source-version feature gate.')
    receipt = ROOT / 'assets/reference/battle-animation/native-graph-sources-runtime-source-manifest.json'
    if args.write:
        write_changed(receipt, json.dumps(record, indent=2) + '\n')
    else:
        assert json.loads(receipt.read_bytes()) == record, 'Native graph runtime bindings changed'
    print('Native graph sources verified against original conversion receipts.')


if __name__ == '__main__':
    main()
