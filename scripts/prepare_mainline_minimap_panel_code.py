"""Migrate the selected original fullscreen minimap coordinator and tab UI."""
import hashlib
import json
import re
import uuid
from pathlib import Path

from official_apk import ROOT
from prepare_mainline_minimap_hud_code import member

SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/World/MainlineMiniMapPanel'
RECEIPT = ROOT / 'assets/reference/official-map/mainline-minimap-panel-code-source-manifest.json'
NS = 'Zjcs.Official.World.Map13MiniMap'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build():
    inputs, outputs = [], []

    def original(name, folder='client-source'):
        path = SOURCE / folder / (name + '.cs')
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes())))
        return path.read_text('utf8')

    def install(name, source):
        path = DEST / name
        path.parent.mkdir(parents=True, exist_ok=True)
        source = '\n'.join(line.rstrip() for line in source.splitlines()).rstrip() + '\n'
        path.write_text(source, encoding='utf8', newline='\n')
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            identity = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/' + path.relative_to(ROOT).as_posix()).hex
            importer = ('MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n' if path.suffix == '.cs' else 'AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
            meta.write_text('fileFormatVersion: 2\nguid: ' + identity + '\n' + importer, encoding='utf8', newline='\n')
        meta.write_bytes(b'\n'.join(line.rstrip(b' \t\r') for line in meta.read_bytes().split(b'\n')))
        for p in (path, meta):
            outputs.append(dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes())))

    def wrap(source):
        imports = [line for line in source.splitlines() if line.startswith('using ')
            and not any(x in line for x in ('Common;', 'Framework', 'R3;'))]
        body = '\n'.join(line for line in source.splitlines() if not line.startswith('using '))
        return '\n'.join(imports) + '\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.World.NativeLabelUI;\nusing Zjcs.Official.World.NativeMap13Labels;\nusing Zjcs.Official.World.NativeTerrain.Common;\nnamespace ' + NS + '\n{\n' + body + '\n}\n'

    panel = original('MiniMapFullScreenPanel')
    panel = panel.replace('[UIWindow("Map/MiniMap/MiniMapFullScreenPanel")]\n', '')
    panel = re.sub(r'\n\t\[Inject\]\n\tprivate (?:IMapRootManagerProxy MapRootManagerProxy|IWorldToUIManager WorldToUIManager|IResourceLoader ResourceLoader) \{ get; set; \}\n', '\n', panel)
    panel = panel.replace('public class MiniMapFullScreenPanel : MonoBehaviour, LoopScrollDataSource',
        'public class MiniMapFullScreenPanel : MonoBehaviour, LoopScrollDataSource, IMiniMapHudFullScreenPanel\n{CONTEXT}').replace('{CONTEXT}\n{', '{\n\tpublic MiniMapFullScreenContext Context { get; set; }')
    panel = re.sub(r'\bMiniMap\b', 'MainlineMiniMapRoot', panel)
    panel = panel.replace('Map/Map_{0}/MainlineMiniMapRoot/MiniMap_{0}', 'Map/Map_{0}/MiniMap/MiniMap_{0}')
    replacements = {
        'MapInfoManager.Instance.GetMapInfo': 'Context.GetMapInfo',
        'Parsers.Instance.MapInfoParser.GetAll()': 'Context.GetAllMapInfos()',
        'MapInfo[]': 'MiniMapPanelMapInfo[]',
        'MapInfo mapInfo': 'MiniMapPanelMapInfo mapInfo',
        'MeManager.MainPlayer.IsWorldMapVisited': 'Context.IsWorldMapVisited',
        'MapRootManagerProxy.CurrentMapClassId': 'Context.CurrentMapClassId',
        'MapRootManagerProxy.MyContainer.ClassId': 'Context.CurrentMapClassId',
        'MapRootManagerProxy.Minimap': 'Context.CurrentMiniMap',
        'WorldToUIManager.HideAll()': 'Context.HideWorldUI()',
        'WorldToUIManager.ShowAll()': 'Context.ShowWorldUI()',
        'WorldToUIManager != null': 'Context != null',
        'TimeManager.Instance.CalcElapsedSecond': 'Context.CalcElapsedSecond',
        'TimeManager.Instance.GetTick()': 'Context.GetTick()',
        'LanguageUtils.GetMapNameKey': 'MiniMapPanelLanguage.GetMapNameKey',
        'UIUtils.InstantiateAsync': 'Context.InstantiateMiniMapAsync',
        'WindowManager.Instance.Close(((Component)this).gameObject)': 'Context.CloseWindow(gameObject)',
        'DisposableBag mDisposableBag': 'MiniMapSpawnDisposableBag mDisposableBag',
        'Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(mapInfo.MiniMapIcon), ref mDisposableBag).Result': 'LoadTabSprite(mapInfo.MiniMapIcon)',
        'Object.Destroy((Object)(object)((Component)minimap).gameObject)': 'Context.ReleaseMiniMap(minimap)',
        'Object.Destroy((Object)(object)((Component)miniMap).gameObject)': 'Context.ReleaseMiniMap(miniMap)',
        'item.Deconstruct(ref num, ref miniMapSwitchTabItem);': 'num = item.Key;\n\t\t\tminiMapSwitchTabItem = item.Value;',
    }
    for before, after in replacements.items():
        panel = panel.replace(before, after)
    # Interpolated logging handlers are decompiler artifacts; preserve both errors
    # and the original fallback/return branches with the same context values.
    start = panel.index('\t\t\tGLoggerErrorInterpolatedStringHandler message', panel.index('catch (Exception exception)'))
    end = panel.index('\n\t\t\tSwitchMiniMap(', start)
    panel = panel[:start] + '\t\t\tDebug.LogException(new Exception("Minimap switch to mapClassId[" + mapClassId + "] failed", exception));' + panel[end:]
    start = panel.index('\t\t\tGLoggerErrorInterpolatedStringHandler message')
    end = panel.index('\n\t\t}', start)
    panel = panel[:start] + '\t\t\tDebug.LogError("MiniMapFullScreenPanel.OnMiniMapLoaded 加载小地图失败, mapClassId:" + mapClassId);' + panel[end:]
    end = panel.rfind('}')
    panel = panel[:end] + '''
    private Sprite LoadTabSprite(string path)
    {
        var lease = Context.LoadSprite(path);
        mDisposableBag.Add(lease);
        return lease.Result;
    }
''' + panel[end:]
    for singleton in ('MapRootManagerProxy', 'TimeManager.Instance', 'MapInfoManager.Instance', 'WorldToUIManager', 'ResourceLoader', 'GLogger', 'Disposable.AddTo'):
        if singleton in panel:
            raise ValueError('Unadapted original panel dependency: ' + singleton)
    install('MiniMapFullScreenPanel.cs', wrap(panel))
    tab = original('MiniMapSwitchTabItem').replace('LanguageUtils.GetMapNameKey', 'MiniMapPanelLanguage.GetMapNameKey')
    install('MiniMapSwitchTabItem.cs', wrap(tab))
    install('MiniMapPanelTitle.cs', wrap(original('PanelTitle').replace('class PanelTitle', 'class MiniMapPanelTitle')))
    panel_path = DEST / 'MiniMapFullScreenPanel.cs'
    # Keep the original field name while binding its selected localized title.
    normalized = panel_path.read_text('utf8').replace('public PanelTitle PanelTitle;', 'public MiniMapPanelTitle PanelTitle;')
    panel_path.write_text(normalized, encoding='utf8', newline='\n')
    next(row for row in outputs if row['path'] == panel_path.relative_to(ROOT).as_posix())['sha256'] = sha(panel_path.read_bytes())
    language = original('LanguageUtils', 'common-source/Common')
    install('MiniMapPanelLanguage.cs', 'namespace ' + NS + '\n{\npublic static class MiniMapPanelLanguage\n{\n' + member(language, 'public static string GetMapNameKey') + '\n}\n}\n')
    install('MiniMapFullScreenContext.cs', '''using System;
using UnityEngine;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Internal inputs to the original panel. Visited-map and entity policies
    // belong to the owning local map session, never to this window.
    public abstract class MiniMapFullScreenContext
    {
        public abstract int CurrentMapClassId { get; }
        // Assigned by the owning common module before Awake.
        public MainlineMiniMapRoot CurrentMiniMap { get; set; }
        public abstract MiniMapPanelMapInfo[] GetAllMapInfos();
        public abstract MiniMapPanelMapInfo GetMapInfo(int map);
        public abstract bool IsWorldMapVisited(int map);
        public abstract long GetTick();
        public abstract double CalcElapsedSecond(long tick);
        public abstract void HideWorldUI();
        public abstract void ShowWorldUI();
        public abstract void InstantiateMiniMapAsync(string path, Transform parent, Action<MainlineMiniMapRoot> callback);
        public abstract void ReleaseMiniMap(MainlineMiniMapRoot map);
        public abstract IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path);
        public abstract void CloseWindow(GameObject window);
        public abstract void CloseLastWindow();
    }
    [Serializable] public sealed class MiniMapPanelMapInfo
    {
        public int ClassId, MapGroupId, MiniMapSwitchTabGroup;
        public string MiniMapIcon;
    }
}
''')
    install('MiniMapPanelResources.cs','''using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeMap13Labels;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Exact consumed fields of the selected MapInfo/MiniMapMarkInfo tables.
    public sealed class MiniMapPanelResources : ScriptableObject
    {
        public MiniMapPanelMapInfo[] Maps;
        public Mark[] Marks;
        public MonoDictionary<string, Sprite> Icons;
        public Vector2 ReferenceResolution;
        public int ScaleMode, ScreenMatchMode;
        public float ReferencePixelsPerUnit, ScaleFactor, MatchWidthOrHeight;
        [Serializable] public sealed class Mark
        {
            public string Name, Image, Description;
            public bool ShowOnFilterEntry;
            public List<int> ShowOnMapGroupId, ShowOnMapClassId;
            public MiniMapFilterMarkInfo GetInfo() => new MiniMapFilterMarkInfo
            {
                Name=Name, Image=Image, Description=Description, ShowOnFilterEntry=ShowOnFilterEntry,
                ShowOnMapGroupId=ShowOnMapGroupId, ShowOnMapClassId=ShowOnMapClassId
            };
        }
    }
}
''')
    guide = original('MiniDirectionGuiderBase')
    guide = guide.replace('[Inject]\n\tprivate IMapRootManagerProxy MapRootManagerProxy { get; set; }',
        'public MiniMapDirectionContext Context { get; set; }')
    guide = guide.replace('\n\t[Inject]\n\tprivate IMapMyCharacterManagerProxy MapMyCharacterManager { get; set; }\n', '\n')
    for before,after in {
        'IMyMapController':'IMiniMapCharacterController',
        'GridGroundComponent mMyGridGround':'IMiniMapDirectionGround mMyGridGround',
        'MapRootManagerProxy.Map != null':'Context.HasMapRoot',
        'MapMyCharacterManager.MyMeUiCharacter':'Context.Characters.MyMeUiCharacter',
        'MapMyCharacterManager.OnMeMapControllerUpdated':'Context.Characters.OnMeMapControllerUpdated',
        'MapRootManagerProxy.OnInitialized':'Context.OnInitialized',
        'MapRootManagerProxy.Minimap':'Context.FullScreen.CurrentMiniMap',
        'MapRootManagerProxy.GridGround':'Context.Ground',
        'MapRootManagerProxy.CurrentMapClassId':'Context.FullScreen.CurrentMapClassId',
        'GetComponentInParent<SafeArea>()':'GetComponentInParent<Zjcs.Official.WorldUI.WorldSafeArea>()',
        'Vector2.op_Implicit(mCamera.WorldToViewportPoint(worldPos))':'(Vector2)mCamera.WorldToViewportPoint(worldPos)',
        'Vector2.op_Implicit(mCamera.WorldToViewportPoint(((Component)mCamera).transform.position))':'(Vector2)mCamera.WorldToViewportPoint(((Component)mCamera).transform.position)',
        'Vector2.op_Implicit(mCamera.WorldToViewportPoint(position))':'(Vector2)mCamera.WorldToViewportPoint(position)',
    }.items():guide=guide.replace(before,after)
    install('MiniDirectionGuiderBase.cs',wrap(guide))
    player = original('MiniPlayerDirectionGuider')
    for before,after in {
        'GridTransformComponent':'Zjcs.Official.EntityTransformViews.ECGridTransformData',
        'mapController.MyEntity.Entity.GridTransform':'mapController.MyGridTransform.Component',
        'mGridTransformComponent.PosUpdatedEvent += (IGridTransformPosUpdatedEventReceiver)this;':'mGridTransformComponent.AddPosUpdatedReceiver(this);',
        'mGridTransformComponent.PosUpdatedEvent -= (IGridTransformPosUpdatedEventReceiver)this;':'mGridTransformComponent.RemovePosUpdatedReceiver(this);',
        'mapController.WorldPos':'Context.GetWorldPosition(mapController)',
    }.items():player=player.replace(before,after)
    # Do not replace substrings in the original field identifier.
    player=player.replace('mZjcs.Official.EntityTransformViews.ECGridTransformData','mGridTransformComponent')
    player=player.replace('IGridTransformPosUpdatedEventReceiver','Zjcs.Official.EntityTransformViews.IGridTransformPosUpdatedEventReceiver')
    player=player.replace('IEventReceiver','Zjcs.Official.EntityViews.IEventReceiver')
    player=player.replace('mGridTransformComponent.PosUpdatedEvent += (Zjcs.Official.EntityTransformViews.IGridTransformPosUpdatedEventReceiver)this;','mGridTransformComponent.AddPosUpdatedReceiver(this);')
    player=player.replace('mGridTransformComponent.PosUpdatedEvent -= (Zjcs.Official.EntityTransformViews.IGridTransformPosUpdatedEventReceiver)this;','mGridTransformComponent.RemovePosUpdatedReceiver(this);')
    install('MiniPlayerDirectionGuider.cs',wrap(player))
    item=original('MiniMapItemDirectionGuider')
    item=re.sub(r'\n\t\[Inject\]\n\tprivate (?:IItemIconManager ItemIconManager|IDirectionGuiderDataManager DirectionGuiderDataManager) \{ get; set; \}\n','\n',item)
    for before,after in {
        'List<ItemType>':'List<MiniMapDirectionItemType>',
        'ItemInfo mItemInfo':'MiniMapDirectionItemInfo mItemInfo',
        'MeManager.MainPlayer.OnItemCountUpdate':'Context.OnItemCountUpdate',
        'MeManager.MainPlayer != null':'Context.HasMainPlayer',
        'PlayerItemDataWrap itemData':'MiniMapDirectionPlayerItemData itemData',
        'AwardSourceData sourceData':'object sourceData',
        'DirectionGuiderDataManager.TryGetData<MapEntityGuiderData>(DirectionGuiderDataType.MapItemGuiderData, out var obj)':'Context.TryGetItemGuiderData(out var obj)',
        'MapEntityGuiderItemData':'MiniMapDirectionItemData',
        'MapEntityGuiderData':'MiniMapDirectionData',
        'Parsers.Instance.ItemInfoParser.Get':'Context.GetItemInfo',
        'IconSlot.DestroyChildren();':'Context.DestroyChildren(IconSlot);',
        'ItemIconManager.GetIcon(mMapEntityGuiderItemData.ItemId, IconSlot).HideText().HideBottomText();':'Context.CreateItemIconWithoutText(mMapEntityGuiderItemData.ItemId, IconSlot);',
        'DirectionGuiderUtils.GetCenterPos(area,':'DirectionGuiderUtils.GetCenterPos(Context, area,',
    }.items():item=item.replace(before,after)
    start=item.index('\t\t\tGLoggerWarnInterpolatedStringHandler message')
    end=item.index('\n\t\t}',start)
    item=item[:start]+'\t\t\tDebug.LogWarning("MiniMapItemDirectionGuider InitMapSourceGuider error, itemId[" + mMapEntityGuiderItemData.ItemId + "] can not find any RefreshGroup");'+item[end:]
    # Replacements above change type tokens only, never the serialized field names.
    item=item.replace('mMiniMapDirectionItemData','mMapEntityGuiderItemData')
    item=item.replace('ItemIconManager.GetIcon(mMapEntityGuiderItemData.ItemId, IconSlot).HideText().HideBottomText();',
        'Context.CreateItemIconWithoutText(mMapEntityGuiderItemData.ItemId, IconSlot);')
    if 'ItemIconManager' in item:raise ValueError('Unadapted original item-guide icon dependency')
    item=item.replace('ItemIconManager.GetIcon(mMapEntityGuiderItemData.ItemId, IconSlot).HideText().HideBottomText();',
        'Context.CreateItemIconWithoutText(mMapEntityGuiderItemData.ItemId, IconSlot);')
    if 'ItemIconManager.GetIcon' in item: raise ValueError('Unadapted item-icon binding')
    install('MiniMapItemDirectionGuider.cs',wrap(item))
    direction=original('DirectionGuiderUtils')
    geometry=member(direction,'public static Vector2 GetGuiderPosition')
    center=member(direction,'public static Pos2d GetCenterPos(int area, int mapClassId, int itemId)')
    center=center.replace('GetCenterPos(int area,','GetCenterPos(MiniMapDirectionContext context, int area,')
    center=center.replace('Parsers.Instance.MapRootEntityLiteInfoPackParser.TryGetMapLiteData','context.FullScreen.CurrentMiniMap.TryGetMapLiteData')
    center=center.replace('Parsers.Instance.MapRegionInfoParser.GetInfoByDropItemId','context.GetRegionInfoByDropItemId')
    center=center.replace('MapRegionInfo infoByDropItemId','MiniMapRegionInfo infoByDropItemId')
    install('DirectionGuiderUtils.cs',wrap('using UnityEngine;\npublic static class DirectionGuiderUtils\n{\n'+geometry+'\n'+center+'\n}'))
    install('MiniMapDirectionItemType.cs',wrap(original('ItemType','common-source/Common').replace('namespace Common;','').replace('enum ItemType','enum MiniMapDirectionItemType')))
    install('MiniMapDirectionContext.cs','''using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    public interface IMiniMapDirectionGround { Vector3 GridToPos(Pos2d position); }
    public abstract class MiniMapDirectionContext
    {
        public abstract MiniMapFullScreenContext FullScreen { get; }
        public abstract bool HasMapRoot { get; }
        public abstract bool HasMainPlayer { get; }
        public abstract IMiniMapCharacterManager Characters { get; }
        public abstract IMiniMapDirectionGround Ground { get; }
        public abstract event Action OnInitialized;
        public abstract event Action<MiniMapDirectionPlayerItemData,long,object> OnItemCountUpdate;
        public abstract Vector3 GetWorldPosition(IMiniMapCharacterController controller);
        public abstract bool TryGetItemGuiderData(out MiniMapDirectionData data);
        public abstract MiniMapDirectionItemInfo GetItemInfo(int item);
        public abstract MiniMapRegionInfo GetRegionInfoByDropItemId(int item);
        public abstract void DestroyChildren(Transform slot);
        public abstract void CreateItemIconWithoutText(int item, Transform slot);
    }
    public class MiniMapDirectionData { public int MapClassId, Index; public List<int> AreaList; }
    public sealed class MiniMapDirectionItemData : MiniMapDirectionData { public int ItemId; public long RequireCount; }
    public sealed class MiniMapDirectionItemInfo { public int ClassId, AgioItemId; public MiniMapDirectionItemType ItemType; public string Icon; }
    public sealed class MiniMapDirectionPlayerItemData { public MiniMapDirectionItemInfo Info; }
}
''')
    close=original('CloseWindowBtn').replace('class CloseWindowBtn','class MiniMapPanelCloseWindowBtn')
    close=close.replace('public enum CloseWindowType','public MiniMapFullScreenContext Context { get; set; }\n\tpublic enum CloseWindowType')
    close=close.replace('WindowManager.Instance.CloseLast()','Context.CloseLastWindow()')
    close=close.replace('WindowManager.Instance.Close','Context.CloseWindow')
    install('MiniMapPanelCloseWindowBtn.cs',wrap(close).replace('using Object = UnityEngine.Object;', 'using Object = UnityEngine.Object;\nusing Zjcs.Official.World.Map13MiniMapWindows;'))
    click=original('UIClickSound','framework-firstpass-source')
    click=click.replace('using AK.Wwise;\n','')
    click=click.replace('class UIClickSound','class MiniMapPanelClickSound')
    click=click.replace('public Event AkEvent;', 'public AudioClip AkEvent;\n\tpublic System.Action<AudioClip,GameObject> Post;')
    click=click.replace('AkEvent.Post(((Component)this).gameObject);','Post?.Invoke(AkEvent, ((Component)this).gameObject);')
    if 'AK.Wwise' in click:raise ValueError('Review original pointer sound declaration')
    install('MiniMapPanelClickSound.cs',wrap(click))
    refs = ['Unity.ugui','Zjcs.Official.WorldUI','Zjcs.Official.World.MainlineMiniMapRoot',
        'Zjcs.Official.World.MainlineMiniMapHud','Zjcs.Official.World.Map13MiniMapMarkFilter',
        'Zjcs.Official.World.Map13MiniMapMarkSpawn','Zjcs.Official.World.Map13MiniMapNonMaskable',
        'Zjcs.Official.World.Map13MiniMapCamera','Zjcs.Official.World.Map13MiniMapCharacter',
        'Zjcs.Official.World.Map13MiniMapDescriptions','Zjcs.Official.World.Map13MiniMapMarkManagers',
        'Zjcs.Official.World.NativeLabelUI','Zjcs.Official.World.NativeMap13Labels',
        'Zjcs.Official.World.Map13MiniMapView','Zjcs.Official.World.Map13MiniMap',
        'Zjcs.Official.World.Map13MiniMapResourcePoints','Zjcs.Official.World.Map13MiniMapMarkData',
        'Zjcs.Official.World.Map13EntityEvents','Zjcs.Official.EntityLifecycle',
        'Zjcs.Official.EntityTransformViews','Zjcs.Official.World.NativeTerrain']
    refs += ['Zjcs.Official.World.Map13MiniMapWindows','Zjcs.Official.StoryPlayables','Zjcs.Official.EntityViews',
        'Zjcs.Official.World.Map13MiniMapMarks','Zjcs.Official.World.Map13MiniMapMasks',
        'Zjcs.Official.World.Map13MiniMapSizing','Zjcs.Official.World.Map13MiniMapRegions']
    install('Zjcs.Official.World.MainlineMiniMapPanel.asmdef', json.dumps(dict(name='Zjcs.Official.World.MainlineMiniMapPanel', references=refs), indent=2))
    meta = Path(str(DEST) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/' + DEST.relative_to(ROOT).as_posix()).hex + '\nfolderAsset: yes\n', encoding='utf8', newline='\n')
    outputs.append(dict(path=meta.relative_to(ROOT).as_posix(),sha256=sha(meta.read_bytes())))
    tools = [Path(__file__), ROOT / 'scripts/prepare_mainline_minimap_hud_code.py']
    RECEIPT.write_text(json.dumps(dict(format=1,manifest='884_156692',inputs=inputs,outputs=outputs,
        tools=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in tools],
        operation='Copy full original fullscreen panel and switch tab. Preserve visited/group enumeration, cooldown, request-version rejection, title/selection notifications, current-map reactivation, camera/slider synchronization, filter refresh and disable/destroy ordering. Replace singleton/resource/window/clock ports and decompiler logging machinery only.',
        limitations=['Formal map session state and window hosting are supplied separately. No offline entity-selection or public-channel policy is introduced.']), ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
    print('Migrated original fullscreen minimap panel:',len(outputs),'outputs')


if __name__ == '__main__':
    build()
