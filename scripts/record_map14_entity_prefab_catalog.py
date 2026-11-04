"""Verify and record the installed original Map14 entity prefab bindings."""
import hashlib
import json
from pathlib import Path
import re

import yaml

from test_map14_entity_prefab_export import main as verify_prefabs

ROOT = Path(__file__).resolve().parents[1]
CATALOG = "unity/Assets/Game/Resources/Map14/Native/entity-prefab-catalog.asset"
SOURCE = "assets/reference/official-map/map14-native-entity-input-source-manifest.json"


def record(relative):
    raw = (ROOT / relative).read_bytes().replace(b"\r\n", b"\n")
    return dict(path=relative, sha256=hashlib.sha256(raw).hexdigest())


def guid(relative):
    return re.search(r"(?m)^guid: (\w+)", (ROOT / (relative + ".meta")).read_text(encoding="utf-8"))[1]


def main():
    # Unity writes blank scalar fields with trailing spaces. Canonicalize only
    # text whitespace in this newly generated catalog and its own metadata.
    for relative in (CATALOG, CATALOG + ".meta"):
        path = ROOT / relative
        raw = path.read_bytes().replace(b"\r\n", b"\n")
        path.write_bytes(b"\n".join(line.rstrip() for line in raw.split(b"\n")))
    verify_prefabs()
    source = json.loads((ROOT / SOURCE).read_bytes())
    # Strip Unity's document tag, retaining its entire serialized object body.
    catalog = yaml.load("\n".join((ROOT / CATALOG).read_text(encoding="utf-8").splitlines()[3:]), Loader=yaml.BaseLoader)["MonoBehaviour"]
    script = "unity/Assets/Game/Runtime/Offline/World/NativeMap14EntityAssets/NativeMap14EntityPrefabCatalog.cs"
    if catalog["m_Script"]["guid"] != guid(script) or catalog["Manifest"] != source["manifest"]:
        raise ValueError("Installed entity catalog script/source identity differs")
    original = source["selection"]["prefabs"]
    if len(catalog["Entries"]) != len(original):
        raise ValueError("Installed entity catalog omits original prefab roots")
    bindings = []
    for entry, expected in zip(catalog["Entries"], original):
        path = expected["path"]
        installed = "unity/Assets/Official/World/Map/14/NativeEntityPrefabs/Prefabs/" + path.removeprefix("Assets/Prefabs/")
        class_use = next((use for use in expected.get("uses", []) if use.get("kind") == "ec_entity"), None)
        identity = class_use["class_id"] if class_use else 0
        if entry["OriginalPath"] != path or int(entry["ClassId"]) != identity or entry["Prefab"]["guid"] != guid(installed):
            raise ValueError("Installed entity catalog changes the original binding: " + path)
        file_id = entry["Prefab"]["fileID"]
        if not re.search(r"(?m)^--- !u!1 &" + str(file_id) + r"\s*$", (ROOT / installed).read_text(encoding="utf-8")):
            raise ValueError("Installed entity catalog does not reference a GameObject: " + path)
        bindings.append(dict(originalPath=path, classId=identity, prefab=record(installed),
            meta=record(installed + ".meta"), fileID=file_id))
    result = dict(format=1, manifest=source["manifest"], mapClassId=14,
        operation="Bind every original selected prefab path and EC class to its installed native GameObject. Verify native YAML against the prior export hashes; preserve the original input/export receipts and version.",
        hashing="UTF-8 text bytes with CRLF normalized to LF; generated catalog trailing whitespace is trimmed; no expected native export hash is changed.",
        inputs=[record(SOURCE), record("assets/reference/official-map/map14-native-entity-export-source-manifest.json")],
        bindings=bindings, outputs=[record(CATALOG), record(CATALOG + ".meta")],
        tools=[record(Path(__file__).relative_to(ROOT).as_posix()), record("scripts/test_map14_entity_prefab_export.py"),
            record("unity/Assets/Game/Editor/Map14/EntityAssets/OfficialMap14EntityPrefabCatalogBuilder.cs"), record(script), record(script + ".meta")])
    target = ROOT / "assets/reference/official-map/map14-entity-prefab-catalog-binding-source-manifest.json"
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Map14 installed entity catalog bindings verified:", len(bindings))


if __name__ == "__main__":
    main()
