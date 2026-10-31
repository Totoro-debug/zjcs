"""Record verified official application inputs for scene appearance parts (no SDK code)."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION = "884_156692"
SOURCE = ROOT / ".tools/source-packages/current" / VERSION


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def identity(path: Path) -> dict[str, str]:
    return {"path": path.relative_to(ROOT).as_posix(), "sha256": sha(path)}


def main() -> None:
    manifest_path = SOURCE / "code-manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    assembly = SOURCE / "official-code/managed/decoded/Assembly-CSharp.dll"
    assert manifest["manifest"] == VERSION
    assert sha(assembly) == manifest["assemblies"]["Assembly-CSharp.dll"]["decoded_sha256"]
    original = [SOURCE / "official-code/client-source" / name for name in (
        "AppearanceRendererPartHandler.cs", "AppearanceMecanimPartHandler.cs", "AppearanceAnimationPartHandler.cs",
        "AppearanceSkeletonPart.cs", "AppearanceSystem.Runtime/AppearanceSkeletonPartEntry.cs",
        "AppearanceSystem.Runtime/EffectPartEntry.cs",
    )]
    runtime = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"
    migrated = [runtime / name for name in (
        "AppearanceRendererPartHandler.cs", "AppearanceMecanimPartHandler.cs", "AppearanceAnimationPartHandler.cs",
        "AppearanceSkeletonPartEntry.cs", "EffectPartEntry.cs", "ProfessionAppearanceRendererPartBindings.cs",
    )]
    proof = {
        "manifest": VERSION,
        "apk_sha256": manifest["apk_sha256"],
        "code_receipt": identity(manifest_path),
        "assembly": identity(assembly),
        "official_application_sources": [identity(path) for path in original],
        "migrated_application_and_ports": [identity(path) for path in migrated],
        "existing_part_dependencies": [identity(runtime / name) for name in (
            "AppearanceSkeletonPart.cs", "AppearancePartHandler.cs", "ProfessionAppearancePartBindings.cs",
            "ICharacterPartEntryProvider.cs", "CharacterPartPoolEntry.cs", "ProfessionAppearancePartPaths.cs",
        )],
        "validation": identity(ROOT / "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.RendererParts.cs"),
        "generator": identity(Path(__file__).resolve()),
        "scope": "Official application lifecycle, resource routing, separation requests and animation notification forwarding through explicit ports. No animation SDK implementation is used.",
        "limits": ["Scene part prefabs and renderer/separator/Mecanim backends are not restored by these ports.",
                   "Play Mode probes verify application call ordering and lifecycle, not actual weapon rendering, motion, gameplay input, saves or Windows Player."],
    }
    output = ROOT / "assets/reference/battle-animation/renderer-parts-source-manifest.json"
    output.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Verified {len(original)} official application sources and {len(migrated)} migrated sources/ports: {output.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
