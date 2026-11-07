"""Bind the verified native ConfirmDialog to official battle-leave wording."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path
from official_apk import ROOT
from official_source import OfficialSource
from refresh_current_localization import language
import xxhash


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build() -> dict:
    native_receipt = ROOT / "assets/reference/partner-confirm-dialog-source-manifest.json"
    recovered = json.loads(native_receipt.read_bytes())
    prefab = ROOT / "unity/Assets/Official/Partners/UI/Confirm/ConfirmDialog.prefab"
    if recovered["manifest"] != OfficialSource().version or digest(prefab) != recovered["installed_output_sha256"][prefab.relative_to(ROOT).as_posix()]:
        raise ValueError("Recovered official ConfirmDialog differs from its selected-source receipt")
    confirm_catalog = ROOT / "unity/Assets/Game/Resources/Partners/OfficialPartnerConfirmAssetCatalog.asset"
    prompt_catalog = ROOT / "unity/Assets/Game/Resources/Partners/OfficialPartnerPromptAssetCatalog.asset"
    dialog_binding = re.search(r"(?m)^  prefab: (.+)$", confirm_catalog.read_text(encoding="utf-8")).group(1)
    mask_binding = re.search(r"(?m)^  maskPrefab: (.+)$", prompt_catalog.read_text(encoding="utf-8")).group(1)
    texts, sources = language(ROOT / ".tools/source-packages/WwiseBnk")
    keys = ("confirmation", "confirm", "cancel", "LeaveGame.ConfirmText",
            "ui_daily_dungeon_end_panel_leave_game_tip", "ui_voidfissure_leave_confirm")
    rows = [{"key": key, "text": texts[xxhash.xxh64_intdigest(key.encode("utf-8"))]} for key in keys]
    output = ROOT / "unity/Assets/Game/Resources/Combat/BattleLeave/leave-texts.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    offline_rows = [{"key": "ui_daily_dungeon_end_panel_leave_game_tip",
                     "text": "退出将按已通过行数结算当前评级奖励及房间掉落，确认要离开副本吗？"}]
    output.write_text(json.dumps({"manifest": OfficialSource().version, "texts": rows,
                                  "offlineTexts": offline_rows},
                                 ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    def guid(path: Path) -> str:
        return re.search(r"(?m)^guid: ([a-f0-9]{32})$", path.read_text(encoding="utf-8")).group(1)
    script_meta = ROOT / "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLeaveAssetCatalog.cs.meta"
    catalog = output.parent / "OfficialBattleLeaveAssetCatalog.asset"
    catalog.write_text("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
                       "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
                       "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
                       "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
                       f"  m_Script: {{fileID: 11500000, guid: {guid(script_meta)}, type: 3}}\n"
                       "  m_Name: OfficialBattleLeaveAssetCatalog\n  m_EditorClassIdentifier: ''\n"
                       f"  dialogPrefab: {dialog_binding}\n"
                       f"  maskPrefab: {mask_binding}\n"
                       f"  texts: {{fileID: 4900000, guid: {guid(Path(str(output) + '.meta'))}, type: 3}}\n", encoding="utf-8")
    paths = [ROOT / ".tools/source-packages/current" / OfficialSource().version
             / "official-code/client-source" / (name + ".cs") for name in (
        "FightMainPanelLeaveModule", "FightDailyDungeonMainPanel", "FightMainPanelLeaveHandler", "DefaultFightMainPanelLeaveHandler",
        "DailyDungeonMainPanelLeaveHandler", "VoidFissureMainPanelLeaveHandler",
        "DemonBattlefieldMainPanelLeaveHandler", "FreeBattleMainPanelLeaveHandler",
        "DemonKingMainPanelLeaveHandler", "LegionBossMainPanelLeaveHandler")]
    paths += [native_receipt, script_meta, prefab, confirm_catalog, prompt_catalog]
    receipt = {"manifest": OfficialSource().version, "language_inputs": sources,
               "offline_wording": {"rows": offline_rows, "decision": "User confirmed that daily dungeon exit awards the grade reached by cleared rows plus cleared-room drops. Original wording is retained in texts; the solo override avoids saying that grading is forfeited."},
               "implementation_inputs": [{"path": path.relative_to(ROOT).as_posix(), "sha256": digest(path)} for path in paths],
               "outputs": {path.relative_to(ROOT).as_posix(): digest(path) for path in (output, catalog)},
               "operation": "xxHash64 lookup in verified official Chinese language rawfiles; reuse the already recovered native ConfirmDialog and WindowMask through their explicit asset catalogs, without duplicating or changing their native payloads.",
               "generator_sha256": digest(Path(__file__))}
    target = ROOT / "assets/reference/battle-rules/leave-source-manifest.json"
    target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return receipt


if __name__ == "__main__":
    print("Battle leave assets:", len(build()["outputs"]))
