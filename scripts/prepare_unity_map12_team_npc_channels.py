"""Record the independent native NPC local animation channels and their exact inputs."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_unity_map12_sources import encode, sha, source_path
from prepare_unity_map12_team_npc_skeletons import CATALOG, RECEIPT as SOURCE_RECEIPT

RECEIPT = ROOT / 'assets/reference/official-map/map12-team-npc-channels-source-manifest.json'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
EDITOR = ROOT / 'unity/Assets/Game/Editor/ProfessionUI'


def build(write=False):
    source_receipt = json.loads(SOURCE_RECEIPT.read_bytes())
    source_record, = [row for row in source_receipt['outputs'] if row['path'] == source_path(CATALOG)]
    if sha(CATALOG.read_bytes()) != source_record['sha256']:
        raise ValueError('Original NPC source catalog changed')
    catalog = json.loads(CATALOG.read_bytes())
    inputs, types = [], {}
    animations = 0
    for model in catalog['skeletons']:
        row = model['decoded']
        raw = (ROOT / row['path']).read_bytes()
        if sha(raw) != row['sha256']:
            raise ValueError('Original NPC animation input differs: ' + row['path'])
        data = json.loads(gzip.decompress(raw))
        animations += len(data['animations'])
        for animation in data['animations'].values():
            for group in ('bones', 'slots'):
                for channels in animation.get(group, {}).values():
                    for kind in channels:
                        key = group + ':' + kind
                        types[key] = types.get(key, 0) + 1
        inputs.append(dict(identity=model['identity'], **row, decodedSha256=model['decodedSha256']))
    if len(inputs) != 34 or animations != 1952 or types.get('bones:inherit') != 27 or types.get('slots:rgba') != 1327:
        raise ValueError('Original NPC local animation inventory differs')
    outputs = {}
    for path in (RUNTIME / 'ProfessionNativeAnimationChannels.cs', EDITOR / 'Map12NpcAnimationValidation.cs'):
        outputs[Path(str(path) + '.meta')] = ('fileFormatVersion: 2\nguid: ' +
            hashlib.md5(('zjcs:map12:npc-local-channels:' + source_path(path)).encode()).hexdigest() +
            '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n'
            '  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()

    def record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))

    outputs[RECEIPT] = encode(dict(format=1, manifest=catalog['manifest'], mapClassId=12,
        sourceCatalog=record(CATALOG), sourceReceipt=record(SOURCE_RECEIPT), inputs=inputs,
        tools=[record(Path(__file__))],
        runtime=[record(RUNTIME / name) for name in ('ProfessionNativeAnimationChannels.cs',
            'ProfessionNativeBonePose.cs', 'ProfessionNativeCurve.cs', 'ProfessionNativeSkeletonData.cs')],
        fixtures=[record(EDITOR / name) for name in ('Map12NpcAnimationValidation.cs', 'Map12NpcDataValidation.cs',
            'ProfessionNativeCurveValidation.cs', 'ProfessionNativeBonePoseValidation.cs')],
        outputs=[dict(path=source_path(path), sha256=sha(raw)) for path, raw in sorted(outputs.items())],
        counts=dict(models=len(inputs), animations=animations, sourceChannels=types),
        operation='Independently evaluate source rotation, paired/single-axis translation/scale/shear, '
                  'discrete inheritance, attachment/draw order and RGBA channels from the complete decoded graph. '
                  'Retain absolute cubic handles, split cubic time at derivative roots and invert the first '
                  'source-time crossing without baking animation frames or using a skeleton SDK. '
                  'Preserve constraint/deform source fields for their separate evaluation stages.',
        validatedScope='DX12 Unity Play Mode verified all 1952 original NPC animations, 168760 scalar bone tracks, '
                       '27 inheritance tracks, 1327 RGBA tracks and 492171 scalar samples. First/last source keys '
                       'and independent crossed/vertical/extrapolating cubic roots passed. Existing male/female '
                       'regressions passed 1171 curves/19537 samples and 3542 bones/425108 scalar checks.',
        limitations=['Local channels alone do not verify ordered constraints, deformation, mixing, rendered NPCs, '
                     'formal invitation, party save, battle or Windows publication. '
                     'Cubic polynomial checks do not claim original SDK sampled-interpolation equivalence.']))
    for path, raw in outputs.items():
        if write:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        elif not path.is_file() or path.read_bytes() != raw:
            raise ValueError('Native NPC local-channel drift: ' + source_path(path))
    print('Original NPC local channels verified:', animations, 'animations', types, flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
