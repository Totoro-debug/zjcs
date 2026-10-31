"""Verify Map13 follower bindings against the existing identical native NPC data.

Uses only Python's standard library. Does not load an animation runtime or bake
poses. The receipt describes source identity and binding, not visual acceptance.
"""
from __future__ import annotations

import gzip
import hashlib
import io
import json
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[1]
MAP13 = ROOT / "unity/Assets/Official/World/Map/13/Source/native-entity-skeletons.json"
MAP12 = ROOT / "unity/Assets/Official/World/Map/12/Source/team-npc-skeletons.json"
CATALOG = ROOT / "unity/Assets/Game/Resources/Map12/Native/TeamNpcBattle/catalog.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-follower-skeleton-resources-source-manifest.json"
MATERIALS = ROOT / "unity/Assets/Game/Resources/Map13/Native/native-entity-materials-catalog.asset"
FOLLOWER_MATERIALS = MATERIALS.with_name("npc-follower-material-catalog.asset")


def record(path: Path) -> dict:
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def package(path: Path) -> tuple[str, str, float, dict, list]:
    raw = io.BytesIO(path.read_bytes())

    def uint() -> int:
        return struct.unpack("<I", raw.read(4))[0]

    def string() -> str:
        return raw.read(uint()).decode("utf8")

    def block():
        length, digest = uint(), raw.read(32)
        data = gzip.decompress(raw.read(uint()))
        if len(data) != length or hashlib.sha256(data).digest() != digest:
            raise ValueError("Original NPC native block hash/length differs")
        return json.loads(data)

    if raw.read(8) != b"ZJCSNPCS" or uint() != 1:
        raise ValueError("Unknown NPC native data format")
    manifest, name = string(), string()
    scale = struct.unpack("<f", raw.read(4))[0]
    skeleton, atlas = block(), block()
    if raw.read():
        raise ValueError("Trailing NPC native package data")
    return manifest, name, scale, skeleton, atlas


def prefab_model(class_id: int) -> dict:
    paths = list((ROOT / "unity/Assets/Official/World/Map/14/NativeEntityPrefabs/Prefabs").rglob(f"{class_id}_view.prefab"))
    if len(paths) != 1:
        raise ValueError("Shared original NPC prefab is absent or ambiguous")
    path = paths[0]
    blocks = {int(match[1]): match[2] for match in re.finditer(
        r"^--- !u!\d+ &(\d+)\n(.*?)(?=^--- !u!|\Z)", path.read_text(encoding="utf8"), re.M | re.S)}
    go, = [key for key, value in blocks.items() if value.startswith("GameObject:")
           and f"  m_Name: NpcModel_{class_id}\n" in value]
    transform, = [value for value in blocks.values() if value.startswith("Transform:")
                   and re.search(r"m_GameObject: \{fileID: " + str(go) + r"\}", value)]

    def vector(name: str) -> list[float]:
        value = re.search(name + r": \{x: ([-.0-9]+), y: ([-.0-9]+), z: ([-.0-9]+)\}", transform)
        return [float(value[i]) for i in (1, 2, 3)]

    scale, offset = vector("m_LocalScale"), vector("m_LocalPosition")
    if scale != [.23 if class_id == 8402 else .3] * 2 + [1] or offset != [0, -.5, -.5]:
        raise ValueError("Original shared NPC model transform differs")
    return dict(prefab=record(path), scale=scale, offset=offset)


def build() -> None:
    source = json.loads(MAP13.read_bytes())
    previous = json.loads(MAP12.read_bytes())
    runtime = json.loads(CATALOG.read_bytes())
    if source["manifest"] != "871_156598" or previous["manifest"] != "871_156598" or runtime["manifest"] != "871_156598":
        raise ValueError("Do not mix original NPC resource versions")
    models, bindings = [], []
    by_identity = {row["identity"]: row for row in previous["skeletons"]}
    wanted = {8402, 8440, 8441, 8442}
    for model in runtime["models"]:
        if model["name"] not in ("npc_middle_model_SkeletonData", "npc_big_model_SkeletonData"):
            continue
        original = next(row for row in source["skeletons"] if row["name"] == model["name"])
        identity = original["cab"] + ":" + str(original["path_id"])
        if identity != model["identity"]:
            raise ValueError("Map13 and shared NPC skeleton object identities differ")
        shared = by_identity[identity]
        atlas13 = original["atlases"][0]["output"]
        atlas12 = shared["atlases"][0]["output"]
        if (ROOT / atlas13["path"]).read_bytes() != (ROOT / atlas12["path"]).read_bytes():
            raise ValueError("Map13 and shared NPC original atlas text differ")
        path = ROOT / ("unity/Assets/Game/Resources/" + model["resource"] + ".bytes")
        manifest, name, scale, skeleton, regions = package(path)
        if manifest != runtime["manifest"] or name != model["name"] or scale != model["scale"]:
            raise ValueError("Shared NPC native package identity/scale differs")
        original_path = ROOT / original["decoded"]["path"]
        if skeleton != json.loads(original_path.read_bytes()):
            raise ValueError("Map13 NPC bones, skins, constraints or animations differ from the shared native package")
        if len(regions) != model["regions"]:
            raise ValueError("Shared NPC atlas region projection is incomplete")
        models.append(dict(identity=identity, name=name, scale=scale, resource=model["resource"],
                           decodedSource=record(original_path), originalAtlas=record(ROOT / atlas13["path"]),
                           sharedAtlas=record(ROOT / atlas12["path"]), package=record(path),
                           sourceContentEqual=True, atlasBytesEqual=True))
    for row in runtime["renderers"]:
        if row["npcClassId"] not in wanted:
            continue
        model = runtime["models"][row["skeletonIndex"]]
        if model["name"] not in {value["name"] for value in models}:
            raise ValueError("Follower references an unverified shared skeleton")
        original_model = prefab_model(row["npcClassId"])
        bindings.append(dict(row, resource=model["resource"], modelScale=original_model["scale"],
                             modelOffset=original_model["offset"], originalPrefab=original_model["prefab"]))
    if {row["npcClassId"] for row in bindings} != wanted:
        raise ValueError("Original four-follower scope is incomplete")
    # Keep the same original material references without loading every Map13
    # entity material when a story follower is created.
    material_ids = {"1601366034732659983", "-8287306839946497736", "-8049600694219881985", "-6569910388499614487", "-8466807034191633858"}
    text = MATERIALS.read_text(encoding="utf8")
    header, rows = text.split("  Bindings:\n", 1)
    chosen = [row for row in re.findall(r"  - OfficialPath: [^\n]+\n    Asset: [^\n]+\n?", rows)
              if row.splitlines()[0].rsplit(":", 1)[-1] in material_ids]
    if len(chosen) != 5:
        raise ValueError("Original NPC material closure is incomplete")
    header = "\n".join(line.rstrip() for line in header.splitlines()) + "\n"
    FOLLOWER_MATERIALS.write_text(header.replace("m_Name: native-entity-materials-catalog", "m_Name: npc-follower-material-catalog")
                                 + "  Bindings:\n" + "".join(row.rstrip() + "\n" for row in chosen), encoding="utf8", newline="\n")
    Path(str(FOLLOWER_MATERIALS) + ".meta").write_text("fileFormatVersion: 2\nguid: "
        + hashlib.md5(b"zjcs:map13:npc-follower-materials").hexdigest()
        + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf8", newline="\n")
    result = dict(format=1, mapClassId=13, manifest="871_156598", generator=record(Path(__file__)),
                  inputs=[record(path) for path in (MAP13, MAP12, CATALOG, MATERIALS)], models=models, bindings=bindings,
                  outputs=[record(FOLLOWER_MATERIALS), record(Path(str(FOLLOWER_MATERIALS) + ".meta"))],
                  operation="Verify exact original CAB/path IDs, atlas byte equality and parsed skeleton/skin/constraint/animation equality. Reuse existing native NPC packages without pose baking or animation runtime dependencies.",
                  limitations=["This receipt proves source/package equality only. Native follower rendering, constraints, story input, persistence and Player acceptance are tracked in docs/maps/map-13.md."])
    RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print("Verified two shared original NPC skeleton/atlas packages and four follower bindings.")


if __name__ == "__main__":
    build()
