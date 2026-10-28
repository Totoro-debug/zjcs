"""Migrate original stage-window operation and its actual native click component."""
import json
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta

NAMESPACE = 'Zjcs.Official.EntityStageOperations'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/EntityStageOperations'
RECEIPT = ROOT / 'assets/reference/official-map/map14-entity-stage-operations-source-manifest.json'

BOUNDARIES = '''using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.EntityStageOperations
{
    public interface IWorldStageOperationState
    {
        ECViewEntityData Entity { get; }
        bool IsActive();
    }
    public interface IWorldStageInteractionStarter
    {
        void StartStage(ECWorldStageStateData state, int subIndex,
            Dictionary<StageComponentType,object> conditions, object stageData, Action<object,int> callback);
    }
    public interface IWorldStageWindowMapRoot
    {
        bool Initialized { get; }
        IDisposable SubscribeHookMapInput(EcEntityIdentifier id);
    }
    public sealed class StagePopupWindowOperation { public GameObject GameObject; }
    public interface IWorldStageWindowHost
    {
        UniTask<StagePopupWindowOperation> Popup(string name);
        UniTask<StagePopupWindowOperation> PopupAndReport(string name, SDKEventPanelId id, SDKEventPanelSource source);
        WorldStageUiContext GetUiContext(ECWorldStageStateData stage);
    }
    public static class WorldStageOperationServices
    {
        public static IServiceProvider ApplicationScope { get; set; }
        public static IWorldStageWindowHost WindowHost { get; set; }
        public static IWorldStageWindowHost RequireWindowHost => WindowHost ??
            throw new InvalidOperationException("Original stage window host is not bound.");
        public static IWorldStageOperationState State(ECWorldStageStateData stage) => stage as IWorldStageOperationState ??
            throw new InvalidOperationException("Original stage entity/activity state is not bound.");
    }
}
'''


def build():
    proof = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / name for name in ('Assembly-CSharp.dll', 'Console.dll', 'Launch.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != proof['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Selected original stage operation assembly changed')
    sources = [CODE / 'client-source' / (name + '.cs') for name in
               ('ECWorldStageInteractionOperationBase', 'ECWorldStageInteractionOperationWindow',
                'WorldStageInteractionOperationWindowClickData', 'WorldStageInteractionOperationWindowClickType',
                'IWorldStageInteractionOperationWindowHandlerBase', 'IWorldStageUiBase',
                'WorldStageUiClickData', 'WorldStageOperationWindowClickUI')]
    sources += [CODE / 'launch-source' / (name + '.cs') for name in ('SDKEventPanelId', 'SDKEventPanelSource')]
    outputs, adaptations = [], []
    for path in sources:
        text = path.read_text(encoding='utf-8-sig')
        if path.stem == 'ECWorldStageInteractionOperationWindow':
            first = text.index('\t[StructLayout(LayoutKind.Auto)]')
            last = text.index('\t[SerializeField]\n\tprivate bool mIsReport;')
            machine = text[first:last]
            for evidence in ('SubscribeHookMapInput(id)', 'mMapRootMgr.Initialized', 'stageDialog.Init(worldStage)',
                             'DoWindowInit(popupWindowOperation.GameObject, worldStage)', '_003Cbroker_003E5__2.Dispose()'):
                if evidence not in machine:
                    raise ValueError('Original stage popup MoveNext changed: ' + evidence)
            text = text[:first] + text[last:]
            first = text.index('\t[AsyncStateMachine(')
            last = text.index('\n\tpublic virtual void OnClick(', first)
            text = text[:first] + '''\tprivate async UniTask DoPopupWindow(WorldStageComponent worldStage)
    {
        var id = WorldStageOperationServices.State(worldStage).Entity.Id;
        using (var broker = mMapRootMgr.SubscribeHookMapInput(id))
        {
            try
            {
                StagePopupWindowOperation operation;
                if (!mIsReport) operation = await WorldStageOperationServices.RequireWindowHost.Popup(mWindowName);
                else operation = await WorldStageOperationServices.RequireWindowHost.PopupAndReport(mWindowName, mSDKEventPanelId, SDKEventPanelSource.Manual);
                if (mMapRootMgr.Initialized && operation != null)
                {
                    if (operation.GameObject.TryGetComponent<StageDialog>(out var dialog))
                        dialog.Init(WorldStageOperationServices.RequireWindowHost.GetUiContext(worldStage));
                    DoWindowInit(operation.GameObject, worldStage);
                }
            }
            catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
        }
    }
''' + text[last:]
        text = re.sub(r'(?m)^using (?:Common|Framework(?:\.[\w.]+)?|Microsoft.Extensions.DependencyInjection|Cysharp.Threading.Tasks.CompilerServices);\n', '', text)
        text = re.sub(r'(?m)^\s*//IL_[^\n]*\n', '', text)
        text, blocks = logging_adapter(text)
        text = re.sub(r'\bWorldStageComponent\b', 'ECWorldStageStateData', text)
        text = re.sub(r'\bMySerializable\b', 'object', text)
        text = re.sub(r'\bPlayerStageStageDataBase\b', 'object', text)
        text = text.replace('worldStageComponent.IsActive()', 'WorldStageOperationServices.State(worldStageComponent).IsActive()')
        text = text.replace('IMapRootManagerProxy', 'IWorldStageWindowMapRoot')
        text = text.replace('ServiceProviderServiceExtensions.GetService<IWorldStageWindowMapRoot>(serviceProvider)',
                            '(IWorldStageWindowMapRoot)serviceProvider.GetService(typeof(IWorldStageWindowMapRoot))')
        text = text.replace('ServiceProviderServiceExtensions.GetService<IMapStageInteractionHandlerProxy>(ApplicationScope.Current)',
                            '((IWorldStageInteractionStarter)WorldStageOperationServices.ApplicationScope.GetService(typeof(IWorldStageInteractionStarter)))')
        text = text.replace('((UnityEvent)Button.onClick).AddListener((UnityAction)OnClick)',
                            'Button.onClick.AddListener(OnClick)')
        text = text.replace('((UnityEvent)Button.onClick).RemoveListener((UnityAction)OnClick)',
                            'Button.onClick.RemoveListener(OnClick)')
        declaration = re.search(r'(?m)^(?:\[Serializable\]|public (?:abstract )?(?:class|interface|struct|enum))', text)
        if not declaration:
            raise ValueError('Original stage operation declaration missing: ' + path.name)
        text = ('// Migrated complete original stage operation; see stage operation receipt.\n'
                'using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityWorldStages;\n'
                'using StageDialog = Zjcs.Official.WorldUI.StageDialog;\n' + text[:declaration.start()] +
                'namespace ' + NAMESPACE + '\n{\n' + text[declaration.start():] + '\n}\n')
        name = 'WorldStageOperationWindowClickUi.cs' if path.stem == 'WorldStageOperationWindowClickUI' else path.name
        target = OUTPUT / name
        write(target, text.encode()); runtime_meta(target)
        outputs += [target, target.with_suffix(target.suffix + '.meta')]
        adaptations.append(dict(source=path.relative_to(CODE).as_posix(), diagnosticBlocks=blocks))
    target = OUTPUT / 'WorldStageOperationBindings.cs'
    write(target, BOUNDARIES.encode()); runtime_meta(target)
    outputs += [target, target.with_suffix(target.suffix + '.meta')]
    asmdef = OUTPUT / (NAMESPACE + '.asmdef')
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, autoReferenced=True,
          references=['Zjcs.Official.EntityViews', 'Zjcs.Official.EntityWorldStages',
                      'Zjcs.Official.StoryPlayables', 'Zjcs.Official.WorldUI', 'UniTask', 'Unity.ugui'])))
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    outputs += [asmdef, asmdef.with_suffix(asmdef.suffix + '.meta'), OUTPUT.with_suffix('.meta')]
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sources],
        tools=[record(ROOT / 'scripts/prepare_unity_entity_stage_operations.py'),
               record(ROOT / 'scripts/prepare_unity_story_playable_runtime.py'),
               record(ROOT / 'scripts/prepare_unity_story_sound_runtime.py')],
        dependencies=[record(ROOT / 'assets/reference/official-map/map14-entity-world-stage-views-source-manifest.json'),
                      record(ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/StageDialog.cs')],
        outputs=[record(p) for p in outputs], adaptations=adaptations,
        operation='Copy original operation base, window operation, nested operation click records/enums, window handler/UI interfaces, actual native click component/struct and complete launch panel ID/source enums. Restore DoPopupWindow from MoveNext with input hook acquired before try, original reporting split, await, initialized/result checks, StageDialog before click initialization, catch and finally lease disposal. Keep ordered first-match click, recursive operation query/release and activity-gated application-scope StartStage. Replace window singleton/UI shared-state consumption with required explicit hosts; opaque stage/condition payloads pass unchanged. Preserve original click-data struct and foreach semantics.',
        limitations=['Concrete local stage interaction, original window/resource/context hosts, native prefab script bindings, formal map/input/save and Windows Player remain required.'])))
    print('Original entity stage/window operation sources migrated:', len(sources))


if __name__ == '__main__':
    build()
