"""Restore pinned official gem forge UI trees and their exact sprite assets."""
import hashlib
import io
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
import UnityPy

SOURCE = ROOT / "assets/official/kitchen-completion.json"
SOURCE_MANIFEST = ROOT / "assets/reference/kitchen-completion/source-manifest.json"
CACHE = ROOT / ".tools/source-packages/history/854_156383/extracted"
PANELS = ("mechanical", "normal", "gem_result", "gem_confirm", "gem_row", "gem_rule",
          "furnace_talk", "furnace_options", "furnace_portrait")
OUTPUT = Path("Assets/Game/Resources/Equipment/gem-forge-ui.json")
MANIFEST = Path("Assets/Game/Resources/Equipment/gem-forge-ui-source-manifest.json")
SPRITE_DIR = Path("Assets/Game/Resources/Equipment/GemForgeSprites")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def replace_verified(path, content):
    target = ROOT / "unity" / path
    if target.exists() and target.read_bytes() == content:
        return False
    if target.exists():
        raise ValueError("Refusing to overwrite changed gem forge resource: " + str(target))
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(content)
    return True


def text_meta(path):
    guid = hashlib.sha256(("zjcs-gem-forge:" + path.as_posix()).encode("ascii")).hexdigest()[:32]
    return ("fileFormatVersion: 2\nguid: " + guid
            + "\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode("ascii")


def folder_meta(path):
    guid = hashlib.sha256(("zjcs-gem-forge-folder:" + path.as_posix()).encode("ascii")).hexdigest()[:32]
    return ("fileFormatVersion: 2\nguid: " + guid
            + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode("ascii")


def unity_meta(path, source_sprite):
    guid = hashlib.sha256(("zjcs-gem-forge:" + path.as_posix()).encode("ascii")).hexdigest()[:32]
    sprite_id = hashlib.sha256(("zjcs-gem-forge-sprite:" + path.as_posix()).encode("ascii")).hexdigest()[:32]
    width, height = int(source_sprite.get("width", 1)), int(source_sprite.get("height", 1))
    pivot = source_sprite.get("pivot", [width / 2, height / 2])
    border = source_sprite.get("border", [0, 0, 0, 0])
    return ("fileFormatVersion: 2\n"
        "guid: " + guid + "\n"
        "TextureImporter:\n  internalIDToNameTable: []\n  externalObjects: {}\n"
        "  serializedVersion: 12\n  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n"
        "    sRGBTexture: 1\n  isReadable: 0\n  streamingMipmaps: 0\n  textureFormat: 1\n"
        "  maxTextureSize: 2048\n  textureSettings:\n    serializedVersion: 2\n"
        "    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n"
        "  nPOTScale: 0\n  lightmap: 0\n  compressionQuality: 50\n  spriteMode: 1\n"
        "  spriteExtrude: 1\n  spriteMeshType: 1\n  alignment: 9\n"
        "  spritePivot: {x: " + format(pivot[0] / width, ".9g") + ", y: " + format(pivot[1] / height, ".9g") + "}\n"
        "  spritePixelsToUnits: " + format(float(source_sprite.get("ppu", 100)), ".9g") + "\n"
        "  spriteBorder: {x: " + ", y: ".join(format(float(x), ".9g") for x in border[:2])
        + ", z: " + format(float(border[2]), ".9g") + ", w: " + format(float(border[3]), ".9g") + "}\n"
        "  spriteGenerateFallbackPhysicsShape: 0\n  alphaUsage: 1\n  alphaIsTransparency: 1\n"
        "  textureType: 8\n  textureShape: 1\n  spriteSheet:\n    serializedVersion: 2\n"
        "    sprites: []\n    outline: []\n    physicsShape: []\n    bones: []\n"
        "    spriteID: " + sprite_id + "\n    internalID: 21300000\n    vertices: []\n"
        "    indices: \n    edges: []\n    weights: []\n    secondaryTextures: []\n"
        "  platformSettings: []\n  spritePackingTag: \n  userData: \n  assetBundleName: \n  assetBundleVariant: \n").encode("ascii")


def prepare():
    payload = json.loads(SOURCE.read_text(encoding="utf-8"))
    manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    resources = manifest["resources"]
    kitchen = payload["panels"]["kitchen"]["root"]

    def find_node(node, name):
        if node.get("name") == name:
            return node
        for child in node.get("children", []):
            found = find_node(child, name)
            if found is not None:
                return found
        return None

    normal = find_node(kitchen, "GemPanel")
    if normal is None:
        raise ValueError("Pinned official BagualuPanel has no GemPanel subtree")
    panels = {name: payload["panels"][name] for name in PANELS if name != "normal"}
    panels["normal"] = {"source": payload["panels"]["kitchen"]["source"], "root": normal}
    sprites = {}

    def walk(value):
        if isinstance(value, dict):
            sprite = value.get("sprite")
            if isinstance(sprite, dict) and sprite.get("file", "").startswith("res://assets/official/sprites/"):
                uri = sprite["file"]
                sprites[uri] = sprite
                sprite["file"] = "Equipment/GemForgeSprites/" + Path(uri).stem
            for child in value.values():
                walk(child)
        elif isinstance(value, list):
            for child in value:
                walk(child)

    walk(panels)
    absent = sorted(set(sprites) - set(resources))
    if absent:
        raise ValueError("Gem forge UI sprites lack pinned source records: " + ", ".join(absent))

    extracted = {}
    records = []
    for uri, source_sprite in sorted(sprites.items()):
        record = resources[uri]
        bundle_path = CACHE / record["bundle"]
        if not bundle_path.is_file():
            raise FileNotFoundError("Pinned gem forge sprite bundle is unavailable: " + str(bundle_path))
        env = extracted.get(record["bundle"])
        if env is None:
            env = UnityPy.load(str(bundle_path))
            extracted[record["bundle"]] = env
        obj = next((item for item in env.objects if item.path_id == int(record["path_id"])), None)
        if obj is None or obj.type.name != "Sprite":
            raise ValueError("Pinned gem forge sprite object is missing: " + uri)
        image = obj.read()
        if image.m_Name != record["object"]:
            raise ValueError("Pinned gem forge sprite name differs: " + uri)
        buffer = io.BytesIO()
        image.image.save(buffer, format="PNG")
        sprite_bytes = buffer.getvalue()
        if digest(sprite_bytes) != record["sha256"]:
            raise ValueError("Pinned gem forge sprite hash differs: " + uri)
        relative = SPRITE_DIR / Path(uri).name
        replace_verified(relative, sprite_bytes)
        replace_verified(Path(relative.as_posix() + ".meta"), unity_meta(relative, source_sprite))
        records.append({"uri": uri, "bundle": record["bundle"], "path_id": record["path_id"],
                        "sha256": record["sha256"], "output": relative.as_posix()})

    timelines = payload["timelines"]
    output = {"manifest": "854_156383", "panels": panels, "timelines": timelines}
    output_bytes = (json.dumps(output, ensure_ascii=False, sort_keys=True,
                               separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")
    source_manifest = {"manifest": "854_156383", "source": "assets/official/kitchen-completion.json",
        "source_sha256": digest(SOURCE.read_bytes()), "source_manifest": "assets/reference/kitchen-completion/source-manifest.json",
        "source_manifest_sha256": digest(SOURCE_MANIFEST.read_bytes()),
        "output": {"path": OUTPUT.as_posix(), "sha256": digest(output_bytes)}, "sprites": records}
    manifest_bytes = (json.dumps(source_manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    output_path, manifest_path = ROOT / "unity" / OUTPUT, ROOT / "unity" / MANIFEST
    if manifest_path.exists() != output_path.exists():
        raise ValueError("Gem forge UI output and manifest must exist together.")
    if manifest_path.exists():
        prior = json.loads(manifest_path.read_text(encoding="utf-8"))
        current_output = output_path.read_bytes()
        output_is_previous = digest(current_output) == prior.get("output", {}).get("sha256")
        output_is_expected = current_output == output_bytes
        if prior.get("manifest") != "854_156383" or not (output_is_previous or output_is_expected):
            raise ValueError("Refusing to overwrite changed gem forge UI output or manifest.")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(output_bytes)
    manifest_path.write_bytes(manifest_bytes)
    for directory in (Path("Assets/Game/Resources/Equipment"), SPRITE_DIR):
        directory_path = ROOT / "unity" / directory
        directory_path.mkdir(parents=True, exist_ok=True)
        meta_path = Path(directory.as_posix() + ".meta")
        if not (ROOT / "unity" / meta_path).exists():
            replace_verified(meta_path, folder_meta(directory))
    for path in (OUTPUT, MANIFEST):
        replace_verified(Path(path.as_posix() + ".meta"), text_meta(path))
    print("Restored", len(PANELS), "official UI trees and", len(records), "verified sprites")


if __name__ == "__main__":
    prepare()
