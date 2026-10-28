"""Restore the three original NPC Animator playable prefabs and explicit GUID bindings."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from prepare_unity_map14_story_playable_inputs import CACHE, SOURCE

EXPORTED = CACHE / "export/ExportedProject/Assets"
PROOF = ROOT / "assets/reference/official-map/map14-story-playable-hierarchy-source-manifest.json"
DESTINATION = ROOT / "unity/Assets/Official/World/Map/14/NativeNpcAnimatorPlayables"
RECEIPT = ROOT / "assets/reference/official-map/map14-native-npc-animator-playables-source-manifest.json"
NAMES = ("playable_npc_idle", "playable_npc_run", "playable_npc_float_02")
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")
REFERENCE = re.compile(r"guid: ([a-f0-9]{32})")


def record(path):
    return dict(path=source_path(path), sha256=sha(path.read_bytes()))


def guid(path):
    return GUID.search(Path(str(path) + ".meta").read_text(encoding="utf8"))[1]


def build():
    proof = json.loads(PROOF.read_bytes())
    if proof["manifest"] != "871_156598" or proof["mapClassId"] != 14:
        raise ValueError("Wrong original story playable selection")
    for entry in proof["inputs"]:
        if sha((ROOT / entry["path"]).read_bytes()) != entry["sha256"]:
            raise ValueError("Original verified playable source changed")
    source = json.loads(SOURCE.read_bytes())
    prefabs = [p for p in source["prefabs"] if Path(p["path"]).stem in NAMES]
    if len(prefabs) != 3:
        raise ValueError("Original NPC Animator prefab coverage differs")
    expected = {r["path"]: r["sha256"] for r in proof["outputs"]}
    index = {guid(p.with_suffix("")): p.with_suffix("") for p in EXPORTED.rglob("*.meta")}
    replacements, scripts, copies, seen = {}, [], [], set()
    queue = [EXPORTED / p["path"].removeprefix("Assets/") for p in prefabs]
    roots = list(queue)
    script_roots = [ROOT / "unity/Assets/Game/Runtime/Official" / n for n in ("StoryPlayables", "StoryAnimation")]
    while queue:
        path = queue.pop()
        if path in seen:
            continue
        seen.add(path)
        if sha(path.read_bytes()) != expected.get(source_path(path)):
            raise ValueError("Verified NPC playable export changed: " + str(path))
        if path.suffix == ".cs":
            matches = [p for folder in script_roots for p in folder.rglob(path.name)]
            if len(matches) != 1:
                raise ValueError("Original NPC playable class is not uniquely migrated: " + path.name)
            target = matches[0];replacements[guid(path)] = guid(target)
            scripts.append(dict(original=record(path), target=record(target), meta=record(Path(str(target) + ".meta"))))
            continue
        if path not in roots or path.suffix != ".prefab":
            raise ValueError("Inspect additional original NPC playable dependency: " + str(path))
        raw = path.read_text(encoding="utf8")
        for value in REFERENCE.findall(raw):
            if value not in index:
                raise ValueError("Unresolved original NPC playable GUID")
            queue.append(index[value])
        relative = path.relative_to(EXPORTED)
        replacements[guid(path)] = hashlib.md5(("zjcs-map14-native-npc-animator-playable:" + relative.as_posix()).encode()).hexdigest()
        copies.append((path, DESTINATION / relative, raw))
    outputs, templates = [], []
    for path, target, raw in copies:
        text = REFERENCE.sub(lambda m: "guid: " + replacements[m[1]], raw)
        write(target, text.encode())
        original_meta = Path(str(path) + ".meta").read_text(encoding="utf8")
        write(Path(str(target) + ".meta"), GUID.sub(lambda m: "guid: " + replacements[guid(path)], original_meta).encode())
        outputs.extend([target, Path(str(target) + ".meta")]);templates.extend([record(path), record(Path(str(path) + ".meta"))])
    for folder in [DESTINATION, *sorted(p for p in DESTINATION.rglob("*") if p.is_dir())]:
        meta(folder, folder=True);outputs.append(Path(str(folder) + ".meta"))
    editor = ROOT / "unity/Assets/Game/Editor/Map14/NpcAnimatorPlayables"
    if editor.is_dir():
        outputs.extend(p for p in editor.rglob("*") if p.is_file());outputs.append(Path(str(editor) + ".meta"))
    catalog = ROOT / "unity/Assets/Game/Resources/Map14/Native/native-npc-animator-playables-catalog.asset"
    if catalog.is_file():
        outputs.extend([catalog, Path(str(catalog) + ".meta")])
    write(RECEIPT, encode(dict(manifest="871_156598", mapClassId=14,
        dependencies=[record(PROOF), record(SOURCE)], templates=templates, scripts=scripts,
        tools=[record(Path(__file__))],
        prefabs=[dict(guid=p["guid"], officialPath=p["path"],
            nativePath=source_path(DESTINATION / p["path"].removeprefix("Assets/"))) for p in prefabs],
        counts=dict(prefabs=3, scriptBindings=len(scripts)), outputs=[record(p) for p in sorted(set(outputs))],
        operation="Restore the three source-verified original idle, run and float_02 NPC Animator playable prefabs. Preserve the original GameObjects, transforms, component order, sequence references, Animator parameters, duration, restoration flags and local IDs. Remap only script/prefab GUIDs to migrated original classes; bind original asset GUIDs explicitly.",
        limitations=["Original NPC skeleton/controller/animation appearance, formal story dispatch, map/save and Windows Player integration remain required. Native asset/parameter validation scope is recorded in map-14.md."])))
    print("Original NPC Animator playable prefabs restored:", len(copies), "prefabs;", len(scripts), "scripts", flush=True)


if __name__ == "__main__":
    build()
