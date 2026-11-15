"""User-requested transparent dialogue rim, derived from the selected APK sprite."""
import hashlib
import io
import json
from pathlib import Path
from official_apk import Apk, ROOT


def main():
    apk = Apk()
    obj = apk.asset('image_ui_dialogue', '/ImgBg_Talk.png')
    sprite = obj.read()
    original = sprite.image.convert('RGBA')
    source = io.BytesIO()
    original.save(source, format='PNG')
    if hashlib.sha256(source.getvalue()).hexdigest() != '6b92d03dd99161e0ed9a71470e216ad8fdcbdd65ae1ddedfa371359f9bc3defc':
        raise ValueError('Dialogue pixels changed; re-inspect the official rim before converting')
    image = original.copy()
    # The official dark shadow is RGB 32..36. Keep every rim/body pixel intact.
    removed = 0
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = original.getpixel((x, y))
            if max(r, g, b) < 64:
                image.putpixel((x, y), (r, g, b, 0))
                removed += a != 0
    path = ROOT / 'unity/Assets/Official/StoryUI/Resources/Story/TransparentDialogueBackground.png'
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, format='PNG')
    meta = (ROOT / 'unity/Assets/Official/World/Map/14/UI/Sprites/ImgBg_Talk-6b92d03dd9.png.meta').read_text(encoding='utf8')
    meta = meta.replace('50286dc1f38e8e447a6828c1ed40ede8', '8970a3cebc0e41d2bf48cc99db5ca111')
    Path(str(path) + '.meta').write_text(meta, encoding='utf8')
    for folder in (path.parent, path.parent.parent):
        target = Path(str(folder) + '.meta')
        if not target.exists():
            guid = hashlib.md5(folder.relative_to(ROOT).as_posix().encode()).hexdigest()
            target.write_text(f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n', encoding='utf8')
    receipt = dict(manifest=apk.version, source=apk.provenance(obj),
        raw_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
        decoded_sha256=hashlib.sha256(source.getvalue()).hexdigest(),
        operation='User-requested removal of baked dark shadow: alpha=0 only where max(R,G,B)<64; all other RGBA pixels unchanged; original size, pivot, border and PPU retained',
        removed_pixels=removed,
        binding='OfficialWorldUiRenderer -> DialogueBackgroundAppearance.Apply -> Resources/Story/TransparentDialogueBackground',
        outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in (path, Path(str(path)+'.meta'))])
    (ROOT / 'assets/reference/official-map/dialogue-transparent-background-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print('Removed shadow pixels:', removed)


if __name__ == '__main__':
    main()
