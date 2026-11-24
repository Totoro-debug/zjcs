"""Bind all original DiningTable prefabs, skins, components and resource closures."""
from pathlib import Path

from home_native_furniture_builder import install_furniture_bundle
from official_apk import ROOT


DEST = ROOT / 'unity/Assets/Official/Home/DiningTable/Native'
RECEIPT = ROOT / 'assets/reference/home-diningtable-native-source-manifest.json'
BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_furniture_diningtable.bundle'
PREFABS = (
    'Prefabs/Home/Dynamic/Furniture/DiningTable/DiningTable.prefab',
    'Prefabs/Home/Dynamic/Furniture/DiningTable/DiningTable1stAnniversary.prefab',
    'Prefabs/Home/Dynamic/Furniture/DiningTable/DiningTableChineseOdyssey.prefab',
    'Prefabs/Home/Dynamic/Furniture/DiningTable/DiningTableChineseStyle.prefab',
    'Prefabs/Home/Dynamic/Furniture/DiningTable/DiningTableChristmas.prefab',
    'Prefabs/Home/Dynamic/Furniture/DiningTable/DiningTableHalloween.prefab',
)


def build(ensure_cache: bool = False):
    return install_furniture_bundle(
        cache_name='home-diningtable-native',
        bundle=BUNDLE,
        prefabs=PREFABS,
        dest=DEST,
        receipt_path=RECEIPT,
        guid_prefix='zjcs:home-diningtable-native:',
        install_prefix='native-diningtable-building',
        script_path=Path(__file__),
        ensure_cache=ensure_cache,
        scope='Complete original DiningTable hierarchy (default and all 5 skin variants) with Spine bridges, time-drive elements, screen-shoot FadeProxy, TipDot linkers, HomeNpcChatEntry, DiningTableNpcPopClick and HomeNpcBubbleTip bindings.',
    )


if __name__ == '__main__':
    build(ensure_cache=True)
