"""Record exact source inputs and independent pre-physics solver bindings."""
import json
from pathlib import Path

from prepare_battle_constraint_channel_fixtures import ROOT, sha, skeleton


def build():
    receipt = ROOT / "assets/reference/profession-ui/skeleton-data-source-manifest.json"
    source = json.loads(receipt.read_bytes())
    if source["manifest"] != "871_156598":
        raise ValueError("Verified original character data version changed")
    inputs, local_constraints = [], []
    for row in source["outputs"]:
        path = ROOT / row["file"]["path"]
        raw = path.read_bytes()
        if sha(raw) != row["file"]["sha256"]:
            raise ValueError("Verified original player data changed")
        data = skeleton(raw)
        if max(value["order"] for value in data["ik"]) >= min(value["order"] for value in data["transform"]):
            raise ValueError("Inspect interleaved original constraint order")
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw)))
        local_constraints.append(dict(sex=row["sex"], definitions=[value for value in data["transform"] if value.get("local")]))
    runtime = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI"
    receipts = [receipt, ROOT / "assets/reference/battle-animation/constraint-channels-source-manifest.json",
        ROOT / "assets/reference/profession-ui/native-ik-source-manifest.json",
        ROOT / "assets/reference/profession-ui/idle-transform-source-manifest.json"]
    files = [runtime / (name + ".cs") for name in ("ProfessionNativeConstraintPose", "ProfessionNativeConstraintAnimation",
        "ProfessionNativeIdleIk", "ProfessionNativeIk", "ProfessionNativeIdleTransform", "ProfessionNativeBonePose",
        "ProfessionNativeAppearanceSkeleton", "ProfessionNativeAnimationChannels", "ProfessionNativeCurve")]
    record = lambda path: dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))
    evidence = dict(manifest=source["manifest"], inputs=inputs, source_receipts=list(map(record, receipts)),
        local_constraints=local_constraints, semantics_reference="https://en.esotericsoftware.com/spine-transform-constraints",
        generator=record(Path(__file__).resolve()), native_runtime=list(map(record, files)),
        operation="Reuse exact packaged original player data, native local/slot/deform sampling, and independently derived unit-scale IK and world-absolute transform geometry. Restore the two original local-absolute rotation/XY constraints with independent planar matrices in the constrained bone parent's frame. Track world overrides explicitly so already-constrained inputs require applied-pose decomposition. Sample constraints at the identical normalized animation time, preserve source ordering and skin selection, and reset from original local pose on each evaluation. No animation SDK source or runtime is used.",
        limitations=["Explicitly pre-physics and unmixed. Existing solver restrictions remain enforced, including nonunit/sheared IK, relative transform constraints, local scale/shear or applied-pose decomposition, and selective inheritance branches. Unknown active world constraint kinds and interleaved IK/transform orders are rejected. Formal player binding remains required."])
    output = ROOT / "assets/reference/battle-animation/constraint-pose-source-manifest.json"
    output.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", "utf8", newline="\n")
    print("Original pre-physics constraint pose inputs:", len(inputs))


if __name__ == "__main__":
    build()
