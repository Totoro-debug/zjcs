"""Record selected official grid-transform inputs and the C# adaptation hashes."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build() -> dict:
    version = OfficialSource().version
    selected = ROOT / ".tools/source-packages/current" / version / "official-code"
    source_paths = (
        "common-source/Common/GridTransformComponent.cs",
        "common-source/Common/MyMath.cs",
        "common-source/Common/Direction.cs",
        "common-source/Common/GridGroundComponent.cs",
        "client-source/MyFightHitBase.cs",
        "client-source/MyFightAI.cs",
        "common-source/Common/FightActionSkillMoveComponent.cs",
        "common-source/Common/ActionAgentComponentImplement.cs",
        "common-source/Common/ActionType.cs",
        "common-source/Common/ActionBehaviour.cs",
    )
    inputs = {}
    for relative in source_paths:
        path = selected / relative
        if not path.is_file():
            raise FileNotFoundError("Selected decompiled source is missing: " + str(path))
        inputs[path.relative_to(ROOT).as_posix()] = digest(path)
    tables_path = ROOT / ".tools/source-packages/current" / version / "official-data/tables.json"
    tables = json.loads(tables_path.read_bytes())
    if tables["game_settings"]["Battle"]["Ai"]["ActiveSkillWaitActionTypes"] != [1]:
        raise ValueError("Original active-skill movement wait policy changed")
    move_mutex = next(row for row in tables["state_mutex"] if row["From"] == 1)
    if move_mutex["Behaviours"]["2"] != 2:
        raise ValueError("Original move/skill mutex changed")
    inputs[tables_path.relative_to(ROOT).as_posix()] = digest(tables_path)
    outputs = {}
    for relative in (
        "unity/Assets/Game/Runtime/Official/Combat/FightGridTransformMath.cs",
        "unity/Assets/Game/Runtime/Official/Combat/FightGridTransformMath.cs.meta",
        "unity/Assets/Game/Runtime/Official/Combat/FightBattleState.cs",
        "unity/Assets/Game/Runtime/Official/Combat/FightBattleTargeting.cs",
        "unity/Assets/Game/Runtime/Official/Combat/FightBattleHitCollector.cs",
        "unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs",
        "unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.cs",
    ):
        outputs[relative] = digest(ROOT / relative)
    generator = Path(__file__).resolve()
    return {
        "manifest": version,
        "inputs": inputs,
        "outputs": outputs,
        "generator": {"path": generator.relative_to(ROOT).as_posix(), "sha256": digest(generator)},
        "processing": [
            "Port GridTransformComponent.CalcLocalToWorldPos/Dir and MyMath.Rotate using FightGridPoint instead of Pos2d and scalar float parameters instead of Vector2.",
            "Preserve original Direction values (Up=0, Down=1, Left=2, Right=3), float arithmetic, reflection before rotation, real-centre offset and Math.Round ties-to-even conversion.",
            "The unsupported-direction branch keeps the original unrotated vector; the pure module does not reference the original Unity logger.",
            "GridGroundComponent and MyFightHitBase record footprint registration and local-scope presentation evidence. MyFightAI is an empty client proxy, not a server targeting implementation.",
            "User-confirmed offline rule: hit entities inherit the current skill's captured direction and flip, using each original hit entity's RealCenter. Convert local Down/Left/Up/Right indices explicitly to Common.Direction. Carry separate random-parent scope metadata and restore missing immutable metadata in legacy checkpoints.",
            "ActionAgentComponentImplement.CanStartActiveSkill waits for Move (1) when state_mutex discards Skill (2). Prefix ordinary skill, damage and nested movement timelines with accumulated approach time; FightActionSkillMoveComponent.GetTotalMoveDuration counts each destination cell, whereas the local path includes its source cell.",
        ],
    }


if __name__ == "__main__":
    target = ROOT / "assets/reference/battle-rules/grid-transform-source-manifest.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(build(), ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(target.relative_to(ROOT).as_posix())
