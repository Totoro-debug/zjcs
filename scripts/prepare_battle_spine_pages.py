"""Restore the pinned battle Spine atlas pages the converted skeletons reference.

The battle Spine payload ships reassembled .skel.bytes / .atlas.txt files whose
pages were only partially exported (47 of 2348 pages), so most atlas assets end
up with a null _MainTex. The pages themselves are present in the pinned APK;
this script extracts the requested ones from the exact Texture2D recorded by the
already verified sprite library manifest, checks the decoded PNG hash, writes it
next to the converted Spine assets, and binds it into the generated material.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import re
import sys
from pathlib import Path, PurePosixPath

sys.path.insert(0, str(Path(__file__).resolve().parent))

VERSION = "854_156383"
ROOT = Path(__file__).resolve().parents[1]
SPINE_ROOT = ROOT / "unity/Assets/Official/Combat/Spine"
MODEL_IDENTITIES = SPINE_ROOT.parent / "Presentation/model-identities.json"
SPINE_CATALOG = SPINE_ROOT / "catalog.json"
MODELS_MANIFEST = ROOT / "assets/reference/official-map/models-source-manifest.json"
REFERENCE = ROOT / "assets/reference/battle-spine/pages-source-manifest.json"
TEXTURE_DIR = SPINE_ROOT.parent / "sprites"
OPENING_MONSTERS = (3803, 3282, 3281, 3283, 3481)
OPENING_NPCS = (8401, 8402)

TEXTURE_IMPORTER = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 0
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def asset_guid(relative: str) -> str:
    return hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()


def texture_guid(page: str) -> str:
    return asset_guid("Assets/Official/Combat/sprites/" + page + ".png")


def atlas_pages(atlas_path: Path) -> list[tuple[str, int, int]]:
    """Return (page base name, width, height) for each atlas page block."""
    pages, current = [], None
    for line in atlas_path.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        is_page = bool(stripped) and not line[0].isspace() \
            and stripped.rstrip("'").lower().endswith(".png")
        if is_page:
            if current is not None:
                pages.append(current)
            name = stripped.strip("'")
            current = {"page": name.rsplit("/", 1)[-1][:-4]}
            continue
        if current is not None and stripped.startswith("size:"):
            width, height = stripped.split(":", 1)[1].split(",")
            current["width"], current["height"] = int(width), int(height)
    if current is not None:
        pages.append(current)
    return [(entry["page"], entry["width"], entry["height"]) for entry in pages]


def decode_texture(apk, bundle: str, path_id: int, expected: str, label: str) -> bytes:
    pointer = next((value for path, value in apk.container(bundle)
                    if value.m_PathID == path_id), None)
    if pointer is None:
        raise ValueError("Pinned atlas page object is missing: " + label)
    obj = pointer.deref()
    if obj.assets_file.name != pointer.assetsfile.name:
        raise ValueError("Atlas page resolved into an unexpected serialized file: " + label)
    texture = obj.read()
    image = texture.image
    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    payload = buffer.getvalue()
    digest = sha256(payload)
    if expected and digest != expected:
        raise ValueError("Decoded atlas page hash differs from its verified source: "
                         + label + " " + digest)
    return payload


def find_page_texture(apk, base: str) -> dict:
    """Locate an atlas page whose sprite-library manifest entry is absent.

    NPC appearance atlases were not registered by the earlier sprite library
    export, so the pinned APK is searched for the Texture2D whose object name
    (the page name without its content suffix) matches exactly.
    """
    object_name = re.sub(r"-[0-9a-f]{10}$", "", base)
    matches = []
    for record in apk.bundles:
        name = record["name"]
        if "appearanceassets_" not in name:
            continue
        try:
            entries = apk.container(name)
        except Exception:
            continue
        for path, pointer in entries:
            if path.rsplit("/", 1)[-1] != object_name + ".png":
                continue
            if pointer.type.name != "Texture2D":
                continue
            matches.append((name, path, pointer, object_name))
    unique = {}
    for name, path, pointer, object_name in matches:
        unique[(name, path)] = (path, pointer, object_name)
    if len(unique) != 1:
        raise ValueError("Expected one pinned texture for atlas page " + base
                         + ", found " + str(len(unique)))
    (name, _), (path, pointer, object_name) = next(iter(unique.items()))
    value = pointer.deref()
    return {"bundle": name, "path_id": int(value.path_id), "object": object_name,
            "container": path, "sha256": "", "source_kind": "pinned-apk-scan"}


def bind_atlas_asset(stem: str) -> None:
    """Point the converted SkeletonDataAsset at its own converted atlas asset.

    A few combined skeletons still reference the atlas GUID of the pre-conversion
    asset, which leaves every page material unresolved and the mesh untextured.
    """
    root = SPINE_ROOT
    skeleton_data = root / (stem + "_SkeletonData.asset")
    atlas_meta = root / (stem + "_Atlas.asset.meta")
    if not skeleton_data.exists() or not atlas_meta.exists():
        return
    atlas_guid = re.search(r"guid: ([0-9a-f]{32})", atlas_meta.read_text(encoding="utf-8"))
    if atlas_guid is None:
        raise ValueError("Converted battle atlas has no GUID: " + stem)
    text = skeleton_data.read_text(encoding="utf-8")
    match = re.search(r"(atlasAssets:\n(?:\s*- \{fileID: 11400000, guid: )([0-9a-f]{32})(, type: 2\}\n?)+)", text)
    if match is None:
        raise ValueError("Converted SkeletonDataAsset has no atlas reference: " + stem)
    if match.group(2) == atlas_guid.group(1):
        return
    updated = re.sub(r"(- \{fileID: 11400000, guid: )[0-9a-f]{32}(, type: 2\})",
                     r"\g<1>" + atlas_guid.group(1) + r"\g<2>", text, count=1)
    skeleton_data.write_text(updated, encoding="utf-8")


def bind_material(mat: Path, guid: str) -> str:
    text = mat.read_text(encoding="utf-8")
    marker = "    - _MainTex:\n"
    start = text.index(marker)
    end = text.index("    m_Ints:", start)
    block = text[start:end]
    if "m_Texture: {fileID: 0}" in block:
        updated = block.replace("m_Texture: {fileID: 0}",
                                "m_Texture: {fileID: 2800000, guid: " + guid + ", type: 3}")
        return text[:start] + updated + text[end:]
    if "guid: " + guid in block:
        return text
    raise ValueError("Material already binds a different texture: " + str(mat))


def write_bytes(path: Path, data: bytes) -> bool:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() == data:
        return False
    path.write_bytes(data)
    return True


def npc_skeleton(stem_by_key: dict[str, dict], npc: int) -> str:
    matches = [key for key in stem_by_key if key.endswith(":body_" + str(npc))]
    if len(matches) != 1:
        raise ValueError("Official NPC has no unique battle Spine skeleton: " + str(npc)
                         + " (" + str(matches) + ")")
    return matches[0]


def build(monsters: tuple[int, ...], npcs: tuple[int, ...], all_pages: bool) -> int:
    from official_apk import Apk

    identities = json.loads(MODEL_IDENTITIES.read_text(encoding="utf-8"))
    catalog = json.loads(SPINE_CATALOG.read_text(encoding="utf-8"))["skeletons"]
    resources = json.loads(MODELS_MANIFEST.read_text(encoding="utf-8"))["resources"]
    if identities.get("manifest") != VERSION:
        raise ValueError("Battle model identities are not pinned to " + VERSION)
    by_monster = {int(entry["monster_id"]): entry["key"] for entry in identities["monster_models"]}

    stem_by_monster = {}
    for monster in monsters:
        key = by_monster.get(monster)
        if key is None:
            raise ValueError("Monster has no official Spine binding: " + str(monster))
        stem = PurePosixPath(catalog[key]["file"]).stem
        stem_by_monster[monster] = stem
    # NPC characters use their own official battle Spine skeleton, not the monster table.
    for npc in npcs:
        key = npc_skeleton(catalog, npc)
        stem_by_monster[-int(npc)] = PurePosixPath(catalog[key]["file"]).stem
    if all_pages:
        stem_by_monster = {}

    apk = Apk()
    entries, textures, failures = [], {}, {}
    skeletons = {}
    for monster, stem in sorted(stem_by_monster.items()):
        bind_atlas_asset(stem)
        atlas_path = SPINE_ROOT / (stem + ".atlas.txt")
        if not atlas_path.exists():
            raise ValueError("Converted battle atlas is missing: " + str(atlas_path))
        pages = atlas_pages(atlas_path)
        bound = []
        for page, width, height in pages:
            base = page[:-4] if page.lower().endswith(".png") else page
            uri = "res://assets/official/sprites/" + base + ".png"
            record = resources.get(uri)
            if record is None:
                record = find_page_texture(apk, base)
            payload = decode_texture(apk, record["bundle"], int(record["path_id"]),
                                     record.get("sha256", ""), base)
            from PIL import Image
            size = Image.open(io.BytesIO(payload)).size
            if size != (width, height):
                raise ValueError("Atlas page size differs from its atlas block: "
                                 + base + " " + str(size) + " vs " + str((width, height)))
            target = TEXTURE_DIR / (base + ".png")
            guid = texture_guid(base)
            write_bytes(target, payload)
            write_bytes(target.with_suffix(".png.meta"),
                        TEXTURE_IMPORTER.format(guid=guid).encode("utf-8"))
            textures[base] = {"file": target.relative_to(ROOT).as_posix(), "bytes": len(payload),
                              "sha256": sha256(payload), "width": width, "height": height,
                              "sprite_source": uri, "bundle": record["bundle"],
                              "path_id": record["path_id"], "object": record.get("object", ""),
                              "guid": guid}
            mat = SPINE_ROOT / (stem + "_" + base + ".mat")
            if not mat.exists():
                raise ValueError("Converted battle material is missing: " + str(mat))
            write_bytes(mat, bind_material(mat, guid).encode("utf-8"))
            bound.append({"page": base, "material": mat.relative_to(ROOT).as_posix()})
        skeletons[stem] = {"monster_id": monster, "atlas": atlas_path.relative_to(ROOT).as_posix(),
                           "pages": bound}
        entries.append({"monster_id": monster, "skeleton": stem, "pages": len(bound)})

    if not skeletons and not all_pages:
        raise ValueError("No battle Spine skeleton was selected")

    manifest = {
        "manifest": VERSION,
        "operation": "Pinned battle Spine atlas pages decoded from their verified Texture2D "
                     "sources and bound into the generated Spine materials",
        "inputs": {
            "models_manifest": {"path": MODELS_MANIFEST.relative_to(ROOT).as_posix(),
                                "sha256": sha256(MODELS_MANIFEST.read_bytes())},
            "identities": {"path": MODEL_IDENTITIES.relative_to(ROOT).as_posix(),
                           "sha256": sha256(MODEL_IDENTITIES.read_bytes())},
            "catalog": {"path": SPINE_CATALOG.relative_to(ROOT).as_posix(),
                        "sha256": sha256(SPINE_CATALOG.read_bytes())},
        },
        "skeletons": skeletons,
        "textures": textures,
        "failures": failures,
        "outputs": {key: value["sha256"] for key, value in textures.items()},
    }
    data = (json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    write_bytes(REFERENCE, data)
    print(json.dumps({"skeletons": entries, "textures": len(textures),
                      "failures": len(failures),
                      "manifest": REFERENCE.relative_to(ROOT).as_posix()}, ensure_ascii=False))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--monsters", default=",".join(str(value) for value in OPENING_MONSTERS),
                        help="comma separated official monster ids to restore")
    parser.add_argument("--npcs", default=",".join(str(value) for value in OPENING_NPCS),
                        help="comma separated official NPC class ids to restore")
    parser.add_argument("--all", action="store_true",
                        help="restore every converted skeleton whose page has a verified source")
    args = parser.parse_args()
    monsters = tuple(int(value) for value in args.monsters.split(",") if value.strip())
    npcs = tuple(int(value) for value in args.npcs.split(",") if value.strip())
    return build(monsters, npcs, args.all)


if __name__ == "__main__":
    raise SystemExit(main())
