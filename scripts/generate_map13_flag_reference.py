"""Independent double-precision cubic/affine reference for native flag validation."""
import json
import math
from pathlib import Path

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write, meta


def cubic(a, b, c, d, t):
    s = 1 - t
    return s * s * s * a + 3 * s * s * t * b + 3 * s * t * t * c + t * t * t * d


def value(keys, time):
    if time <= keys[0]["time"]: return keys[0]["value"]
    if time >= keys[-1]["time"]: return keys[-1]["value"]
    for a, b in zip(keys, keys[1:]):
        if a["time"] <= time < b["time"]:
            control = a["curve"]
            if not control: return a["value"] + (b["value"] - a["value"]) * (time - a["time"]) / (b["time"] - a["time"])
            lo, hi = 0.0, 1.0
            for _ in range(80):
                middle = (lo + hi) / 2
                if cubic(a["time"], control[0], control[2], b["time"], middle) < time: lo = middle
                else: hi = middle
            return cubic(a["value"], control[1], control[3], b["value"], (lo + hi) / 2)
    raise ValueError("Reference timeline was not covered")


def build():
    source = ROOT / "unity/Assets/Official/World/Map/13/Source/native-flags.json"
    models = json.loads(source.read_text(encoding="utf8"))["models"]
    output = []
    for model in models:
        times = {model["duration"] * i / 64 for i in range(64)}
        times.update(k["time"] for t in model["tracks"] for k in t["keys"] if k["time"] < model["duration"])
        samples = []
        for time in sorted(times):
            angles = [b["rotation"] for b in model["bones"]]
            for track in model["tracks"]: angles[track["bone"]] += value(track["keys"], time)
            # Each point is composed from the leaf to the root using complex
            # multiplication, independently of the runtime's Unity matrix path.
            def transform(index, point):
                while index >= 0:
                    bone = model["bones"][index]
                    point = point * complex(math.cos(math.radians(angles[index])), math.sin(math.radians(angles[index])))
                    point += complex(bone["x"], bone["y"]) * model["scale"]
                    index = bone["parent"]
                return point
            vertices = []
            for vertex in model["vertices"]:
                point = 0j
                for weight in model["influences"][vertex["first"]:vertex["first"] + vertex["count"]]:
                    point += transform(weight["bone"], complex(weight["x"], weight["y"]) * model["scale"]) * weight["weight"]
                vertices.append(dict(x=point.real, y=point.imag))
            samples.append(dict(time=time, rotations=angles, vertices=vertices))
        output.append(dict(name=model["name"], samples=samples))
    path = ROOT / "unity/Assets/Official/World/Map/13/Source/native-flags-reference.json"
    write(path, encode(dict(mapClassId=13, source=dict(path=source_path(source), sha256=sha(source.read_bytes())),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())), models=output)))
    meta(path)
    print("Generated independent native flag reference:", sum(len(m["samples"]) for m in output), "poses,",
        sum(len(s["vertices"]) for m in output for s in m["samples"]), "weighted vertices")


if __name__ == "__main__": build()
