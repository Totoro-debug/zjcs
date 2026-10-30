"""Analyze the AssetRipper Map15 export and its Unity GUID closure."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")
GUID_REFERENCE = re.compile(r"guid: ([a-f0-9]{32})")
TEXT_ASSETS = {
    ".anim", ".asset", ".controller", ".cs", ".mat", ".meta", ".overridecontroller",
    ".prefab", ".rendertexture", ".shader", ".shadervariants", ".spriteatlas",
    ".terrainlayer",
}
BUILTIN_GUIDS = {
    "00000000000000000000000000000000",
    "0000000000000000e000000000000000",
    "0000000000000000f000000000000000",
}


def io_path(path: Path) -> Path:
    resolved = path.resolve()
    if os.name == "nt" and not str(resolved).startswith("\\\\?\\"):
        return Path("\\\\?\\" + str(resolved))
    return resolved


def sha256(path: Path) -> str:
    return hashlib.sha256(io_path(path).read_bytes()).hexdigest()


def roots_for(assets: Path) -> list[Path]:
    roots = set()
    for path in (assets / "Prefabs/Map").glob("Map_Tower15_*/**/*.prefab"):
        roots.add(path)
    for path in (assets / "Partition").glob("MapGround_15_*/**/*.prefab"):
        roots.add(path)

    rendering = json.loads((ROOT / "assets/official/map15-rendering.json").read_text(encoding="utf-8"))
    for terrain in rendering["terrains"].values():
        source = terrain.get("ground")
        if source:
            path = assets.parent / source
            if not io_path(path).is_file():
                raise FileNotFoundError("Official Map15 ground prefab is absent from export: " + source)
            roots.add(path)
    roots.update(interaction_roots(assets))
    if not roots:
        raise ValueError("No official Map15 prefab roots were found")
    return sorted(roots)


def interaction_roots(assets: Path) -> list[Path]:
    roots = []
    for folder in (
        "Prefabs/UI/WorldMap",
        "Prefabs/UI/ConfirmDialog",
        "Prefabs/UI/MiniGame",
        "Prefabs/Playables/EC/ActionWorld",
    ):
        roots.extend((assets / folder).rglob("*.prefab"))
    scene = assets / "Scenes/map_world.unity"
    if not io_path(scene).is_file():
        raise FileNotFoundError("Official world-map scene is absent from export: " + scene.as_posix())
    roots.append(scene)
    return roots


def build_index(assets: Path) -> dict[str, Path]:
    result = {}
    for meta in assets.rglob("*.meta"):
        asset = meta.with_suffix("")
        if not io_path(asset).is_file():
            continue
        match = GUID.search(io_path(meta).read_text(encoding="utf-8", errors="strict"))
        if not match:
            continue
        guid = match[1]
        if guid in result:
            raise ValueError("AssetRipper export contains duplicate GUID " + guid)
        result[guid] = asset
    return result


def close(assets: Path, roots: list[Path], index: dict[str, Path]) -> tuple[list[Path], list[dict]]:
    pending, found, unresolved = list(roots), set(), {}
    while pending:
        path = pending.pop()
        if path in found:
            continue
        if not io_path(path).is_file():
            raise FileNotFoundError(path)
        found.add(path)
        companions = [path, Path(str(path) + ".meta")]
        for source in companions:
            if source.suffix not in TEXT_ASSETS or not io_path(source).is_file():
                continue
            for guid in GUID_REFERENCE.findall(io_path(source).read_text(encoding="utf-8", errors="strict")):
                target = index.get(guid)
                if target is not None:
                    pending.append(target)
                elif guid not in BUILTIN_GUIDS:
                    unresolved.setdefault(guid, []).append(source.relative_to(assets).as_posix())
    return sorted(found), [dict(guid=guid, referenced_by=sorted(paths))
                           for guid, paths in sorted(unresolved.items())]


def analyze(export_assets: Path) -> dict:
    assets = export_assets.resolve()
    if not assets.is_dir() or assets.name != "Assets":
        raise ValueError("--export-assets must point to AssetRipper ExportedProject/Assets")
    roots = roots_for(assets)
    index = build_index(assets)
    files, unresolved = close(assets, roots, index)
    scripts = [path for path in files if path.suffix in {".cs", ".dll"}]
    return dict(
        manifest="871_156598",
        export_assets=assets.as_posix(),
        root_count=len(roots),
        roots=[path.relative_to(assets).as_posix() for path in roots],
        indexed_assets=len(index),
        closure_files=len(files),
        closure_bytes=sum(io_path(path).stat().st_size for path in files),
        scripts=[path.relative_to(assets).as_posix() for path in scripts],
        unresolved_guids=unresolved,
        outputs=[dict(path=path.relative_to(assets).as_posix(), sha256=sha256(path),
                      meta_sha256=sha256(Path(str(path) + ".meta"))
                      if io_path(Path(str(path) + ".meta")).is_file() else None)
                 for path in files],
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-assets", type=Path, required=True)
    args = parser.parse_args()
    result = analyze(args.export_assets)
    print(json.dumps({key: value for key, value in result.items() if key != "outputs"},
                     ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
