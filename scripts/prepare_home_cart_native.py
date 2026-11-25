"""Bind all original Cart skins, controllers, FurnitureCart/HangUpItem TipDot graphs, and resource closure."""
from pathlib import Path

from home_native_furniture_builder import install_furniture_bundle
from official_apk import ROOT


DEST = ROOT / 'unity/Assets/Official/Home/Cart/Native'
RECEIPT = ROOT / 'assets/reference/home-cart-native-source-manifest.json'
PREFABS = (
    'Prefabs/Home/Dynamic/Furniture/Cart/Cart.prefab',
    'Prefabs/Home/Dynamic/Furniture/Cart/CartSpring.prefab',
    'Prefabs/Home/Dynamic/Furniture/Cart/CartSnow.prefab',
    'Prefabs/Home/Dynamic/Furniture/Cart/CartSpringSnow.prefab',
    'Prefabs/Home/Dynamic/Furniture/Cart/CartMaidCafe.prefab',
    'Prefabs/Home/Dynamic/Furniture/Cart/CartMaidCafeSnow.prefab',
)


def install():
    install_furniture_bundle(
        cache_name='home-cart-native',
        bundle='defaultpackage_assets_prefabs_home_dynamic_furniture_cart.bundle',
        prefabs=PREFABS,
        dest=DEST,
        receipt_path=RECEIPT,
        guid_prefix='zjcs:home-cart-native:',
        install_prefix='native-cart-building',
        script_path=Path(__file__),
        scope='Native Cart building closure across all 6 skin/weather prefabs, CartFurniture/HomeDayNight/HomeMutex/HomeTipDots components, and FurnitureCart/HangUpItem TipDot graphs.',
        ensure_cache=True,
    )


if __name__ == '__main__':
    install()
