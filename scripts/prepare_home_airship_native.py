"""Recover the current official airship furniture and its native dependencies."""
import argparse
import hashlib
import json
import re
from pathlib import Path

import home_native_furniture_builder as furniture_builder
from home_native_furniture_builder import install_furniture_bundle, prepare_native_cache
from official_apk import ROOT
from official_source import OfficialSource
from prepare_partner_card_native import guid


BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_furniture_airship.bundle'
PREFABS = tuple('Prefabs/Home/Dynamic/Furniture/Airship/' + name + '.prefab' for name in (
    'Airship', 'AirshipSnow', 'Airship_YellowDuck', 'Airship_YellowDuckSnow',
))
DEST = ROOT / 'unity/Assets/Official/Home/Airship/Native'
RECEIPT = ROOT / 'assets/reference/home-airship-native-source-manifest.json'
CONTROLLER = ROOT / 'unity/Assets/Game/Runtime/Official/HomeFurniture/AirshipFurniture.cs'
SCROLL = ROOT / 'unity/Assets/Game/Runtime/Official/HomeFurniture/HomeScrollTextBord.cs'
BANNER = ROOT / 'unity/Assets/Game/Runtime/Official/HomeFurniture/AirShipBanner.cs'
CUSTOM_MATERIALS = ROOT / 'unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonRendererCustomMaterials/SkeletonRendererCustomMaterials.cs'
SLOT_COLOR = ROOT / 'unity/Assets/Game/Runtime/Official/HomeFurniture/SkeletonSlotColor.cs'
RENDER_MESH = ROOT / 'unity/Assets/Game/Runtime/Official/HomeFurniture/RenderExistingMesh.cs'


class AirshipFurnitureBinding(furniture_builder.HomeNativeFurnitureBinding):
    def build_furniture_prefabs(self, bundle, prefab_relatives, dest, **kwargs):
        # LanguageUtils.GetAirshipItemBg selects this sprite dynamically; it is
        # not referenced by the repaired prefab's serialized object graph.
        native = self.native / 'Image/Home/Dynamic/Airship/SmallPoster_1.asset'
        source_guid = guid(Path(str(native) + '.meta'))
        matches = [pointer.deref() for path, pointer in self.recovery.apk.container(
            'defaultpackage_assets_image_home_dynamic_airship.bundle')
            if path.casefold().endswith('/smallposter_1.png') and pointer.type.name == 'Sprite']
        if len(matches) != 1:
            raise ValueError('The original unrepaired airship background is missing or ambiguous.')
        self.objects[source_guid] = matches[0]
        self.load(source_guid)
        result = super().build_furniture_prefabs(bundle, prefab_relatives, dest, **kwargs)
        outputs, staged, _, _ = result
        referenced = set()
        for name in outputs:
            if not name.endswith('.meta'):
                referenced.update(match.decode('ascii') for match in
                    re.findall(rb'guid: ([0-9a-f]{32})', (staged / name).read_bytes()))
        self.script_sources = {name: row for name, row in self.script_sources.items()
                               if row['pointer']['guid'] in referenced}
        return result

    def replace(self, value):
        if isinstance(value, dict) and value == {
                'class': 'TipDotTradeLedgerHasTipConditionNode', 'ns': '', 'asm': 'Assembly-CSharp'}:
            return {'class': value['class'], 'ns': 'Zjcs.Offline.HomeAirship', 'asm': 'Zjcs.Offline.HomeAirship'}
        if isinstance(value, dict) and value == {
                'class': 'TipDotFunctionUnlockConditionNode', 'ns': '', 'asm': 'Assembly-CSharp'}:
            return {'class': value['class'], 'ns': 'Zjcs.Official.HomeFurniture', 'asm': 'Zjcs.Official.HomeFurniture'}
        return super().replace(value)


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
    previous = furniture_builder.HomeNativeFurnitureBinding
    furniture_builder.HomeNativeFurnitureBinding = AirshipFurnitureBinding
    try:
        result = install_furniture_bundle(
        cache_name='home-airship-native', bundle=BUNDLE, prefabs=PREFABS,
        dest=DEST, receipt_path=RECEIPT, guid_prefix='zjcs:home-airship-native:',
        install_prefix='native-airship-building', script_path=Path(__file__),
        extra_scripts={'AirshipFurniture': CONTROLLER, 'HomeScrollTextBord': SCROLL, 'AirShipBanner': BANNER,
                       'Spine.Unity.SkeletonRendererCustomMaterials': CUSTOM_MATERIALS,
                       'AppearanceSystem.SpineExt.SkeletonSlotColor': SLOT_COLOR,
                       'Spine.Extension.RenderExistingMesh': RENDER_MESH},
        scope='Original airship scene prefabs and dependencies, including weather and Yellow Duck variants.',
        )
        source = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'official-code/client-source'
        original = ['AirshipFurniture.cs', 'AirShipBanner.cs', 'HomeScrollTextBord.cs',
                    'AppearanceSystem.SpineExt/SkeletonSlotColor.cs', 'TipDotTradeLedgerHasTipConditionNode.cs',
                    'TipDotFunctionUnlockConditionNode.cs']
        managed = [ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldStateSwitcher.cs',
                   ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/TipDotCommonNode.cs',
                   ROOT / 'unity/Assets/Game/Runtime/Official/HomeFurniture/TipDotFunctionUnlockConditionNode.cs',
                   ROOT / 'unity/Assets/Game/Runtime/Offline/HomeAirship/TipDotTradeLedgerHasTipConditionNode.cs',
                   ROOT / 'assets/reference/home-airship-render-mesh-source-manifest.json']
        result['controller_sources'] = {path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                                        for path in [*(source / name for name in original), *managed]}
        RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
        return result
    finally:
        furniture_builder.HomeNativeFurnitureBinding = previous


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepare-only', action='store_true', help='Convert verified source inputs without installing prefabs.')
    args = parser.parse_args()
    prepare() if args.prepare_only else install()
