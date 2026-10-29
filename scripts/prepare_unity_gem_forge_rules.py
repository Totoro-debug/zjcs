"""Create a verified Unity resource containing the pinned gem merge tables."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT


MANIFEST = "854_156383"
SOURCE = ROOT / "assets/official/rules.json"
SOURCE_MANIFEST = ROOT / "assets/reference/official-city/source-manifest.json"
TARGET = ROOT / "unity/Assets/Game/Resources/Equipment/gem-forge-rules.json"
META = Path(str(TARGET) + ".meta")
GUID = "922f1de9056b44f896e2fd07c1ce3e91"


def digest(content):
    return hashlib.sha256(content).hexdigest()


def restore():
    manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    expected = manifest.get("data_sha256", {}).get("rules.json")
    source_bytes = SOURCE.read_bytes()
    if manifest.get("manifest") != MANIFEST or digest(source_bytes) != expected:
        raise ValueError("Gem forge source does not match the pinned official manifest.")
    source = json.loads(source_bytes)
    rows = source.get("gem_merge")
    gems = source.get("gem")
    stories = source.get("game_settings", {}).get("GemMergeStorySetting", {}).get("StoryList")
    if not rows or not gems or not stories or len(stories) != 4:
        raise ValueError("Pinned gem merge tables or story configuration are incomplete.")
    payload = {
        "manifest": MANIFEST,
        "gem_merge": rows,
        "gem_merge_stories": stories[:-1],
        "source_sha256": digest(source_bytes),
        "table_counts": {"gem": len(gems), "gem_merge": len(rows)},
    }
    output = (json.dumps(payload, ensure_ascii=False, sort_keys=True,
                         separators=(",", ":")) + "\n").encode("utf-8")
    output_hash = digest(output)
    target_manifest = {
        "manifest": MANIFEST,
        "source": "assets/official/rules.json",
        "source_manifest": "assets/reference/official-city/source-manifest.json",
        "source_sha256": digest(source_bytes),
        "output": {"path": "Assets/Game/Resources/Equipment/gem-forge-rules.json",
                   "bytes": len(output), "sha256": output_hash},
        "tables": payload["table_counts"],
    }
    manifest_output = (json.dumps(target_manifest, ensure_ascii=False, sort_keys=True,
                                  indent=2) + "\n").encode("utf-8")
    manifest_path = TARGET.with_name("gem-forge-source-manifest.json")
    manifest_meta = Path(str(manifest_path) + ".meta")
    writes = [(TARGET, output), (META, ("fileFormatVersion: 2\nguid: " + GUID
        + "\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode("utf-8")),
        (manifest_path, manifest_output), (manifest_meta, ("fileFormatVersion: 2\nguid: 922f1de9056b44f896e2fd07c1ce3e92\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode("utf-8"))]
    for path, content in writes:
        if path.exists() and path.read_bytes() != content:
            if path == TARGET:
                old = json.loads(path.read_text(encoding="utf-8"))
                new = json.loads(content.decode("utf-8"))
                old.pop("source_sha256", None)
                new.pop("source_sha256", None)
                if old == new:
                    continue
            if path == manifest_path:
                old = json.loads(path.read_text(encoding="utf-8"))
                if (old.get("manifest") == MANIFEST and old.get("output", {}).get("path")
                        == target_manifest["output"]["path"]
                        and old.get("output", {}).get("sha256")
                        == digest(TARGET.read_bytes())):
                    continue
            if path.suffix == ".meta" and ("guid: " + (GUID if path == META
                    else "922f1de9056b44f896e2fd07c1ce3e92")) in path.read_text(encoding="utf-8"):
                if path.name == "gem-forge-rules.json.meta" or path.name == "gem-forge-source-manifest.json.meta":
                    continue
            raise ValueError("Refusing to overwrite a changed gem forge payload: " + str(path))
    for path, content in writes:
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != content:
            path.write_bytes(content)
    print("Restored pinned gem forge rules:", output_hash, "rows", len(rows))


if __name__ == "__main__":
    restore()
