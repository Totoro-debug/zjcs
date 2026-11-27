"""Bind all original Desk skins, controllers, and resource closure."""
from pathlib import Path

from home_native_furniture_builder import install_furniture_bundle, rebuild_furniture_bundle
from official_apk import ROOT


DEST = ROOT / 'unity/Assets/Official/Home/Desk/Native'
RECEIPT = ROOT / 'assets/reference/home-desk-native-source-manifest.json'
PREFABS = (
    'Prefabs/Home/Dynamic/Furniture/Desk/Desk.prefab',
    'Prefabs/Home/Dynamic/Furniture/Desk/Desk1stAnniversary.prefab',
    'Prefabs/Home/Dynamic/Furniture/Desk/DeskChineseOdyssey.prefab',
    'Prefabs/Home/Dynamic/Furniture/Desk/DeskChineseStyle.prefab',
    'Prefabs/Home/Dynamic/Furniture/Desk/DeskChristmas.prefab',
    'Prefabs/Home/Dynamic/Furniture/Desk/DeskHalloween.prefab',
)


def install():
    options = dict(
        bundle='defaultpackage_assets_prefabs_home_dynamic_furniture_desk.bundle',
        prefabs=PREFABS,
        dest=DEST,
        receipt_path=RECEIPT,
        guid_prefix='zjcs:home-desk-native:',
        install_prefix='native-desk-building',
        script_path=Path(__file__),
        scope='Native Desk building closure across all 6 skin/theme prefabs and DeskFurniture/HomeDayNight/HomeMutex/HomeTipDots components.',
    )
    if RECEIPT.exists():
        return rebuild_furniture_bundle(**options)
    return install_furniture_bundle(cache_name='home-desk-native', ensure_cache=True, **options)


if __name__ == '__main__':
    install()
