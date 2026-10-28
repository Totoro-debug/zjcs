"""Copy hash-verified native reward assets into an isolated Unity import probe."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
from prepare_unity_profession_native_inputs import ROOT, CACHE


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(project):
    project = project.resolve()
    if project == (ROOT / "unity").resolve() or not project.is_relative_to(ROOT / ".tools"):
        raise ValueError("Use an isolated Unity project under this repository's .tools directory")
    if not (project / "ProjectSettings/ProjectVersion.txt").is_file():
        raise ValueError("The isolated Unity project is missing")
    dest = project / "Assets/ProfessionDynamicResourcesProbe"
    if dest.exists():
        raise ValueError("Existing import probe is not overwritten")
    proof = json.loads((ROOT / "assets/reference/profession-ui/dynamic-resources-source-manifest.json").read_bytes())
    if proof["manifest"] != "871_156598" or len(proof["native_assets"]) != 8:
        raise ValueError("Inspect original profession reward asset scope")
    for row in proof["native_assets"]:
        source = ROOT / row["path"]
        if sha(source) != row["sha256"] or sha(source.with_name(source.name + ".meta")) != row["meta_sha256"]:
            raise ValueError("Original reward native output changed: " + str(source))
    for row in proof["native_assets"]:
        source = ROOT / row["path"]
        target = dest / row["native_relative_path"]
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        shutil.copy2(source.with_name(source.name + ".meta"), target.with_name(target.name + ".meta"))
    fixture = dict(items=[row["expected"] for row in proof["reward_sprite_proofs"]])
    fixture_path = CACHE / "typed/reward-sprite-native-fixture.json"
    fixture_path.write_text(json.dumps(fixture, indent=2) + "\n", encoding="utf-8")
    print("Prepared 8 original native reward assets and their source geometry/GPU pixel fixture")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    build(parser.parse_args().project)
