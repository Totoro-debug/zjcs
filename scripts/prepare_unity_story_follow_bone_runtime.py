"""Migrate original application transform following without an animation SDK."""
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_unity_story_attachment_runtime import CODE, record

OUTPUT = ROOT / "unity/Assets/Game/Runtime/Official/StoryFollowBone"
RECEIPT = ROOT / "assets/reference/official-map/map14-story-follow-bone-runtime-source-manifest.json"
NAMESPACE = "Zjcs.Official.StoryFollowBone"


def build():
    manifest = json.loads((CODE.parent / "code-manifest.json").read_bytes())
    assemblies = [CODE / "managed/decoded" / name for name in ("Assembly-CSharp.dll", "GameFramework.Runtime.dll", "Framework.FirstPass.Runtime.dll")]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest["assemblies"][path.name]["decoded_sha256"]:
            raise ValueError("Selected original bone-following assembly changed")
    sources = [CODE / "client-source" / (name + ".cs") for name in
        ("MyPlayableFollowBone", "TransformUtils", "TransformSyncType", "IPlayableOverrideAttachOffset", "MySizeCfg", "ApplyOnStopFadeOut", "MyEffectFadeOut")]
    sources += [CODE / "gameframework-csharp9-source/MyPlayableConfig.cs", CODE / "gameframework-csharp9-source/GameFramework/ApplyOnStop.cs"]
    sources += [CODE / "framework-firstpass-source" / (name + ".cs") for name in ("MonoDictionary", "MonoKeyValuePair")]
    outputs, adaptations = [], []
    for path in sources:
        text = path.read_text(encoding="utf8")
        text = re.sub(r"(?m)^using (?:Common|GameFramework|Framework(?:\.[\w.]+)?);\n", "", text)
        text = re.sub(r"(?m)^using Cysharp.Threading.Tasks.CompilerServices;\n", "", text)
        if path.stem == "ApplyOnStop":
            text = text.replace("namespace GameFramework\n{\n", "").rstrip()
            assert text.endswith("}")
            text = text[:-1]
        text = re.sub(r"(?m)^\s*\[(?:Rename|ScriptableObjectConfig)[^\n]*\n", "", text)
        diagnostics = 0
        if path.stem == "MyPlayableFollowBone":
            text, diagnostics = logging_adapter(text)
            old = "[Inject]\n\tprivate IScriptableObjectConfig<MyPlayableConfig> MyPlayableConfig { get; set; }"
            assert text.count(old) == 1
            text = text.replace(old, "public MyPlayableConfig MyPlayableConfig { private get; set; }")
            text = text.replace("MyPlayableConfig.Value.FollowBoneGridSizeToSize", "MyPlayableConfig.FollowBoneGridSizeToSize")
        if path.stem == "MyEffectFadeOut":
            # Restore the async loop from its emitted MoveNext, keeping the first
            # delta increment before evaluation and NextFrame only before the
            # next iteration. UniTask supplies the same cancellation behavior.
            for token in ("_003Ctime_003E5__2 += Time.deltaTime;", "mFadeCurve.Evaluate(num2)",
                "SetParticlesColor(num3)", "SetRendersColor(num3)", "SetTMPTextAlpha(num3", "UniTask.NextFrame(cancellationToken, false)"):
                assert token in text, token
            start = text.index("\t[StructLayout(")
            end = text.index("\t[SerializeField]\n\tprivate AnimationCurve", start)
            text = text[:start] + text[end:]
            start = text.index("\t[AsyncStateMachine(")
            end = text.index("\tprivate void SetParticlesEnable", start)
            text = text[:start] + '''\tprivate async UniTaskVoid FadeOutColor(float duration, CancellationToken cancellationToken)
\t{
\t\tfloat time = 0f;
\t\twhile (true)
\t\t{
\t\t\ttime += UnityEngine.Time.deltaTime;
\t\t\tfloat alpha = mFadeCurve.Evaluate(time / duration);
\t\t\tif (!myParticlesUseEnabledForFade) SetParticlesColor(alpha);
\t\t\tSetRendersColor(alpha);
\t\t\tSetTMPTextAlpha(alpha, mTMPTextColorList);
\t\t\tif (time >= duration) return;
\t\t\tawait UniTask.NextFrame(cancellationToken, false);
\t\t}
\t}

''' + text[end:]
            text, diagnostics = logging_adapter(text)
            text = text.replace("_ = ref textInfo.meshInfo[materialReferenceIndex];", "ref TMP_MeshInfo originalMeshInfo = ref textInfo.meshInfo[materialReferenceIndex];")
            text = "using EmissionModule = UnityEngine.ParticleSystem.EmissionModule;\n" + text
        if path.stem == "MySizeCfg":
            old = "\t\tGridTransformComponent component = ((Component)this).GetComponent<MyEntity>().Entity.GetComponent<GridTransformComponent>();\n\t\tif (component == null || component.Info == null)"
            assert text.count(old) == 1
            text = text.replace(old, "\t\tint? bodyRangeCount = BodyRangeInfoCount();\n\t\tif (!bodyRangeCount.HasValue)")
            text = text.replace("component.Info.BodyRangeInfos.Count", "bodyRangeCount.Value")
            text = text.replace("\tpublic int GetGridSize()", "\t// Explicit offline entity-binding boundary. Null result retains the original\n\t// missing GridTransform/Info branch; an unbound provider fails rather than\n\t// silently inventing an automatic body size.\n\tpublic System.Func<int?> BodyRangeInfoCount { private get; set; }\n\n\tpublic int GetGridSize()")
        out_calls = len(re.findall(r"(?:TryGetComponent|Remove)[^;\n]*\bref ", text))
        text = re.sub(r"(TryGetComponent<MySizeCfg>\()ref ", r"\1out ", text)
        text = text.replace("mSerialized.Remove(key, ref ", "mSerialized.Remove(key, out ")
        match = re.search(r"(?m)^[ \t]*(?:\[Serializable\]|\[Flags\]|\[MyPlayableApply|public (?:class|static class|interface|struct|enum))", text)
        assert match, path.name
        pos = match.start()
        text = "// Migrated official application/framework source; see bone-following source receipt.\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.StoryAttachment;\n" + text[:pos] + "namespace " + NAMESPACE + "\n{\n" + text[pos:] + "\n}\n"
        target = OUTPUT / path.name
        write(target, text.encode())
        runtime_meta(target)
        outputs.extend([target, Path(str(target) + ".meta")])
        adaptations.append(dict(source=path.name, diagnosticBlocks=diagnostics, outArgumentCalls=out_calls,
            explicitConfigInjection=path.stem == "MyPlayableFollowBone", explicitEntityBodyRangeBinding=path.stem == "MySizeCfg",
            asyncRestoredFromOriginalMoveNext=path.stem == "MyEffectFadeOut"))
    asmdef = OUTPUT / (NAMESPACE + ".asmdef")
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE,
        references=["Zjcs.Official.StoryPlayables", "Zjcs.Official.StoryAttachment", "UniTask", "Unity.TextMeshPro"], autoReferenced=True)))
    runtime_meta(asmdef)
    runtime_meta(OUTPUT)
    outputs.extend([asmdef, Path(str(asmdef) + ".meta"), Path(str(OUTPUT) + ".meta")])
    write(RECEIPT, encode(dict(manifest="871_156598", mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sources],
        tools=[record(Path(__file__)), record(ROOT / "scripts/prepare_unity_story_playable_runtime.py"), record(ROOT / "scripts/prepare_unity_story_sound_runtime.py"), record(ROOT / "scripts/prepare_unity_story_attachment_runtime.py")],
        outputs=[record(p) for p in outputs], adaptations=adaptations,
        operation="Copy original application bone-follow playable, transform utilities, size configuration, fade callbacks and original framework dictionary/config types. Restore MyEffectFadeOut's async loop from emitted MoveNext, preserving delta increment, curve evaluation, particle/sprite/text order, NextFrame and cancellation. Preserve transform formulas, static/dynamic following, duration gates, original reset/captured transforms, fade/visibility/grid-size branches and mutex order. Adapt namespaces, diagnostics, DI to explicit original config injection, Unity out syntax, and the entity body-range lookup to an explicitly bound count provider; retain the original rounding/square-root and missing-info fallback.",
        limitations=["Original config asset/entity body-range provider, original playable prefab/art closure, actor bone/skeleton binding, full story/map/save and Windows Player remain required."])))
    print("Original bone-follow application runtime migrated:", len(sources), "files.", flush=True)


if __name__ == "__main__":
    build()
