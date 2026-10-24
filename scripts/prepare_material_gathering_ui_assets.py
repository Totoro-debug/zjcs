"""Decode official MopUp UI sprites from the verified active resource bundle."""
import hashlib
import io
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BUNDLE = "defaultpackage_assets_image_ui_notes_mopup.bundle"
VERSION = "871_156598"
EXTRACTED = ROOT / ".tools/source-packages/extracted"
OUTPUT = ROOT / "unity/Assets/Official/Gathering/UI"
REFERENCE = ROOT / "assets/reference/dungeon-ui/gathering-ui-source-manifest.json"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def stable_guid(path):
    return digest(("zjcs:" + path).encode("utf-8"))[:32]


def sprite_meta(relative):
    guid = stable_guid(relative)
    return ("fileFormatVersion: 2\n"
            "guid: " + guid + "\n"
            "TextureImporter:\n"
            "  serializedVersion: 13\n"
            "  mipmaps:\n"
            "    mipMapMode: 0\n"
            "    enableMipMap: 0\n"
            "    sRGBTexture: 1\n"
            "    linearTexture: 0\n"
            "  isReadable: 0\n"
            "  streamingMipmaps: 0\n"
            "  textureFormat: 3\n"
            "  maxTextureSize: 4096\n"
            "  textureSettings:\n"
            "    serializedVersion: 2\n"
            "    filterMode: 1\n"
            "    aniso: 1\n"
            "    mipBias: 0\n"
            "    wrapU: 1\n"
            "    wrapV: 1\n"
            "    wrapW: 1\n"
            "  spriteMode: 1\n"
            "  spritePixelsToUnits: 100\n"
            "  alphaUsage: 1\n"
            "  alphaIsTransparency: 1\n"
            "  textureType: 8\n"
            "  textureShape: 1\n"
            "  spriteSheet:\n"
            "    serializedVersion: 2\n"
            "    sprites: []\n"
            "    outline: []\n"
            "    physicsShape: []\n"
            "    bones: []\n"
            "    spriteID: " + guid + "\n"
            "    internalID: 0\n"
            "    vertices: []\n"
            "    indices: \n"
            "    edges: []\n"
            "    weights: []\n"
            "    secondaryTextures: []\n"
            "    nameFileIdTable: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n").encode("utf-8")


def folder_meta(relative):
    return ("fileFormatVersion: 2\n"
            "guid: " + stable_guid(relative) + "\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n").encode("utf-8")


def main():
    sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
    import UnityPy

    bundle_path = EXTRACTED / BUNDLE
    sources = json.loads((EXTRACTED / "bundle-sources.json").read_text(encoding="utf-8"))
    index = json.loads((EXTRACTED / "bundle-index.json").read_text(encoding="utf-8"))
    source = sources.get(BUNDLE)
    if not bundle_path.is_file() or source is None:
        raise FileNotFoundError("The official MopUp sprite bundle has not been extracted.")
    raw_bundle = bundle_path.read_bytes()
    if source.get("manifest") != VERSION or source.get("apk_manifest") != "854_156383":
        raise ValueError("MopUp UI sprite bundle does not match the recorded source manifests.")
    if source.get("bundle_sha256") != digest(raw_bundle) or index.get(BUNDLE) != source.get("bundle_hash"):
        raise ValueError("MopUp UI sprite bundle hash differs from the verified source index.")

    environment = UnityPy.load(str(bundle_path))
    container_paths = {pointer.path_id: name for name, pointer in environment.container.items()}
    outputs = []
    names = set()
    for obj in environment.objects:
        if obj.type.name != "Sprite":
            continue
        sprite = obj.read()
        name = sprite.m_Name
        if not name or name in names:
            raise ValueError("Official MopUp sprite names are missing or duplicated: " + str(name))
        names.add(name)
        image = io.BytesIO()
        sprite.image.save(image, format="PNG")
        data = image.getvalue()
        relative = "Assets/Official/Gathering/UI/" + name + ".png"
        target = ROOT / "unity" / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and target.read_bytes() != data:
            raise ValueError("Refusing to overwrite changed official MopUp sprite: " + str(target))
        target.write_bytes(data)
        meta = Path(str(target) + ".meta")
        expected_meta = sprite_meta(relative)
        if meta.exists() and meta.read_bytes() != expected_meta:
            raise ValueError("Refusing to overwrite changed Unity sprite metadata: " + str(meta))
        meta.write_bytes(expected_meta)
        outputs.append({
            "name": name,
            "source_path": container_paths.get(obj.path_id, ""),
            "path": relative,
            "bytes": len(data),
            "sha256": digest(data),
        })
    if len(outputs) != 68 or any(not item["source_path"] for item in outputs):
        raise ValueError("The verified MopUp bundle did not yield all 68 named sprites.")

    for relative in ("Assets/Official/Gathering", "Assets/Official/Gathering/UI"):
        folder = ROOT / "unity" / relative
        folder.mkdir(parents=True, exist_ok=True)
        meta = Path(str(folder) + ".meta")
        expected_meta = folder_meta(relative)
        if meta.exists() and meta.read_bytes() != expected_meta:
            raise ValueError("Refusing to overwrite changed Unity folder metadata: " + str(meta))
        meta.write_bytes(expected_meta)

    manifest = {
        "manifest": VERSION,
        "apk_manifest": "854_156383",
        "source_bundle": {
            "name": BUNDLE,
            "origin": source["origin"],
            "bundle_sha256": digest(raw_bundle),
            "bundle_md5": source["bundle_md5"],
            "url": source.get("url", ""),
        },
        "sprites": sorted(outputs, key=lambda item: item["name"]),
    }
    encoded = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    payload_manifest = OUTPUT / "source-manifest.json"
    reference_manifest = REFERENCE
    for target in (payload_manifest, reference_manifest):
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and target.read_bytes() != encoded:
            raise ValueError("Refusing to overwrite changed MopUp source manifest: " + str(target))
        target.write_bytes(encoded)
        meta = Path(str(target) + ".meta")
        relative = target.relative_to(ROOT / "unity").as_posix() if target.is_relative_to(ROOT / "unity") else target.relative_to(ROOT).as_posix()
        expected_meta = ("fileFormatVersion: 2\n"
                         "guid: " + stable_guid(relative) + "\n"
                         "TextScriptImporter:\n"
                         "  externalObjects: {}\n"
                         "  userData: \n"
                         "  assetBundleName: \n"
                         "  assetBundleVariant: \n").encode("utf-8")
        if meta.exists() and meta.read_bytes() != expected_meta:
            raise ValueError("Refusing to overwrite changed MopUp manifest metadata: " + str(meta))
        meta.write_bytes(expected_meta)
    print("Extracted", len(outputs), "official MopUp UI sprites")


if __name__ == "__main__":
    main()
