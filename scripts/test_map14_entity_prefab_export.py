"""Verify that every selected Map14 prefab root has a byte-verified native export."""
import argparse
import json
import hashlib
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "assets/reference/official-map/map14-native-entity-input-source-manifest.json"
EXPORT_RECEIPT = ROOT / "assets/reference/official-map/map14-native-entity-export-source-manifest.json"
EXPORT_ROOT = ROOT / ".tools/map14-entity-native-cache-871_156598/restored"
PACKAGED_ROOT = ROOT / "unity/Assets/Official/World/Map/14/NativeEntityPrefabs"


def main(require_cache=False):
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
        assert packaged.is_file(), entry["path"]
        # Git can check text assets out with CRLF. The recorded native YAML is
        # LF; compare that exact text without changing fields or expected hashes.
        assert hashlib.sha256(packaged.read_bytes().replace(b"\r\n", b"\n")).hexdigest() == output["sha256"], entry["path"]
        if require_cache or restored.exists():
            assert restored.is_file(), entry["path"]
            assert hashlib.sha256(restored.read_bytes().replace(b"\r\n", b"\n")).hexdigest() == output["sha256"], entry["path"]
    print(f"Map14 native entity prefab export passed: {len(prefabs)} packaged roots match recorded native YAML (LF); cache required={require_cache}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--require-cache", action="store_true")
    main(parser.parse_args().require_cache)
