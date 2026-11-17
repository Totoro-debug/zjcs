"""Extract pinned AppearanceConfig randomization flags for offline character creation."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from official_apk import ROOT, Apk
from official_unity_schema import ScriptSchemas

BUNDLE = "defaultpackage_assets_scriptableconfig_modules_appearance.bundle"
PAYLOAD = ROOT / "unity/Assets/Official/CharacterCreation/appearance-config-rules.json"
SOURCE_MANIFEST = ROOT / "unity/Assets/Official/CharacterCreation/source-manifest.json"
REFERENCE = ROOT / "assets/reference/character-creation/appearance-config-rules-source-manifest.json"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_if_unchanged(path: Path, data: bytes) -> None:
    if path.exists() and path.read_bytes() != data:
        raise ValueError("Refusing to overwrite changed character creation data: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        path.write_bytes(data)


def build() -> dict:
    apk = Apk.current()
    matches = [obj for obj in apk.objects(BUNDLE)
               if obj.type.name == "MonoBehaviour"
               and obj.read(check_read=False).m_Script.read().m_ClassName == "AppearanceConfig"]
    if len(matches) != 1:
        raise ValueError("Expected one pinned AppearanceConfig, found " + str(len(matches)))

    obj = matches[0]
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    tree = ScriptSchemas(code_root=code).read(obj)
    rows = tree.get("mPlayerSkinTypes")
    if not isinstance(rows, list) or not rows:
        raise ValueError("Pinned AppearanceConfig has no player skin type rules")

    types = []
    seen = set()
    for row in rows:
        skin_type = int(row["mSkinType"])
        if skin_type < 0 or skin_type in seen:
            raise ValueError("Pinned AppearanceConfig contains an invalid duplicate skin type")
        seen.add(skin_type)
        types.append({"type": skin_type,
                      "base_skin": bool(row["mBaseSkin"]),
                      "can_normal_set": bool(row["mCanNormalSet"])})
    types.sort(key=lambda item: item["type"])

    from prepare_character_creation_opening import without_source_versions
    source = dict(without_source_versions(apk.provenance(obj)), **{
        "raw_sha256": sha256(obj.get_raw_data()),
        "asset_class": "AppearanceConfig",
        "path_id": str(obj.path_id),
        "code": code.relative_to(ROOT).as_posix() + "/common-source/AppearanceConfig.cs"
    })
    payload = {"skin_types": types}
    encoded = (json.dumps(payload, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    digest = sha256(encoded)
    write_if_unchanged(PAYLOAD, encoded)

    reference = {
        "manifest": apk.version,
        "source": source,
        "output": "unity/Assets/Official/CharacterCreation/appearance-config-rules.json",
        "output_sha256": digest
    }
    reference_bytes = (json.dumps(reference, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    write_if_unchanged(REFERENCE, reference_bytes)

    if not SOURCE_MANIFEST.is_file():
        raise FileNotFoundError("The character creation source manifest is missing")
    manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    outputs = manifest.get("outputs")
    sources = manifest.get("sources")
    if not isinstance(outputs, list) or not isinstance(sources, dict):
        raise ValueError("The character creation source manifest has an unsupported structure")
    output = {"name": PAYLOAD.name, "bytes": len(encoded), "sha256": digest}
    existing = next((index for index, item in enumerate(outputs)
                     if item.get("name") == PAYLOAD.name), None)
    if existing is None:
        outputs.append(output)
    else:
        outputs[existing] = output
    sources[PAYLOAD.name] = {
        "generator": "scripts/prepare_character_creation_appearance_rules.py",
        "input_manifest": "assets/reference/character-creation/appearance-config-rules-source-manifest.json",
        "input_manifest_sha256": sha256(reference_bytes),
        "output_sha256": digest
    }
    manifest_bytes = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    SOURCE_MANIFEST.write_bytes(manifest_bytes)
    return {"output": PAYLOAD.relative_to(ROOT).as_posix(), "sha256": digest,
            "skin_types": len(types), "manifest": REFERENCE.relative_to(ROOT).as_posix()}


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, sort_keys=True))
