"""Copy the game's original transform attachment configuration and playable."""
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_unity_story_attachment_runtime import CODE, OUTPUT, NAMESPACE, record

RECEIPT = ROOT / "assets/reference/official-map/map14-story-attachment-transform-runtime-source-manifest.json"


def build():
    manifest = json.loads((CODE.parent / "code-manifest.json").read_bytes())
    assemblies = [CODE / "managed/decoded" / name for name in ("Assembly-CSharp.dll", "Console.dll")]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest["assemblies"][path.name]["decoded_sha256"]:
            raise ValueError("Selected original attachment transform assembly changed")
    sources = [CODE / "client-source" / (name + ".cs") for name in
        ("AttachPosCfg", "AttachPosRotCfg", "EAttachTo", "MyPlayableAttachToBone")]
    sources.append(CODE / "common-source/Common/Direction.cs")
    outputs, adaptations = [], []
    for path in sources:
        text = path.read_text(encoding="utf8")
        text = re.sub(r"(?m)^using (?:Common|GameFramework);\n", "", text)
        text = text.replace("namespace Common;\n", "")
        # ILSpy interpreted the out/ref metadata ambiguously. Unity's public
        # TryGetComponent(out T) is the identical call on the same object.
        out_calls = text.count("TryGetComponent<AttachAgent>(ref attachAgent)")
        text = text.replace("TryGetComponent<AttachAgent>(ref attachAgent)", "TryGetComponent<AttachAgent>(out attachAgent)")
        pos = min([m.start() for m in re.finditer(r"(?m)^(?:\[Serializable\]|\[MyPlayableApply|public (?:class|enum))", text)])
        text = "// Migrated official application source; see attachment transform source receipt.\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\n" + text[:pos] + "namespace " + NAMESPACE + "\n{\n" + text[pos:] + "\n}\n"
        target = OUTPUT / path.name
        write(target, text.encode())
        runtime_meta(target)
        outputs.extend([target, Path(str(target) + ".meta")])
        adaptations.append(dict(source=path.name, unityTryGetComponentOutCalls=out_calls))
    write(RECEIPT, encode(dict(manifest="871_156598", mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sources],
        tools=[record(Path(__file__)), record(ROOT / "scripts/prepare_unity_story_attachment_runtime.py"), record(ROOT / "scripts/prepare_unity_story_sound_runtime.py")],
        dependencies=[record(ROOT / "assets/reference/official-map/map14-story-attachment-runtime-source-manifest.json")],
        outputs=[record(p) for p in outputs], adaptations=adaptations,
        operation="Copy original AttachPosCfg, AttachPosRotCfg, EAttachTo, Common.Direction and MyPlayableAttachToBone. Preserve source/target/skill/damage selection, missing-bone root fallback, original direction/offset/rotation/scale formulas, parent restoration, show/hide following, fade and state mutex branches. Adapt namespaces and Unity out argument syntax only.",
        limitations=["Original actor named-transform/skeleton binding, full playable prefab closure, formal story/map/save and Windows Player remain required."])))
    print("Original transform attachment application runtime migrated:", len(sources), "files.", flush=True)


if __name__ == "__main__":
    build()
