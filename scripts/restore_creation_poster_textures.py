"""Restore the original uncropped standalone poster textures, with source receipts."""
import hashlib
import io
import json
from pathlib import Path

from official_apk import UnityPy
from official_bundle_reader import OfficialBundleReader

ROOT = Path(__file__).resolve().parents[1]
BUNDLE = 'defaultpackage_assets_image_ui_createplayer.bundle'
ART = ROOT / 'unity/Assets/Official/CharacterCreation/RuntimeUI'
TARGETS = {
    'ImgRole_Male': 'ImgRole_Male-f13908db32.png',
    'ImgRole_Female': 'ImgRole_Female-49e8988709.png',
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def restore():
    from PIL import Image

    records = []
    with OfficialBundleReader() as reader:
        bundle = reader.read('DefaultPackage', BUNDLE)
        environment = UnityPy.load(bundle.data)
        for obj in environment.objects:
            if obj.type.name != 'Sprite':
                continue
            sprite = obj.read()
            if sprite.m_Name not in TARGETS:
                continue
            filename = TARGETS[sprite.m_Name]
            target = ART / 'Sprites' / filename
            texture = sprite.m_RD.texture.deref()
            source = texture.read()
            full = source.image.convert('RGBA')
            cropped = sprite.image.convert('RGBA')
            # These are standalone source textures, not atlas rectangles. Do not
            # round the fractional tight-mesh rectangle or invent padding pixels.
            assert full.size == (round(sprite.m_Rect.width), round(sprite.m_Rect.height))
            assert source.m_Name == sprite.m_Name and sprite.m_Rect.x == sprite.m_Rect.y == 0
            old = Image.open(target).convert('RGBA')
            assert old.size in (cropped.size, full.size)
            expected = cropped if old.size == cropped.size else full
            assert old.tobytes() == expected.tobytes(), target
            stream = io.BytesIO()
            full.save(stream, format='PNG')
            data = stream.getvalue()
            legacy = next(row for row in json.loads((ART / 'source-manifest.json').read_text(encoding='utf-8'))['outputs']
                          if row.get('path') == target.relative_to(ROOT).as_posix())
            records.append(dict(name=sprite.m_Name, sprite_path_id=obj.path_id, texture_path_id=texture.path_id,
                source_cropped_sha256=legacy['source_sha256'], source_cropped_size=list(cropped.size),
                source_rect=[sprite.m_Rect.width, sprite.m_Rect.height],
                texture_rect_offset=[sprite.m_RD.textureRectOffset.x, sprite.m_RD.textureRectOffset.y],
                path=target.relative_to(ROOT).as_posix(), sha256=sha(data), bytes=len(data),
                binding='CreatePlayerPanel/Content/BgGo/RoleContent/' +
                    ('MaleGo' if sprite.m_Name.endswith('Male') else 'FemaleGo') + '/ColorImg/RoleImg'))
            target.write_bytes(data)
        assert len(records) == 2
        receipt = dict(format=1, manifest=reader.source.version, bundle=BUNDLE, source=dict(bundle.provenance),
            generator='scripts/restore_creation_poster_textures.py', generator_sha256=sha(Path(__file__).read_bytes()),
            operation='Decode each original standalone Texture2D at its full Sprite.m_Rect dimensions. Verify the earlier cropped PNG pixels against the same APK Sprite.image before replacement. Preserve the existing PNG paths, GUIDs, prefab references, RectTransforms, aspect ratios and animation curves. No resampling, hand padding or layout adjustment.',
            outputs=records)
    for name in ('source-manifest.json', 'native-ui-source-manifest.json'):
        path = ART / name
        manifest = json.loads(path.read_text(encoding='utf-8'))
        for entry in manifest.get('outputs' if name == 'source-manifest.json' else 'inputs', []):
            for record in records:
                if entry.get('path') == record['path']:
                    entry['sha256'] = record['sha256']
                    entry['conversion_receipt'] = 'assets/reference/character-creation/poster-textures-source-manifest.json'
        path.write_text(json.dumps(manifest, ensure_ascii=False, indent=4 if name == 'native-ui-source-manifest.json' else 2) + '\n', encoding='utf-8')
    output = ROOT / 'assets/reference/character-creation/poster-textures-source-manifest.json'
    output.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(records, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    restore()
