"""Migrate original character lifecycle, movement binding and map entity tags."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_mainline_character_components import record
from prepare_unity_map13_sources import encode, write
from prepare_unity_story_sound_runtime import runtime_meta


def replace(text, old, new):
    if old not in text:
        raise ValueError("Original character adaptation anchor changed: " + old)
    return text.replace(old, new)


def main():
    code = ROOT / ".tools/source-packages/recovered/871_156598/official-code"
    code_receipt = ROOT / "assets/reference/official-map/mainline-character-client-sources-source-manifest.json"
    proof = json.loads(code_receipt.read_bytes())
    assemblies = [code / "managed/decoded" / name for name in ("Assembly-CSharp.dll", "Console.dll")]
    if proof["manifest"] != "871_156598":
        raise ValueError("Original character source version differs")
    for assembly in assemblies:
        pinned = next(row["output"]["sha256"] for row in proof["assemblies"] if row["name"] == assembly.name)
        if record(assembly)["sha256"] != pinned:
            raise ValueError("Original character assembly changed: " + assembly.name)
    output = ROOT / "unity/Assets/Game/Runtime/Official/EntityClientComponents"
    inputs, outputs = [code_receipt], []
    for name in ("MyCharacter", "MyGridMover", "MapEntityTag", "EMapEntityTagType"):
        source = code / "client-source" / (name + ".cs")
        inputs.append(source)
        text = source.read_text("utf-8-sig")
        text = text.replace("using Common;\n", "").replace("using Framework.DependencyInjection;\n", "").replace("using GameFramework;\n", "")
        if name == "MyCharacter":
            text = text.replace("\t[Inject]\n", "")
            text = replace(text, "MyComponent<CharacterComponent>", "MyComponent<CharacterComponentData>")
            text = replace(text, "protected IFightRootProxy mFightRoot { get; set; }", """private Func<bool> mJoinInMiddle;

    public void Configure(IMyPlayableManager playables, Func<bool> joinInMiddle)
    {
        mMyPlayableManager = playables ?? throw new ArgumentNullException(nameof(playables));
        mJoinInMiddle = joinInMiddle ?? throw new ArgumentNullException(nameof(joinInMiddle));
    }""")
            text = replace(text, "mFightRoot.StageInfo.JoinInMiddle", "mJoinInMiddle()")
            text = replace(text, "OnEnd(MySerializable userData)", "OnEnd(object userData)")
            text = "using System;\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.StoryAnimation;\nusing Zjcs.Official.StoryPlayableManager;\nusing Zjcs.Official.StoryPlayables;\n" + text
        elif name == "MyGridMover":
            text = replace(text, "MyComponent<GridMoverComponent, GridMoverComponentInfo>", "MyComponent<GridMoverComponentData, object>")
        # Preserve complete original method bodies; only the shared ABI types,
        # namespaces and explicit dependency binding differ.
        text = "// Original client character view; see mainline character views source receipt.\nusing Zjcs.Official.EntityLifecycle;\n" + text
        index = text.index("public ")
        text = text[:index] + "namespace Zjcs.Official.EntityClientComponents\n{\n" + text[index:] + "\n}\n"
        # Keep the Flags attribute attached to its enum inside the namespace.
        text = text.replace("[Flags]\nnamespace Zjcs.Official.EntityClientComponents\n{\npublic enum", "namespace Zjcs.Official.EntityClientComponents\n{\n[Flags]\npublic enum")
        target = output / (name + ".cs")
        write(target, text.encode())
        runtime_meta(target)
        outputs.extend((target, Path(str(target) + ".meta")))
    inputs.extend(code / "common-source/Common" / (name + ".cs")
                  for name in ("CharacterComponent", "GridMoverComponent", "GridMoverComponentInfo"))
    outputs.extend(ROOT / path for path in (
        "unity/Assets/Game/Runtime/Official/EntityClientComponents/EntityClientComponentData.cs",
        "unity/Assets/Game/Runtime/Official/EntityClientComponents/Zjcs.Official.EntityClientComponents.asmdef",
        "unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientComponents/NativeMap14ClientComponentBindings.cs",
        "unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.CharacterComponents.cs",
        "unity/Assets/Game/Editor/MainlineWorldStartup/Zjcs.Editor.MainlineWorldStartup.asmdef"))
    dependencies = [ROOT / "assets/reference/official-map" / name for name in (
        "mainline-character-components-source-manifest.json", "map14-shared-ec-runtime-source-manifest.json")]
    write(ROOT / "assets/reference/official-map/mainline-character-views-source-manifest.json", encode(dict(
        format=1, manifest="871_156598", assemblies=[record(path) for path in assemblies],
        inputs=[record(path) for path in inputs], tools=[record(Path(__file__).resolve())],
        dependencies=[record(path) for path in dependencies], outputs=[record(path) for path in outputs],
        operation="Copy complete original MyCharacter, MyGridMover, MapEntityTag and EMapEntityTagType. Preserve original character online subscription, initial refresh, base lifecycle, join-in-middle branch and Play request ordering. Replace original DI with explicit playable-manager and deferred StageInfo.JoinInMiddle injection. Bind the real original CharacterComponent event directly without copying dispatch/state; expose original grid mover Info identity. Preserve original tag flags and any-bit query.",
        validation=dict(entry="Zjcs.Editor.MainlineWorldStartup.OfficialMainlineWorldStartupAcceptance.RunClientContext"),
        limitations=["Formal complete native character prefab loading and minimap root/HUD wiring are unfinished.",
                     "Request verification does not prove the entry light effect is rendered."])))
    print("Migrated original character lifecycle, grid mover and map tag classes:", 4, flush=True)


if __name__ == "__main__":
    main()
