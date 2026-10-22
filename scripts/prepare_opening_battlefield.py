"""Migrate the pinned opening fight-stage scene (game_902) into the Unity project.

The official stage package is 2D: no mesh renderers, but 144 sprite renderers,
4 tilemaps (3,813 tiles), 23 particle systems, two orthographic cameras and the
grid ground that lives in the stage tables. This script reads the verified
pinned bundle, flattens sprites and tiles into world-space quad batches grouped
by atlas page and sorting order, exports the official atlas/FX textures that the
scene and its particle materials use, and writes a compact runtime payload plus
a source manifest.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import math
import sys
from pathlib import Path, PurePosixPath

sys.path.insert(0, str(Path(__file__).resolve().parent))

VERSION = "854_156383"
ROOT = Path(__file__).resolve().parents[1]
SCENES = ROOT / "assets/official/battle-stages.json"
SCENES_MANIFEST = ROOT / "assets/reference/official-map/battle-stages-source-manifest.json"
BUNDLE = "defaultpackage_assets_prefabs_ecentity_fight_fightstage_902.bundle"
BUNDLE_MD5 = "67c160d271bba532c38a428252304295"
VISUAL_PATH = "Assets/Prefabs/EcEntity/Fight/FightStage/902/game_902.prefab"
HOST_PATH = "Assets/Prefabs/EcEntity/Fight/FightStage/902/902.prefab"
PAYLOAD = ROOT / "unity/Assets/StreamingAssets/Opening/battlefield"
TEXTURES = ROOT / "unity/Assets/Official/Opening/Battlefield/Resources/OpeningBattlefield/Textures"
REFERENCE = ROOT / "assets/reference/character-creation/opening-battlefield-source-manifest.json"
STAGE_ID = 902
LAYOUT_SOURCE = ROOT / "assets/official/battle-stages.json"

TEXTURE_META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
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
  alphaIsTransparency: 1
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


def write_bytes(path: Path, data: bytes) -> bool:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() == data:
        return False
    path.write_bytes(data)
    return True


def sanitize(value: str) -> str:
    keep = []
    for character in value:
        keep.append(character if character.isalnum() or character in "._-" else "_")
    return "".join(keep)


def texture_guid(name: str) -> str:
    return hashlib.md5(("zjcs-official:Assets/Official/Opening/Battlefield/Resources/"
                        "OpeningBattlefield/Textures/" + name + ".png").encode("utf-8")).hexdigest()


class StageScene:
    def __init__(self, apk, bundle: str, path: str):
        self.apk = apk
        self.bundle_name = bundle
        self.bundle = apk.load(bundle)
        self.container = dict(apk.container(bundle))
        pointer = self.container.get(path)
        if pointer is None:
            raise ValueError("Official fight stage prefab is missing: " + path)
        self.root = pointer.read()
        self.asset = pointer.assetsfile
        self.by_path = {obj.path_id: obj for obj in self.asset.objects.values()}
        self.textures = {}
        self.sprites = {}
        self.materials = {}
        self.records = {}

    def record(self, obj, kind: str) -> dict:
        key = (obj.assets_file.name, obj.path_id)
        if key not in self.records:
            info = {"bundle": self.bundle_name, "cab": obj.assets_file.name,
                    "path_id": str(obj.path_id), "kind": kind,
                    "raw_sha256": sha256(obj.get_raw_data())}
            container = getattr(obj.assets_file, "container", None) or {}
            for path, pointer in container.items():
                if pointer.path_id == obj.path_id:
                    info["container"] = path
                    break
            self.records[key] = info
        return self.records[key]

    def texture(self, pointer, role: str) -> dict | None:
        if pointer is None or not pointer.path_id:
            return None
        obj = pointer.deref()
        key = (obj.assets_file.name, obj.path_id)
        record = self.textures.get(key)
        if record is not None:
            record["roles"].add(role)
            return record
        data = obj.read()
        image = data.image
        buffer = io.BytesIO()
        image.save(buffer, format="PNG")
        payload = buffer.getvalue()
        record = {"name": data.m_Name, "width": data.m_Width, "height": data.m_Height,
                  "format": int(data.m_TextureFormat), "png": payload, "sha256": sha256(payload),
                  "roles": {role}, "source": self.record(obj, "Texture2D")}
        self.textures[key] = record
        return record

    def sprite(self, pointer, role: str) -> dict | None:
        if pointer is None or not pointer.path_id:
            return None
        obj = pointer.deref()
        key = (obj.assets_file.name, obj.path_id)
        record = self.sprites.get(key)
        if record is not None:
            record["roles"].add(role)
            return record
        data = obj.read()
        render = data.m_RD
        atlas = None
        if getattr(data, "m_SpriteAtlas", None) is not None and data.m_SpriteAtlas.path_id:
            atlas_obj = data.m_SpriteAtlas.deref()
            atlas_data = atlas_obj.read()
            render = dict(atlas_data.m_RenderDataMap).get(data.m_RenderDataKey)
            atlas = self.record(atlas_obj, "SpriteAtlas")
        if render is None:
            return None
        texture = self.texture(render.texture, role)
        rect = render.textureRect
        record = {
            "name": data.m_Name,
            "rect": [int(rect.x), int(rect.y), int(rect.width), int(rect.height)],
            "pivot": [float(data.m_Pivot.x), float(data.m_Pivot.y)],
            "pixels_to_units": float(data.m_PixelsToUnits or 100.0),
            "texture": texture["name"], "texture_width": texture["width"],
            "texture_height": texture["height"],
            "roles": {role}, "atlas": atlas, "source": self.record(obj, "Sprite"),
        }
        self.sprites[key] = record
        return record

    def material_texture(self, materials) -> dict | None:
        for pointer in materials or []:
            if pointer is None or not pointer.path_id:
                continue
            obj = pointer.deref()
            key = (obj.assets_file.name, obj.path_id)
            if key in self.materials:
                return self.materials[key]
            try:
                data = obj.read()
                saved = getattr(data, "m_SavedProperties", None)
                texture_pointer = None
                if saved is not None:
                    for name, environment in saved.m_TexEnvs:
                        if name == "_MainTex":
                            texture_pointer = environment.m_Texture
                            break
                info = self.texture(texture_pointer, "particle material") if texture_pointer is not None else None
            except Exception:
                info = None
            self.materials[key] = info
            return info
        return None


def quaternion_matrix(rotation) -> list[list[float]]:
    x, y, z, w = rotation
    return [
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ]


def multiply(left, right):
    return [[sum(left[row][k] * right[k][column] for k in range(3)) for column in range(3)]
            for row in range(3)]


def transform_point(matrix, position):
    return [matrix[row][0] * position[0] + matrix[row][1] * position[1]
            + matrix[row][2] * position[2] for row in range(3)]


def add(left, right):
    return [left[index] + right[index] for index in range(3)]


def local_matrix(position, rotation, scale):
    matrix = quaternion_matrix(rotation)
    for row in range(3):
        for column in range(3):
            matrix[row][column] *= scale[column]
    return matrix, list(position)


def compose(parent, local):
    matrix, position = local
    return multiply(parent[0], matrix), add(transform_point(parent[0], position), parent[1])


class Batches:
    def __init__(self):
        self.items = {}

    def add(self, texture, layer, order, z, corners, uv, color):
        key = (texture, layer, order, round(z, 3))
        batch = self.items.get(key)
        if batch is None:
            batch = {"texture": texture, "layer": layer, "order": order, "z": z,
                     "positions": [], "uvs": [], "colors": [], "indices": []}
            self.items[key] = batch
        base = len(batch["positions"]) // 3
        for index in range(4):
            batch["positions"].extend([round(corners[index][0], 4), round(corners[index][1], 4), 0.0])
            batch["uvs"].extend(uv[index])
            batch["colors"].extend(color)
        batch["indices"].extend([base, base + 1, base + 2, base, base + 2, base + 3])


def sprite_corners(matrix, position, sprite, scale, flip):
    width = sprite["rect"][2] / sprite["pixels_to_units"]
    height = sprite["rect"][3] / sprite["pixels_to_units"]
    pivot = sprite["pivot"]
    left = -pivot[0] * width
    right = (1.0 - pivot[0]) * width
    bottom = -pivot[1] * height
    top = (1.0 - pivot[1]) * height
    local = [[left, bottom, 0.0], [left, top, 0.0], [right, top, 0.0], [right, bottom, 0.0]]
    corners = []
    for point in local:
        scaled = [point[0] * scale[0], point[1] * scale[1], point[2] * scale[2]]
        corners.append(add(transform_point(matrix, scaled), position))
    u0 = sprite["rect"][0] / sprite["texture_width"]
    v0 = sprite["rect"][1] / sprite["texture_height"]
    u1 = (sprite["rect"][0] + sprite["rect"][2]) / sprite["texture_width"]
    v1 = (sprite["rect"][1] + sprite["rect"][3]) / sprite["texture_height"]
    if flip[0]:
        u0, u1 = u1, u0
    if flip[1]:
        v0, v1 = v1, v0
    uv = [[u0, v0], [u0, v1], [u1, v1], [u1, v0]]
    return corners, uv


def read_transform(data):
    position = [round(data.m_LocalPosition.x, 6), round(data.m_LocalPosition.y, 6),
                round(data.m_LocalPosition.z, 6)]
    rotation = [round(data.m_LocalRotation.x, 6), round(data.m_LocalRotation.y, 6),
                round(data.m_LocalRotation.z, 6), round(data.m_LocalRotation.w, 6)]
    scale = [round(data.m_LocalScale.x, 6), round(data.m_LocalScale.y, 6), round(data.m_LocalScale.z, 6)]
    return position, rotation, scale


def walk(scene: StageScene, root, parent_matrix, batches: Batches, particles, cameras,
         grid_cell, warnings, depth=0):
    data = root
    transform = None
    components = []
    for entry in data.m_Component:
        pointer = entry.component
        if pointer is None or not pointer.path_id:
            continue
        obj = pointer.deref()
        components.append((obj, obj.read() if obj.type.name not in ("MonoBehaviour",) else None))
        if obj.type.name in ("Transform", "RectTransform") and transform is None:
            transform = obj.read()
    local = ([ [1.0,0.0,0.0],[0.0,1.0,0.0],[0.0,0.0,1.0] ], [0.0,0.0,0.0])
    if transform is not None:
        local = local_matrix(*read_transform(transform))
    matrix, position = compose(parent_matrix, local)
    active = bool(data.m_IsActive)

    if active:
        for obj, component in components:
            kind = obj.type.name
            if kind == "SpriteRenderer" and component is not None:
                sprite = scene.sprite(component.m_Sprite, "scene")
                if sprite is None:
                    continue
                # The composed matrix already carries the local scale; only the
                # sprite's own unit size is applied here.
                corners, uv = sprite_corners(matrix, position, sprite, [1.0, 1.0, 1.0],
                                             (bool(component.m_FlipX), bool(component.m_FlipY)))
                color = [round(component.m_Color.r, 4), round(component.m_Color.g, 4),
                         round(component.m_Color.b, 4), round(component.m_Color.a, 4)]
                batches.add(sanitize(sprite["texture"]), int(component.m_SortingLayer),
                            int(component.m_SortingOrder), position[2], corners, uv, color)
            elif kind == "Tilemap" and component is not None:
                sprites = {}
                for index, entry in enumerate(component.m_TileSpriteArray):
                    pointer = getattr(entry, "m_Data", None)
                    if pointer is None or not pointer.path_id:
                        continue
                    info = scene.sprite(pointer, "tile")
                    if info is not None:
                        sprites[index] = info
                layer, order = 0, 0
                for renderer_obj, renderer in components:
                    if renderer_obj.type.name == "TilemapRenderer" and renderer is not None:
                        layer, order = int(renderer.m_SortingLayer), int(renderer.m_SortingOrder)
                cell = grid_cell
                anchor = 0.5
                for cell_position, tile in component.m_Tiles:
                    index = getattr(tile, "m_TileSpriteIndex", 0)
                    sprite = sprites.get(index)
                    if sprite is None:
                        continue
                    tile_matrix = [[1.0,0.0,0.0],[0.0,1.0,0.0],[0.0,0.0,1.0]]
                    tile_color = [1.0, 1.0, 1.0, 1.0]
                    raw = getattr(tile, "m_Transform", None)
                    if raw is not None:
                        columns = raw.m_C0, raw.m_C1, raw.m_C2
                        tile_matrix = [[getattr(columns[column], "x"), getattr(columns[column], "y"),
                                        getattr(columns[column], "z")] for column in range(3)]
                    raw_color = getattr(tile, "m_Color", None)
                    if raw_color is not None:
                        tile_color = [round(raw_color.r, 4), round(raw_color.g, 4),
                                      round(raw_color.b, 4), round(raw_color.a, 4)]
                    offset = transform_point(tile_matrix,
                                             [(cell_position.x + anchor) * cell[0],
                                              (cell_position.y + anchor) * cell[1], 0.0])
                    tile_position = add(transform_point(matrix, offset), position)
                    corners, uv = sprite_corners(matrix, tile_position, sprite, [1.0, 1.0, 1.0], (False, False))
                    batches.add(sanitize(sprite["texture"]), layer, order, position[2], corners, uv, tile_color)
            elif kind == "ParticleSystem" and component is not None:
                info = particle_data(component)
                material = None
                for other_obj, other in components:
                    if other_obj.type.name == "ParticleSystemRenderer" and other is not None:
                        material = scene.material_texture(other.m_Materials)
                        info["render_mode"] = int(other.m_RenderMode)
                        info["sorting_layer"] = int(other.m_SortingLayer)
                        info["sorting_order"] = int(other.m_SortingOrder)
                info.update(position=[round(position[0], 3), round(position[1], 3), round(position[2], 3)],
                            rotation=float(math.degrees(math.atan2(matrix[1][0], matrix[0][0]))),
                            scale=[round(transform.m_LocalScale.x, 4) if transform is not None else 1.0,
                                   round(transform.m_LocalScale.y, 4) if transform is not None else 1.0])
                if material is not None:
                    info["texture"] = sanitize(material["name"])
                particles.append(info)
            elif kind == "Camera" and component is not None:
                camera_object = component.m_GameObject.read()
                viewport = component.m_NormalizedViewPortRect
                cameras.append({
                    "name": camera_object.m_Name,
                    "position": [round(position[0], 4), round(position[1], 4), round(position[2], 4)],
                    "orthographic": bool(component.orthographic),
                    "orthographic_size": round(float(component.orthographic_size), 4),
                    "near": round(float(component.near_clip_plane), 4),
                    "far": round(float(component.far_clip_plane), 4),
                    "depth": round(float(component.m_Depth), 4),
                    "clear_flags": int(component.m_ClearFlags),
                    "culling_mask": int(component.m_CullingMask.m_Bits),
                    "viewport": [round(float(viewport.x), 4), round(float(viewport.y), 4),
                                 round(float(viewport.width), 4), round(float(viewport.height), 4)],
                    "background": [round(component.m_BackGroundColor.r, 4),
                                   round(component.m_BackGroundColor.g, 4),
                                   round(component.m_BackGroundColor.b, 4),
                                   round(component.m_BackGroundColor.a, 4)],
                    "active": bool(component.m_GameObject.read().m_IsActive),
                })
            elif kind == "Grid" and component is not None:
                grid_cell[0] = float(component.m_CellSize.x)
                grid_cell[1] = float(component.m_CellSize.y)

    if transform is not None:
        for child in transform.m_Children:
            if child is None or not child.path_id:
                continue
            walk(scene, child.read().m_GameObject.read(), (matrix, position), batches, particles,
                 cameras, grid_cell, warnings, depth + 1)


def minmax(value):
    if value is None:
        return None
    scalar = getattr(value, "scalar", None)
    if scalar is not None:
        return [float(scalar), float(scalar)]
    minimum = getattr(value, "minScalar", None)
    maximum = getattr(value, "maxScalar", None)
    if minimum is None or maximum is None:
        return None
    return [float(minimum), float(maximum)]


def particle_color(value):
    if value is None:
        return [1.0, 1.0, 1.0, 1.0]
    constant = getattr(value, "minColor", None)
    maximum = getattr(value, "maxColor", None)
    color = constant if constant is not None else maximum
    if color is None:
        return [1.0, 1.0, 1.0, 1.0]
    return [round(float(color.r), 4), round(float(color.g), 4),
            round(float(color.b), 4), round(float(color.a), 4)]


def particle_data(data) -> dict:
    initial = getattr(data, "InitialModule", None)
    emission = getattr(data, "EmissionModule", None)
    shape = getattr(data, "ShapeModule", None)
    info = {
        "duration": round(float(data.lengthInSec), 3),
        "looping": bool(data.looping),
        "play_on_awake": bool(data.playOnAwake),
        "simulation_speed": round(float(data.simulationSpeed), 4),
        "scaling_mode": int(data.scalingMode),
        "prewarm": bool(getattr(data, "prewarm", False)),
        "max_particles": int(initial.maxNumParticles) if initial is not None else 10,
        "start_lifetime": minmax(getattr(initial, "startLifetime", None)),
        "start_speed": minmax(getattr(initial, "startSpeed", None)),
        "start_size": minmax(getattr(initial, "startSize", None)),
        "start_rotation": minmax(getattr(initial, "startRotation", None)),
        "gravity": minmax(getattr(initial, "gravityModifier", None)),
        "start_color": particle_color(getattr(initial, "startColor", None)),
        "emission_enabled": bool(emission.enabled) if emission is not None else False,
        "rate_over_time": minmax(emission.rateOverTime) if emission is not None else None,
        "bursts": len(emission.m_Bursts or []) if emission is not None else 0,
        "shape_enabled": bool(shape.enabled) if shape is not None else False,
        "shape_type": int(shape.type) if shape is not None else 0,
        "shape_angle": round(float(shape.angle), 3) if shape is not None else 0.0,
        "shape_radius": minmax(shape.radius) if shape is not None else None,
    }
    return info


def load_npc_models() -> list[dict]:
    import gzip
    record_path = ROOT / "unity/Assets/StreamingAssets/Opening/opening-record.bytes"
    data = gzip.decompress(record_path.read_bytes()).decode("utf-8", "replace")
    class_ids = sorted({int(value) for value in __import__("re").findall(r'"NpcClassId":\s*(\d+)', data)})
    catalog = json.loads((ROOT / "unity/Assets/Official/Combat/Spine/catalog.json").read_text(encoding="utf-8"))
    skeletons = catalog["skeletons"]
    models = []
    for class_id in class_ids:
        matches = [key for key in skeletons if key.endswith(":body_" + str(class_id))]
        if len(matches) != 1:
            raise ValueError("Official NPC class has no unique battle Spine skeleton: " + str(class_id))
        key = matches[0]
        entry = skeletons[key]
        skins = entry.get("skins") or []
        skin = entry.get("skin") or (skins[0] if len(skins) == 1 else "")
        models.append({"class_id": class_id, "skeleton": key,
                       "skin": skin, "scale": float(entry.get("scale") or 1.0),
                       "file": PurePosixPath(entry["file"]).stem})
    return models


def load_grid() -> dict:
    data = json.loads(SCENES.read_text(encoding="utf-8"))
    stage = data["stages"][str(STAGE_ID)]
    ground = stage["FightGridGroundComponent"]
    fight = stage.get("FightComponent") or {}
    blocked = []
    for cell in ground["PosInfos"]:
        if int(cell["GroundType"]) in (2, 4):
            blocked.append([int(cell["Pos"]["X"]), int(cell["Pos"]["Y"])])
    entry = ((fight.get("FactionInfoList") or [{}])[0].get("EntryPosList") or [{"X": 0, "Y": 0}])[0]
    waves = stage.get("FightRoleMonsterNormalComponent") or {}
    return {
        "stage_id": STAGE_ID,
        "scene": fight.get("SceneName", "game_902"),
        "origin": [float(ground["OriginPos"][0]), float(ground["OriginPos"][1]),
                   float(ground["OriginPos"][2])],
        "size": [int(ground["Size"]["X"]), int(ground["Size"]["Y"])],
        "entry": [int(entry["X"]), int(entry["Y"])],
        "wave_origin": [[int(value["X"]), int(value["Y"])] for value in waves.get("WaveOriPos") or []],
        "blocked": blocked,
    }


def build() -> int:
    from official_apk import Apk

    apk = Apk()
    if apk.index.get(BUNDLE) != BUNDLE_MD5:
        raise ValueError("Fight stage bundle hash differs from the pinned manifest")
    scene = StageScene(apk, BUNDLE, VISUAL_PATH)
    host = StageScene(apk, BUNDLE, HOST_PATH)

    batches = Batches()
    particles, cameras, warnings = [], [], []
    identity = ([ [1.0,0.0,0.0],[0.0,1.0,0.0],[0.0,0.0,1.0] ], [0.0,0.0,0.0])
    grid_cell = [1.0, 1.0]
    walk(scene, scene.root, identity, batches, particles, cameras, grid_cell, warnings)
    if not batches.items:
        raise ValueError("Official fight stage produced no render batches")
    if not cameras:
        raise ValueError("Official fight stage has no camera")

    texture_list = []
    texture_index = {}
    for record in scene.textures.values():
        name = sanitize(record["name"])
        if name in texture_index:
            if texture_index[name] != record["sha256"]:
                raise ValueError("Two official textures share a sanitized name: " + name)
            continue
        texture_index[name] = record["sha256"]
        texture_list.append({"name": name, "width": record["width"], "height": record["height"],
                             "sha256": record["sha256"], "source": record["source"],
                             "roles": sorted(record["roles"])})
        target = TEXTURES / (name + ".png")
        write_bytes(target, record["png"])
        write_bytes(target.with_suffix(".png.meta"),
                    TEXTURE_META.format(guid=texture_guid(name)).encode("utf-8"))

    unknown = {batch["texture"] for batch in batches.items.values()} - set(texture_index)
    if unknown:
        raise ValueError("Render batches reference unexported textures: " + str(sorted(unknown)))

    payload_batches = []
    for batch in batches.items.values():
        payload_batches.append({
            "texture": sanitize(batch["texture"]), "layer": batch["layer"], "order": batch["order"],
            "positions": batch["positions"], "uvs": batch["uvs"],
            "colors": batch["colors"], "indices": batch["indices"]})
    payload_batches.sort(key=lambda entry: (entry["layer"], entry["order"], entry["texture"]))

    root_transform = None
    for entry in scene.root.m_Component:
        if entry.component is not None and entry.component.type.name == "Transform":
            root_transform = entry.component.read()
            break
    root_offset = [round(root_transform.m_LocalPosition.x, 4), round(root_transform.m_LocalPosition.y, 4),
                   round(root_transform.m_LocalPosition.z, 4)] if root_transform is not None else [0.0, 0.0, 0.0]
    main = next((camera for camera in cameras if camera["active"]), cameras[0])
    payload = {
        "manifest": VERSION,
        "stage": STAGE_ID,
        "scene": "game_902",
        "cell_size": grid_cell,
        "root_offset": root_offset,
        "world_origin": root_offset,
        "cameras": cameras,
        "grid": load_grid(),
        "textures": texture_list,
        "batches": payload_batches,
        "particles": particles,
        "npc_models": load_npc_models(),
    }
    data = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    target = PAYLOAD / "game_902.json"
    write_bytes(target, data)

    manifest = {
        "manifest": VERSION,
        "operation": "Pinned fight-stage prefab flattened into world-space quad batches; "
                     "official sprite atlas and particle textures decoded from their pinned Texture2D objects",
        "inputs": {
            "bundle": {"name": BUNDLE, "md5": BUNDLE_MD5, "packaged_in_apk": True,
                       "visual_prefab": VISUAL_PATH, "host_prefab": HOST_PATH},
            "stage_table": {"path": SCENES_MANIFEST.relative_to(ROOT).as_posix(),
                            "sha256": sha256(SCENES.relative_to(ROOT).resolve().read_bytes())},
        },
        "counts": {
            "batches": len(payload_batches),
            "vertices": sum(len(batch["positions"]) // 3 for batch in payload_batches),
            "triangles": sum(len(batch["indices"]) // 3 for batch in payload_batches),
            "textures": len(texture_list),
            "particles": len(particles),
            "cameras": len(cameras),
        },
        "textures": texture_list,
        "objects": list(scene.records.values()),
        "warnings": warnings,
        "outputs": {
            target.relative_to(ROOT).as_posix(): sha256(data),
            **{("unity/Assets/Official/Opening/Battlefield/Resources/OpeningBattlefield/Textures/"
                + entry["name"] + ".png"): entry["sha256"] for entry in texture_list},
        },
    }
    write_bytes(REFERENCE, (json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True)
                            + "\n").encode("utf-8"))
    print(json.dumps({"stage": STAGE_ID, "batches": len(payload_batches), "textures": len(texture_list),
                      "particles": len(particles), "cameras": len(cameras),
                      "vertices": manifest["counts"]["vertices"],
                      "grid": payload["grid"]["size"], "cell": grid_cell}, ensure_ascii=False))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.parse_args()
    return build()


if __name__ == "__main__":
    raise SystemExit(main())
