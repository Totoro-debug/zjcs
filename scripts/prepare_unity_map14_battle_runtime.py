"""Package the verified current Map14 battle closure for the offline host."""
from pathlib import Path
import hashlib
import json
from prepare_unity_map10_battle_data import write_meta

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unity/Assets/Official/World/Map/14/map14-battles.json"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map14-battles-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map14/battle-runtime.json"
RECEIPT = ROOT / "assets/reference/official-map/map14-battle-runtime-source-manifest.json"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def encode(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode()


def build():
    source = json.loads(SOURCE.read_bytes())
    receipt = json.loads(SOURCE_RECEIPT.read_bytes())
    if source.get("manifest") != "871_156598" or source.get("mapClassId") != 14:
        raise ValueError("Map14 battle source is not current")
    if receipt.get("output", {}).get("sha256") != sha(SOURCE):
        raise ValueError("Map14 battle source differs from its extraction receipt")
    stages, rosters, scenes = source["stages"], source["monsterRosters"], source["battleScenes"]
    if len(stages) != 217 or {r["Id"] for r in stages} != {r["StageId"] for r in rosters}:
        raise ValueError("Map14 stage and roster closure is incomplete")
    if len(scenes) != 20 or len(source["monsterArrays"]) != 90:
        raise ValueError("Map14 battle scene or formation closure is incomplete")
    payload = {"manifest": source["manifest"], "mapClassId": 14, "sourceSha256": sha(SOURCE),
               "stages": stages, "monsterRosters": rosters,
               "battleScenes": [dict(GameId=int(key), **value) for key, value in sorted(scenes.items(), key=lambda r: int(r[0]))],
               "entityStageBindings": source["entityStageBindings"]}
    previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    if OUTPUT.exists() and (previous is None or previous["outputs"][0]["sha256"] != sha(OUTPUT)):
        raise ValueError("Refusing to replace an unrelated Map14 runtime battle payload")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(encode(payload))
    write_meta(OUTPUT, "TextScriptImporter")
    entry = lambda path: {"path": path.relative_to(ROOT).as_posix(), "sha256": sha(path)}
    record = {"format": 1, "manifest": source["manifest"], "mapClassId": 14,
              "operation": "Package verified current official stages, monster rosters, battle scenes and entity stage bindings. Mainline gameplay and FightOther models are separate bindings.",
              "inputs": [entry(path) for path in (SOURCE, SOURCE_RECEIPT, Path(__file__))],
              "runtimeSources": [{"path": name, "sha256": hashlib.sha256((ROOT / name).read_bytes().replace(b"\r\n", b"\n")).hexdigest(), "hashLineEndings": "LF"}
                                 for name in ("unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.BattleMonsters.cs",
                                              "unity/Assets/Game/Editor/OfficialMap14BattleDataAcceptance.cs")],
              "outputs": [entry(OUTPUT), entry(OUTPUT.with_name(OUTPUT.name + ".meta"))],
              "counts": receipt["counts"]}
    RECEIPT.write_bytes(encode(record))
    print(json.dumps(record["counts"]))


if __name__ == "__main__":
    build()
