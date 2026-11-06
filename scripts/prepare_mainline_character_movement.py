"""Migrate the complete original character movement agent and handler chain."""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_mainline_character_components import record
from prepare_unity_map13_sources import encode, write
from prepare_unity_story_sound_runtime import runtime_meta


def replace(text, before, after):
    if before not in text:
        raise ValueError("Original movement adaptation anchor changed: " + before)
    return text.replace(before, after)


def main():
    recovered = ROOT / ".tools/source-packages/recovered/871_156598/official-code"
    source_receipt = ROOT / "assets/reference/official-map/mainline-character-client-sources-source-manifest.json"
    proof = json.loads(source_receipt.read_bytes())
    pinned = {row["path"]: row["sha256"] for row in proof["outputs"]}
    names = ("MoveAgent", "IMapEntityMoveHandler", "MapEntityMoveHandlerBase",
             "MapEntityMoveHandlerCharacterBase", "MapEntityMoveHandlerCharacterOther", "IMoveEffectSupport")
    inputs = [recovered / "client-source" / (name + ".cs") for name in names]
    inputs += [recovered / "common-source/Common" / (name + ".cs") for name in ("MapMoveType", "MapMoveUtils", "TimeManager")]
    for path in inputs:
        actual = record(path)
        if proof["manifest"] != "871_156598" or pinned.get(actual["path"]) != actual["sha256"]:
            raise ValueError("Original movement source differs: " + str(path))
    output = ROOT / "unity/Assets/Game/Runtime/Official/EntityClientComponents"
    outputs, adaptations = [], []
    for source in inputs[:len(names) + 1]:
        text = source.read_text("utf-8-sig")
        text = re.sub(r"(?m)^using (?:Common|Framework(?:\.[\w.]+)?|Microsoft.Extensions.Logging|GFrameCore.Extensions.ZLogger|ZLogger);\n", "", text)
        text = text.replace("namespace Common;\n", "")
        text = re.sub(r"(?m)^\s*//IL_[^\n]*\n", "", text)
        text = text.replace("Vector2.op_Implicit(", "(Vector2)(").replace("Vector3.op_Implicit(", "(Vector3)(")
        text = text.replace("TimeManager.Instance.", "Runtime.")
        text = text.replace("MapMoveUtils.IsWithinThresholdSqrDistance", "Runtime.IsWithinThresholdSqrDistance")
        text = text.replace("MapMoveUtils.SClientDistanceThreshold", "Runtime.SClientDistanceThreshold")
        if source.stem == "MoveAgent":
            text = replace(text, "MonoBehaviour, IMyEntityRemoveEventReceiver", "ECViewMoveAgent, IMyEntityRemoveEventReceiver")
            text = replace(text, "public bool IsMoving =>", "public override bool IsMoving =>")
            text = text.replace("\t[Inject]\n", "")
            text = replace(text, 'private ILogger<MoveAgent> Logger { get; set; }',
                           'public UnityEngine.ILogger Logger { get; set; } = Debug.unityLogger;\n\n\tpublic IMapEntityMovementRuntime Runtime { get; set; }')
            text = replace(text, 'mMyEnt, Logger);', 'mMyEnt, Logger, Runtime);')
            text = replace(text, '\tprivate void Update()\n', '\tpublic void Advance(float deltaTime)\n')
            text = replace(text, 'mMoveHandler.Update(Time.deltaTime);', 'mMoveHandler.Update(deltaTime);')
            # Unity still drives the original Update body in formal use. The
            # explicit frame entry lets deterministic playback use that body.
            text = replace(text, '\tpublic void Advance(float deltaTime)\n',
                           '\tprivate void Update() => Advance(Time.deltaTime);\n\n\tpublic override void Stop() => Stop(forceStop: false, pos: null);\n\n\tpublic void Advance(float deltaTime)\n')
            text = text.replace('[Rename("禁止转向")]', '[Tooltip("禁止转向")]')
        elif source.stem.startswith("MapEntityMoveHandler"):
            text = replace(text, 'ILogger<MoveAgent> logger)', 'UnityEngine.ILogger logger, IMapEntityMovementRuntime runtime)')
            text = text.replace('base(forbidTurn, myEnt, logger)', 'base(forbidTurn, myEnt, logger, runtime)')
            if source.stem == "MapEntityMoveHandlerBase":
                text = replace(text, 'protected ILogger<MoveAgent> Logger { get; private set; }',
                               'protected UnityEngine.ILogger Logger { get; private set; }\n\n\tprotected IMapEntityMovementRuntime Runtime { get; private set; }')
                text = replace(text, 'Logger = logger;', 'Logger = logger;\n\t\tRuntime = runtime ?? throw new ArgumentNullException(nameof(runtime));')
                text, count = re.subn(r'catch \(Exception ex\)\n\t\t\t\{.*?\n\t\t\t\}',
                                     'catch (Exception ex)\n\t\t\t{\n\t\t\t\tDebug.LogException(ex);\n\t\t\t}', text, flags=re.S)
                if count != 2:
                    raise ValueError("Original movement effect catch blocks changed")
                text = text.replace('\t\tbool flag = default;\n', '')
            elif source.stem == "MapEntityMoveHandlerCharacterOther":
                text, count = re.subn(r'\t\t\tILogger logger = .*?\n\t\t\tGZLoggerExtensions.Info\([^\n]+;\n',
                                     '\t\t\tDebug.Log("FixUpdatePath failed!");\n', text, flags=re.S)
                if count != 1:
                    raise ValueError("Original path repair diagnostic changed")
        if any(token in text for token in ('ZLogger', 'GZLogger', 'TimeManager.', 'MapMoveUtils.')):
            raise ValueError("Unbound original movement dependency: " + source.name)
        first = text.index('[RequireComponent') if source.stem == 'MoveAgent' else text.index('public ')
        imports = ('// Complete original movement implementation; see mainline character movement source receipt.\n'
                   'using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityLifecycle;\n'
                   'using Zjcs.Official.EntityTransformViews;\nusing Zjcs.Official.ProfessionUI;\n'
                   'using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;\n'
                   'using Zjcs.Official.World.NativeTerrain.Common;\n')
        text = imports + text[:first] + 'namespace Zjcs.Official.EntityClientComponents\n{\n' + text[first:] + '\n}\n'
        target = output / source.name
        write(target, text.encode()); runtime_meta(target)
        outputs += [target, Path(str(target) + '.meta')]
        adaptations.append(source.name)
    for folder, name, refs in (
        (output, 'Zjcs.Official.EntityClientComponents', ['Zjcs.Official.EntityTransformViews', 'Zjcs.Official.World.NativeTerrain', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.World.Map13EntityEvents']),
        (ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientComponents', 'Zjcs.Offline.World.NativeMap14ClientComponents', []),
        (ROOT / 'unity/Assets/Game/Editor/MainlineWorldStartup', 'Zjcs.Editor.MainlineWorldStartup', ['Zjcs.Official.EntityTransformViews', 'Zjcs.Official.ProfessionUI'])):
        path = folder / (name + '.asmdef')
        assembly = json.loads(path.read_bytes())
        for reference in refs:
            if reference not in assembly['references']: assembly['references'].append(reference)
        write(path, encode(assembly)); outputs.append(path)
    for path in (
        'unity/Assets/Game/Runtime/Official/EntityClientComponents/IMapEntityMovementRuntime.cs',
        'unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientComponents/NativeMap14MovementRuntime.cs',
        'unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.CharacterMovement.cs'):
        target = ROOT / path
        runtime_meta(target); outputs += [target, Path(str(target) + '.meta')]
    outputs.append(ROOT / 'unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.ClientContext.cs')
    write(ROOT / 'assets/reference/official-map/mainline-character-movement-source-manifest.json', encode(dict(
        format=1, manifest='871_156598', inputs=[record(path) for path in inputs],
        dependencies=[record(source_receipt), record(ROOT / 'assets/reference/official-map/map14-entity-lifecycle-source-manifest.json'),
                      record(ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/SafeAction.cs')],
        tools=[record(Path(__file__).resolve())], outputs=[record(path) for path in outputs],
        validation={'entry': 'Zjcs.Editor.MainlineWorldStartup.OfficialMainlineWorldStartupAcceptance.RunClientContext'},
        operation='Copy complete original MoveAgent, movement-handler interface and three-class implementation chain, move-effect interface and MapMoveType. Preserve path conversion, clock-based interpolation, correction thresholds, turn/scale, path updates, forced/nonforced stop, callbacks, effects and removal cleanup. Original Console TimeManager and MapMoveUtils are called through an explicit runtime adapter, without copying their rules. Reuse the existing separately sourced original SafeAction. Replace DI/logger metadata, repair explicit Unity conversions, and expose the unchanged per-frame body to deterministic driving. Bind runtime before original Awake.',
        limitations=['Formal complete character prefab and map input/native minimap integration remain unfinished; movement fixtures do not prove effect rendering.'])))
    print('Complete original character movement agent and handler chain migrated.', flush=True)


if __name__ == '__main__':
    main()
