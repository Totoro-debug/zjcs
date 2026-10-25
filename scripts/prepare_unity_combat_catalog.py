"""Restore verified official combat JSON and source manifests into Unity's local payload."""
import argparse
import copy
import hashlib
import json
import os
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCES = (
    ("assets/official/skill-rules.json", "assets/reference/skill-rules/source-manifest.json",
     "Assets/Official/Combat/skill-rules.json", "Assets/Official/Combat/skill-rules-source-manifest.json",
     "output_sha256"),
    ("assets/official/battle-rules.json", "assets/reference/battle-rules/source-manifest.json",
     "Assets/Official/Combat/battle-rules.json", "Assets/Official/Combat/battle-rules-source-manifest.json",
     "output_sha256"),
    ("assets/official/rules.json", "assets/reference/official-city/source-manifest.json",
     "Assets/Official/Combat/rules.json", "Assets/Official/Combat/rules-source-manifest.json",
     "data_sha256.rules.json"),
)
EXPECTED_VERSION = "854_156383"
MODULE_MANIFEST = "Assets/Official/Combat/source-manifest.json"
BATTLE_RULES = "Assets/Official/Combat/battle-rules.json"
BATTLE_SIDECAR = "Assets/Official/Combat/battle-rules-source-manifest.json"
SKILL_RULES = "Assets/Official/Combat/skill-rules.json"
ACTION_SOURCE_CODE = (
    "ActionAgentComponentImplement.cs",
    "ActionBehaviour.cs",
    "ActionType.cs",
    "BattleAISetting.cs",
    "StateMutex.cs",
    "StateMutexParser.cs",
    "TbStateMutex.cs",
)


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while block := stream.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def target_path(unity_root, relative):
    root = unity_root.resolve()
    target = root.joinpath(*Path(relative).parts)
    if target.is_symlink() or not target.resolve().is_relative_to(root):
        raise ValueError("Combat payload path escapes Unity project: " + relative)
    return target


def copy_verified(source, target, expected_hash, replace_from_hash=None):
    if target.exists():
        current_hash = sha256(target)
        if current_hash == expected_hash:
            return False
        if current_hash != replace_from_hash:
            raise ValueError("Refusing to overwrite a changed Unity combat payload: " + str(target))

    target.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".combat-payload-", dir=target.parent)
    temporary = Path(temporary_name)
    try:
        with source.open("rb") as input_stream, os.fdopen(handle, "wb") as output_stream:
            while block := input_stream.read(1024 * 1024):
                output_stream.write(block)
        if sha256(temporary) != expected_hash:
            raise ValueError("Copied combat payload hash differs: " + str(source))
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)
    return True


def is_verified_pre_mutex_battle_pair(unity_root, source_root, module_entry, sidecar):
    source_manifest = json.loads((source_root / "assets/reference/battle-rules/source-manifest.json")
        .read_text(encoding="utf-8"))
    if "state_mutex" not in source_manifest.get("tables", {}):
        return False
    source_payload = json.loads((source_root / "assets/official/battle-rules.json").read_text(
        encoding="utf-8"))
    current_payload = json.loads(target_path(unity_root, BATTLE_RULES).read_text(encoding="utf-8"))
    baseline_sidecar = copy.deepcopy(source_manifest)
    baseline_sidecar.get("tables", {}).pop("state_mutex", None)
    for path in list(baseline_sidecar.get("original_code", {})):
        if any(path.endswith(name) for name in ACTION_SOURCE_CODE):
            baseline_sidecar["original_code"].pop(path, None)
    baseline_sidecar["output_sha256"] = sidecar.get("output_sha256")
    source_payload.get("tables", {}).pop("state_mutex", None)
    return (
        module_entry.get("output_sha256") == sidecar.get("output_sha256")
        and sidecar.get("manifest") == EXPECTED_VERSION
        and current_payload == source_payload
        and sidecar == baseline_sidecar
    )


def is_verified_current_combat_skill_pair(unity_root, source_root, module_entry, sidecar):
    payload = source_root / "assets/official/skill-rules.json"
    source_manifest_path = source_root / "assets/reference/skill-rules/source-manifest.json"
    source_manifest = json.loads(source_manifest_path.read_text(encoding="utf-8"))
    conversion = source_manifest.get("current_combat_payload_conversion", {})
    conversion_path = source_root / Path(conversion.get("source", ""))
    staged_path = source_root / Path(conversion.get("staged", ""))
    if not conversion_path.is_file() or not staged_path.is_file() or not payload.is_file():
        return False
    conversion_bytes = conversion_path.read_bytes()
    conversion_receipt = json.loads(conversion_bytes)
    output = next((item for item in conversion_receipt.get("outputs", [])
        if item.get("path") == "assets/official/skill-rules.json"), None)
    payload_hash = sha256(payload)
    baseline_sidecar = copy.deepcopy(source_manifest)
    baseline_sidecar.pop("current_combat_payload_conversion", None)
    baseline_sidecar["output_sha256"] = conversion.get("previous_output_sha256")
    return (
        conversion.get("manifest") == "871_156598"
        and conversion.get("source_manifest_sha256") == hashlib.sha256(conversion_bytes).hexdigest()
        and conversion.get("staged_sha256") == payload_hash
        and sha256(staged_path) == payload_hash
        and conversion_receipt.get("manifest") == "871_156598"
        and output is not None
        and output.get("sha256") == payload_hash
        and output.get("previous_sha256") == conversion.get("previous_output_sha256")
        and module_entry.get("output_sha256") == conversion.get("previous_output_sha256")
        and module_entry.get("source_manifest_sha256") == sha256(target_path(unity_root,
            "Assets/Official/Combat/skill-rules-source-manifest.json"))
        and sidecar == baseline_sidecar
        and sidecar.get("output_sha256") == conversion.get("previous_output_sha256")
        and payload_hash == sha256(target_path(unity_root, SKILL_RULES))
    )


def previous_verified_hashes(unity_root, source_root):
    module_target = target_path(unity_root, MODULE_MANIFEST)
    if not module_target.exists():
        raise ValueError("Cannot refresh combat payloads without their module manifest")
    module_manifest = json.loads(module_target.read_text(encoding="utf-8"))
    if module_manifest.get("manifest") != EXPECTED_VERSION:
        raise ValueError("Existing Unity combat module manifest has an unexpected version")
    entries = {entry.get("path"): entry for entry in module_manifest.get("assets", [])}
    verified = {MODULE_MANIFEST: sha256(module_target)}
    battle_entry = entries.get(BATTLE_RULES)
    battle_sidecar_target = target_path(unity_root, BATTLE_SIDECAR)
    battle_sidecar = (json.loads(battle_sidecar_target.read_text(encoding="utf-8"))
        if battle_sidecar_target.exists() else {})
    legacy_battle_pair = battle_entry is not None and is_verified_pre_mutex_battle_pair(
        unity_root, source_root, battle_entry, battle_sidecar)
    skill_entry = entries.get(SKILL_RULES)
    skill_sidecar_target = target_path(unity_root,
        "Assets/Official/Combat/skill-rules-source-manifest.json")
    skill_sidecar = (json.loads(skill_sidecar_target.read_text(encoding="utf-8"))
        if skill_sidecar_target.exists() else {})
    converted_skill_pair = skill_entry is not None and is_verified_current_combat_skill_pair(
        unity_root, source_root, skill_entry, skill_sidecar)
    for _, source_manifest_name, target_name, manifest_target_name, manifest_field in SOURCES:
        entry = entries.get(target_name)
        if entry is None or entry.get("source_manifest") != source_manifest_name:
            raise ValueError("Existing Unity combat module manifest is incomplete: " + target_name)
        payload_target = target_path(unity_root, target_name)
        manifest_target = target_path(unity_root, manifest_target_name)
        if not payload_target.exists() or not manifest_target.exists():
            raise ValueError("Existing Unity combat payload pair is incomplete: " + target_name)
        payload_hash = sha256(payload_target)
        is_legacy_battle = target_name == BATTLE_RULES and legacy_battle_pair
        is_converted_skill = target_name == SKILL_RULES and converted_skill_pair
        if (payload_hash != entry.get("output_sha256") and not is_legacy_battle
                and not is_converted_skill):
            raise ValueError("Existing Unity combat payload differs from its module manifest: " + target_name)
        sidecar = json.loads(manifest_target.read_text(encoding="utf-8"))
        sidecar_matches_legacy = (is_legacy_battle and sidecar == battle_sidecar) or is_converted_skill
        if (sidecar.get("manifest") != EXPECTED_VERSION
                or sidecar.get("output_sha256") != payload_hash and not sidecar_matches_legacy):
            raise ValueError("Existing Unity combat source manifest does not verify: " + manifest_target_name)
        if manifest_field == "output_sha256":
            if (sha256(manifest_target) != entry.get("source_manifest_sha256")
                    and not sidecar_matches_legacy):
                raise ValueError("Existing Unity combat source manifest hash differs: " + manifest_target_name)
        elif (sidecar.get("source_manifest") != source_manifest_name
              or sidecar.get("source_manifest_sha256") != entry.get("source_manifest_sha256")):
            raise ValueError("Existing Unity combat source manifest provenance differs: " + manifest_target_name)
        verified[target_name] = payload_hash
        verified[manifest_target_name] = sha256(manifest_target)
    return verified


def allowed_replacement_hash(target_name, target, expected_hash, previous_hashes):
    if not target.exists():
        return None
    current_hash = sha256(target)
    if current_hash == expected_hash:
        return None
    if previous_hashes.get(target_name) != current_hash:
        raise ValueError("Refusing to overwrite a changed Unity combat payload: " + str(target))
    return current_hash


def restore(source_root=ROOT, unity_root=ROOT / "unity", replace_verified_stale=False):
    source_root = Path(source_root).resolve()
    unity_root = Path(unity_root).resolve()
    restored = []
    verified_sources = []
    for payload_name, manifest_name, target_name, manifest_target_name, manifest_field in SOURCES:
        payload = source_root / Path(payload_name)
        manifest_path = source_root / Path(manifest_name)
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        if manifest.get("manifest") != EXPECTED_VERSION:
            raise ValueError("Combat source manifest is not pinned to " + EXPECTED_VERSION + ": " + manifest_name)
        if manifest_field == "output_sha256":
            expected_hash = manifest.get("output_sha256")
            manifest_bytes = manifest_path.read_bytes()
        else:
            section, key = manifest_field.split(".", 1)
            expected_hash = manifest.get(section, {}).get(key)
            manifest_bytes = (json.dumps({
                "manifest": EXPECTED_VERSION,
                "output_sha256": expected_hash,
                "source_manifest": manifest_name,
                "source_manifest_sha256": sha256(manifest_path),
                "source_field": manifest_field,
            }, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
        actual_hash = sha256(payload)
        if not expected_hash or actual_hash != expected_hash:
            raise ValueError("Combat JSON output hash differs from source manifest: " + payload_name)
        payload_target = target_path(unity_root, target_name)
        manifest_target = target_path(unity_root, manifest_target_name)
        manifest_hash = hashlib.sha256(manifest_bytes).hexdigest()
        verified_sources.append((payload, payload_target, actual_hash, manifest_bytes,
                                 manifest_target, manifest_hash, target_name, manifest_target_name))

    module_entries = []
    for (payload, payload_target, actual_hash, manifest_bytes, manifest_target, manifest_hash,
         target_name, manifest_target_name) in verified_sources:
        source_manifest = next(item[1] for item in SOURCES if item[2] == target_name)
        module_entries.append({
            "path": target_name,
            "output_sha256": actual_hash,
            "source_manifest": source_manifest,
            "source_manifest_sha256": sha256(source_root / Path(source_manifest)),
        })
    module_manifest = (json.dumps({
        "manifest": EXPECTED_VERSION,
        "assets": module_entries,
    }, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    module_target = target_path(unity_root, MODULE_MANIFEST)
    module_hash = hashlib.sha256(module_manifest).hexdigest()
    previous_hashes = previous_verified_hashes(unity_root, source_root) if replace_verified_stale else {}
    replacements = {}
    for (_, payload_target, actual_hash, _, manifest_target, manifest_hash,
         target_name, manifest_target_name) in verified_sources:
        payload_previous = allowed_replacement_hash(target_name, payload_target, actual_hash,
            previous_hashes)
        manifest_previous = allowed_replacement_hash(manifest_target_name, manifest_target,
            manifest_hash, previous_hashes)
        if payload_previous is not None:
            replacements[target_name] = payload_previous
        if manifest_previous is not None:
            replacements[manifest_target_name] = manifest_previous
    module_previous = allowed_replacement_hash(MODULE_MANIFEST, module_target, module_hash,
        previous_hashes)
    if module_previous is not None:
        replacements[MODULE_MANIFEST] = module_previous

    for (payload, payload_target, actual_hash, manifest_bytes, manifest_target, manifest_hash,
         target_name, manifest_target_name) in verified_sources:
        restored_payload = copy_verified(payload, payload_target, actual_hash,
            replacements.get(target_name))
        restored_manifest = copy_verified_bytes(manifest_bytes, manifest_target, manifest_hash,
            replacements.get(manifest_target_name))
        restored.extend([
            {"path": target_name, "bytes": payload_target.stat().st_size, "sha256": actual_hash,
             "restored": restored_payload},
            {"path": manifest_target_name, "bytes": manifest_target.stat().st_size,
             "sha256": manifest_hash, "restored": restored_manifest},
        ])
    restored_module_manifest = copy_verified_bytes(module_manifest, module_target, module_hash,
        replacements.get(MODULE_MANIFEST))
    restored.append({
        "path": MODULE_MANIFEST,
        "bytes": module_target.stat().st_size,
        "sha256": module_hash,
        "restored": restored_module_manifest,
    })
    return restored


def copy_verified_bytes(content, target, expected_hash, replace_from_hash=None):
    if target.exists():
        current_hash = sha256(target)
        if current_hash == expected_hash:
            return False
        if current_hash != replace_from_hash:
            raise ValueError("Refusing to overwrite a changed Unity combat payload: " + str(target))

    target.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".combat-payload-", dir=target.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as output_stream:
            output_stream.write(content)
        if sha256(temporary) != expected_hash:
            raise ValueError("Generated combat source manifest hash differs: " + str(target))
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)
    return True


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, default=ROOT,
                        help="Workspace holding assets/official and assets/reference")
    parser.add_argument("--unity-root", type=Path, default=ROOT / "unity",
                        help="Unity project receiving Assets/Official/Combat payloads")
    parser.add_argument("--replace-verified-stale", action="store_true",
                        help="Replace payloads only when existing bytes match their generated module manifest")
    args = parser.parse_args()
    print(json.dumps(restore(args.source_root, args.unity_root, args.replace_verified_stale),
        ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
