"""Migrate official source-actor fading and its ordered callbacks.

The animation SDK is not imported. A native actor must explicitly implement the
small color boundary; its implementation is deliberately not fabricated here.
"""
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_unity_story_attachment_runtime import CODE, record

OUTPUT = ROOT / "unity/Assets/Game/Runtime/Official/StorySourceFade"
RECEIPT = ROOT / "assets/reference/official-map/map14-story-source-fade-runtime-source-manifest.json"
NAMESPACE = "Zjcs.Official.StorySourceFade"

PORT = '''using UnityEngine;
namespace Zjcs.Official.StorySourceFade
{
    // Explicit boundary to the independent native actor. No SDK implementation.
    public abstract class NativeActorFadeColor
    {
        public abstract float R { get; set; }
        public abstract float G { get; set; }
        public abstract float B { get; set; }
        public abstract float A { get; set; }
    }
    public abstract class NativeActorFadeRenderer : MonoBehaviour
    {
        public abstract NativeActorFadeColor Skeleton { get; }
    }
    internal static class NativeActorFadeColorExtensions
    {
        public static void SetColor(NativeActorFadeColor target, Color value)
        {
            target.R=value.r; target.G=value.g; target.B=value.b; target.A=value.a;
        }
    }
}
'''


def build():
    manifest = json.loads((CODE.parent / "code-manifest.json").read_bytes())
    assemblies = [CODE / "managed/decoded" / name for name in
                  ("Assembly-CSharp.dll", "CustomWater.dll", "GameFramework.Runtime.dll", "Console.dll")]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest["assemblies"][path.name]["decoded_sha256"]:
            raise ValueError("Selected original actor fading assembly changed")
    names = ("ApplyInitOnSourceFadeIn", "ApplyInitOnSourceFadeOut", "ApplyOnStopSourceFadeOut",
             "IEntityFadeAnimationBridge", "IEntityFadeAnimationSupporter", "EntityFadeAnimationSupporter",
             "SpriteRendererList", "CustomRendererFade")
    sources = [CODE / "client-source" / (name + ".cs") for name in names]
    sources.append(CODE / "customwater-source/FordingSpriteRenderer.cs")
    outputs, adaptations = [], []
    for path in sources:
        text = path.read_text(encoding="utf8")
        text = re.sub(r"(?m)^using (?:Common|GameFramework|Framework(?:\.[\w.]+)?|Spine\.Unity);\n", "", text)
        text, diagnostic_blocks = logging_adapter(text)
        if path.stem == "SpriteRendererList":
            # Only application use sites of SDK color APIs are redirected. The
            # original state machine, coroutine order and color values remain.
            text = re.sub(r"\bSkeletonRenderer\b", "NativeActorFadeRenderer", text)
            # Preserve the public application property name despite the type map.
            text = text.replace("public NativeActorFadeRenderer NativeActorFadeRenderer", "public NativeActorFadeRenderer SkeletonRenderer")
            text = text.replace("SkeletonExtensions.SetColor", "NativeActorFadeColorExtensions.SetColor")
        match = re.search(r"(?m)^(?:\[ExecuteInEditMode\]|public (?:abstract )?(?:class|interface))", text)
        assert match, path.name
        pos = match.start()
        text = ("// Migrated official application source; see source-actor fading receipt.\n"
                "using Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\n"
                "using Zjcs.Official.StoryAttachment;\nusing Zjcs.Official.StoryFollowBone;\n" +
                text[:pos] + "namespace " + NAMESPACE + "\n{\n" + text[pos:] + "\n}\n")
        target = OUTPUT / path.name
        write(target, text.encode()); runtime_meta(target)
        outputs.extend([target, Path(str(target) + ".meta")])
        adaptations.append(dict(source=path.name, diagnosticBlocks=diagnostic_blocks,
                                explicitNativeColorBoundary=path.stem == "SpriteRendererList"))
    target = OUTPUT / "NativeActorFadeRenderer.cs"
    write(target, PORT.encode()); runtime_meta(target)
    outputs.extend([target, Path(str(target) + ".meta")])
    asmdef = OUTPUT / (NAMESPACE + ".asmdef")
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE,
                             references=["Zjcs.Official.StoryPlayables", "Zjcs.Official.StoryAttachment",
                                         "Zjcs.Official.StoryFollowBone"], autoReferenced=True)))
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    outputs.extend([asmdef, Path(str(asmdef) + ".meta"), Path(str(OUTPUT) + ".meta")])
    write(RECEIPT, encode(dict(manifest="871_156598", mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sources],
        tools=[record(Path(__file__)), record(ROOT / "scripts/prepare_unity_story_playable_runtime.py"),
               record(ROOT / "scripts/prepare_unity_story_sound_runtime.py"),
               record(ROOT / "scripts/prepare_unity_story_attachment_runtime.py")],
        outputs=[record(p) for p in outputs], adaptations=adaptations,
        operation="Copy original source-actor fade callbacks, fade bridge/supporter, SpriteRendererList, CustomRendererFade and CustomWater FordingSpriteRenderer. Preserve coroutine delta/lerp/yield order, fade events, AheadInTime, queued disabled fade-in, disable/reset order, original priority comparisons and attachment reference-count calls. Redirect only original application SDK color use sites to an explicit independent native color boundary. Preserve even the original FadeOut fallback's null-test branch; do not silently fix it.",
        limitations=["Native actor color boundary requires actual actor binding; prefab source fields, original art/shaders, formal story/map/save and Windows Player remain required."])))
    print("Original source-actor fading migrated:", len(sources), "application files; explicit native color boundary.")


if __name__ == "__main__":
    build()
