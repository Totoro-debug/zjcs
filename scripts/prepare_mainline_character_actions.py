"""Migrate the complete original action-playable client, including historical fields."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_mainline_character_components import record
from prepare_mainline_character_views import replace
from prepare_unity_map13_sources import encode, write
from prepare_unity_story_sound_runtime import runtime_meta


def main():
    source_root = ROOT / ".tools/source-packages/recovered/871_156598/official-code"
    source = source_root / "client-source/MyActionAgent.cs"
    enum_source = source_root / "common-source/Common/ActionType.cs"
    source_receipt = ROOT / "assets/reference/official-map/mainline-character-client-sources-source-manifest.json"
    proof = json.loads(source_receipt.read_bytes())
    pinned = {row["path"]: row["sha256"] for row in proof["outputs"]}
    for path in (source, enum_source):
        actual = record(path)
        if proof["manifest"] != "871_156598" or pinned.get(actual["path"]) != actual["sha256"]:
            raise ValueError("Original action source differs: " + str(path))
    text = source.read_text("utf-8-sig")
    for namespace in ("Common", "Framework.DependencyInjection", "GameFramework"):
        text = text.replace("using " + namespace + ";\n", "")
    text = replace(text, "MyComponent<ActionAgentComponent>, IEcEntityEndEventReceiver, IEventReceiver, IActionAgentStartEventReceiver, IActionAgentEndEventReceiver", "MyComponent<ActionAgentComponentData>")
    text = replace(text, "\t[Inject]\n", "")
    text = replace(text, "private IMyPlayableManager mMyPlayableManager { get; set; }", """private IMyPlayableManager mMyPlayableManager { get; set; }

    public void Configure(IMyPlayableManager playables)
    {
        mMyPlayableManager = playables ?? throw new ArgumentNullException(nameof(playables));
    }""")
    text = text.replace("ActionAgentComponent actionAgent", "ActionAgentComponentData actionAgent")
    text = text.replace("ActionComponent", "ActionAgentActionData")
    text = text.replace("MySerializable", "object")
    text = replace(text, "(IActionAgentStartEventReceiver)this", "ActionAgent_OnStart")
    text = replace(text, "(IActionAgentEndEventReceiver)this", "ActionAgent_OnEnd")
    text = replace(text, "action.Entity.Id", "action.EntityId")
    text = replace(text, "action.Entity.EndEvent += (IEcEntityEndEventReceiver)this", "action.EntityEndEvent += Entity_OnEnd")
    text = replace(text, "void IEcEntityEndEventReceiver.Entity_OnEnd(EcEntity actionEntity, object mySerializable)", "private void Entity_OnEnd(ActionAgentActionData actionEntity, object mySerializable)")
    text = replace(text, "actionEntity.EndEvent -= (IEcEntityEndEventReceiver)this", "actionEntity.EntityEndEvent -= Entity_OnEnd")
    text = replace(text, "actionEntity.Id", "actionEntity.EntityId")
    text = "// Original 871 action-playable client; see mainline character actions source receipt.\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.EntityLifecycle;\nusing Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.StoryPlayableManager;\n" + text
    index = text.index("public class")
    text = text[:index] + "namespace Zjcs.Official.EntityClientComponents\n{\n" + text[index:] + "\n}\n"
    output = ROOT / "unity/Assets/Game/Runtime/Official/EntityClientComponents"
    target = output / "MyActionAgent.cs"
    write(target, text.encode())
    enum_target = output / "ActionType.cs"
    write(enum_target, enum_source.read_text("utf-8-sig").replace("namespace Common;", "namespace Zjcs.Official.EntityClientComponents\n{").encode() + b"\n}\n")
    paths = [target, enum_target, output / "ActionAgentComponentData.cs",
             ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientComponents/NativeMap14ClientComponentBindings.Actions.cs",
             ROOT / "unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.CharacterActions.cs"]
    for path in paths:
        runtime_meta(path)
    paths += [Path(str(path) + ".meta") for path in paths]
    paths += [ROOT / name for name in (
        "unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientComponents/NativeMap14ClientComponentBindings.cs",
        "unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientComponents/Zjcs.Offline.World.NativeMap14ClientComponents.asmdef",
        "unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedEventSubscription.cs",
        "unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.ClientContext.cs")]
    write(ROOT / "assets/reference/official-map/mainline-character-actions-source-manifest.json", encode(dict(
        format=1, manifest="871_156598", inputs=[record(source), record(enum_source)],
        dependencies=[record(source_receipt)],
        tools=[record(Path(__file__).resolve())], outputs=[record(path) for path in paths],
        operation="Preserve the complete original MyActionAgent fields, dictionary construction, callback order, start/end/stop bodies and action type enum. Replace only original ABI types, receiver registrations and DI with adapters over the unchanged shared action state/events. Preserve the original EndPlayable condition with Playable as the requested prefab.",
        limitations=["Full native character prefab, real configured effect rendering and formal minimap/HUD wiring remain unfinished."])))


if __name__ == "__main__":
    main()
