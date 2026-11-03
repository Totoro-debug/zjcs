from __future__ import annotations

import hashlib
import json
import re
import struct
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / ".tools/source-packages/current/884_156692/native-conversion/native-export-all/ExportedProject/Assets"
SPRITE = SOURCE / "Image/UI/World/World_Atlas/Img_RoundLoad.asset"
SPRITE_META = SOURCE / "Image/UI/World/World_Atlas/Img_RoundLoad.asset.meta"
TEXTURE = SOURCE / "Texture2D/sactx-0-1024x2048-ASTC 6x6-World_Atlas-68ad306f.png"
TEXTURE_META = TEXTURE.with_name(TEXTURE.name + ".meta")
DESTINATION = ROOT / "unity/Assets/Official/World/Map/16/NativeCurrent/Image/UI/World/World_Atlas"
OUTPUT_SPRITE = DESTINATION / "Img_RoundLoad.asset"
OUTPUT_TEXTURE = DESTINATION / "World_Atlas.png"
RECEIPT = ROOT / "assets/reference/official-map/map16-entity-rendering-assets-source-manifest.json"
SPRITE_GUID = "5db3a929a5462c34d9e25967f68a5727"
TEXTURE_GUID = "ca1d4aa91394d5f4e8cab7add736d525"
EXPECTED_INPUTS = {
    "sprite": "d88986ba6e43111b166e5b46315f8291009f6535976ae0a8a7f16742249dc1ba",
    "sprite_meta": "a203848b6502443bbcc31fcc460b2de76007c526b1097d548e1a90dee7cf8638",
    "texture": "e9600374f87302f27efeb503c810f2bb6484a88b0ecf9fc1cca81b90baf20864",
    "texture_meta": "a89b46105627a835a0bb33d8790223ba4be2a886a310927ce36d03a2c1f32152",
}


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def read_guid(path: Path) -> str:
    match = re.search(r"(?m)^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8"))
    if not match:
        raise ValueError("Missing native GUID: " + path.as_posix())
    return match.group(1)


def texture_meta() -> bytes:
    text = f"""fileFormatVersion: 2
guid: {TEXTURE_GUID}
TextureImporter:
  serializedVersion: 13
  internalIDToNameTable: []
  externalObjects: {{}}
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
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
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 0
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
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
  flipbookRows: 0
  flipbookColumns: 0
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
    forceMaximumCompressionQuality_BC6_HQ: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    indices: []
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
    return text.encode("utf-8")


def build() -> dict:
    inputs = {"sprite": SPRITE.read_bytes(), "sprite_meta": SPRITE_META.read_bytes(),
              "texture": TEXTURE.read_bytes(), "texture_meta": TEXTURE_META.read_bytes()}
    if any(sha(data) != EXPECTED_INPUTS[name] for name, data in inputs.items()):
        raise ValueError("Selected 884_156692 Sprite or atlas input bytes changed")
    sprite_text = inputs["sprite"].decode("utf-8-sig")
    if read_guid(SPRITE_META) != SPRITE_GUID or read_guid(TEXTURE_META) != TEXTURE_GUID:
        raise ValueError("Selected original Sprite or atlas Texture2D GUID changed")
    if "m_Name: Img_RoundLoad" not in sprite_text or "guid: " + TEXTURE_GUID not in sprite_text:
        raise ValueError("Original Img_RoundLoad texture binding differs")
    expected_fields = ("x: 668.0761", "y: 610.0761", "width: 29.897127", "height: 29.897127",
                      "m_VertexCount: 8", "indexCount: 18", "uvTransform: {x: 100, y: 683, z: 100, w: 625}")
    if any(value not in sprite_text for value in expected_fields):
        raise ValueError("Original Sprite rectangle, mesh, or UV transform differs")
    if inputs["texture"][:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("AssetRipper atlas output is not a PNG")
    width, height = struct.unpack(">II", inputs["texture"][16:24])
    if (width, height) != (1024, 2048):
        raise ValueError("Original World_Atlas dimensions differ")

    outputs = {
        OUTPUT_SPRITE: inputs["sprite"],
        OUTPUT_SPRITE.with_name(OUTPUT_SPRITE.name + ".meta"): inputs["sprite_meta"],
        OUTPUT_TEXTURE: inputs["texture"],
        OUTPUT_TEXTURE.with_name(OUTPUT_TEXTURE.name + ".meta"): texture_meta(),
    }
    for path, data in outputs.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    receipt = {
        "format": 1,
        "mapClassId": 16,
        "manifest": "884_156692",
        "spriteGuid": SPRITE_GUID,
        "textureGuid": TEXTURE_GUID,
        "spriteGeometry": {"rect": [668.0761, 610.0761, 29.897127, 29.897127], "vertices": 8, "indices": 18,
                           "uvTransform": [100, 683, 100, 625]},
        "texture": {"width": width, "height": height, "sourceFormat": "ASTC 6x6", "installedFormat": "PNG/RGBA import"},
        "inputs": [{"path": path.relative_to(ROOT).as_posix(), "sha256": sha(data)}
                   for path, data in ((SPRITE, inputs["sprite"]), (SPRITE_META, inputs["sprite_meta"]),
                                      (TEXTURE, inputs["texture"]), (TEXTURE_META, inputs["texture_meta"]))],
        "outputs": [{"path": path.relative_to(ROOT).as_posix(), "sha256": sha(data)} for path, data in outputs.items()],
        "operation": "Keep the original Sprite native asset, GUID, mesh/index/UV data and atlas coordinates. Install the full AssetRipper-decoded original atlas as an uncompressed 2D Texture2D using its original texture GUID; do not substitute the same-name cropped image.",
    }
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return receipt


if __name__ == "__main__":
    proof = build()
    print("Installed Map16 original Img_RoundLoad and World_Atlas:", len(proof["outputs"]), "files")
