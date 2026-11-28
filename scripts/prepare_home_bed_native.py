"""Bind all original Bed skins, controllers, HomeBed TipDot graph, and resource closure."""
from pathlib import Path

from home_native_furniture_builder import install_furniture_bundle, rebuild_furniture_bundle
from official_apk import ROOT


DEST = ROOT / 'unity/Assets/Official/Home/Bed/Native'
RECEIPT = ROOT / 'assets/reference/home-bed-native-source-manifest.json'
PREFABS = (
    'Prefabs/Home/Dynamic/Furniture/Bed/Bed.prefab',
    'Prefabs/Home/Dynamic/Furniture/Bed/BedChristmas.prefab',
    'Prefabs/Home/Dynamic/Furniture/Bed/BedChineseStyle.prefab',
    'Prefabs/Home/Dynamic/Furniture/Bed/Bed1stAnniversary.prefab',
    'Prefabs/Home/Dynamic/Furniture/Bed/BedChineseOdyssey.prefab',
    'Prefabs/Home/Dynamic/Furniture/Bed/BedNon_Human.prefab',
    'Prefabs/Home/Dynamic/Furniture/Bed/BedHalloween.prefab',
)


def install():
    options = dict(
        bundle='defaultpackage_assets_prefabs_home_dynamic_furniture_bed.bundle',
        prefabs=PREFABS,
        dest=DEST,
        receipt_path=RECEIPT,
        guid_prefix='zjcs:home-bed-native:',
        install_prefix='native-bed-building',
        script_path=Path(__file__),
        scope='Native Bed building closure across all 7 skin prefabs, BedFurniture/HomeMutex/HomeTipDots components, and HomeBed TipDot graph.',
    )
    if RECEIPT.exists():
        return rebuild_furniture_bundle(**options)
    return install_furniture_bundle(cache_name='home-bed-native', ensure_cache=True, **options)


if __name__ == '__main__':
    install()
