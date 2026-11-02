"""Record the common visibility extraction without changing original source receipts."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PREDECESSOR = "f827eab75e6404731d5fb73a97e8fbb427e46797"
OLD = "unity/Assets/Game/Runtime/Offline/World/NativeMap13GridVisibility/OfflineMap13GridVisibility.cs"
NEW = "unity/Assets/Game/Runtime/Offline/World/MapGridVisibility"


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def record(relative):
    return {"path": relative, "sha256": sha((ROOT / relative).read_bytes().replace(b"\r\n", b"\n"))}


def build():
    # A Git predecessor records our offline implementation's origin. It is not
    # represented as an official client/server source.
    previous = subprocess.check_output(["git", "show", PREDECESSOR + ":" + OLD], cwd=ROOT)
    official = "assets/reference/official-map/map13-grid-visibility-source-manifest.json"
    outputs = sorted(p.relative_to(ROOT).as_posix() for p in (ROOT / NEW).iterdir() if p.is_file())
    outputs += [NEW + ".meta",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.NativeMap13.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Zjcs.Presentation.World.asmdef",
        "unity/Assets/Game/Editor/Map13GridVisibility/OfficialMap13LocalVisibilityAcceptance.cs",
        "unity/Assets/Game/Editor/Map13GridVisibility/Zjcs.Editor.Map13.GridVisibility.asmdef",
        "unity/Assets/Game/Editor/Map13GridVisibility/SharedMapGridVisibilityAcceptance.cs",
        "unity/Assets/Game/Editor/Map13GridVisibility/SharedMapGridVisibilityAcceptance.cs.meta"]
    outputs += ["unity/Assets/Game/Editor/Map13MiniMap/" + name for name in (
        "OfficialMap13MiniMapMarkDataAcceptance.cs", "OfficialMap13MiniMapMarkManagersAcceptance.cs",
        "OfficialMap13MiniMapMarkSpawnAcceptance.cs", "OfficialMap13MiniMapMarkSupportsAcceptance.cs",
        "OfficialMap13MiniMapMasksAcceptance.cs", "Zjcs.Editor.Map13.MiniMap.asmdef")]
    result = {
        "format": 1,
        "operation": "Extract the existing local visibility implementation into one configuration-driven module. Preserve the migrated original component, body/camp queries, event ordering, RLE and legacy Map13 save field. Move Map13-specific source assertions to its loader. Validate local snapshot runs before the original recovering decoder.",
        "official_source_receipt": record(official),
        "offline_predecessor": {"commit": PREDECESSOR, "path": OLD, "sha256": sha(previous)},
        "unchanged_official_implementation": [record(p.relative_to(ROOT).as_posix()) for p in sorted(
            (ROOT / "unity/Assets/Game/Runtime/Official/World/Map13GridVisibility").glob("*.cs"))],
        "text_hashing": "UTF-8 file bytes with CRLF normalized to LF; original source receipts retain their own hash conventions.",
        "outputs": [record(p) for p in outputs],
        "tool": record(Path(__file__).relative_to(ROOT).as_posix()),
    }
    path = ROOT / "assets/reference/official-map/mainline-grid-visibility-source-manifest.json"
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Recorded common grid visibility extraction:", len(outputs), "outputs")


if __name__ == "__main__":
    build()
