"""Recover original current official airship activity banner resources."""
import argparse
import re
from pathlib import Path
import home_native_furniture_builder as builder
from home_native_furniture_builder import install_furniture_bundle, prepare_native_cache
from home_native_cache_verification import verify_native_cache
from official_apk import ROOT
from official_source import OfficialSource
from prepare_partner_card_native import guid
from validation_workspace import validation_directory

BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_banner.bundle'
LANGUAGE_BUNDLES = ('defaultpackage_assets_image_language_zh_cn_logo.bundle',
                    'defaultpackage_assets_image_language_zh_cn_logoip.bundle')
PREFABS = tuple('Prefabs/Home/Dynamic/Banner/' + name + '.prefab' for name in (
    'ActivePayItem', 'SeasonPreviewItem1', 'SeasonPreviewItem2', 'SimpleSunnyItemPreview',
    'ActivityEntrySummerItem'))
DEST = ROOT / 'unity/Assets/Official/Home/Airship/Banner'
RECEIPT = ROOT / 'assets/reference/home-airship-activity-banner-native-source-manifest.json'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official'


class AirshipActivityBannerBinding(builder.HomeNativeFurnitureBinding):
    def build_furniture_prefabs(self, bundle, prefab_relatives, dest, **kwargs):
        for relative, source_bundle, suffix in (
            ('Image/Language/zh_CN/Logo/Logo.asset', LANGUAGE_BUNDLES[0], '/logo.png'),
            ('Image/Language/zh_CN/LogoIP/LogoIP_1.asset', LANGUAGE_BUNDLES[1], '/logoip_1.png'),
        ):
            native = self.native / relative
            source_guid = guid(Path(str(native) + '.meta'))
            matches = [pointer.deref() for path, pointer in self.recovery.apk.container(source_bundle)
                       if path.casefold().endswith(suffix) and pointer.type.name == 'Sprite']
            if len(matches) != 1:
                raise ValueError('Missing or ambiguous original language sprite: ' + relative)
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


def prepare():
    cache = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'home-airship-banner-native'
    if (cache / 'native-export-source-manifest.json').exists() or (cache / 'native-export').exists():
        return verify_native_cache(cache, PREFABS, OfficialSource().version)
    prepare_native_cache(cache, [BUNDLE, *LANGUAGE_BUNDLES])
    return verify_native_cache(cache, PREFABS, OfficialSource().version)


def bind(dest, receipt):
    prepare()
    previous = builder.HomeNativeFurnitureBinding
    builder.HomeNativeFurnitureBinding = AirshipActivityBannerBinding
    try:
        return install_furniture_bundle(
            cache_name='home-airship-banner-native', bundle=BUNDLE, prefabs=PREFABS,
            dest=dest, receipt_path=receipt, guid_prefix='zjcs:home-airship-banner-native:',
            install_prefix='native-airship-banner', script_path=Path(__file__),
            extra_scripts={'AirshipActivityBannerItem': RUNTIME / 'HomeAirship/AirshipActivityBannerItem.cs',
                           'MultiLanguageSpriteRenderer': RUNTIME / 'WorldUI/WorldMultiLanguageSpriteRenderer.cs'},
            scope='Original airship activity banners and dynamically loaded zh_CN language sprites.',
        )
    finally:
        builder.HomeNativeFurnitureBinding = previous


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument('--prepare-only', action='store_true')
    action.add_argument('--stage', action='store_true', help='Bind into this session validation directory')
    action.add_argument('--install', action='store_true', help='Install previously validated prefabs')
    args = parser.parse_args()
    if args.prepare_only:
        prepare()
    elif args.stage:
        work = validation_directory()
        if work == ROOT / 'output/validation':
            raise ValueError('Use an owned validation workspace')
        bind(work / 'airship-banner-staged', work / 'airship-banner-staged-source.json')
    else:
        bind(DEST, RECEIPT)
