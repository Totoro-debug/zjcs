"""Restore hash-pinned Unity camera/timeline packages used by official Map13 resources."""
import hashlib
import io
import json
from pathlib import Path
import tarfile
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
PACKAGES = (
    ("com.unity.cinemachine", "2.9.7", "9a2edc5823fc8d40dccff73c2afe7477f2586ebd0d1feca3fa36d41038de9372"),
    ("com.unity.timeline", "1.7.6", "2bca1f538615b002f640fb0bc43f43af38a38309a3dfaca3722a4f491e46cf65"),
)
RECEIPT = ROOT / "assets/reference/official-map/map13-camera-timeline-packages-source-manifest.json"


def sha(raw): return hashlib.sha256(raw).hexdigest()


def build():
    records = []
    for name, version, expected in PACKAGES:
        url = f"https://download.packages.unity.com/{name}/-/{name}-{version}.tgz"
        archive = ROOT / ".tools/map13-camera-timeline-packages" / (name + "-" + version + ".tgz")
        if not archive.exists():
            original_cache = ROOT / ".tools/unity-cinemachine-source" / archive.name
            raw = original_cache.read_bytes() if original_cache.exists() else urllib.request.urlopen(url, timeout=60).read()
            if sha(raw) != expected: raise ValueError("Pinned official Unity package hash differs: " + name)
            archive.parent.mkdir(parents=True, exist_ok=True); archive.write_bytes(raw)
        raw = archive.read_bytes()
        if sha(raw) != expected: raise ValueError("Cached official Unity package hash differs: " + name)
        destination = ROOT / "unity/Packages" / name
        outputs, omitted = [], []
        with tarfile.open(fileobj=io.BytesIO(raw), mode="r:gz") as tar:
            for item in tar.getmembers():
                if not item.isfile(): continue
                path = Path(item.name)
                if path.parts[0] != "package" or ".." in path.parts: raise ValueError("Unexpected package archive member")
                relative = Path(*path.parts[1:])
                # Runtime, importer/editor code and licenses are required; sample
                # scenes, docs and package tests do not enter the game project.
                if len(relative.parts) > 1 and relative.parts[0] not in ("Runtime", "Editor", "Gizmos", "Presets"):
                    omitted.append(relative.as_posix()); continue
                if len(relative.parts) == 1 and relative.name in ("Tests.meta", "DocCodeExamples.meta"):
                    omitted.append(relative.as_posix()); continue
                if len(relative.parts) == 1 and relative.suffix.lower() in (".png", ".jpg"):
                    omitted.append(relative.as_posix()); continue
                data = tar.extractfile(item).read(); target = destination / relative
                if not target.resolve().is_relative_to(destination.resolve()): raise ValueError("Package output escaped destination")
                if target.exists() and target.read_bytes() != data: raise ValueError("Existing embedded package differs: " + str(target))
                target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
                outputs.append(dict(path=target.relative_to(ROOT).as_posix(), sha256=sha(data), bytes=len(data)))
        package = json.loads((destination / "package.json").read_bytes())
        if package["name"] != name or package["version"] != version: raise ValueError("Wrong original package identity")
        records.append(dict(name=name, version=version, url=url, archive=dict(path=archive.relative_to(ROOT).as_posix(), sha256=expected), outputs=outputs, omitted=omitted))
        print("Restored original Unity package:", name, version, len(outputs), "files", flush=True)
    manifest_path = ROOT / "unity/Packages/manifest.json"
    manifest = json.loads(manifest_path.read_bytes())
    for name, _, _ in PACKAGES:
        binding = "file:" + name
        if name in manifest["dependencies"] and manifest["dependencies"][name] != binding: raise ValueError("Different camera/timeline dependency already bound")
        manifest["dependencies"][name] = binding
    manifest["dependencies"]["com.unity.modules.director"] = "1.0.0"
    manifest_path.write_bytes((json.dumps(manifest, indent=2) + "\n").encode())
    proof = dict(manifest="871_156598", mapClassId=13, packages=records,
        tools=[dict(path=Path(__file__).resolve().relative_to(ROOT).as_posix(), sha256=sha(Path(__file__).read_bytes()))],
        operation="Restore the previously hash-pinned official Unity camera/timeline source packages as embedded dependencies, preserving every included original byte and license. This restores the engine runtime APIs required by the original components; Map13 native prefab, script, camera controller and Timeline binding/playback still require migration and verification.")
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + "\n").encode("utf8"))
    return proof


if __name__ == "__main__": build()
