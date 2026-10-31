"""Project verified original physics fields; do not execute an animation SDK."""
import collections
import json
from pathlib import Path

from prepare_battle_constraint_channel_fixtures import ROOT, interval_values, sha, skeleton
from validation_workspace import validation_directory

AXES = ("x", "y", "rotate", "scaleX", "shearX", "limit", "step", "inertia", "strength", "damping", "massInverse", "wind", "gravity", "mix")
GLOBALS = ("inertiaGlobal", "strengthGlobal", "dampingGlobal", "massGlobal", "windGlobal", "gravityGlobal", "mixGlobal")
READER_HASH = "e6a0c0c5a38c811c7b23074fb22381615edbbc0be42db2cd12468764228e8897"


def build():
    receipt = ROOT / "assets/reference/profession-ui/skeleton-data-source-manifest.json"
    original = json.loads(receipt.read_bytes())
    if original["manifest"] != "871_156598":
        raise ValueError("Original character payload source version differs")
    reader = ROOT / ".tools/skeltojson-1.0.1/skeltojson-1.0.1-py3-none-any.whl"
    if sha(reader.read_bytes()) != READER_HASH:
        raise ValueError("Independent MIT binary format reader differs")
    inputs, bodies, cases, setups, skins = [], [], [], [], []
    key_count, interpolation = 0, collections.Counter()
    for entry in original["outputs"]:
        path = ROOT / entry["file"]["path"]
        raw = path.read_bytes()
        if sha(raw) != entry["file"]["sha256"]:
            raise ValueError("Verified lossless player data changed: " + str(path))
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw)))
        data, sex = skeleton(raw), entry["sex"]
        animations = data["animations"]
        setup_animation = next(name for name, row in animations.items() if not row.get("physics"))
        bodies.append(dict(sex=sex, animations=len(animations), constraints=len(data["physics"]), setupAnimation=setup_animation,
            tracks=sum(len(channels) for row in animations.values() for channels in row.get("physics", {}).values())))
        definitions = {row["name"]: row for row in data["physics"]}
        indices = {kind + ":" + row["name"]: index for index, (kind, row) in enumerate(
            (kind, row) for kind in ("ik", "transform", "path", "physics") for row in data.get(kind, []))}
        bones = {row["name"]: index for index, row in enumerate(data["bones"])}
        for row in data["physics"]:
            values = [row.get(axis, 5000 if axis == "limit" else 1 if axis == "mix" else 0) for axis in AXES]
            mass = row.get("mass", 1)
            values[AXES.index("massInverse")] = 1 / mass if mass else 0
            setups.append(dict(sex=sex, constraint=row["name"], index=indices["physics:" + row["name"]],
                order=row.get("order", 0), bone=bones[row["bone"]], skin=row.get("skin", False), values=values,
                globals=[row.get(name, False) for name in GLOBALS]))
        for skin in data["skins"]:
            if skin.get("physics"):
                skins.append(dict(sex=sex, name=skin["name"], indices=sorted(indices["physics:" + name] for name in skin["physics"])))
        for animation, fields in animations.items():
            for constraint, channels in fields.get("physics", {}).items():
                if constraint not in definitions or set(channels) != {"mix"}:
                    raise ValueError("Unverified original physics target or channel")
                keys = channels["mix"]
                key_count += len(keys)
                for key in keys:
                    curve = key.get("curve", "linear")
                    interpolation["bezier" if isinstance(curve, list) else curve] += 1
                    cases.append(dict(sex=sex, animation=animation, constraint=constraint, time=key.get("time", 0), value=key.get("value", 0)))
                for first, last in zip(keys, keys[1:]):
                    for fraction in (.25, .5, .75):
                        time = first.get("time", 0) + fraction * (last.get("time", 0) - first.get("time", 0))
                        cases.append(dict(sex=sex, animation=animation, constraint=constraint, time=time,
                            value=interval_values(first, last, ("value",), (0,), time)[0]))
                if keys[0].get("time", 0) > 0:
                    cases.append(dict(sex=sex, animation=animation, constraint=constraint, time=keys[0]["time"] / 2,
                        value=definitions[constraint].get("mix", 1)))
                cases.append(dict(sex=sex, animation=animation, constraint=constraint, time=keys[-1].get("time", 0) + .25,
                    value=keys[-1].get("value", 0)))
    fixture = dict(bodies=bodies, setups=setups, skins=skins, cases=cases)
    payload = (json.dumps(fixture, ensure_ascii=False, separators=(",", ":")) + "\n").encode()
    (validation_directory() / "physics-cases.json").write_bytes(payload)
    paths = ("unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativePhysicsAnimation.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativePhysicsAnimation.cs.meta",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAnimationChannels.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeCurve.cs")
    evidence = dict(manifest=original["manifest"], inputs=inputs,
        source_receipt=dict(path=receipt.relative_to(ROOT).as_posix(), sha256=sha(receipt.read_bytes())),
        format_reader=dict(path=reader.relative_to(ROOT).as_posix(), sha256=READER_HASH, module="SkelToJson/converter.py", license="MIT",
            operation="Inspect only binary format projection defaults for physics setup and scalar timelines; no animation SDK implementation."),
        generator_sha256=sha(Path(__file__).read_bytes()),
        reference_helper=dict(path="scripts/prepare_battle_constraint_channel_fixtures.py", sha256=sha((ROOT / "scripts/prepare_battle_constraint_channel_fixtures.py").read_bytes())),
        bodies=bodies, setup_cases=len(setups), skin_cases=len(skins),
        key_cases=key_count, interpolation=dict(interpolation), sample_cases=len(cases), fixture_sha256=sha(payload),
        native_runtime=[dict(path=path, sha256=sha((ROOT / path).read_bytes())) for path in paths],
        operation="Read byte-verified historical lossless skeleton payloads; project all physics setup parameters, global flags, original global indices/order/bones and skin memberships. Independently derive every mix key, three interior interval samples, delayed-first setup and held-last values. Retain setup mix default one and omitted timeline value zero. No source version substitution, animation SDK execution or generated art.",
        limitations=["Unmixed physics parameter sampling only; physical integration, world-pose modification, animation mixing, complete player appearance binding and Windows Player release remain required.", "Only mix timelines occur in these source data. Other physics timeline kinds and global timeline targets are explicitly rejected until bound."])
    destination = ROOT / "assets/reference/battle-animation/physics-channels-source-manifest.json"
    destination.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", "utf8", newline="\n")
    print("Original physics:", len(setups), "setups,", len(skins), "skins,", key_count, "keys,", len(cases), "samples", dict(interpolation))


if __name__ == "__main__":
    build()
