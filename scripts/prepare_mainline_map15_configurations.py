"""Restore Map15's omitted official configuration records without inventing defaults."""
import hashlib
import json
from pathlib import Path
import subprocess

from official_apk import Apk, ROOT
from official_map_codec import ground
from official_tables import Constructors
from prepare_unity_map13_sources import encode, write

PREDECESSOR = "fa3fa56ebc05f52d698d5266f040caaa4a25b8f7"
DEFINITION = "unity/Assets/Official/World/Map/15/map15.json"
MANIFEST = "unity/Assets/Official/World/Map/15/source-manifest.json"


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def build():
    original = subprocess.check_output(["git", "show", PREDECESSOR + ":" + DEFINITION], cwd=ROOT)
    previous_manifest = subprocess.check_output(["git", "show", PREDECESSOR + ":" + MANIFEST], cwd=ROOT)
    old = json.loads(previous_manifest)
    if sha(original) != old["output"]["sha256"]:
        raise ValueError("Historical Map15 definition differs from its source receipt")
    definition = json.loads(original)
    apk, constructors = Apk.current(), Constructors()
    prepared = ROOT / ".tools/source-packages/prepared-code-source-manifest.json"
    expected_source = json.loads(prepared.read_bytes())
    if expected_source["manifest"] != apk.version:
        raise ValueError("Selected constructor source version differs")
    expected = {row["path"]: row["sha256"] for row in expected_source["outputs"]}
    inputs = []

    def payload(bundle, suffix):
        obj = apk.asset(bundle, suffix)
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        info = dict(apk.provenance(obj), payload_sha256=sha(raw), object_sha256=sha(obj.get_raw_data()))
        inputs.append(info)
        return obj, raw

    _, raw = payload("config_ec_maprootentitylite", "/15.g.bytes")
    current = ground(raw)
    nav = definition["Navigation"]
    if (current["id"] != 15 or current["size"] != [nav["Width"], nav["Height"]]
        or current["character"] != nav["CharacterId"] or current["member"] != nav["MemberId"]
        or current["origin"] != [nav["Origin"][k] for k in ("X", "Y", "Z")]
        or current["cells"] != [[cell["RawValues"] for cell in row["Cells"]] for row in nav["Rows"]]
        or current["areas"] != [[row["Cell"], row["Value"]] for row in nav["AreaMappings"]]
        or current["max_steps"] != nav["MaxSteps"]):
        raise ValueError("Historical Map15 navigation differs from the selected actual ground")
    configs = []
    for name, kind, identity in (("map", "TbMapInfo", 15), ("map_world", "TbMapWorldInfo", 15),
            ("map_region", "TbMapRegionInfo", 15), ("conditions", "TbConditionInfo", 115),
            ("entity_extend_parameter", "TbEntityExtendParameterInfo", None)):
        bundle = "config_luban" if name == "conditions" else "config_luban_map"
        obj, raw = payload(bundle, "/" + name + ".g.bytes")
        rows = [row for row in constructors.table(raw, kind) if identity is None or row["ClassId"] == identity]
        if not rows or (identity is not None and len(rows) != 1):
            raise ValueError("Original Map15 configuration identity/count changed: " + name)
        for row in rows:
            if kind == "TbMapInfo" and row["MiniMapPath"] != "":
                raise ValueError("Original Map15 minimap absence changed")
            text = json.dumps(row, ensure_ascii=False, separators=(",", ":"))
            configs.append(dict(TableName=kind, RecordId=str(row["EntityClassId"] if identity is None else identity),
                Json=text, Sha256=sha(text.encode()), SourceBundle=bundle, SourcePathId=str(obj.path_id)))
    source_paths = [ROOT / ".tools/source-packages/official-code/global-metadata.dat"]
    for name in constructors.sources:
        path = constructors.source_root / (name + ".cs")
        if not path.exists():
            path = constructors.source_root.parent / "Common.LubanGen" / (name + ".cs")
        if path.exists():
            source_paths.append(path)
    for path in source_paths:
        row = record(path)
        if expected.get(row["path"]) != row["sha256"]:
            raise ValueError("Consumed selected constructor source differs: " + row["path"])
    definition["Configs"] = configs
    proof = dict(format=1, manifest=apk.version, operation="Restore omitted original Map15 map/world, region, access-condition and complete entity-parameter rows. Preserve the original empty MiniMapPath. Keep the historical definition's other fields, with every navigation cell and navigation field explicitly compared against the selected actual ground; no whole-version equivalence is asserted.",
        predecessor=dict(commit=PREDECESSOR, definition=dict(path=DEFINITION, sha256=sha(original)),
            source_manifest=dict(path=MANIFEST, sha256=sha(previous_manifest))),
        inputs=inputs, prepared_source=record(prepared), constructors=[record(p) for p in source_paths],
        historical_inputs=old["inputs"], checked_navigation_cells=nav["Width"] * nav["Height"],
        tool=record(Path(__file__)))
    receipt_path = ROOT / "assets/reference/official-map/map15-configurations-source-manifest.json"
    write(receipt_path, encode(proof))
    new_inputs = old["inputs"] + [record(receipt_path)]
    text = json.dumps(new_inputs, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    definition["SourceManifestSha256"] = sha(text.encode())
    output = encode(definition)
    manifest = dict(format=1, mapClassId=15, inputs=new_inputs, sourceInputsJson=text,
        sourceInputsSha256=sha(text.encode()), output=dict(path="map15.json", size=len(output), sha256=sha(output)))
    write(ROOT / DEFINITION, output)
    write(ROOT / MANIFEST, encode(manifest))
    apk.archive.close()
    print("Restored original Map15 world/region/condition/entity-parameter records; verified every one of 750 navigation cells.")


if __name__ == "__main__":
    build()
