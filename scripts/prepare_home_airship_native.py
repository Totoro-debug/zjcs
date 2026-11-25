"""Recover the current official airship furniture and its native dependencies."""
import argparse
import hashlib
import json
from pathlib import Path

from home_native_furniture_builder import install_furniture_bundle, prepare_native_cache
from official_apk import ROOT
from official_source import OfficialSource


BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_furniture_airship.bundle'
PREFABS = tuple('Prefabs/Home/Dynamic/Furniture/Airship/' + name + '.prefab' for name in (
    'Airship', 'AirshipSnow', 'Airship_YellowDuck', 'Airship_YellowDuckSnow',
))
DEST = ROOT / 'unity/Assets/Official/Home/Airship/Native'
RECEIPT = ROOT / 'assets/reference/home-airship-native-source-manifest.json'
CONTROLLER = ROOT / 'unity/Assets/Game/Runtime/Official/HomeFurniture/AirshipFurniture.cs'


def prepare():
    cache = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'home-airship-native'
    receipt = cache / 'native-export-source-manifest.json'
    export = cache / 'native-export'
    if receipt.exists() or export.exists():
        if not receipt.is_file() or not export.is_dir():
            raise ValueError('Incomplete airship native export; retain it for inspection.')
        proof = json.loads(receipt.read_text(encoding='utf8'))
        def digest(path):
            with path.open('rb') as stream:
                return hashlib.file_digest(stream, 'sha256').hexdigest()
        if (not proof.get('completed') or proof.get('manifest') != OfficialSource().version
                or proof.get('input_manifest_sha256') != digest(cache / 'source-manifest.json')
                or proof.get('code_manifest_sha256') != digest(cache.parent / 'code-manifest.json')):
            raise ValueError('Airship native export does not match its official source inputs.')
        outputs = {row['path']: row['sha256'] for row in proof['outputs']}
        if len(outputs) != len(proof['outputs']):
            raise ValueError('Duplicate paths in the airship export receipt.')
        actual = {path.relative_to(export).as_posix() for path in export.rglob('*') if path.is_file()}
        if actual != set(outputs):
            raise ValueError('Airship export files differ from the receipt.')
        for key, expected in outputs.items():
            path = (export / key).resolve()
            if not path.is_relative_to(export.resolve()) or digest(path) != expected:
                raise ValueError('Airship export file differs from its receipt: ' + key)
        source = json.loads((cache / 'source-manifest.json').read_text(encoding='utf8'))
        inputs = {row['output']: row['decoded_sha256'] for row in source['inputs']}
        for key, expected in inputs.items():
            path = (cache / key).resolve()
            if not path.is_relative_to(cache.resolve()) or digest(path) != expected:
                raise ValueError('Airship conversion input differs from its receipt: ' + key)
        for row in proof['type_dependencies']:
            path = (cache / row['output']).resolve()
            if not path.is_relative_to(cache.resolve()) or digest(path) != row['sha256']:
                raise ValueError('Airship type descriptor differs from its receipt: ' + row['output'])
        code = json.loads((cache.parent / 'code-manifest.json').read_text(encoding='utf8'))
        for name, row in code['assemblies'].items():
            path = (cache / 'inputs/Managed' / name).resolve()
            if not path.is_relative_to((cache / 'inputs/Managed').resolve()) or digest(path) != row['decoded_sha256']:
                raise ValueError('Airship managed input differs from its receipt: ' + name)
        for relative in PREFABS:
            key = 'ExportedProject/Assets/' + relative
            if key not in outputs:
                raise ValueError('Airship native prefab is missing from its export receipt: ' + key)
        print('Verified existing airship native export:', len(outputs), 'files;', len(PREFABS), 'prefabs')
        return cache
    return prepare_native_cache(cache, [BUNDLE])


def install():
    if not CONTROLLER.is_file() or not Path(str(CONTROLLER) + '.meta').is_file():
        raise FileNotFoundError('Migrate the original AirshipFurniture controller before binding the prefabs.')
    prepare()
    return install_furniture_bundle(
        cache_name='home-airship-native', bundle=BUNDLE, prefabs=PREFABS,
        dest=DEST, receipt_path=RECEIPT, guid_prefix='zjcs:home-airship-native:',
        install_prefix='native-airship-building', script_path=Path(__file__),
        extra_scripts={'AirshipFurniture': CONTROLLER},
        scope='Original airship scene prefabs and dependencies, including weather and Yellow Duck variants.',
    )


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepare-only', action='store_true', help='Convert verified source inputs without installing prefabs.')
    args = parser.parse_args()
    prepare() if args.prepare_only else install()
