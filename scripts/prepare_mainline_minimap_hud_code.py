"""Migrate the selected original small-map HUD and its local service boundaries."""
import hashlib
import json
import re
import uuid
from pathlib import Path

from official_apk import ROOT

SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/World/MainlineMiniMapHud'
RECEIPT = ROOT / 'assets/reference/official-map/mainline-minimap-hud-code-source-manifest.json'
NAMESPACE = 'Zjcs.Official.World.Map13MiniMap'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def member(text, signature):
    start = text.index(signature)
    brace = text.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


def build():
    inputs, outputs = [], []

    def original(name, folder='client-source'):
        path = SOURCE / folder / (name + '.cs')
        raw = path.read_bytes()
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw)))
        return raw.decode('utf8').replace('\r\n', '\n')

    def install(name, text):
        path = DEST / name
        path.parent.mkdir(parents=True, exist_ok=True)
        normalized = '\n'.join(line.rstrip(' \t') for line in text.replace('\r\n', '\n').split('\n'))
        path.write_text(normalized.rstrip() + '\n', encoding='utf8', newline='\n')
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            identity = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/' + path.relative_to(ROOT).as_posix()).hex
            importer = ('MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n' if path.suffix == '.cs' else 'AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
            meta.write_text('fileFormatVersion: 2\nguid: ' + identity + '\n' + importer, encoding='utf8', newline='\n')
        meta.write_bytes(b'\n'.join(line.rstrip(b' \t\r') for line in meta.read_bytes().split(b'\n')))
        for item in (path, meta):
            outputs.append(dict(path=item.relative_to(ROOT).as_posix(), sha256=sha(item.read_bytes())))

    def wrap(text, extra=''):
        imports = [line for line in text.splitlines() if line.startswith('using ')
            and not any(x in line for x in ('Common;', 'Framework', 'R3;'))]
        body = '\n'.join(line for line in text.splitlines() if not line.startswith('using '))
        return '// Selected original HUD path; see mainline-minimap-hud-code-source-manifest.json.\n' + '\n'.join(imports) + '\n' + extra + '\nnamespace ' + NAMESPACE + '\n{\n' + body.strip() + '\n}\n'

    small = original('MiniMapMaskedUI')
    small = small.replace('[Inject]\n\tprivate IMapRootManagerProxy MapRootManagerProxy { get; set; }',
        'public MiniMapHudContext Context { get; set; }\n\tpublic System.Func<RenderTexture> GetMiniMapTexture { get; set; }')
    small = small.replace('MapRootManagerProxy.Minimap.Character.GetOrAddRenderTexture()', 'GetMiniMapTexture()')
    small = small.replace('WindowManager.Instance.Popup((MiniMapFullScreenPanel panel)', 'Context.PopupFullScreen((IMiniMapHudFullScreenPanel panel)')
    small = small.replace('MapRootManagerProxy.CurrentMapClassId', 'Context.CurrentMapClassId')
    install('MiniMapMaskedUI.cs', wrap(small))

    controller = original('MultiLanguageTMPControler')
    controller = controller.replace('[Inject]\n\tprivate IScriptableObjectConfig<LanguageGroupConfig> LanguageGroupConfig { get; set; }',
        'public LanguageGroupConfig LanguageGroupConfig { get; set; }')
    controller = controller.replace('[Inject]\n\tprotected ILanguageTypeService LanguageTypeService { get; set; }',
        'public ILanguageTypeService LanguageTypeService { get; set; }')
    controller = controller.replace('LanguageGroupConfig.Value.GetLanguageGroupByType', 'LanguageGroupConfig.GetLanguageGroupByType')
    install('MultiLanguageTMPControler.cs', wrap(controller, '''using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;'''))
    group = original('LanguageGroup', 'launch-source')
    install('LanguageGroup.cs', wrap(group))
    group_config = original('LanguageGroupConfig', 'launch-source').replace('[ScriptableObjectConfig]\n', '')
    install('LanguageGroupConfig.cs', wrap(group_config, '''using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;'''))

    area = original('AreaInfoUI')
    area = re.sub(r'\bGridTransformComponent\b', 'ECGridTransformData', area)
    area = area.replace('[Inject]\n\tprivate IMapRootManagerProxy MapRootManagerProxy { get; set; }',
        'public MiniMapHudContext Context { get; set; }')
    area = area.replace('\n\t[Inject]\n\tprivate IResourceLoader ResourceLoader { get; set; }\n', '\n')
    replacements = {
        'private DisposableBag mDisposableBag;': 'private MiniMapSpawnDisposableBag mDisposableBag;',
        'LanguageUtils.TextKeyArgWrap': 'MiniMapHudLanguageKey',
        'myMapController.MyEntity.Entity.GridTransform': 'myMapController.MyGridTransform.Component',
        'myMapController.GridPos': 'myMapController.MyGridTransform.Pos',
        'mGridTransformComponent.PosUpdatedEvent += (IGridTransformPosUpdatedEventReceiver)this;': 'mGridTransformComponent.AddPosUpdatedReceiver(this);',
        'mGridTransformComponent.PosUpdatedEvent -= (IGridTransformPosUpdatedEventReceiver)this;': 'mGridTransformComponent.RemovePosUpdatedReceiver(this);',
        'MeManager.MainPlayer.OnWorldMapProgressUpdate': 'Context.Root.LabelServices.Player.OnWorldMapProgressUpdate',
        'MeManager.MainPlayer != null': 'Context.Root.LabelServices.Player != null',
        'MapRootManagerProxy.CurrentMapClassId': 'Context.CurrentMapClassId',
        'MapRootManagerProxy.MyContainer.ClassId': 'Context.Root.MapClassId',
        'MapRootManagerProxy.AreaAgent': 'Context.Root.ManagedAreaAgent',
        'AreaAgentComponent areaAgent': 'IMiniMapManagedAreaAgent areaAgent',
        'LanguageUtils.GetRegionNameKey': 'MiniMapHudLanguage.GetRegionNameKey',
        'MapInfoManager.Instance.GetMapInfo(classId)?.GetMapGoddessHead()': 'Context.GetMapGoddessHead(classId)',
        'Sprite sprite = Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(text), ref mDisposableBag).Result;': 'var spriteHandle = Context.Root.LoadSprite(text);\n\t\tmDisposableBag.Add(spriteHandle);\n\t\tSprite sprite = spriteHandle.Result;',
        'WindowManager.Instance.Popup((MapNotifyUI go)': 'Context.PopupMapNotify((IMiniMapHudNotify go)',
        'MeManager.MainPlayer.GetWorldMapProgresses': 'Context.Root.GetWorldMapProgresses',
        'Formula.Instance.CalcWorldMapRegionProgress': 'Context.Root.LabelServices.Data.CalcWorldMapRegionProgress',
        'MyRegionInfoText.Format(mRegionTextArg, textKeyArgWrap);': 'FormatRegionInfo(textKeyArgWrap);',
    }
    for old, new in replacements.items():
        area = area.replace(old, new)
    end = area.rfind('}')
    area = area[:end] + '''
    // Supply the local language service without introducing a global singleton.
    private void FormatRegionInfo(MiniMapHudLanguageKey position)
    {
        mRegionTextArg.Formatter = Context.FormatText;
        position.Formatter = Context.FormatText;
        MyRegionInfoText.Format(mRegionTextArg, position);
    }
''' + area[end:]
    install('AreaInfoUI.cs', wrap(area, '''using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using IGridTransformPosUpdatedEventReceiver = Zjcs.Official.EntityTransformViews.IGridTransformPosUpdatedEventReceiver;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;'''))

    branch = original('PublicMapBranchLineBtn')
    branch = branch.replace('[Inject]\n\tprivate IScriptableObjectConfig<GlobalColors> GlobalColors { get; set; }',
        'public MiniMapHudContext Context { get; set; }')
    replacements = {
        'MapPartInfoBase': 'MiniMapHudMapPartInfo',
        'MapInfoManager.Instance.GetMapPartInfo(MeManager.MainMap.Container.ClassId)': 'Context.GetMapPartInfo(Context.CurrentMapClassId)',
        'MeManager.MainPlayer.OnMapStatusUpdate': 'Context.OnPlayerMapStatusUpdate',
        'MeManager.MainPlayer != null': 'Context.HasMainPlayer',
        'MeManager.MainPlayer == null': '!Context.HasMainPlayer',
        'MeManager.MainPlayer.CurrentGames': 'Context.CurrentGames',
        'MeManager.MainPlayer.MapRegionStatusDict': 'Context.MapRegionStatusDict',
        'MeManager.Instance.MapData.OnPublicMapLineStatusChanged': 'Context.OnPublicMapLineStatusChanged',
        'MeManager.Instance.MapData.OnGroupMapIdOrderListChanged': 'Context.OnGroupMapIdOrderListChanged',
        'MeManager.Instance.MapData.TryGetContainerIdByStatus(MapRegionStatus.Public, out var containerId)': 'Context.TryGetPublicContainerId(out var containerId)',
        'MeManager.Instance.MapData.PublicMapLineStatus': 'Context.PublicMapLineStatus',
        'MeManager.Instance.MapData.GetMapLineIndex()': 'Context.GetMapLineIndex()',
        'MeManager.Instance.MapData.GroupMapIdOrderList': 'Context.GroupMapIdOrderList',
        'PlayerFightData': 'MiniMapHudFightData',
        'BroadcastManager.ShowErrText(4131, null, false)': 'Context.ShowError(4131)',
        'WindowManager.Instance.Popup<PublicMapBranchLineDialog>()': 'Context.PopupPublicMapBranchLineDialog()',
        'PublicMapBranchLineDialog.GetChannelUiInfo(GlobalColors.Value,': 'MiniMapHudChannelStyle.GetChannelUiInfo(Context.ChannelColors,',
    }
    for old, new in replacements.items():
        branch = branch.replace(old, new)
    install('PublicMapBranchLineBtn.cs', wrap(branch, '''using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeLabelUI;
using MapRegionStatus = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;'''))
    enum = original('PublicMapLineStatus', 'common-source/Common').replace('namespace Common;', '')
    install('PublicMapLineStatus.cs', wrap(enum))
    dialog = original('PublicMapBranchLineDialog')
    style = member(dialog, 'public static (Color Color, string DescKey) GetChannelUiInfo')
    style = style.replace('GlobalColors globalColors', 'MiniMapHudChannelColors globalColors')
    install('MiniMapHudChannelStyle.cs', 'using UnityEngine;\nnamespace ' + NAMESPACE + '\n{\npublic static class MiniMapHudChannelStyle\n{\n' + style + '\n}\n}\n')
    language = original('LanguageUtils', 'common-source/Common')
    region = member(language, 'public static string GetRegionNameKey')
    constructor = member(language, 'public TextKeyArgWrap(string key, params object[] args)').replace('TextKeyArgWrap', 'MiniMapHudLanguageKey')
    constructor = constructor.replace('Args = args;', 'Args = args;\n\t\t\tFormatter = null;')
    tostring = member(member(language, 'public struct TextKeyArgWrap'), 'public override string ToString()')
    tostring = tostring.replace('LanguageManager.Instance.GetTextFormated', 'Formatter')
    install('MiniMapHudLanguage.cs', 'using System;\nnamespace ' + NAMESPACE + '\n{\npublic static class MiniMapHudLanguage\n{\n' + region + '\n}\npublic struct MiniMapHudLanguageKey\n{\npublic readonly string Key;\npublic object[] Args;\npublic Func<string,object[],string> Formatter;\n' + constructor + '\n' + tostring + '\n}\n}\n')
    install('MiniMapHudContext.cs', '''using System;
using System.Collections.Generic;
using UnityEngine;
using MapRegionStatus = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Local state/window/resource inputs to the original HUD. No channel or
    // offline entity-selection policy is chosen by this service boundary.
    public abstract class MiniMapHudContext
    {
        public abstract MiniMapManagedContext Root { get; }
        public abstract int CurrentMapClassId { get; }
        public abstract string GetMapGoddessHead(int map);
        public abstract string FormatText(string key, object[] args);
        public abstract void PopupFullScreen(Action<IMiniMapHudFullScreenPanel> callback);
        public abstract void PopupMapNotify(Action<IMiniMapHudNotify> callback);
        public abstract MiniMapHudMapPartInfo GetMapPartInfo(int map);
        public abstract bool HasMainPlayer { get; }
        public abstract Dictionary<long,MiniMapHudFightData> CurrentGames { get; }
        public abstract Dictionary<int,List<MapRegionStatus>> MapRegionStatusDict { get; }
        public abstract bool TryGetPublicContainerId(out long id);
        public abstract PublicMapLineStatus PublicMapLineStatus { get; }
        public abstract List<long> GroupMapIdOrderList { get; }
        public abstract int GetMapLineIndex();
        public abstract MiniMapHudChannelColors ChannelColors { get; }
        public abstract event Action<int,int,MapRegionStatus,MapRegionStatus> OnPlayerMapStatusUpdate;
        public abstract event Action<PublicMapLineStatus> OnPublicMapLineStatusChanged;
        public abstract event Action<List<long>> OnGroupMapIdOrderListChanged;
        public abstract void ShowError(int code);
        public abstract void PopupPublicMapBranchLineDialog();
    }
    public interface IMiniMapHudFullScreenPanel { void Init(int map); }
    public interface IMiniMapHudNotify { void AddData(Sprite sprite,string key); }
    public sealed class MiniMapHudMapPartInfo { public int ClassId; public bool SupportPublicRegion; }
    public sealed class MiniMapHudFightData { public long PlayerMapId; }
    public sealed class MiniMapHudChannelColors
    {
        public Color ServiceLoadFree,ServiceLoadBusy,ServiceLoadFull,ServiceLoadOverLoad;
    }
}
''')
    references = ['Zjcs.Official.World.Map13MiniMap', 'Zjcs.Official.World.Map13MiniMapRegions',
        'Zjcs.Official.World.Map13MiniMapMarkManagers',
        'Zjcs.Official.EntityViews', 'Zjcs.Official.World.Map13MiniMapCharacter',
        'Zjcs.Official.World.Map13MiniMapView',
        'Zjcs.Official.World.Map13MiniMapMarks', 'Zjcs.Official.World.Map13MiniMapMasks',
        'Zjcs.Official.World.Map13MiniMapSizing',
        'Zjcs.Official.World.Map13MiniMapMarkSpawn', 'Zjcs.Official.World.Map13MiniMapDescriptions',
        'Zjcs.Official.World.NativeMap13Labels', 'Zjcs.Official.World.NativeLabelUI',
        'Zjcs.Official.World.NativeTerrain', 'Zjcs.Official.EntityLifecycle',
        'Zjcs.Official.EntityTransformViews', 'Zjcs.Official.World.Map13EntityEvents', 'Unity.ugui', 'Unity.TextMeshPro']
    install('Zjcs.Official.World.MainlineMiniMapHud.asmdef', json.dumps(dict(
        name='Zjcs.Official.World.MainlineMiniMapHud', references=references), indent=2))
    folder_meta = Path(str(DEST) + '.meta')
    if not folder_meta.exists():
        identity = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/' + DEST.relative_to(ROOT).as_posix()).hex
        folder_meta.write_text('fileFormatVersion: 2\nguid: ' + identity + '\nfolderAsset: yes\n', encoding='utf8', newline='\n')
    folder_meta.write_bytes(b'\n'.join(line.rstrip(b' \t\r') for line in folder_meta.read_bytes().split(b'\n')))
    outputs.append(dict(path=folder_meta.relative_to(ROOT).as_posix(), sha256=sha(folder_meta.read_bytes())))
    receipt = dict(format=1, manifest='884_156692', inputs=inputs, outputs=outputs,
        operation='Copy the complete MiniMapMaskedUI, AreaInfoUI and PublicMapBranchLineBtn paths. Preserve start/disable/destroy ordering, deferred window initialization, region-position/progress updates, original public-channel visibility/combat rejection and color switch. Replace only local singleton/resource/event/transform ports. Copy the used language-key fields/constructor/formatting and region-key function; provide its formatter per component. Reuse the existing original UI bases, localization components and R3 disposable bag migration.',
        limitations=['Production WorldCanvas placement, complete window contents and map-save wiring remain pending.',
            'Public-channel storage policy is deliberately not supplied.'],
        tool=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha(Path(__file__).read_bytes())))
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
    print('Migrated selected original HUD code:', len(outputs), 'outputs')


if __name__ == '__main__':
    build()
