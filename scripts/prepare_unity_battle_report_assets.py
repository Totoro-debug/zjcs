"""Install the fixed-version official battle report prefab dependency closure."""
from __future__ import annotations

import hashlib
import json
import re
import uuid
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TEMP = ROOT / ".tools/assetripper-1.3.14/temp"
INPUTS = TEMP / "battle-report-ui-inputs/source-manifest.json"
EXPORT = TEMP / "battle-report-ui-ripper/ExportedProject/Assets"
OFFICIAL = ROOT / "unity/Assets/Official/Combat/BattleReport"
SCRIPT_OUTPUT = ROOT / "unity/Assets/Game/Runtime/Official/Combat/BattleReport/AssetRipper"
RESOURCES = ROOT / "unity/Assets/Game/Resources/Combat/BattleReport"
RECEIPT = ROOT / "assets/reference/battle-hud/report-panels-source-manifest.json"
MANIFEST = "854_156383"
GUID_PATTERN = re.compile(r"guid: ([0-9a-f]{32})")
META_GUID_PATTERN = re.compile(r"^guid: ([0-9a-f]{32})$", re.MULTILINE)
BUILTIN_GUIDS = {
    "0000000000000000e000000000000000",
    "0000000000000000f000000000000000",
    "00000000000000000000000000000000",
}
TEXT_EXTENSIONS = {
    ".anim", ".asmdef", ".asset", ".controller", ".mat", ".overridecontroller",
    ".prefab", ".rendertexture", ".shader", ".spriteatlas", ".txt",
}
BUNDLES = (
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendallplayerreportpanel.bundle",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportpanel.bundle",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportplayertabview.bundle",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportplayertabviewitem.bundle",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportskilltabview.bundle",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportskilltabviewitem.bundle",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportsplit.bundle",
)
EXPECTED_BUNDLE_SHA256 = {
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendallplayerreportpanel.bundle": "cff2583e5e8fd2dc5b920e0175202589759052d747bceee7a33f82d6340f5ec5",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportpanel.bundle": "893c404eda7a038aa3b2f9036cd4128c6c7a0e5aeb75825e7ef0b9e7d928c791",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportplayertabview.bundle": "7c8c8934fb82f2f0dc2152306bcd03c316fbdd0f28190ef545f80ee2705930c0",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportplayertabviewitem.bundle": "7c79c754d8e9739d76a25729403b6fb8798ab1fdca17362bff4f39f8d7ea85a5",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportskilltabview.bundle": "0657a6c7b571626d851707d25bd6ea49555a13678474b9392db1d7002bd2eac7",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportskilltabviewitem.bundle": "62ef00250427e8348fcb2271e39ac473a86de18ca67b733c6e2e5fa884c6e967",
    "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportsplit.bundle": "1cec0059a027d63d90965c4f199017c52d5bd4f1e4f0499456ed64a6bcbbce5d",
}
PREFABS = (
    "Prefabs/UI/Fight/FightEndReportPanel/FightEndAllPlayerReportPanel.prefab",
    "Prefabs/UI/Fight/FightEndReportPanel/FightEndReportPanel.prefab",
    "Prefabs/UI/Fight/FightEndReportPanel/FightEndReportPlayerTabView.prefab",
    "Prefabs/UI/Fight/FightEndReportPanel/FightEndReportPlayerTabViewItem.prefab",
    "Prefabs/UI/Fight/FightEndReportPanel/FightEndReportSkillTabView.prefab",
    "Prefabs/UI/Fight/FightEndReportPanel/FightEndReportSkillTabViewItem.prefab",
    "Prefabs/UI/Fight/FightEndReportPanel/FightEndReportSplit.prefab",
)
SCRIPT_GUID_MAPPINGS = {
    "18d0a90695249463551c00f45766e642": "4e29b1a8efbd4b44bb3f3716e73f07ff",
    "16c0ec1e295995dd9a2d2df2d7b6cc7e": "31a19414c41e5ae4aae2af33fee712f6",
    "19fbb4a32b59286cd89b624f52ff7943": "30649d3a9faa99c48a7b1166b86bf2a0",
    "21c7954052da7655d96bff866c2b7662": "3245ec927659c4140ac4f8d17403cc18",
    "4293fd42559bc8bb1c81715179adf536": "306cc8c2b49d7114eaa3623786fc2126",
    "f18f2cc2fe71a3a6d76a570588d5047c": "59f8146938fff824cb5fd77236b75775",
    "3cf5a44414476512e00c3e7a2569a919": "fe87c0e1cc204ed48ad3b37840f39efc",
    "3f96b1d166d19b209697e35b35d65c76": "f4688fdb7df04437aeb418b961361dc5",
    "86fe8f3fc59dc06ea6b45a1bbee64682": "dc42784cf147c0c48a680349fa168899",
    "69dacd7a039c12e90cde90bbae65247a": "3312d7739989d2b4e91e6319e9a96d76",
    "76ccfb4bfe5eebba5766d6f50f76ae37": "cfabb0440166ab443bba8876756fdfa9",
    "c0467acf10a7d1a8e81912a9a7988750": "1aa08ab6e0800fa44ae55d278d1423e3",
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_guid(meta: Path) -> str | None:
    match = META_GUID_PATTERN.search(meta.read_text(encoding="utf-8", errors="replace"))
    return match.group(1) if match else None


def folder_meta(path: Path) -> None:
    meta = path.with_name(path.name + ".meta")
    if meta.exists():
        if read_guid(meta) is None:
            raise ValueError("Existing battle report folder metadata is invalid: " + str(meta))
        return
    relative = path.relative_to(ROOT / "unity/Assets").as_posix()
    asset_guid = hashlib.sha256(("zjcs-battle-report-folder:" + relative).encode()).hexdigest()[:32]
    meta.write_text("fileFormatVersion: 2\n"
                    f"guid: {asset_guid}\n"
                    "folderAsset: yes\nDefaultImporter:\n"
                    "  externalObjects: {}\n  userData: \n"
                    "  assetBundleName: \n  assetBundleVariant: \n", encoding="ascii")


def write_verified(source: Path, target: Path) -> bool:
    expected = sha256(source)
    if target.exists():
        if sha256(target) != expected:
            raise ValueError("Refusing to overwrite changed official battle report asset: " + str(target))
        return False
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(source.read_bytes())
    return True


def write_meta_verified(source: Path, target: Path) -> bool:
    expected = sha256(source)
    if target.exists():
        if sha256(target) != expected:
            raise ValueError("Refusing to overwrite changed official battle report metadata: " + str(target))
        return False
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(source.read_bytes())
    return True


def write_runtime_meta(path: Path, asset_guid: str) -> None:
    meta = path.with_name(path.name + ".meta")
    value = ("fileFormatVersion: 2\n"
             f"guid: {asset_guid}\n"
             "PrefabImporter:\n  externalObjects: {}\n  userData: \n"
             "  assetBundleName: \n  assetBundleVariant: \n").encode("ascii")
    if meta.exists():
        if meta.read_bytes() != value:
            raise ValueError("Refusing to overwrite changed battle report runtime metadata: " + str(meta))
        return
    meta.parent.mkdir(parents=True, exist_ok=True)
    meta.write_bytes(value)


def convert_runtime_prefab(source: Path) -> bytes:
    value = source.read_text(encoding="utf-8")
    for source_guid, runtime_guid in SCRIPT_GUID_MAPPINGS.items():
        value = value.replace("guid: " + source_guid + ", type: 3",
                              "guid: " + runtime_guid + ", type: 3")
    return value.encode("utf-8")


def build() -> dict:
    input_manifest = json.loads(INPUTS.read_text(encoding="utf-8"))
    if input_manifest.get("manifest") != MANIFEST:
        raise ValueError("Battle report input bundles are not pinned to " + MANIFEST)
    if sorted(input_manifest.get("requested_bundles", [])) != sorted(BUNDLES):
        raise ValueError("Battle report input bundle set differs from the pinned source list")
    input_by_name = {record.get("name"): record for record in input_manifest.get("inputs", [])}
    for name, expected_sha256 in EXPECTED_BUNDLE_SHA256.items():
        record = input_by_name.get(name)
        if record is None or record.get("decoded_sha256") != expected_sha256:
            raise ValueError("Pinned battle report bundle hash differs: " + name)
    if not EXPORT.is_dir():
        raise FileNotFoundError("AssetRipper battle report export is missing: " + str(EXPORT))

    for record in input_manifest.get("inputs", []):
        output = record.get("output")
        if not output:
            raise ValueError("Battle report staged input has no output file record")
        source = INPUTS.parent / output
        if not source.is_file() or sha256(source) != record.get("decoded_sha256"):
            raise ValueError("Battle report staged source hash differs: " + str(source))

    exported: dict[str, Path] = {}
    for meta in EXPORT.rglob("*.meta"):
        asset = meta.with_suffix("")
        asset_guid = read_guid(meta)
        if not asset_guid or not asset.is_file():
            continue
        if asset_guid in exported:
            raise ValueError("Duplicate AssetRipper GUID in battle report export: " + asset_guid)
        exported[asset_guid] = asset

    roots = [EXPORT / relative for relative in PREFABS]
    if any(not path.is_file() for path in roots):
        missing = [path.relative_to(EXPORT).as_posix() for path in roots if not path.is_file()]
        raise FileNotFoundError("Official battle report prefab is missing: " + ", ".join(missing))

    external: dict[str, Path] = {}
    for meta in (ROOT / "unity/Assets").rglob("*.meta"):
        asset = meta.with_suffix("")
        asset_guid = read_guid(meta)
        resolved_asset = asset.resolve()
        if any(resolved_asset.is_relative_to(path.resolve())
               for path in (OFFICIAL, SCRIPT_OUTPUT, RESOURCES)):
            continue
        if asset_guid and asset.is_file() and asset_guid not in external:
            external[asset_guid] = asset

    selected = {path.resolve() for path in roots}
    pending = list(selected)
    unresolved: dict[str, Path] = {}
    while pending:
        asset = pending.pop()
        if asset.suffix.lower() not in TEXT_EXTENSIONS:
            continue
        try:
            text = asset.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        for reference in GUID_PATTERN.findall(text):
            if reference in BUILTIN_GUIDS or reference in external:
                continue
            dependency = exported.get(reference)
            if dependency is None:
                unresolved.setdefault(reference, asset)
            elif dependency.resolve() not in selected:
                selected.add(dependency.resolve())
                pending.append(dependency.resolve())
    if unresolved:
        reference, owner = next(iter(unresolved.items()))
        raise ValueError("Official report UI dependency is unresolved: " + reference + " in "
                         + owner.relative_to(EXPORT).as_posix())

    for path in (OFFICIAL, SCRIPT_OUTPUT.parent, SCRIPT_OUTPUT, RESOURCES):
        path.mkdir(parents=True, exist_ok=True)
        folder_meta(path)

    outputs = []
    shared = []
    for source in sorted(selected):
        relative = source.relative_to(EXPORT)
        meta = source.with_name(source.name + ".meta")
        asset_guid = read_guid(meta)
        if not asset_guid:
            raise ValueError("AssetRipper report dependency has no GUID: " + str(source))
        existing = external.get(asset_guid)
        if existing:
            if sha256(existing) != sha256(source):
                raise ValueError("Existing Unity asset conflicts with report dependency GUID " + asset_guid)
            shared.append({"source": relative.as_posix(), "guid": asset_guid,
                           "shared_asset": existing.relative_to(ROOT).as_posix(),
                           "sha256": sha256(existing)})
            continue

        is_script = source.suffix.lower() in {".cs", ".asmdef"}
        destination_root = SCRIPT_OUTPUT if is_script else OFFICIAL
        target = destination_root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        for parent in [target.parent, *target.parent.parents]:
            if parent == destination_root.parent:
                break
            if parent.is_relative_to(ROOT / "unity/Assets"):
                folder_meta(parent)
        write_verified(source, target)
        write_meta_verified(meta, target.with_name(target.name + ".meta"))
        outputs.append({"path": target.relative_to(ROOT).as_posix(),
                        "source": relative.as_posix(), "guid": asset_guid,
                        "bytes": source.stat().st_size, "sha256": sha256(source),
                        "meta_sha256": sha256(meta), "kind": "script" if is_script else "asset"})
        external[asset_guid] = target

    runtime_prefabs = []
    runtime_script_guids = set(SCRIPT_GUID_MAPPINGS.values())
    for source in roots:
        target = RESOURCES / source.name
        converted = convert_runtime_prefab(source)
        unresolved_runtime_guids = sorted({guid for guid in GUID_PATTERN.findall(converted.decode("utf-8"))
                                           if guid not in BUILTIN_GUIDS
                                           and guid not in runtime_script_guids
                                           and guid not in external})
        if unresolved_runtime_guids:
            raise ValueError("Runtime report prefab has unresolved Unity GUIDs: "
                             + ", ".join(unresolved_runtime_guids[:8]))
        converted_hash = hashlib.sha256(converted).hexdigest()
        if target.exists() and hashlib.sha256(target.read_bytes()).hexdigest() != converted_hash:
            raise ValueError("Refusing to overwrite changed battle report runtime prefab: " + str(target))
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            target.write_bytes(converted)
        runtime_guid = uuid.uuid5(uuid.NAMESPACE_URL,
                                  "zjcs/battle-report/" + MANIFEST + "/" + source.name).hex
        write_runtime_meta(target, runtime_guid)
        runtime_prefabs.append({"path": target.relative_to(ROOT).as_posix(),
                                "source": source.relative_to(EXPORT).as_posix(),
                                "source_sha256": sha256(source),
                                "sha256": hashlib.sha256(converted).hexdigest(),
                                "guid": runtime_guid,
                                "official_guid": read_guid(source.with_name(source.name + ".meta")),
                                "conversion": "official-source-guid-preserved; Unity UGUI/TMP script GUID adaptation"})

    receipt = {
        "format": 1,
        "manifest": MANIFEST,
        "operation": "Convert fixed official battle report prefab bundles to Unity assets; preserve prefab dependency GUIDs and map Android-exported UGUI/TMP scripts to the installed Unity packages.",
        "requested_bundles": list(BUNDLES),
        "bundle_inputs": input_manifest["inputs"],
        "ripper_export_root": EXPORT.relative_to(ROOT).as_posix(),
        "prefab_roots": [path.relative_to(EXPORT).as_posix() for path in roots],
        "outputs": outputs,
        "shared_assets": shared,
        "runtime_prefabs": runtime_prefabs,
        "counts": {"prefabs": len(roots), "dependency_assets": len(outputs),
                   "shared_assets": len(shared), "runtime_prefabs": len(runtime_prefabs)},
    }
    receipt_bytes = (json.dumps(receipt, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    if RECEIPT.exists() and RECEIPT.read_bytes() != receipt_bytes:
        raise ValueError("Refusing to overwrite changed battle report source receipt: " + str(RECEIPT))
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    if not RECEIPT.exists():
        RECEIPT.write_bytes(receipt_bytes)
    return receipt


if __name__ == "__main__":
    result = build()
    print(json.dumps(result["counts"], ensure_ascii=False, sort_keys=True))
    print("receipt:", RECEIPT.relative_to(ROOT).as_posix(), sha256(RECEIPT))
