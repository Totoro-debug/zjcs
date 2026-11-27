"""Bind native graph sources to their existing, independently verified conversion receipts."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed


def code_identity(path):
    record = identity(path)
    record['sha256'] = hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest()
    record['normalization'] = 'UTF-8, LF line endings'
    return record


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    directory = ROOT / 'unity/Assets/Game/Resources/Combat'
    migrated_skill_catalogs = (
        'conqueror-current-skill-graphs',
        'flowing-fire-skill-skin-current-skill-graphs',
        'conqueror-passive-current-skill-graphs',
        'hurricane-grief-current-skill-graphs',
        'fire-life-current-skill-graphs',
        'fire-core-current-skill-graphs',
        'chaos-rune-current-skill-graphs',
        'heavenly-rain-current-skill-graphs',
        'sage-psychic-ward-current-skill-graphs',
        'sage-dark-blast-current-skill-graphs',
        'sage-rock-summon-current-skill-graphs',
        'sage-summon-restoration-current-skill-graphs',
        'sage-voodoo-doll-current-skill-graphs',
        'warlock-explosion-current-skill-graphs',
        'quick-casting-current-skill-graphs',
        'light-scorch-current-skill-graphs',
        'divine-thunder-current-skill-graphs',
        'ice-guardian-current-skill-graphs',
        'wind-roar-current-skill-graphs',
        'water-vortex-current-skill-graphs',
        'meteor-fire-current-skill-graphs',
        'potential-rebirth-current-skill-graphs',
        'eclipse-slashes-current-skill-graphs',
        'asura-hand-current-skill-graphs',
        'wild-blade-siphon-current-skill-graphs',
    )
    receipts = {
        'skill-native-graphs': 'skill-native-graph-assets-source-manifest',
        'hit-native-settings': 'hit-view-runtime-source-manifest',
        'skill-entity-views': 'skill-entity-views-source-manifest',
        **{name: name + '-source-manifest' for name in migrated_skill_catalogs},
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
    index = dict(resources=['Combat/' + name for name in ('skill-native-graphs', *migrated_skill_catalogs)])
    path = directory / 'skill-native-graph-sources.json'
    if args.write:
        write_changed(path, json.dumps(index, indent=2) + '\n')
        meta(path)
    else:
        assert json.loads(path.read_bytes()) == index, 'Native catalog source index changed'
    outputs.extend((identity(path), identity(Path(str(path) + '.meta'))))
    runtime = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat'
    record = dict(inputs=inputs, outputs=outputs, generator=identity(Path(__file__)),
                  runtime=[code_identity(runtime / (name + '.cs')) for name in
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
