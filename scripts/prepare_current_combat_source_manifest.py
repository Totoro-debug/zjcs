"""Verify a staged current combat conversion and bind it to its fixed-version skill manifest."""
import argparse
import hashlib
import json
import os
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
CURRENT_VERSION = "871_156598"
PAYLOAD = Path("assets/official/skill-rules.json")
SOURCE_MANIFEST = Path("assets/reference/skill-rules/source-manifest.json")
CONVERSION_MANIFEST = Path("assets/reference/official-map/current-combat-source-manifest.json")


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def build():
    payload_path = ROOT / PAYLOAD
    source_path = ROOT / SOURCE_MANIFEST
    conversion_path = ROOT / CONVERSION_MANIFEST
    conversion_bytes = conversion_path.read_bytes()
    conversion = json.loads(conversion_bytes)
    source = json.loads(source_path.read_text(encoding="utf-8"))
    payload_bytes = payload_path.read_bytes()
    payload_hash = sha256(payload_bytes)
    if source.get("manifest") != VERSION or conversion.get("manifest") != CURRENT_VERSION:
        raise ValueError("Combat payload conversion versions do not match the pinned manifests")
    record = next((item for item in conversion.get("outputs", [])
        if item.get("path") == PAYLOAD.as_posix()), None)
    if record is None or record.get("sha256") != payload_hash:
        raise ValueError("Current combat receipt does not identify the active skill-rules payload")
    staged_path = ROOT / record.get("staged", "")
    if not staged_path.is_file() or sha256(staged_path.read_bytes()) != payload_hash:
        raise ValueError("Staged current combat payload differs from the active skill-rules payload")
    previous_hash = record.get("previous_sha256")
    conversion_entry = {
        "manifest": CURRENT_VERSION,
        "source": CONVERSION_MANIFEST.as_posix(),
        "source_manifest_sha256": sha256(conversion_bytes),
        "previous_output_sha256": previous_hash,
        "staged_sha256": payload_hash,
        "staged": record["staged"],
    }
    existing = source.get("current_combat_payload_conversion")
    if source.get("output_sha256") not in (previous_hash, payload_hash):
        if not (isinstance(existing, dict)
                and existing.get("previous_output_sha256") == previous_hash
                and existing.get("staged_sha256") == payload_hash):
            raise ValueError("Skill source manifest output is unrelated to this verified conversion")
    source["output_sha256"] = payload_hash
    source["current_combat_payload_conversion"] = conversion_entry
    manifest_bytes = (json.dumps(source, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    return manifest_bytes, {
        "payload_sha256": payload_hash,
        "previous_output_sha256": previous_hash,
        "current_manifest_sha256": conversion_entry["source_manifest_sha256"],
    }


def write_atomic(path, data):
    handle, temporary_name = tempfile.mkstemp(prefix=".combat-source-manifest-", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(data)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true",
        help="verify the conversion and compare without writing")
    args = parser.parse_args()
    manifest, record = build()
    target = ROOT / SOURCE_MANIFEST
    if args.verify:
        if target.read_bytes() != manifest:
            raise SystemExit("skill-rules source manifest differs from its verified current conversion")
    else:
        write_atomic(target, manifest)
    print(json.dumps(record, ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
