"""Port the selected original dynamic-separator application, never its SDK base."""
import json
from pathlib import Path

from prepare_battle_scene_appearance_runtime import BASE, ROOT, identity, meta, replace, write_changed


def main():
    receipt = BASE / "code-manifest.json"
    code = json.loads(receipt.read_bytes())
    assembly = BASE / "official-code/managed/decoded/Assembly-CSharp.dll"
    assert code["manifest"] == "884_156692"
    assert identity(assembly)["sha256"] == code["assemblies"]["Assembly-CSharp.dll"]["decoded_sha256"]
    source = BASE / "official-code/client-source/AppearanceSystem.SpineExt/DynamicSkeletonRenderSeparator.cs"
    text = source.read_text(encoding="utf-8-sig")
    start = text.index("\tprivate Queue<SkeletonPartsRenderer>")
    text = text[start:text.rfind("}")]
    text = replace(text, "SkeletonPartsRenderer", "IProfessionSceneMeshPart")
    text = replace(text, "SkeletonRenderer skeletonRenderer", "IProfessionSceneSeparatorRenderer skeletonRenderer")
    text = replace(text, "((SkeletonRenderSeparator)dynamicSkeletonRenderSeparator).skeletonRenderer = skeletonRenderer;",
        "dynamicSkeletonRenderSeparator.skeletonRenderer = skeletonRenderer;\n        dynamicSkeletonRenderSeparator.backend = skeletonRenderer.CreateSeparatorBackend();")
    text = replace(text, "((SkeletonRenderSeparator)dynamicSkeletonRenderSeparator).OptimizeRender", "dynamicSkeletonRenderSeparator.backend.OptimizeRender")
    text = replace(text, "((SkeletonRenderSeparator)dynamicSkeletonRenderSeparator).OnEnable()", "dynamicSkeletonRenderSeparator.backend.OnEnable()")
    text = replace(text, "((SkeletonRenderSeparator)this).SkeletonRenderer.separatorSlots.Count", "skeletonRenderer.SeparatorSlots.Count")
    text = replace(text, "((Component)((SkeletonRenderSeparator)this).SkeletonRenderer).transform", "skeletonRenderer.GameObject.transform")
    text = replace(text, "((Component)skeletonRenderer).gameObject", "skeletonRenderer.GameObject")
    text = replace(text, "base.partsRenderers", "backend.PartsRenderers")
    text = replace(text, "base.skeletonRenderer", "skeletonRenderer")
    text = replace(text, "IProfessionSceneMeshPart.NewPartsRendererGameObject", "backend.NewPartsRendererGameObject")
    text = replace(text, "mPartsRendererPool.TryDequeue(ref val)", "mPartsRendererPool.TryDequeue(out val)")
    text = replace(text, "(Object)(object)skeletonRenderer == (Object)null", "skeletonRenderer == null || skeletonRenderer is Object unitySource && !unitySource")
    text = replace(text, "!((Object)(object)partsRenderer == (Object)null)", "partsRenderer != null && !(partsRenderer is Object unityPart && !unityPart)")
    text = replace(text, "((Component)partsRenderers[num2]).gameObject", "partsRenderers[num2].GameObject")
    text = replace(text, "((Component)backend.PartsRenderers[num]).gameObject", "backend.PartsRenderers[num].GameObject")
    text = replace(text, "((Component)val).gameObject", "val.GameObject")
    text = text.replace("(Object)(object)", "")
    text = text.replace("((Renderer)meshRenderer).", "meshRenderer.")
    text = replace(text, "((Renderer)partsRenderer.MeshRenderer).isVisible", "partsRenderer.MeshRenderer.isVisible")
    start = text.index("\t\t\tGLoggerDebugInterpolatedStringHandler")
    end = text.index("\n\t\t\treturn null;", start)
    text = text[:start] + "            // Original null-source branch returns without adding a component." + text[end:]
    header = '''using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.CombatAppearance
{
// Original 884_156692 application pool, ordering and visibility decisions.
[ExecuteAlways]
public class DynamicSkeletonRenderSeparator : MonoBehaviour, IProfessionAppearancePartSeparator
{
    private IProfessionSceneSeparatorRenderer skeletonRenderer;
    private IProfessionSceneSeparatorBackend backend;
    public IReadOnlyList<IProfessionSceneMeshPart> PartsRenderers => backend.PartsRenderers;
    public event ProfessionPartSharedMaterial OnGetSharedMaterial
    {
        add { backend.OnGetSharedMaterial += value; }
        remove { backend.OnGetSharedMaterial -= value; }
    }
    public void ClearSharedMaterials() => backend.ClearSharedMaterials();
    void IProfessionAppearancePartSeparator.ReInitPartRenderers(int layer, int extra, int increment, int order)
        => ReInitPartRenderers(layer, extra, increment, order, true);
'''
    text = header + text + "}\n}\n"
    for token in ("GLogger", "SkeletonRenderSeparator)", "using Spine", "((Component)", "(Object)(object)"):
        assert token not in text, token
    target = ROOT / "unity/Assets/Game/Runtime/Official/CombatAppearance/DynamicSkeletonRenderSeparator.cs"
    write_changed(target, text)
    files = [target,
        ROOT / "unity/Assets/Game/Runtime/Official/CombatAppearance/ProfessionSceneSeparatorBindings.cs",
        ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneMeshSeparator.cs",
        ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneMeshPart.cs",
        ROOT / "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneSeparator.cs"]
    for path in files:
        meta(path)
    proof = dict(manifest=code["manifest"], receipt=identity(receipt), assembly=identity(assembly),
        original_application=identity(source), outputs=[identity(p) for p in files],
        generator=identity(Path(__file__).resolve()), helpers=[identity(ROOT / "scripts/prepare_battle_scene_appearance_runtime.py")],
        operation="Preserve the original application's reverse shrink/FIFO reuse, new-only sorting, minimum/extra part counts, visibility notifications and active-only destruction. Replace SDK base calls with explicit ports and independent native Unity mesh assignment.",
        limits=["The native backend consumes completed geometry and source page materials; it does not implement animation, physics, mixing or the SDK optimization algorithm. Production scene/character bindings and Windows Player validation remain required."])
    write_changed(ROOT / "assets/reference/battle-animation/scene-separator-source-manifest.json", json.dumps(proof, ensure_ascii=False, indent=2) + "\n")
    print("Migrated current dynamic separator application and recorded native mesh assignment sources.")


if __name__ == "__main__":
    main()
