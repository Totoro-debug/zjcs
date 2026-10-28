"""Reuse byte-identical selected-version original NPC appearance performances."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map13_story_playable_inputs import CACHE
from prepare_unity_map13_water_entity_export import native_io_path
from verify_unity_map13_npc_appearance_native import verify as verify_native_fields

PREFIX = ROOT / "assets/reference/official-map"
SOURCE = ROOT / "unity/Assets/Official/World/Map/13/Source/story-playables.json"
SHARED_PROOF = PREFIX / "map14-native-npc-appearance-playables-source-manifest.json"
HIERARCHY_PROOF = PREFIX / "map13-story-playable-hierarchy-source-manifest.json"
NATIVE_PROOF = PREFIX / "map13-native-npc-appearance-fields-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map13/Native/native-npc-appearance-playables-catalog.asset"
RECEIPT = PREFIX / "map13-native-npc-appearance-bindings-source-manifest.json"
NAMES = {"playable_npc_appear", "playable_npc_disappear", "playable_FeiLe_appear", "playable_FeiLe_disappear"}


def record(path):
    return dict(path=source_path(path), sha256=sha(native_io_path(path).read_bytes()))


def identities(node):
    yield node["object"]
    yield from node["components"]
    for child in node["children"]:
        yield from identities(child)


def build():
    source, proof = (json.loads(p.read_bytes()) for p in (SOURCE, SHARED_PROOF))
    if any(s["manifest"] != "871_156598" for s in (source, proof)):
        raise ValueError("Original appearance performance version selection differs")
    if source["mapClassId"] != 13 or proof["mapClassId"] != 14:
        raise ValueError("Original appearance performance provenance differs")
    selected = [p for p in source["prefabs"] if Path(p["path"]).stem in NAMES]
    if len(selected) != 4:
        raise ValueError("Original appearance performance coverage differs")
    objects = {o["identity"]: o for o in source["objects"]}
    verified = []
    for prefab in selected:
        for identity in identities(prefab["hierarchy"]):
            verified.append(dict(identity=identity, sha256=objects[identity]["object_sha256"]))
    # Every delivered shared dependency must still match its source receipt.
    # This includes the explicit runtime script and original material bindings.
    for row in proof["outputs"]:
        if sha((ROOT / row["path"]).read_bytes()) != row["sha256"]:
            raise ValueError("Verified shared appearance output changed: " + row["path"])
    native = verify_native_fields()
    exported = CACHE / "export/ExportedProject/Assets"
    guid_pattern = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")
    reference_pattern = re.compile(r"guid: ([a-f0-9]{32})")
    def asset_guid(path):
        return guid_pattern.search(native_io_path(Path(str(path) + ".meta")).read_text(encoding="utf-8-sig"))[1]
    export_index = {asset_guid(p.with_suffix("")): p.with_suffix("") for p in exported.rglob("*.meta")}
    native_index = {}
    pinned = {}
    def collect(value):
        if isinstance(value, dict):
            if value.get("path", "").startswith("unity/") and "sha256" in value:
                path = ROOT / value["path"]
                pinned[path] = value["sha256"]
                if path.suffix == ".meta":
                    if sha(path.read_bytes()) != value["sha256"]:
                        raise ValueError("Shared native importer changed: " + str(path))
                    match = guid_pattern.search(path.read_text(encoding="utf-8-sig"))
                    if match:
                        native_index[match[1]] = path.with_suffix("")
            for child in value.values(): collect(child)
        elif isinstance(value, list):
            for child in value: collect(child)
    collect(proof)
    source_identities = {asset_guid(ROOT / row["path"]): row["source"] for row in native["references"]}
    materials = {row["cab"].casefold() + ":" + row["path_id"]: row for row in proof["materials"]}
    scripts = {ROOT / row["target"]["path"]: row for row in proof["scripts"]}
    visited, remaps, templates = set(), {}, []
    def verify_pair(original, target):
        pair = original, target
        if pair in visited: return
        visited.add(pair)
        if target not in pinned or sha(target.read_bytes()) != pinned[target]:
            raise ValueError("Shared appearance target is not source-pinned: " + str(target))
        templates.append(record(original))
        if original.suffix == ".cs":
            script = scripts[target]
            if original.name != Path(script["original"]["path"]).name or sha(original.read_bytes()) != script["original"]["sha256"]:
                raise ValueError("Shared migrated original script differs from Map13 export: " + str(original))
            return
        if original.suffix == ".mat":
            identity = source_identities[asset_guid(original)]
            if identity not in materials or ROOT / materials[identity]["output"]["path"] != target:
                raise ValueError("Shared original material CAB/path identity differs: " + identity)
            return
        left = native_io_path(original).read_text(encoding="utf-8-sig")
        right = target.read_text(encoding="utf-8-sig")
        if reference_pattern.sub("guid: ORIGINAL_TARGET", left) != reference_pattern.sub("guid: ORIGINAL_TARGET", right):
            raise ValueError("Shared appearance native fields/local IDs differ from Map13: " + str(target))
        for old, new in zip(reference_pattern.findall(left), reference_pattern.findall(right)):
            if old in remaps and remaps[old] != new:
                raise ValueError("Original dependency maps to different shared objects")
            remaps[old] = new
            verify_pair(export_index[old], native_index[new])
    shared_prefabs = {p["guid"]: p for p in proof["prefabs"]}
    for prefab in selected:
        shared = shared_prefabs[prefab["guid"]]
        if shared["officialPath"] != prefab["path"]:
            raise ValueError("Shared original prefab path differs")
        verify_pair(exported / prefab["path"].removeprefix("Assets/"), ROOT / shared["nativePath"])
    catalog = ROOT / "unity/Assets/Game/Resources/Map14/Native/native-npc-appearance-playables-catalog.asset"
    if source_path(catalog) not in {r["path"] for r in proof["outputs"]}:
        raise ValueError("Shared original appearance catalog is not source-pinned")
    raw = catalog.read_text(encoding="utf-8-sig")
    bound = re.findall(r"^  - OfficialPath: ([a-f0-9]{32})$", raw, re.M)
    if set(bound) != {p["guid"] for p in selected} or len(bound) != 4:
        raise ValueError("Shared original appearance catalog bindings differ")
    write(OUTPUT, raw.encode())
    metadata = Path(str(catalog) + ".meta").read_text(encoding="utf-8-sig")
    identity = hashlib.md5(b"zjcs-map13-native-original-npc-appearance-catalog").hexdigest()
    metadata, count = re.subn(r"(?m)^guid: [a-f0-9]{32}$", "guid: " + identity, metadata)
    if count != 1:
        raise ValueError("Shared native catalog importer metadata differs")
    write(Path(str(OUTPUT) + ".meta"), metadata.encode())
    outputs = [OUTPUT, Path(str(OUTPUT) + ".meta")]
    editor = ROOT / "unity/Assets/Game/Editor/Map13NpcAppearancePlayables"
    if editor.is_dir():
        outputs.extend(path for path in editor.iterdir() if path.is_file())
        outputs.append(Path(str(editor) + ".meta"))
    write(RECEIPT, encode(dict(manifest="871_156598", mapClassId=13,
        inputs=[record(p) for p in (SOURCE, HIERARCHY_PROOF, NATIVE_PROOF, SHARED_PROOF, catalog, Path(str(catalog) + ".meta"))],
        tools=[record(Path(__file__)), record(ROOT / "scripts/verify_unity_map13_npc_appearance_native.py")], objects=verified,
        templates=templates, native_fields=native["counts"],
        prefabs=[p for p in proof["prefabs"] if p["guid"] in bound],
        shared_outputs=proof["outputs"], outputs=[record(path) for path in outputs],
        counts=dict(prefabs=4, identical_original_objects=len(verified), verified_shared_outputs=len(proof["outputs"])),
        operation="Verify Map13 original hierarchy, script values and native particle/renderer/mesh/sound fields against its own pinned inputs. Compare shared native prefab/mesh/sound YAML byte-for-byte except GUIDs; prove each remapped script by exact converted-source hash and migrated-code binding, each material by original Map13 CAB/path identity and the pinned shared native output. Verify the complete shared closure and expose original asset GUID bindings in Map13. No dependency on uncommitted source extractions from other maps, or prefab/art/material/animation/runtime approximation.",
        limitations=["Shared native source verification does not prove Map13 live NPC appearance, audible events, complete story/map/save or Windows Player. These remain required."])))
    print("Original Map13 appearance bindings:", len(verified), "identical source objects; four native prefabs")


if __name__ == "__main__":
    build()
