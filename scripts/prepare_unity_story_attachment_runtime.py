"""Migrate the game's own attachment registry and visibility playable.

These are application classes, not animation SDK implementation. The native
actor host must bind the original named transforms to AttachAgent separately.
"""
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_story_playable_runtime import logging_adapter, runtime_meta

CODE = ROOT / ".tools/source-packages/current/871_156598/official-code"
OUTPUT = ROOT / "unity/Assets/Game/Runtime/Official/StoryAttachment"
RECEIPT = ROOT / "assets/reference/official-map/map14-story-attachment-runtime-source-manifest.json"
NAMESPACE = "Zjcs.Official.StoryAttachment"


def record(path):
    return dict(path=source_path(path), sha256=sha(path.read_bytes()))


def method(text, signature):
    start = text.index(signature)
    body = text.index("{", start)
    depth = 1
    end = body + 1
    while depth:
        depth += (text[end] == "{") - (text[end] == "}")
        end += 1
    return text[start:end]


def build():
    manifest = json.loads((CODE.parent / "code-manifest.json").read_bytes())
    assemblies = [CODE / "managed/decoded" / name for name in ("Assembly-CSharp.dll", "Console.dll")]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest["assemblies"][path.name]["decoded_sha256"]:
            raise ValueError("Selected original attachment assembly changed: " + path.name)
    inputs, outputs, adaptations = [], [], []
    for name in ("AttachAgent", "MyPlayableActiveBone"):
        path = CODE / "client-source" / (name + ".cs")
        inputs.append(record(path))
        text = path.read_text(encoding="utf8")
        text = re.sub(r"(?m)^using (?:Common|Framework(?:\.[\w.]+)?|GameFramework);\n", "", text)
        text, diagnostics = logging_adapter(text)
        # Preserve both original utility methods below, including map clearing,
        # duplicate-key overwrite, null filtering and Array.Sort ordering.
        text = text.replace("Utils.FillMapByList", "AttachmentRegistryUtils.FillMapByList")
        text = text.replace("Utils.FillListByMapSort", "AttachmentRegistryUtils.FillListByMapSort")
        pos = text.index("[MyPlayableApply") if name == "MyPlayableActiveBone" else text.index("public class ")
        text = "using Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\n" + text[:pos] + "namespace " + NAMESPACE + "\n{\n" + text[pos:] + "\n}\n"
        target = OUTPUT / (name + ".cs")
        write(target, ("// Migrated official application source; see map14-story-attachment-runtime source receipt.\n" + text).encode())
        runtime_meta(target)
        outputs.extend([target, Path(str(target) + ".meta")])
        adaptations.append(dict(source=name, diagnosticBlocks=diagnostics, utilityClassQualification=name == "AttachAgent"))
    utility = CODE / "common-source/Common/Utils.cs"
    inputs.append(record(utility))
    text = utility.read_text(encoding="utf8")
    methods = [method(text, signature) for signature in (
        "public static void FillMapByList<T1, T2>", "public static void FillListByMapSort<T1, T2>")]
    target = OUTPUT / "AttachmentRegistryUtils.cs"
    write(target, ("// Two original Common.Utils methods; see attachment runtime source receipt.\nusing System;\nusing System.Collections.Generic;\nnamespace " + NAMESPACE + "\n{\ninternal static class AttachmentRegistryUtils\n{\n" + "\n\n".join(methods) + "\n}\n}\n").encode())
    runtime_meta(target)
    outputs.extend([target, Path(str(target) + ".meta")])
    asmdef = OUTPUT / (NAMESPACE + ".asmdef")
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, references=["Zjcs.Official.StoryPlayables"], autoReferenced=True)))
    runtime_meta(asmdef)
    runtime_meta(OUTPUT)
    outputs.extend([asmdef, Path(str(asmdef) + ".meta"), Path(str(OUTPUT) + ".meta")])
    write(RECEIPT, encode(dict(manifest="871_156598", mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=inputs,
        tools=[record(Path(__file__)), record(ROOT / "scripts/prepare_unity_story_playable_runtime.py")],
        outputs=[record(p) for p in outputs], adaptations=adaptations,
        operation="Copy original application AttachAgent, MyPlayableActiveBone and two original Common.Utils methods. Preserve named-transform lookup/registration, initial active-state refcounts, increment/decrement order, root visibility, stop restoration and original duration. Adapt namespace and diagnostics only; no animation SDK implementation is used.",
        limitations=["Original actor transform binding, skeleton animation/rendering, full story/map/save and Windows Player remain separate required work."])))
    print("Original application attachment/visibility runtime migrated.", flush=True)


if __name__ == "__main__":
    build()
