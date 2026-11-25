"""Bind the verified original fruit-tree world and fruit display prefabs."""
import hashlib
import json
from pathlib import Path

import home_native_furniture_builder as furniture_builder
from home_native_cache_verification import verify_native_cache
from home_native_furniture_builder import install_furniture_bundle, prepare_native_cache
from official_apk import ROOT
from official_source import OfficialSource
from official_manifest import read_asset_manifest

BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_furniture_fruittree.bundle'
BASE = 'Prefabs/Home/Dynamic/Furniture/FruitTree/'
TREE_PREFABS = (
    'FruitTree', 'FruitTreeSnow', 'FruitTree1stAnniversary', 'FruitTree1stAnniversarySnow',
    'FruitTreeChineseOdyssey', 'FruitTreeChineseOdysseySnow',
    'FruitTreeChineseStyle', 'FruitTreeChineseStyleSnow',
    'FruitTreeChristmas', 'FruitTreeChristmasSnow', 'FruitTreeHalloween', 'FruitTreeHalloweenSnow',
)
FRUIT_PREFABS = (
    'Fruit', 'FruitSnow', 'FruitChineseStyle', 'FruitChineseStyleSnow',
    'FruitChristmas', 'FruitChristmasSnow',
    *(f'Fruit1stAnniversary{index:02}' for index in range(1, 6)),
    *(f'FruitHalloween{index:02}' for index in range(1, 6)),
    *(f'FruitChineseOdyssey{index:02}{suffix}' for index in range(1, 4) for suffix in ('', 'Snow')),
)
PREFABS = tuple(BASE + name + '.prefab' for name in (*TREE_PREFABS, *FRUIT_PREFABS))
DEST = ROOT / 'unity/Assets/Official/Home/FruitTree/Native'
RECEIPT = ROOT / 'assets/reference/home-fruit-tree-native-source-manifest.json'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official'
EXTRA = {
    'HomeFruitTree': RUNTIME / 'HomeFruitTreeWorld/HomeFruitTree.cs',
    'HomeSpineDayNightSwitcher': RUNTIME / 'HomeFruitTreeWorld/HomeSpineDayNightSwitcher.cs',
    'HomeFruitTreePet': RUNTIME / 'HomeFruitTreeNative/HomeFruitTreePet.cs',
    'HomeFruitTreeProduceFruit': RUNTIME / 'HomeFruitTreeNative/HomeFruitTreeProduceFruit.cs',
    'HomeFruitTreeHangUpHandlerFruitItem': RUNTIME / 'HomeFruitTreeOutput/HomeFruitTreeHangUpHandlerFruitItem.cs',
    'HomeFruitTreeHangUpEffectItem': RUNTIME / 'HomeFruitTreeOutput/HomeFruitTreeHangUpEffectItem.cs',
    'HomeFruitTreeChristmasHangUpEffectItem': RUNTIME / 'HomeFruitTreeOutput/HomeFruitTreeChristmasHangUpEffectItem.cs',
    'HomeFurnitureProxy': RUNTIME / 'HomeFurnitureProxy/HomeFurnitureProxy.cs',
    'AchievementRedDotItem': RUNTIME / 'HomePetGachaUI/AchievementRedDotItem.cs',
    'GachaBlueDotForSetupMinCount': RUNTIME / 'HomePetGachaUI/GachaBlueDotForSetupMinCount.cs',
    'Spine.Extension.RenderExistingMesh': RUNTIME / 'HomeFurniture/RenderExistingMesh.cs',
    'AppearanceSystem.SpineExt.SkeletonSlotColor': RUNTIME / 'HomeFurniture/SkeletonSlotColor.cs',
    'Spine.Unity.SkeletonRenderSeparator': ROOT / 'unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonRenderSeparator/SkeletonRenderSeparator.cs',
    'Spine.Unity.SkeletonPartsRenderer': ROOT / 'unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonRenderSeparator/SkeletonPartsRenderer.cs',
    'Spine.Unity.SkeletonRendererCustomMaterials': ROOT / 'unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonRendererCustomMaterials/SkeletonRendererCustomMaterials.cs',
}
TIP_NODES = {
    'TipDotFreeAwardConditionNode', 'TipDotActivityGachaRemainCostBlueDotConditionNode',
    'TipDotActivityGachaHasCostConditionNode', 'TipDotLinkActivityGachaItemNode',
    'TipDotActivityIsOpenNode', 'TipDotLinkAnyPetGachaActivityNode',
}


class FruitTreeBinding(furniture_builder.HomeNativeFurnitureBinding):
    def build_furniture_prefabs(self, bundle, prefab_relatives, dest, **kwargs):
        original_finalize = self.recovery.finalize

        def finalize():
            original_finalize()
            original_targets = {}
            for _, _, panel, record in self.recovery.outputs:
                if record['name'] not in FRUIT_PREFABS:
                    continue
                token = hashlib.md5((self.guid_prefix + 'prefab:' + record['originalRoot']).encode()).hexdigest()
                for key, file_id in panel['objectMap'].items():
                    if key in original_targets:
                        raise ValueError('Ambiguous original fruit prefab object: ' + key)
                    original_targets[key] = dict(fileID=file_id, guid=token, type=3)
                self.user_script_guids.add(token)
                self.script_guids.add(token)
            for _, _, panel, _ in self.recovery.outputs:
                for key, reference in panel['external'].items():
                    if key in original_targets:
                        target = original_targets[key]
                        if reference['fileID'] != target['fileID']:
                            raise ValueError('Original cross-prefab component fileID differs: ' + key)
                        self.redirects[reference['guid']] = target
            for _, _, panel, _ in self.recovery.outputs:
                for key, pending in panel['unresolved'].items():
                    obj = self.original(key)
                    if pending['type'] != 'MonoBehaviour' or obj.read(check_read=False).m_Script.read().m_ClassName != 'TipDotGraphNode':
                        raise ValueError('Inspect unexpected original external reference: ' + key)
                    if hashlib.sha256(obj.get_raw_data()).hexdigest() != pending['payload_sha256']:
                        raise ValueError('Original graph-node payload changed: ' + key)

        self.recovery.finalize = finalize
        try:
            return super().build_furniture_prefabs(bundle, prefab_relatives, dest, **kwargs)
        finally:
            self.recovery.finalize = original_finalize

    def replace(self, value):
        if isinstance(value, dict) and value.get('ns') == '' and value.get('asm') == 'Assembly-CSharp':
            name = value.get('class')
            if name in TIP_NODES:
                return dict(value, ns='Zjcs.Official.HomeActivityTipDots', asm='Zjcs.Official.HomeActivityTipDots')
            path = RUNTIME / 'HomeTipDots' / (str(name) + '.cs')
            if str(name).startswith('TipDot') and path.is_file() and 'namespace Zjcs.Official.HomeTipDots' in path.read_text('utf8'):
                return dict(value, ns='Zjcs.Official.HomeTipDots', asm='Zjcs.Official.HomeTipDots')
        return super().replace(value)


def prepare():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'home-fruit-tree-native'
    if not (cache / 'native-export-source-manifest.json').is_file():
        prepare_native_cache(cache, [BUNDLE])
    return verify_native_cache(cache, PREFABS, source.version)


def install():
    prepare()
    previous = furniture_builder.HomeNativeFurnitureBinding
    furniture_builder.HomeNativeFurnitureBinding = FruitTreeBinding
    try:
        receipt = install_furniture_bundle(
            cache_name='home-fruit-tree-native', bundle=BUNDLE, prefabs=PREFABS,
            dest=DEST, receipt_path=RECEIPT, guid_prefix='zjcs:home-fruit-tree-native:',
            install_prefix='native-fruit-tree', script_path=Path(__file__), extra_scripts=EXTRA,
            scope='Original fruit-tree and fruit prefabs including decoration and snow variants, serialized dependencies, source graph nodes, and cross-prefab fruit-effect component references.')
        code = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'official-code/client-source'
        sources = [code / name for name in ('HomeFruitTree.cs', 'HomeFruitTreePet.cs', 'HomeFruitTreeProduceFruit.cs',
            'HomeFruitTreeHangUpHandlerFruitItem.cs', 'HomeFruitTreeHangUpEffectItem.cs',
            'AchievementRedDotItem.cs', 'GachaBlueDotForSetupMinCount.cs')]
        managed = [*EXTRA.values(), ROOT / 'unity/Assets/Game/Runtime/Official/HomeActivityTipDots/TipDotActivityGachaHasCostConditionNode.cs',
            ROOT / 'unity/Assets/Game/Runtime/Official/HomeActivityTipDots/TipDotActivityGachaRemainCostBlueDotConditionNode.cs',
            ROOT / 'unity/Assets/Game/Runtime/Official/HomeActivityTipDots/TipDotActivityIsOpenNode.cs',
            ROOT / 'unity/Assets/Game/Runtime/Official/HomeActivityTipDots/TipDotLinkActivityGachaItemNode.cs',
            ROOT / 'unity/Assets/Game/Runtime/Official/HomeActivityTipDots/TipDotLinkAnyPetGachaActivityNode.cs']
        receipt['controller_sources'] = {path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in [*sources, *managed]}
        receipt['fortune_tree_graph_manifest'] = 'assets/reference/home-fruit-tree-fortune-tree-graph-source-manifest.json'
        manifest_raw = OfficialSource().manifest_bytes('DefaultPackage')
        manifest = read_asset_manifest(manifest_raw)
        dynamic = []
        for prefab in receipt['prefabs']:
            if prefab['name'] not in FRUIT_PREFABS:
                continue
            original_path = 'Assets/' + BASE + prefab['name'] + '.prefab'
            original, = [row for row in manifest if row['path'] == original_path]
            if not original['guid']:
                raise ValueError('Original dynamic fruit has no asset GUID: ' + original_path)
            dynamic.append(dict(asset_guid=original['guid'], original_path=original_path,
                prefab=prefab['prefab'], pointer=prefab['pointer']))
        receipt['dynamic_prefabs'] = dynamic
        receipt['dynamic_manifest_sha256'] = hashlib.sha256(manifest_raw).hexdigest()
        RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
        return receipt
    finally:
        furniture_builder.HomeNativeFurnitureBinding = previous


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepare-only', action='store_true')
    args = parser.parse_args()
    prepare() if args.prepare_only else install()
