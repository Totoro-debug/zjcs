"""Derive constraint key expectations from the verified lossless original payloads."""
import gzip
import hashlib
import json
from pathlib import Path
import struct

from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def skeleton(raw):
    if raw[:12] != b"ZJCSPROF\x01\x00\x00\x00":
        raise ValueError("Native skeleton envelope differs")
    offset = 12
    for _ in range(2):
        length, = struct.unpack_from("<I", raw, offset)
        offset += 4 + length
    offset += 4
    length, = struct.unpack_from("<I", raw, offset)
    expected = raw[offset + 4:offset + 36]
    compressed, = struct.unpack_from("<I", raw, offset + 36)
    decoded = gzip.decompress(raw[offset + 40:offset + 40 + compressed])
    if len(decoded) != length or hashlib.sha256(decoded).digest() != expected:
        raise ValueError("Native skeleton payload checksum differs")
    return json.loads(decoded)


def interval_values(first, last, axes, defaults, time):
    values = []
    for axis, (name, default) in enumerate(zip(axes, defaults)):
        start, end = first.get(name, default), last.get(name, default)
        curve = first.get("curve")
        if curve == "stepped":
            value = start
        elif isinstance(curve, list):
            t0, t1 = first.get("time", 0), last.get("time", 0)
            x1, y1, x2, y2 = curve[axis * 4:axis * 4 + 4]
            if not t0 <= x1 <= x2 <= t1:
                raise ValueError("Add independent roots for a nonmonotone source time curve")
            def cubic(a, b, c, d, parameter):
                inverse = 1 - parameter
                return inverse**3 * a + 3 * inverse**2 * parameter * b + 3 * inverse * parameter**2 * c + parameter**3 * d
            low, high = 0., 1.
            for _ in range(72):
                middle = (low + high) / 2
                if cubic(t0, x1, x2, t1, middle) < time:
                    low = middle
                else:
                    high = middle
            value = cubic(start, y1, y2, end, (low + high) / 2)
        else:
            value = start + (end - start) * (time - first.get("time", 0)) / (last.get("time", 0) - first.get("time", 0))
        values.append(value)
    return values


def build():
    receipt = ROOT / "assets/reference/profession-ui/skeleton-data-source-manifest.json"
    original = json.loads(receipt.read_bytes())
    if original["manifest"] != "871_156598":
        raise ValueError("Original character payload source version differs")
    cases, sources, counts = [], [], {}
    key_cases = 0
    for entry in original["outputs"]:
        path = ROOT / entry["file"]["path"]
        raw = path.read_bytes()
        if sha(raw) != entry["file"]["sha256"]:
            raise ValueError("Verified lossless player data changed: " + str(path))
        data = skeleton(raw)
        sex = entry["sex"]
        sources.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw)))
        counts[sex] = len(data["animations"])
        for name, animation in data["animations"].items():
            for kind, axes, defaults in (("ik", ("mix", "softness"), (1, 0)),
                    ("transform", ("mixRotate", "mixX", "mixY", "mixScaleX", "mixScaleY", "mixShearY"), (1, 1, 1, 1, 1, 0))):
                for constraint, keys in animation.get(kind, {}).items():
                    for key in keys:
                        cases.append(dict(sex=sex, animation=name, kind=kind, constraint=constraint,
                            time=key.get("time", 0), values=[key.get(axis, default) for axis, default in zip(axes, defaults)],
                            bend=key.get("bendPositive", True), stretch=key.get("stretch", False), compress=key.get("compress", False)))
                        key_cases += 1
                    for first, last in zip(keys, keys[1:]):
                        for fraction in (.25, .5, .75):
                            time = first.get("time", 0) + fraction * (last.get("time", 0) - first.get("time", 0))
                            cases.append(dict(sex=sex, animation=name, kind=kind, constraint=constraint, time=time,
                                values=interval_values(first, last, axes, defaults, time), bend=first.get("bendPositive", True),
                                stretch=first.get("stretch", False), compress=first.get("compress", False)))
                    if keys and keys[0].get("time", 0) > 0:
                        setup = next(row for row in data[kind] if row["name"] == constraint)
                        setup_defaults = (1, 0) if kind == "ik" else (0, 0, 0, 0, 0, 0)
                        cases.append(dict(sex=sex, animation=name, kind=kind, constraint=constraint, time=keys[0]["time"] / 2,
                            values=[setup.get(axis, default) for axis, default in zip(axes, setup_defaults)],
                            bend=setup.get("bendPositive", True), stretch=setup.get("stretch", False), compress=setup.get("compress", False)))
    fixture = dict(cases=cases, counts=[dict(sex=sex, animations=count) for sex, count in counts.items()])
    payload = (json.dumps(fixture, ensure_ascii=False, separators=(",", ":")) + "\n").encode()
    target = validation_directory() / "constraint-cases.json"
    target.write_bytes(payload)
    evidence = dict(manifest=original["manifest"], source_receipt=dict(path=receipt.relative_to(ROOT).as_posix(), sha256=sha(receipt.read_bytes())),
        inputs=sources, generator_sha256=sha(Path(__file__).read_bytes()), animations=counts, key_cases=key_cases,
        sample_cases=len(cases), fixture_sha256=sha(payload),
        native_runtime=[dict(path=path, sha256=sha((ROOT / path).read_bytes())) for path in (
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeConstraintAnimation.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeConstraintAnimation.cs.meta",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAnimationChannels.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeCurve.cs")],
        operation="Read verified packaged lossless player data; derive IK/transform keys and quarter/mid/three-quarter interval expectations directly from original fields and the existing independent format projection defaults. Independently invert each monotone source cubic time curve by 72 bisections. Check setup values before delayed first keys. Do not read or execute an animation SDK.",
        limitations=["Constraint parameter sampling only. Ordered world constraint solving, physics, animation mixing and rendered player actions remain separate required work."])
    destination = ROOT / "assets/reference/battle-animation/constraint-channels-source-manifest.json"
    destination.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", "utf8", newline="\n")
    print("Original animation constraint cases:", len(cases), counts)


if __name__ == "__main__":
    build()
