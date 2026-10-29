"""Migrate complete original minimap marker UI/data/dialog classes and ports."""
import re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,write
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map13_minimap_sources import record

SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code/client-source'
DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarks'


def method(text,signature):
    start=text.index(signature);opening=text.index('{',start);depth=1;end=opening+1
    while depth:
        if text[end]=='{':depth+=1
        elif text[end]=='}':depth-=1
        end+=1
    return text[start:end]


def build():
    inputs=[];outputs=[]
    def read(p):inputs.append(record(p));return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    def emit(name,text):
        p=DEST/name;write(p,text.encode());meta(p,'MonoImporter' if p.suffix=='.cs' else 'AssemblyDefinitionImporter');outputs.append(record(p))
    def wrap(text):
        for ns in ['Common','Framework','Framework.DependencyInjection','R3']:text=text.replace('using '+ns+';\n','')
        text=re.sub(r'\bMiniMap(?=\s+\w+\s*[,;{)])','MiniMapMarkContext',text)
        at=text.index('public ');attributes=text.rfind('\n[',0,at)
        if attributes>=0:at=attributes+1
        if text.startswith('['):at=0
        return text[:at]+'''namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
'''+text[at:]+'\n}\n'
    for name in ['MiniMapMarkType','MiniMapMarkDataType','MiniMapMarkSupportBase','IMiniMapMark','MiniMapOnlyMarkUI','IMiniMapWayPointBlockHandler','MapEntityDialogIconLiteEntityData']:
        emit(name+'.cs',wrap(read(SOURCE/(name+'.cs'))))
    text=read(SOURCE/'MiniMapMarkBase.cs');start=text.index('\tpublic Vector3 LocalPosition');end=text.index('\n\tpublic abstract bool EntityActive',start)
    text=text[:start]+'\tpublic Vector3 LocalPosition { get; protected set; }\n'+text[end:]
    emit('MiniMapMarkBase.cs',wrap(text.replace('LanguageUtils.TextKeyArgWrap','MiniMapMarkTextKeyArg')))
    emit('MiniMapCustomMark.cs',wrap(read(SOURCE/'MiniMapCustomMark.cs').replace('LanguageUtils.TextKeyArgWrap','MiniMapMarkTextKeyArg')))
    emit('MiniMapMarkUIBase.cs',wrap(read(SOURCE/'MiniMapMarkUIBase.cs')))
    text=read(SOURCE/'IMapEntityDialogIconHandler.cs').replace('Init(EcEntity ent)','Init(object ent)');emit('IMapEntityDialogIconHandler.cs',wrap(text))
    for name in ['MiniMapDisplayMarkUI','MiniMapWayPointMarkUI','MiniMapEntryMarkUI']:
        text=read(SOURCE/(name+'.cs')).replace('WindowManager.Instance.Popup<','MiniMap.Popup<').replace('BroadcastManager.ShowErrText(2116, null, false)','MiniMap.ShowError(2116)');emit(name+'.cs',wrap(text))
    text=read(SOURCE/'MapDialogIconPreviewSlot.cs').replace('UIUtils.Instantiate(prefab, mPreviewSlot)','MiniMapMarkUIUtils.Instantiate(prefab, mPreviewSlot, MiniMap)')
    text=text.replace('\tpublic Transform PreviewSlot => mPreviewSlot;','\tpublic Transform PreviewSlot => mPreviewSlot;\n\n\tpublic MiniMapMarkContext MiniMap { get; set; }');emit('MapDialogIconPreviewSlot.cs',wrap(text))
    text=read(SOURCE/'MiniMapMarkDialogBase.cs').replace('private DisposableBag mDisposableBag;','private MiniMapMarkDisposableBag mDisposableBag;')
    text=text.replace('\t[Inject]\n\tprivate IResourceLoader ResourceLoader { get; set; }','\tprivate MiniMapMarkContext ResourceLoader => miniMapMark.MiniMap;')
    text=text.replace('LanguageUtils.TextKeyArgWrap','MiniMapMarkTextKeyArg').replace('Disposable.AddTo<AssetOperationHandle<GameObject>>','MiniMapMarkDisposable.AddTo<IMiniMapDescriptionAssetLease<GameObject>>').replace('Disposable.AddTo<AssetOperationHandle<Sprite>>','MiniMapMarkDisposable.AddTo<IMiniMapDescriptionAssetLease<Sprite>>')
    text=text.replace('myIconSlot.PreviewSlot.DestroyChildren();','MiniMapMarkUIUtils.DestroyChildren(myIconSlot.PreviewSlot);\n\t\t\tmyIconSlot.MiniMap = miniMapMark.MiniMap;')
    text=text.replace('Parsers.Instance.MapAreaInfoParser.Get(mapClassId, MapUtils.GridToArea(mapClassId, miniMapMark.GridPos)).AreaTag','miniMapMark.MiniMap.GetAreaTag(mapClassId, miniMapMark.GridPos)').replace('MapUIDisplayInfo mapUIDisplayInfo = Parsers.Instance.MapUIDisplayInfoParser.Get(mapClassId, areaTag)','MiniMapMarkDisplayInfo mapUIDisplayInfo = miniMapMark.MiniMap.GetMapUIDisplay(mapClassId, areaTag)')
    text=text.replace('WindowManager.Instance.Close(((Component)this).gameObject)','miniMapMark.MiniMap.CloseWindow(((Component)this).gameObject)');emit('MiniMapMarkDialogBase.cs',wrap(text))
    for name in ['MiniMapDisplayMarkDialog','MiniMapWayPointMarkDialog','MiniMapEntryMarkDialog']:
        text=read(SOURCE/(name+'.cs')).replace('[UIWindow(','[MiniMapWindow(')
        text=text.replace('\t[Inject]\n\tprivate IMapRootManagerProxy MapRootManagerProxy { get; set; }','\tprivate MiniMapMarkContext MapRootManagerProxy => miniMapMark.MiniMap;')
        text=text.replace('MapRootManagerProxy.GridGround.GridToPos(mark.GridPos)','MapRootManagerProxy.GridToWorld(MapRootManagerProxy.CurrentMapClassId, mark.GridPos)').replace('(Object)(object)MapRootManagerProxy.MapGroundRoot != (Object)null && (Object)(object)MapRootManagerProxy.MapGroundRoot.PartitionController != (Object)null','MapRootManagerProxy.HasMapGroundRoot && MapRootManagerProxy.HasPartitionController').replace('MapRootManagerProxy.MapGroundRoot.PartitionController.Reserve(worldPosition)','MapRootManagerProxy.ReservePartition(worldPosition)');emit(name+'.cs',wrap(text))
    text=read(SOURCE/'MiniMapFunctionMarkDialog.cs').replace('\t[Inject]\n\tprivate IMapCharacterActionGroupHandle MapCharacterActionGroupHandle { get; set; }','\tprivate IMiniMapCharacterActionGroupHandle MapCharacterActionGroupHandle => miniMapMark.MiniMap.CharacterActionGroup;')
    emit('MiniMapFunctionMarkDialog.cs',wrap(text))
    language=read(SOURCE.parent/'common-source/Common/LanguageUtils.cs');text=method(language,'\tpublic struct TextKeyArgWrap').replace('TextKeyArgWrap','MiniMapMarkTextKeyArg').replace('Singleton<MiniMapMarkTextKeyArg>.Create(() => new MiniMapMarkTextKeyArg(""))','new MiniMapMarkTextKeyArg("")').replace('LanguageManager.Instance.GetTextFormated(Key, Args)','MiniMapMarkLanguage.Format(Key, Args)')
    emit('MiniMapMarkTextKeyArg.cs','using System;\nnamespace Zjcs.Official.World.Map13MiniMap\n{\n'+text+'\n}\n')
    ui=read(SOURCE/'UIUtils.cs');transform=read(SOURCE.parent/'framework-source/Framework/TransformExtensions.cs');reset=method(ui,'\tpublic static void ResetUI(');destroy=method(transform,'\tpublic static void DestroyChildren(')
    instantiate=method(ui,'\tpublic static GameObject Instantiate(GameObject prefab, Transform parentTs,').replace('EUIInstantiateFlag flag = EUIInstantiateFlag.All','MiniMapMarkContext miniMap, EUIInstantiateFlag flag = EUIInstantiateFlag.All').replace('ServiceProviderExtensions.Instantiate(((Component)(object)parentTs).GetSceneContainer(), prefab, parentTs, false)','miniMap.InstantiateDisplay(prefab, parentTs)')
    emit('MiniMapMarkUIUtils.cs','using UnityEngine;\nnamespace Zjcs.Official.World.Map13MiniMap\n{\nusing Object=UnityEngine.Object;\npublic static class MiniMapMarkUIUtils\n{\n'+reset+'\n'+instantiate+'\n'+destroy+'\n}\n}\n');emit('EUIInstantiateFlag.cs',wrap(read(SOURCE/'EUIInstantiateFlag.cs')))
    emit('MiniMapMarkContext.cs',CONTEXT)
    emit('Zjcs.Official.World.Map13MiniMapMarks.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapMarks',references=['Zjcs.Official.World.Map13MiniMapCharacter','Zjcs.Official.World.Map13MiniMapMasks','Zjcs.Official.World.Map13MiniMapDescriptions','Zjcs.Official.World.Map13MiniMapSizing','Zjcs.Official.World.Map13MiniMapRegions','Zjcs.Official.World.Map13MiniMapView','Zjcs.Official.World.Map13MiniMapCamera','Zjcs.Official.World.NativeTerrain','Zjcs.Official.World.NativeMap13Labels','Zjcs.Official.World.NativeLabelUI','Zjcs.Official.ProfessionUI','Zjcs.Official.StoryPlayables','UniTask','Unity.ugui'],autoReferenced=True)).decode());meta(DEST,folder=True)
    write(ROOT/'assets/reference/official-map/map13-minimap-mark-core-code-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest='871_156598',inputs=inputs,outputs=outputs,generator=record(Path(__file__).resolve()),operation='Copy complete original marker base/custom data, UI base/support/pool surface, four marker UI types, three dialog subclasses plus full dialog base/function behavior, icon slot/DTO/interface, original text-key value equality/hash/validity, UI reset/instantiate/child-destruction methods and flags. Preserve all branches, support order, event order, waypoint 2116 condition, nullable action-group path, button subscriptions, click/close/fullscreen sequence, icon/background lookup and asset lifetime. Adapt only injected map/resource/window/action/entity boundaries, namespaces, database-independent metadata and decompiler backing-property syntax. Original entity references pass intact through object-based icon bridge; no fabricated entity/state.',limitations=['Native marker/dialog prefab bindings, full live/offline entity data/spawn/query/click managers, actual root/local ports, Play Mode validation, formal map input/save and Windows Player remain required.'])))
    print('Original minimap mark data/UI/dialog core:',len(outputs),'files')


CONTEXT='''using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
namespace Zjcs.Official.World.Map13MiniMap
{
    public sealed class MiniMapMarkDisplayInfo { public string MapBgPath; }
    public interface IMiniMapCharacterActionGroupHandle { UniTask ConfirmInterruptAndContinueAsync(Action callback); }
    [AttributeUsage(AttributeTargets.Class,Inherited=true)]
    public sealed class MiniMapWindowAttribute:Attribute { public string Path { get; } public MiniMapWindowAttribute(string path){Path=path;} }
    public abstract class MiniMapMarkContext:MiniMapCharacterContext
    {
        public abstract IMiniMapCharacterActionGroupHandle CharacterActionGroup { get; }
        public abstract bool HasMapGroundRoot { get; }
        public abstract bool HasPartitionController { get; }
        public abstract void ReservePartition(Vector3 position);
        public abstract string GetAreaTag(int map,Zjcs.Official.World.NativeTerrain.Common.Pos2d position);
        public abstract MiniMapMarkDisplayInfo GetMapUIDisplay(int map,string areaTag);
        public abstract IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path);
        public abstract void Popup(Type dialogType,Action<MiniMapMarkDialogBase> callback);
        public void Popup<T>(Action<MiniMapMarkDialogBase> callback) where T:MiniMapMarkDialogBase=>Popup(typeof(T),callback);
        public abstract void CloseWindow(GameObject window);
        public abstract void ShowError(int code);
        public abstract void FireToCloseFullScreen();
    }
    public static class MiniMapMarkLanguage
    {
        public static Func<string,object[],string> Formatter { get; set; }
        public static string Format(string key,object[] args)=>(Formatter??throw new InvalidOperationException("Original marker language service is unbound."))(key,args);
    }
    internal struct MiniMapMarkDisposableBag:IDisposable
    {
        private List<IDisposable> values;
        public void Add(IDisposable value){if(values==null)values=new List<IDisposable>();values.Add(value);}
        public void Dispose(){if(values==null)return;foreach(var value in values)value.Dispose();values.Clear();}
    }
    internal static class MiniMapMarkDisposable
    {
        public static T AddTo<T>(T value,ref MiniMapMarkDisposableBag bag) where T:IDisposable { bag.Add(value);return value; }
    }
}
'''


if __name__=='__main__':build()
