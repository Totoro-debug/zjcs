"""Bind original character visibility and follower scripts to shared native state."""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_mainline_character_components import record
from prepare_unity_map13_sources import encode, write
from prepare_unity_story_sound_runtime import runtime_meta


def class_body(text, marker):
    start = text.index(marker)
    opening = text.index("{", start)
    depth = 1
    for index in range(opening + 1, len(text)):
        depth += (text[index] == "{") - (text[index] == "}")
        if depth == 0:
            return text[start:index + 1]
    raise ValueError("Original visibility class body is incomplete")


def main():
    source_root = ROOT / ".tools/source-packages/recovered/871_156598/official-code"
    source_receipt = ROOT / "assets/reference/official-map/mainline-character-client-sources-source-manifest.json"
    proof = json.loads(source_receipt.read_bytes())
    pinned = {row["path"]: row["sha256"] for row in proof["outputs"]}
    inputs = [source_root / "client-source" / (name + ".cs")
              for name in ("MyVisibility", "MyVisibilityCustom", "MyNpcFollowerController")]
    inputs += [source_root / "common-source/Common" / (name + ".cs")
               for name in ("VisibilityComponent", "VisibilityCustomComponent",
                            "NpcFollowerControllerComponent", "NpcFollowerControllerComponentInfo")]
    for path in inputs:
        actual = record(path)
        if proof["manifest"] != "871_156598" or pinned.get(actual["path"]) != actual["sha256"]:
            raise ValueError("Original character view source differs: " + str(path))
    # Reuse the existing complete original visibility state machine. Prove its
    # entire generic body still matches the freshly recovered historical IL.
    text = inputs[0].read_text("utf-8-sig")
    text = re.sub(r"(?m)^using (?:Common|Framework.DependencyInjection);\n", "", text)
    text = text.replace(" where T : VisibilityComponent", "").replace(" where TInfo : EcComponentInfo", "")
    text = re.sub(r"\bVisibilityComponent\b", "Map13VisibilityData", text)
    text = re.sub(r"\bMySerializable\b", "object", text)
    text = text.replace("Component.", "((Map13VisibilityData)(object)Component).")
    text = text.replace("\t[Inject]\n", "").replace("private IEntityClientVisibilityHandlerFactory", "public IEntityClientVisibilityHandlerFactory")
    reused = ROOT / "unity/Assets/Game/Runtime/Official/EntityRootVisibility/MyVisibility.cs"
    marker = "public class MyVisibility<T, TInfo>"
    if class_body(text, marker) != class_body(reused.read_text("utf-8-sig"), marker):
        raise ValueError("Review the reused original visibility body before binding the character")
    output = ROOT / "unity/Assets/Game/Runtime/Official/EntityClientComponents"
    outputs = []
    for name, before, after in (
        ("MyVisibilityCustom", "MyVisibility<VisibilityCustomComponent>", "MyVisibility<Map13VisibilityData>"),
        ("MyNpcFollowerController", "MyComponent<NpcFollowerControllerComponent, NpcFollowerControllerComponentInfo>", "MyComponent<NpcFollowerControllerComponentData, object>"),
    ):
        source = source_root / "client-source" / (name + ".cs")
        original = source.read_text("utf-8-sig")
        expected = "using Common;\n\npublic class " + name + " : " + before + "\n{\n}\n"
        if original != expected:
            raise ValueError("Review changed original character script: " + name)
        adapted = original.replace("using Common;", "using Zjcs.Official.EntityLifecycle;\nusing Zjcs.Official.EntityRootVisibility;\nusing Zjcs.Official.World.Map13EntityVisibilityViews;")
        adapted = adapted.replace(before, after)
        index = adapted.index("public class")
        adapted = "// Original character script; see mainline character visibility source receipt.\n" + adapted[:index] + "namespace Zjcs.Official.EntityClientComponents\n{\n" + adapted[index:] + "\n}\n"
        target = output / (name + ".cs")
        write(target, adapted.encode())
        runtime_meta(target)
        outputs += [target, Path(str(target) + ".meta")]
    runtime_meta(ROOT / "unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.CharacterVisibility.cs")
    outputs += [ROOT / path for path in (
        "unity/Assets/Game/Runtime/Official/EntityClientComponents/EntityClientComponentData.cs",
        "unity/Assets/Game/Runtime/Official/EntityClientComponents/Zjcs.Official.EntityClientComponents.asmdef",
        "unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientComponents/NativeMap14ClientComponentBindings.cs",
        "unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientComponents/Zjcs.Offline.World.NativeMap14ClientComponents.asmdef",
        "unity/Assets/Game/Runtime/Offline/World/NativeMap14Visibility/NativeMap14VisibilityData.cs",
        "unity/Assets/Game/Runtime/Offline/World/NativeMap14Visibility/Zjcs.Offline.World.NativeMap14Visibility.asmdef",
        "unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.CharacterVisibility.cs",
        "unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.CharacterVisibility.cs.meta",
        "unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.ClientContext.cs",
        "unity/Assets/Game/Editor/MainlineWorldStartup/Zjcs.Editor.MainlineWorldStartup.asmdef")]
    write(ROOT / "assets/reference/official-map/mainline-character-visibility-source-manifest.json", encode(dict(
        format=1, manifest="871_156598", inputs=[record(path) for path in inputs] + [record(reused)],
        dependencies=[record(source_receipt)] + [record(ROOT / "assets/reference/official-map" / name) for name in (
            "map14-entity-root-visibility-source-manifest.json", "map14-native-visibility-source-manifest.json")],
        tools=[record(Path(__file__).resolve())] + [record(ROOT / "scripts" / name) for name in (
            "prepare_unity_map14_client_components.py", "prepare_unity_map14_native_visibility.py")],
        outputs=[record(path) for path in outputs],
        operation="Copy both complete original character scripts. Verify the complete reused generic MyVisibility body against matching recovered historical IL after the existing ABI/DI substitutions. Bind the real custom visibility component through the existing native flag/event adapter and expose original Info identity. Bind the original follower controller and Info without implementing follower rules.",
        limitations=["Full native character prefab, real visibility handlers/fade rendering, formal minimap/HUD and Windows Player remain unfinished."])))
    print("Original character visibility/follower scripts bound; complete generic visibility body verified.", flush=True)


if __name__ == "__main__":
    main()
