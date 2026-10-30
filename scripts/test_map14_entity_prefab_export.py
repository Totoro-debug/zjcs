"""Verify that every selected Map14 prefab root has a byte-verified native export."""
import json
import hashlib
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "assets/reference/official-map/map14-native-entity-input-source-manifest.json"
EXPORT_RECEIPT = ROOT / "assets/reference/official-map/map14-native-entity-export-source-manifest.json"
EXPORT_ROOT = ROOT / ".tools/map14-entity-native-cache-871_156598/restored"
PACKAGED_ROOT = ROOT / "unity/Assets/Official/World/Map/14/NativeEntityPrefabs"


def main():
    source = json.loads(MANIFEST.read_bytes())
    receipt = json.loads(EXPORT_RECEIPT.read_bytes())
    assert source["manifest"] == receipt["manifest"] == "871_156598"
    prefabs = source["selection"]["prefabs"]
    assert len(prefabs) == 416
    exported = {
        Path(row["input"]).as_posix(): row
        for row in receipt["outputs"]
        if row["input"].endswith(".prefab")
    }
    assert len(exported) >= len(prefabs)
    for entry in prefabs:
        relative = Path(entry["path"].replace("Assets/Prefabs/", "", 1))
        export_input = Path(".tools/map14-entity-native-cache-871_156598/export/ExportedProject") / entry["path"]
        output = exported.get(export_input.as_posix())
        assert output is not None, entry["path"]
        restored = EXPORT_ROOT / "Prefabs" / relative
        packaged = PACKAGED_ROOT / "Prefabs" / relative
        assert restored.is_file() and packaged.is_file(), entry["path"]
        assert hashlib.sha256(restored.read_bytes()).hexdigest() == output["sha256"], entry["path"]
        assert hashlib.sha256(packaged.read_bytes()).hexdigest() == output["sha256"], entry["path"]
    print(f"Map14 native entity prefab export passed: {len(prefabs)} roots, {len(receipt['outputs'])} restored assets")


if __name__ == "__main__":
    main()
