"""Copy the official clip-override playable; verify its existing runtime dependencies."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_story_playable_runtime import logging_adapter


def build():
    # Resolve the selected manifest through the same source-selection implementation
    # used by extraction, rather than assuming an APK and cloud version coincide.
    from official_source import OfficialSource
    version = OfficialSource().version
    code = ROOT / ".tools/source-packages/current" / version / "official-code"
    manifest = json.loads((code.parent / "code-manifest.json").read_bytes())
    assembly = code / "managed/decoded/Assembly-CSharp.dll"
    if sha(assembly.read_bytes()) != manifest["assemblies"][assembly.name]["decoded_sha256"]:
        raise ValueError("Official client assembly hash changed")
    dependency_receipt = ROOT / "assets/reference/official-map/map14-story-animator-runtime-source-manifest.json"
    dependencies = json.loads(dependency_receipt.read_bytes())
    compatibility = []
    for entry in dependencies["inputs"]:
        path = code / "client-source" / Path(entry["path"]).name
        if sha(path.read_bytes()) != entry["sha256"]:
            raise ValueError("Selected original Animator dependency differs: " + path.name)
        compatibility.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    runtime_dependencies = []
    for entry in dependencies["outputs"]:
        path = ROOT / entry["path"]
        raw = path.read_bytes()
        # Git's Windows checkout may convert text to CRLF; verify the recorded
        # generated LF bytes without changing the expected source fingerprint.
        if sha(raw.replace(b"\r\n", b"\n")) != entry["sha256"]:
            raise ValueError("Migrated Animator dependency changed: " + str(path))
        runtime_dependencies.append(dict(path=source_path(path), checkoutSha256=sha(raw),
            generatedLfSha256=entry["sha256"]))
    original = code / "client-source/MyPlayableOverrideAnimator.cs"
    text = original.read_text(encoding="utf8")
    text = text.replace("using GameFramework;", "using Zjcs.Official.StoryPlayables;")
    text = re.sub(r"(?m)^using Framework\.Logging[^\n]*\n", "", text)
    text, count = logging_adapter(text)
    pos = text.index("[MyPlayableApply")
    text = ("// Migrated official source; see battle-rules/animator-runtime-source-manifest.json.\n"
            "using Object = UnityEngine.Object;\n" + text[:pos]
            + "namespace Zjcs.Official.StoryAnimation\n{\n" + text[pos:] + "\n}\n")
    output = ROOT / "unity/Assets/Game/Runtime/Official/StoryAnimation/MyPlayableOverrideAnimator.cs"
    write(output, text.encode("utf8"))
    meta = Path(str(output) + ".meta")
    guid = hashlib.sha256(source_path(output).encode()).hexdigest()[:32]
    if meta.exists():
        guid = re.search(r"(?m)^guid: ([a-f0-9]{32})$", meta.read_text(encoding="utf8"))[1]
    write(meta, ("fileFormatVersion: 2\nguid: " + guid
        + "\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
        + "  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode())
    record = lambda path: dict(path=source_path(path), sha256=sha(path.read_bytes()))
    receipt = dict(manifest=version, assemblies=[record(assembly)], inputs=[record(original)],
        tools=[record(Path(__file__))], dependencyReceipt=record(dependency_receipt),
        identicalDependencySources=compatibility, runtimeDependencies=runtime_dependencies,
        outputs=[record(output), record(meta)],
        diagnosticBlocks=count,
        operation="Copy MyPlayableOverrideAnimator; adapt only namespaces, Unity Object alias and diagnostic logging. Apply the original clip override before the inherited parameter/state checks. Existing Animator runtime dependencies are byte-identical in the selected source version.",
        limitations=["Existing battle cue/clip payloads keep their recorded source versions; this operation does not update assets or claim full prefab, particle, bone-follow or combat timing migration."])
    write(ROOT / "assets/reference/battle-rules/animator-runtime-source-manifest.json", encode(receipt))
    print("Official clip-override playable migrated from", version)


if __name__ == "__main__":
    build()
