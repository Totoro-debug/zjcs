"""Build the offline daily-dungeon claim-count achievement catalog from official data."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = "854_156383"
TABLES = ROOT / ".tools/source-packages/official-data/tables.json"
ACHIEVEMENT_BYTES = ROOT / ".tools/source-packages/official-data/achievement.g.bytes"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Dungeons/daily-dungeon-achievements.json"
RECEIPT = ROOT / "assets/reference/daily-dungeon-achievements-source-manifest.json"
CLIENT_SOURCES = (
    ROOT / ".tools/source-packages/official-code/client-source/DailyDungeonAchievementPanel.cs",
    ROOT / ".tools/source-packages/official-code/common-source/Common/AchievementDataDailyDungeonGetRewardCount.cs",
    ROOT / ".tools/source-packages/official-code/common-source/Common/AchievementTaskDailyDungeonGetRewardCount.cs",
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def count_parameter(value: str | None) -> int:
    if not value:
        return 0
    match = re.fullmatch(r"\s*\{\s*Count\s*:\s*(\d+)\s*\}\s*", value)
    if match is None:
        raise ValueError("Unsupported daily-dungeon achievement parameter: " + repr(value))
    return int(match.group(1))


def build() -> dict:
    tables = json.loads(TABLES.read_text(encoding="utf-8"))
    rows = [row for row in tables["achievement"]
            if int(row["Group"]) == 39 and int(row["AchievementType"]) == 22]
    rows.sort(key=lambda row: int(row["Id"]))
    if len(rows) != 8:
        raise ValueError("Expected eight official daily-dungeon Type 22 rows, found " + str(len(rows)))

    output_rows = []
    for row in rows:
        rewards = row.get("Reward") or {}
        if not rewards:
            raise ValueError("Daily-dungeon achievement reward is missing: " + str(row["Id"]))
        output_rows.append({
            "Id": int(row["Id"]),
            "RequiredCount": count_parameter(row.get("CompleteParams")),
            "CycleIncrement": count_parameter(row.get("CycleIncrementParams")),
            "CycleCount": int(row.get("CycleCount") or 0),
            "Rewards": [
                {"ItemId": int(item_id), "Count": int(count)}
                for item_id, count in sorted(rewards.items(), key=lambda value: int(value[0]))
            ],
        })

    payload = {
        "Manifest": MANIFEST,
        "Group": 39,
        "AchievementType": 22,
        "Rows": output_rows,
    }
    encoded = (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(encoded)

    inputs = [TABLES, ACHIEVEMENT_BYTES]
    receipt = {
        "manifest": MANIFEST,
        "achievementGroup": 39,
        "achievementType": 22,
        "inputs": [
            {"path": path.relative_to(ROOT).as_posix(), "bytes": path.stat().st_size,
             "sha256": sha256(path)}
            for path in inputs
        ],
        "officialCode": [
            {"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path)}
            for path in CLIENT_SOURCES
        ],
        "conversion": "Filter achievement rows by Group=39 and AchievementType=22; parse Count from CompleteParams and CycleIncrementParams; retain CycleCount and reward item counts.",
        "generator": {
            "path": Path(__file__).resolve().relative_to(ROOT).as_posix(),
            "sha256": sha256(Path(__file__).resolve()),
        },
        "output": {
            "path": OUTPUT.relative_to(ROOT).as_posix(),
            "bytes": len(encoded),
            "sha256": hashlib.sha256(encoded).hexdigest(),
            "rows": len(output_rows),
        },
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return receipt


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))
