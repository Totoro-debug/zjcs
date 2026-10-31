"""Derive the original player attachment-deformation cases without an animation SDK."""
import json
from pathlib import Path

from prepare_battle_constraint_channel_fixtures import ROOT, sha, skeleton
from validation_workspace import validation_directory


def build():
    receipt = ROOT / "assets/reference/profession-ui/skeleton-data-source-manifest.json"
    source = json.loads(receipt.read_bytes())
    if source["manifest"] != "871_156598":
        raise ValueError("Verified original character data version changed")
    cases, inputs, counts = [], [], []
    for row in source["outputs"]:
        path = ROOT / row["file"]["path"]
        raw = path.read_bytes()
        if sha(raw) != row["file"]["sha256"]:
            raise ValueError("Verified lossless character data changed")
        data = skeleton(raw)
        counts.append(dict(sex=row["sex"], animations=len(data["animations"])))
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw)))
        for name, animation in data["animations"].items():
            for skin, slots in animation.get("attachments", {}).items():
                for slot, attachments in slots.items():
                    for attachment, channels in attachments.items():
                        if set(channels) != {"deform"} or len(channels["deform"]) != 1:
                            raise ValueError("Inspect additional original attachment channel")
                        key = channels["deform"][0]
                        definition = next(value for value in data["skins"] if value["name"] == skin)["attachments"][slot][attachment]
                        if definition.get("type") != "clipping":
                            raise ValueError("Inspect additional original deform attachment type")
                        expected = [0.] * (definition["vertexCount"] * 2)
                        start = key.get("offset", 0)
                        vertices = key.get("vertices", [])
                        if start < 0 or start + len(vertices) > len(expected):
                            raise ValueError("Original deform range differs")
                        expected[start:start + len(vertices)] = vertices
                        cases.append(dict(sex=row["sex"], animation=name, skin=skin, slot=slot, attachment=attachment,
                            time=key.get("time", 0), vertices=expected, setup=definition["vertices"], end=definition["end"]))
    payload = (json.dumps(dict(cases=cases, counts=counts), ensure_ascii=False, separators=(",", ":")) + "\n").encode()
    (validation_directory() / "deform-cases.json").write_bytes(payload)
    runtime = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI"
    evidence = dict(manifest=source["manifest"], inputs=inputs,
        source_receipt=dict(path=receipt.relative_to(ROOT).as_posix(), sha256=sha(receipt.read_bytes())),
        generator_sha256=sha(Path(__file__).read_bytes()),
        decoder_helper=dict(path="scripts/prepare_battle_constraint_channel_fixtures.py", sha256=sha((ROOT / "scripts/prepare_battle_constraint_channel_fixtures.py").read_bytes())),
        original_tracks=len(cases), fixture_sha256=sha(payload),
        native_runtime=[dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes())) for path in
            (runtime / (name + ".cs") for name in ("ProfessionNativeDeformAnimation", "ProfessionNativeVertexAttachment",
                "ProfessionNativeAttachment", "ProfessionNativePathAttachment", "ProfessionNativeMeshBatches",
                "ProfessionNativeAnimationChannels", "ProfessionNativeAppearanceSkeleton"))],
        operation="Reuse exact lossless 871_156598 player data. Bind the eight single-key clipping-deformation tracks by original skin, slot and shared attachment identity; preserve source-unit influence offsets. Independently transform offsets through the original bone matrices and renderer data scale.",
        limitations=["Multiframe attachment deformation is explicitly unsupported; the eight original player tracks have one key each. Physics, world constraint solving, mixing and complete player actions remain required."])
    output = ROOT / "assets/reference/battle-animation/deform-source-manifest.json"
    output.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", "utf8", newline="\n")
    print("Original attachment deformation tracks:", len(cases))


if __name__ == "__main__":
    build()
