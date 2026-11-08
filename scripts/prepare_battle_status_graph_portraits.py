"""Recover official portraits for the original status-graph diagnostic casters."""
import hashlib
import io
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_unity_battle_monster_portraits import asset_yaml, sprite_meta


RECEIPT = ROOT / 'assets/reference/battle-hud/status-graph-portraits-source-manifest.json'
BUNDLE = 'defaultpackage_assets_image_itemicon_gameheadicon_monster_map23_noatlas.bundle'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def guid(path):
    return hashlib.md5(('zjcs-official:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()


def main():
    with Apk.current() as apk:
        if apk.version != '884_156692':
            raise ValueError('Review the selected original portrait source version')
        obj, = [value for value in apk.objects(BUNDLE)
                if value.type.name == 'Sprite' and value.peek_name() == 'G_3665']
        sprite = obj.read()
        texture = sprite.m_RD.texture.deref()
        source_texture = texture.read()
        settings = source_texture.m_TextureSettings
        if (sprite.m_PixelsToUnits != 100 or sprite.m_Pivot.x != .5 or sprite.m_Pivot.y != .5
                or sprite.m_Extrude != 1 or sprite.m_IsPolygon
                or any(getattr(sprite.m_Border, axis) != 0 for axis in 'xyzw')
                or source_texture.m_MipCount != 1 or source_texture.m_ColorSpace != 1
                or settings.m_FilterMode != 1 or settings.m_Aniso != 1 or settings.m_MipBias != 0
                or any(getattr(settings, axis) != 1 for axis in ('m_WrapU', 'm_WrapV', 'm_WrapW'))):
            raise ValueError('Recover the differing original Sprite/Texture importer fields')
        source = apk.provenance(obj)
        packaged = 'assets/yoo/DefaultPackage/' + apk.index[BUNDLE] + '.bundle'
        if source['origin'] != 'official_cdn' or packaged in apk.available:
            raise ValueError('Review the original APK-first portrait location')
        image = ROOT / 'unity/Assets/Official/Combat/StatusGraphPortraits/G_3665.png'
        catalog = ROOT / 'unity/Assets/Game/Resources/Combat/StatusGraphMonsterPortraitCatalog.asset'
        script = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMonsterPortraitCatalog.cs'
        script_guid = re.search(r'^guid: (\w+)', Path(str(script) + '.meta').read_text(), re.M).group(1)
        stream = io.BytesIO()
        sprite.image.save(stream, format='PNG')
        metadata = sprite_meta(image, guid(image), guid(image)).replace(
            b'  alphaIsTransparency: 1\n', b'  alphaIsTransparency: 0\n').replace(
            b'  platformSettings: []\n',
            b'  platformSettings:\n  - serializedVersion: 3\n    buildTarget: Standalone\n'
            b'    maxTextureSize: 2048\n    resizeAlgorithm: 0\n    textureFormat: 4\n'
            b'    textureCompression: 0\n    compressionQuality: 100\n    crunchedCompression: 0\n'
            b'    allowsAlphaSplitting: 0\n    overridden: 1\n    androidETC2FallbackOverride: 0\n'
            b'    forceMaximumCompressionQuality_BC6H_BC7: 0\n')
        outputs = {
            image: stream.getvalue(), Path(str(image) + '.meta'): metadata,
            catalog: asset_yaml(script_guid, [dict(iconId=3665, guid=guid(image))]).replace(
                b'm_Name: OfficialBattleMonsterPortraitCatalog', b'm_Name: StatusGraphMonsterPortraitCatalog'),
            Path(str(catalog) + '.meta'): ('fileFormatVersion: 2\nguid: ' + guid(catalog)
                + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n'
                '  userData: \n  assetBundleName: \n  assetBundleVariant: \n').encode(),
            Path(str(image.parent) + '.meta'): ('fileFormatVersion: 2\nguid: ' + guid(image.parent)
                + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
                '  userData: \n  assetBundleName: \n  assetBundleVariant: \n').encode(),
        }
        previous = json.loads(RECEIPT.read_bytes()).get('outputs', {}) if RECEIPT.exists() else {}
        for path, raw in outputs.items():
            if path.exists() and path.read_bytes() != raw and previous.get(path.relative_to(ROOT).as_posix()) != sha(path.read_bytes()):
                raise ValueError('Preserve changed original portrait output: ' + str(path))
        for path, raw in outputs.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        proof = dict(manifest=apk.version, source=source,
            apk_checked=dict(entry=packaged, packaged=False, apk_manifest=apk.source.apk_version,
                apk_sha256=apk.source.apk_sha256),
            sprite_object_sha256=sha(obj.get_raw_data()),
            texture_source=apk.provenance(texture), texture_object_sha256=sha(texture.get_raw_data()),
            sprite=dict(pixels_per_unit=sprite.m_PixelsToUnits, pivot=[sprite.m_Pivot.x, sprite.m_Pivot.y],
                rect={axis: getattr(sprite.m_Rect, axis) for axis in ('x', 'y', 'width', 'height')}),
            sampling=dict(mip_count=source_texture.m_MipCount, color_space=source_texture.m_ColorSpace,
                filter_mode=settings.m_FilterMode, aniso=settings.m_Aniso, mip_bias=settings.m_MipBias,
                wrap_u=settings.m_WrapU, wrap_v=settings.m_WrapV, wrap_w=settings.m_WrapW),
            generator_sha256=sha(Path(__file__).read_bytes()),
            outputs={path.relative_to(ROOT).as_posix(): sha(raw) for path, raw in outputs.items()},
            operation='APK-first, verified selected cloud cache. Decode original RGBA pixels; preserve original '
                'Sprite geometry and texture sampling. Windows RGBA32 without added block compression or alpha bleeding. '
                'Explicit supplemental resource catalog; no existing icon binding is replaced.')
        RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
        print('Original G_3665 portrait and supplemental resource catalog recovered.')


if __name__ == '__main__':
    main()
