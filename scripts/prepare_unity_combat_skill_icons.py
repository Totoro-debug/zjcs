"""Generate the Unity skill-icon catalog from the pinned official icons payload.

The official icon PNGs are imported as plain textures, so the catalog stores texture references
plus the normalized pivot and lets the runtime build the sprite. Every source and output hash is
recorded in ``assets/reference/combat/skill-icons-source.json``.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
ICONS = ROOT / "assets/official/icons.json"
SPRITE_ROOT = ROOT / "unity/Assets/Official/Wish/Art/sprites"
RUNTIME_CLASS = ROOT / "unity/Assets/Game/Runtime/Presentation/Combat/OfficialCombatSkillIconCatalog.cs"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Combat/OfficialCombatSkillIconCatalog.asset"
MANIFEST = ROOT / "assets/reference/combat/skill-icons-source.json"


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) < 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("Not a PNG file: " + str(path))
    width, height = struct.unpack(">II", header[16:24])
    if width <= 0 or height <= 0:
        raise ValueError("PNG has an invalid size: " + str(path))
    return width, height


def meta_guid(path: Path) -> str:
    meta = Path(str(path) + ".meta")
    if not meta.is_file():
        return ""
    for line in meta.read_text(encoding="utf-8").splitlines():
        if line.startswith("guid: "):
            return line[6:].strip()
    return ""


def find_sprite(name: str) -> Path | None:
    candidate = SPRITE_ROOT / name
    if candidate.is_file():
        return candidate
    for path in (ROOT / "unity/Assets/Official").rglob(name):
        if path.is_file() and path.suffix == ".png":
            return path
    return None


def build() -> tuple[bytes, dict]:
    runtime_class = RUNTIME_CLASS.read_text(encoding="utf-8")
    if "class OfficialCombatSkillIconCatalog" not in runtime_class:
        raise ValueError("Runtime skill icon catalog class is missing.")
    class_guid = meta_guid(RUNTIME_CLASS)
    if not class_guid:
        raise ValueError("Runtime skill icon catalog meta is missing.")
    icons_bytes = ICONS.read_bytes()
    icons = json.loads(icons_bytes.decode("utf-8"))
    items = []
    for key, value in icons.items():
        file = str(value.get("file") or "")
        if "/skill_" not in file:
            continue
        name = file.rsplit("/", 1)[-1]
        try:
            item_id = int(name[len("skill_"):].split("-", 1)[0])
        except ValueError:
            continue
        png = find_sprite(name)
        if png is None:
            continue
        guid = meta_guid(png)
        if not guid:
            continue
        width, height = png_size(png)
        pivot = value.get("pivot") or [width / 2.0, height / 2.0]
        pivot_x = min(1.0, max(0.0, float(pivot[0]) / width))
        pivot_y = min(1.0, max(0.0, float(pivot[1]) / height))
        ppu = float(value.get("ppu") or 100.0)
        items.append({
            "itemId": item_id,
            "file": name,
            "guid": guid,
            "width": width,
            "height": height,
            "pivotX": round(pivot_x, 6),
            "pivotY": round(pivot_y, 6),
            "pixelsPerUnit": int(ppu) if ppu == int(ppu) else ppu,
            "pngSha256": sha256_file(png),
        })
    if not items:
        raise ValueError("No official skill icons were resolved.")
    # icons.json maps both the skill item id and its piece id to the same file; the file name
    # carries the skill item id, so duplicate file names collapse to one binding.
    deduped = {}
    for item in items:
        deduped.setdefault(item["itemId"], item)
    items = sorted(deduped.values(), key=lambda item: item["itemId"])

    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: " + class_guid + ", type: 3}",
        "  m_Name: OfficialCombatSkillIconCatalog",
        "  m_EditorClassIdentifier: ",
        "  icons:",
    ]
    for item in items:
        lines.append("  - itemId: " + str(item["itemId"]))
        lines.append("    texture: {fileID: 2800000, guid: " + item["guid"] + ", type: 3}")
        lines.append("    pivotX: " + format(item["pivotX"], ".6g"))
        lines.append("    pivotY: " + format(item["pivotY"], ".6g"))
        lines.append("    pixelsPerUnit: " + format(item["pixelsPerUnit"], ".6g"))
    payload = ("\n".join(lines) + "\n").encode("utf-8")

    manifest = {
        "format": 1,
        "manifest": VERSION,
        "operation": "Bind official skill icon textures from icons.json; runtime builds sprites because the pinned PNGs are imported as textures.",
        "inputs": {
            "icons": {
                "path": ICONS.relative_to(ROOT).as_posix(),
                "sha256": sha256_bytes(icons_bytes),
            },
            "runtime_class": {
                "path": RUNTIME_CLASS.relative_to(ROOT).as_posix(),
                "guid": class_guid,
                "sha256": sha256_file(RUNTIME_CLASS),
            },
            "sprite_roots": [SPRITE_ROOT.relative_to(ROOT).as_posix()],
        },
        "itemCount": len(items),
        "items": items,
        "output": {
            "path": OUTPUT.relative_to(ROOT).as_posix(),
            "sha256": sha256_bytes(payload),
            "bytes": len(payload),
        },
    }
    return payload, manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true", help="compare without writing")
    args = parser.parse_args()
    payload, manifest = build()
    manifest_bytes = (json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    if args.verify:
        if not OUTPUT.is_file() or OUTPUT.read_bytes() != payload:
            raise SystemExit("Skill icon catalog differs from the generated bytes.")
        if not MANIFEST.is_file() or MANIFEST.read_bytes() != manifest_bytes:
            raise SystemExit("Skill icon manifest differs from the generated bytes.")
        print("Skill icon catalog is up to date:", manifest["itemCount"], "icons")
        return
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    output_meta = Path(str(OUTPUT) + ".meta")
    if output_meta.is_file():
        guid = meta_guid(OUTPUT) or hashlib.md5(("zjcs-official:" + OUTPUT.relative_to(ROOT).as_posix()).encode()).hexdigest()
    else:
        guid = hashlib.md5(("zjcs-official:" + OUTPUT.relative_to(ROOT).as_posix()).encode()).hexdigest()
    OUTPUT.write_bytes(payload)
    output_meta.write_text(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n  externalObjects: {}\n"
        "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
        encoding="utf-8",
    )
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_bytes(manifest_bytes)
    print("Skill icon catalog:", manifest["itemCount"], "icons")
    print("Output SHA256:", manifest["output"]["sha256"])


if __name__ == "__main__":
    main()
