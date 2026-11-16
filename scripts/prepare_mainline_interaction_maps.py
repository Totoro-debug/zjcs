"""Bind scoped map interactions from the active local official source."""
import hashlib
import json
from pathlib import Path

from prepare_unity_map14_interactions import interaction_payload
from prepare_unity_map13_story_runtime import json_meta, write_json
from prepare_mainline_interactions import load_current_interaction_inputs

ROOT = Path(__file__).resolve().parents[1]


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def update_map16_interaction_receipt(input_records, template_evidence):
    path = ROOT / "assets/reference/official-map/map16-interactions-source-manifest.json"
    receipt = json.loads(path.read_bytes())
    active = json.loads((ROOT / "official-source.json").read_bytes())["active"]
    if receipt.get("manifest") != active or receipt.get("mapClassId") != 16:
        raise ValueError("Map16 interaction receipt source identity changed")
    targets = {
        "unity/Assets/Game/Resources/Map16/interactions.json": record(
            ROOT / "unity/Assets/Game/Resources/Map16/interactions.json"),
        "unity/Assets/Game/Resources/Map16/interactions.json.meta": record(
            ROOT / "unity/Assets/Game/Resources/Map16/interactions.json.meta"),
    }
    found = set()
    for output in receipt["outputs"]:
        if output["path"] in targets:
            output["sha256"] = targets[output["path"]]["sha256"]
            found.add(output["path"])
    if found != set(targets):
        raise ValueError("Map16 interaction receipt does not bind its runtime payload")
    receipt["operation"] = (
        "Convert Map16 from the active local official source. Preserve hero result component/index identities, "
        "ordered weights, status IDs, costs, static FastForwardType templates, region thresholds and configured "
        "game fast types, empty DropItems and area refresh groups/intervals."
    )
    receipt["officialSource"] = record(ROOT / "official-source.json")
    receipt["inputs"] = dict(tables=input_records, templates=template_evidence)
    tools = {item["path"]: item for item in receipt.get("tools", [])}
    for tool in (Path(__file__), ROOT / "scripts/prepare_unity_map14_interactions.py"):
        tools[tool.relative_to(ROOT).as_posix()] = record(tool)
    receipt["tools"] = list(tools.values())
    runtime_bindings = {item["path"]: item for item in receipt.get("runtime_bindings", [])}
    for relative in (
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineInteractionCatalog.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapProfile.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Battle.cs",
    ):
        source = (ROOT / relative).read_bytes().replace(b"\r\n", b"\n")
        runtime_bindings[relative] = dict(path=relative, hashLineEndings="LF", sha256=hashlib.sha256(source).hexdigest())
    receipt["runtime_bindings"] = list(runtime_bindings.values())
    write_json(path, receipt)
    return path


def build():
    version, templates, tables, stage_fast_types, template_evidence, input_records = load_current_interaction_inputs()
    proof = dict(format=1,
        operation="Convert each public mainline interaction catalog from the active local official source, preserving original result indices, weights, costs, static FastForwardType templates, region thresholds and configured game fast types.",
        manifest=version, officialSource=record(ROOT / "official-source.json"), maps=[], outputs=[])
    for map_id, count in ((12, 99), (13, 106), (16, 43)):
        hero_ids = None
        if map_id == 16:
            hero_ids = [int(key) for key, components in templates.items()
                        if "StageResultPeriodicityRandomAwardComponent" in components]
        payload = interaction_payload(templates, tables, map_id, count, version, hero_ids,
                                      stage_fast_types)
        target = ROOT / f"unity/Assets/Game/Resources/Map{map_id}/interactions.json"
        write_json(target, payload)
        json_meta(target)
        interaction_receipt = update_map16_interaction_receipt(input_records, template_evidence) if map_id == 16 else None
        map_record = dict(mapClassId=map_id, manifest=version, inputs=input_records,
            templates=template_evidence, regions=len(payload["regions"]), areas=len(payload["areas"]),
            heroes=len(payload["heroes"]), parameters=len(payload["parameters"]),
            stageFastTemplates=len(stage_fast_types))
        if interaction_receipt is not None:
            map_record["interaction_receipt"] = record(interaction_receipt)
        proof["maps"].append(map_record)
        proof["outputs"] += [record(target), record(Path(str(target) + ".meta"))]
    map14_receipt = ROOT / "assets/reference/official-map/map14-interactions-source-manifest.json"
    proof["maps"].append(dict(mapClassId=14, manifest=version,
        interaction_receipt=record(map14_receipt),
        outputs=[record(ROOT / "unity/Assets/Game/Resources/Map14/interactions.json"),
                 record(ROOT / "unity/Assets/Game/Resources/Map14/interactions.json.meta")]))
    proof["outputs"] += [record(ROOT / "unity/Assets/Game/Resources/Map14/interactions.json"),
                         record(ROOT / "unity/Assets/Game/Resources/Map14/interactions.json.meta")]
    proof["tools"] = [record(Path(__file__)), record(ROOT / "scripts/prepare_unity_map14_interactions.py")]
    proof["runtime_bindings"] = [dict(path=relative, hashLineEndings="LF",
        sha256=hashlib.sha256((ROOT / relative).read_bytes().replace(b"\r\n", b"\n")).hexdigest())
        for relative in ("unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineInteractionCatalog.cs",
            "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapProfile.cs",
            "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Battle.cs")]
    write_json(ROOT / "assets/reference/official-map/mainline-interaction-maps-source-manifest.json", proof)
    print("Bound original scoped interaction maps:", [row["mapClassId"] for row in proof["maps"]])


if __name__ == "__main__":
    build()
