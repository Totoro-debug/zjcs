"""Install the MIT upstream UniTask dependency for original async state machines."""
import argparse
import json
from pathlib import Path
import urllib.request
import zipfile

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write


URL = "https://codeload.github.com/Cysharp/UniTask/zip/refs/tags/2.5.10"
HASH = "d0d780981391bed739e36bd927557a0a63be0e9f0b86d0fbec24be780422d78d"


def build(project):
    archive = ROOT / ".tools/map13-unitask/2.5.10.zip"
    if not archive.exists(): write(archive, urllib.request.urlopen(URL, timeout=45).read())
    if sha(archive.read_bytes()) != HASH: raise ValueError("Pinned upstream UniTask archive changed")
    outputs = []
    prefix = "UniTask-2.5.10/src/UniTask/Assets/Plugins/UniTask/"
    with zipfile.ZipFile(archive) as source:
        for name in source.namelist():
            if not name.startswith(prefix) or name.endswith("/"): continue
            relative = name[len(prefix):]
            path = project / "Packages/com.cysharp.unitask" / relative
            raw = source.read(name); write(path, raw)
            outputs.append(dict(path=source_path(path), sha256=sha(raw), archive_entry=name))
        license_path = project / "Packages/com.cysharp.unitask/LICENSE"
        raw = source.read("UniTask-2.5.10/LICENSE"); write(license_path, raw)
        outputs.append(dict(path=source_path(license_path), sha256=sha(raw), archive_entry="UniTask-2.5.10/LICENSE"))
    manifest = project / "Packages/manifest.json"
    value = json.loads(manifest.read_text(encoding="utf8"))
    value["dependencies"]["com.cysharp.unitask"] = "file:com.cysharp.unitask"
    write(manifest, encode(value))
    receipt = (ROOT / "assets/reference/official-map/map13-async-dependency-source-manifest.json"
               if project == ROOT / "unity" else ROOT / ".tools/map13-native-cache-871_156598/async-dependency-source-manifest.json")
    write(receipt, encode(dict(format=1, mapClassId=13,
        input=dict(url=URL, path=source_path(archive), sha256=HASH, version="2.5.10", license="MIT"),
        outputs=outputs, generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Install byte-identical upstream UniTask package and license to execute the original game's decompiled async state machines. This is a compatible dependency baseline, not a claim to have recovered the game's precise UniTask package version. The original APK PlayerLoopTiming values are verified separately from its metadata.")))
    print("Installed unmodified upstream UniTask 2.5.10:", len(outputs), "files")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(); parser.add_argument("--project", type=Path, default=ROOT / "unity")
    build(parser.parse_args().project.resolve())
