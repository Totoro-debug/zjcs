"""Bind each map's verified original interaction rows through the shared converter."""
import hashlib
import json
from pathlib import Path

from prepare_unity_map14_interactions import interaction_payload
from prepare_unity_map13_story_runtime import json_meta, write_json

ROOT = Path(__file__).resolve().parents[1]


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    proof = dict(format=1, operation="Reuse the original hero result-index converter with each map's own verified region, area and EC tables. Preserve ordered rewards, original costs and empty DropItems arrays; retain separate historical source receipts.", maps=[], outputs=[])
    for map_id, count in ((12, 99), (13, 106)):
        receipt_path = ROOT / f"assets/reference/official-map/map{map_id}-source-manifest.json"
        receipt = json.loads(receipt_path.read_bytes())
        if receipt["manifest"] != "871_156598" or receipt["mapClassId"] != map_id:
            raise ValueError("Original interaction source identity changed")
        expected = {row["path"]: row["sha256"] for row in receipt["outputs"]}
        source = ROOT / f"unity/Assets/Official/World/Map/{map_id}/Source"
        paths = [source / name for name in ("templates.json", "tables.json")]
        for path in paths:
            row = record(path)
            if expected[row["path"]] != row["sha256"]:
                raise ValueError("Original interaction source bytes changed: " + row["path"])
        templates, tables = (json.loads(path.read_bytes()) for path in paths)
        payload = interaction_payload(templates, tables, map_id, count, receipt["manifest"])
        target = ROOT / f"unity/Assets/Game/Resources/Map{map_id}/interactions.json"
        write_json(target, payload)
        json_meta(target)
        proof["maps"].append(dict(mapClassId=map_id, manifest=receipt["manifest"], source_receipt=record(receipt_path),
            inputs=[record(path) for path in paths], regions=len(payload["regions"]), areas=len(payload["areas"]),
            heroes=len(payload["heroes"]), parameters=len(payload["parameters"])))
        proof["outputs"] += [record(target), record(Path(str(target) + ".meta"))]
    proof["tools"] = [record(Path(__file__)), record(ROOT / "scripts/prepare_unity_map14_interactions.py")]
    proof["runtime_bindings"] = [dict(path=relative, hashLineEndings="LF",
        sha256=hashlib.sha256((ROOT / relative).read_bytes().replace(b"\r\n", b"\n")).hexdigest())
        for relative in ("unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineInteractionCatalog.cs",
            "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapProfile.cs")]
    write_json(ROOT / "assets/reference/official-map/mainline-interaction-maps-source-manifest.json", proof)
    print("Bound original scoped interaction maps:", [row["mapClassId"] for row in proof["maps"]])


if __name__ == "__main__":
    build()
