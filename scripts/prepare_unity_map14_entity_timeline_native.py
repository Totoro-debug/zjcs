"""Bind supported original entity Timelines to migrated code and native Unity packages.

The fifth Timeline remains verified source until its skeleton track has an
independent native animation implementation; no exported runtime stub is used.
"""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from verify_unity_map14_entity_timeline_export import EXPORTED, PROOF, build as verify

DESTINATION = ROOT / "unity/Assets/Official/World/Map/14/NativeEntityTimelines"
RECEIPT = ROOT / "assets/reference/official-map/map14-native-entity-timelines-source-manifest.json"
SUPPORTED = ("GriffinShakeTimeline 4", "SuiMo_StoryShow02Timeline", "SuiMo_StoryShow05", "XianHaiYunZhou")
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")
REFERENCE = re.compile(r"guid: ([a-f0-9]{32})")


def record(path):
    return dict(path=source_path(path), sha256=sha(path.read_bytes()))


def guid(path):
    return GUID.search(Path(str(path) + ".meta").read_text(encoding="utf8"))[1]


def build():
    proof = verify()
    index = {}
    for path in EXPORTED.rglob("*.meta"):
        match = GUID.search(path.read_text(encoding="utf8"))
        if match:
            if match[1] in index:
                raise ValueError("Ambiguous original export GUID")
            index[match[1]] = path.with_suffix("")
    queue = [ROOT / r["path"] for r in proof["roots"] if r["name"] in SUPPORTED]
    if len(queue) != 4:
        raise ValueError("Supported original Timeline coverage differs")
    script_roots = [ROOT / "unity" / name for name in (
        "Assets/Game/Runtime/Official/StoryNodes", "Assets/Game/Runtime/Official/StoryAnimatorTimeline",
        "Assets/Game/Runtime/Official/StorySounds", "Assets/Game/Runtime/Official/StoryBubbleTimeline",
        "Assets/Game/Runtime/Official/ProfessionUI", "Packages/com.unity.timeline", "Packages/com.unity.cinemachine")]
    scripts, copies, replacements, seen = [], [], {}, set()
    while queue:
        path = queue.pop()
        if path in seen:
            continue
        seen.add(path)
        if path.suffix == ".cs":
            matches = [p for root in script_roots for p in root.rglob(path.name)]
            if len(matches) != 1:
                raise ValueError("Original Timeline script is not uniquely migrated: " + path.stem)
            target = matches[0]
            replacements[guid(path)] = guid(target)
            scripts.append(dict(original=record(path), target=record(target), meta=record(Path(str(target) + ".meta"))))
            continue
        if path.suffix not in (".asset", ".playable", ".anim"):
            raise ValueError("Inspect additional original Timeline dependency: " + str(path))
        raw = path.read_text(encoding="utf8")
        for value in REFERENCE.findall(raw):
            if value in index:
                queue.append(index[value])
            elif value not in ("0000000000000000e000000000000000", "0000000000000000f000000000000000"):
                raise ValueError("Unresolved original Timeline reference: " + value)
        relative = path.relative_to(EXPORTED)
        replacements[guid(path)] = hashlib.md5(("zjcs-map14-native-entity-timeline:" + relative.as_posix()).encode()).hexdigest()
        copies.append((path, DESTINATION / relative, raw))
    outputs, templates = [], []
    for path, target, raw in copies:
        text = REFERENCE.sub(lambda m: "guid: " + replacements[m[1]] if m[1] in replacements else m[0], raw)
        write(target, text.encode())
        original_meta = Path(str(path) + ".meta").read_text(encoding="utf8")
        write(Path(str(target) + ".meta"), GUID.sub(lambda m: "guid: " + replacements[guid(path)], original_meta).encode())
        outputs.extend([target, Path(str(target) + ".meta")])
        templates.extend([record(path), record(Path(str(path) + ".meta"))])
    for folder in [DESTINATION, *sorted(p for p in DESTINATION.rglob("*") if p.is_dir())]:
        meta(folder, folder=True)
        outputs.append(Path(str(folder) + ".meta"))
    editor = ROOT / "unity/Assets/Game/Editor/Map14/EntityTimelines"
    if editor.is_dir():
        outputs.extend(p for p in editor.rglob("*") if p.is_file())
        outputs.append(Path(str(editor) + ".meta"))
    catalog = ROOT / "unity/Assets/Game/Resources/Map14/Native/native-entity-timelines-catalog.asset"
    if catalog.is_file():
        outputs.extend([catalog, Path(str(catalog) + ".meta")])
    write(RECEIPT, encode(dict(manifest="871_156598", mapClassId=14,
        dependencies=[record(PROOF), record(ROOT / "assets/reference/official-map/map14-entity-timeline-bindings-source-manifest.json")], templates=templates, scripts=scripts,
        tools=[record(Path(__file__)), record(ROOT / "scripts/verify_unity_map14_entity_timeline_export.py")],
        roots=[dict(r, nativePath=source_path(DESTINATION / (ROOT / r["path"]).relative_to(EXPORTED)))
               for r in proof["roots"] if r["name"] in SUPPORTED],
        outputs=[record(p) for p in sorted(set(outputs))],
        counts=dict(timelines=4, nativeFiles=len(copies), scriptBindings=len(scripts)),
        operation="Copy the four verified original entity Timelines and their complete native dependency closures, preserving all YAML object IDs and fields. Remap GUIDs only to migrated original runtime classes and installed Timeline/Cinemachine package scripts; never import generated runtime stubs.",
        limitations=["SuiMo_StoryShow06 remains verified source pending independent native skeleton track/actor support; none of its tracks are deleted or replaced.",
                      "Exact graphical validation scope is recorded in map-14.md. Full actors, split sound playback, formal story/map/save and Windows Player remain pending."])))
    print("Original entity Timeline native bindings:", len(copies), "native files;", len(scripts), "scripts", flush=True)


if __name__ == "__main__":
    build()
