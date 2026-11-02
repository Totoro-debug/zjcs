"""Migrate the selected original MiniMap root behind the existing local state ports."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, write
from prepare_unity_map14_sources import meta
from prepare_unity_map13_prop_parts import meta as script_meta
from prepare_unity_map13_minimap_mark_core import method


SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/MiniMap.cs'
EXPECTED = '191e08c425fa9390556bb003077b65aa1b0a472b0467fd4ceb207f1af44622d7'
DESTINATION = ROOT / 'unity/Assets/Game/Runtime/Official/World/MainlineMiniMapRoot'
RECEIPT = ROOT / 'assets/reference/official-map/mainline-minimap-root-source-manifest.json'


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def replace_once(text, before, after):
    if text.count(before) != 1:
        raise ValueError('Review the original MiniMap root before adapting: ' + before)
    return text.replace(before, after)


def port_offline_initialization():
    original = SOURCE.parent / 'OfflineMiniMapMarkManager.cs'
    raw = original.read_bytes()
    if digest(raw) != '7ecf5246233275784ad29ed971ce29d739815684e9fba1f51dfb69881105275b':
        raise ValueError('Review the selected original offline minimap marker initialization')
    signature = 'public void Init(int mapClassId, PlayerOfflineMiniMapDataResponse data)'
    body = method(raw.decode('utf8').replace('\r\n', '\n'), signature)
    body = replace_once(body, '\t\tbool enabled;\n', '')
    start = body.index('\t\tif (data.LiteEntityDataDict == null)')
    end = body.index('\n\t\tforeach ', start)
    body = body[:start] + ('\t\tif (data.LiteEntityDataDict == null)\n\t\t{\n'
        '\t\t\tDebug.LogError("OfflineMiniMapMarkManager.Init 离线数据缺少实体字典, mapClassId:" + mapClassId);\n'
        '\t\t\treturn;\n\t\t}') + body[end:]
    start = body.index('\t\t\tif (containerEntityInfo == null)')
    end = body.index('\n\t\t\telse\n', start)
    body = body[:start] + ('\t\t\tif (containerEntityInfo == null)\n\t\t\t{\n'
        '\t\t\t\tDebug.LogWarning("OfflineMiniMapMarkManager.Init 实体在mapinfowrap中不存在, mapClassId:" + mapClassId + " loEntityId:" + item.Key);\n'
        '\t\t\t}') + body[end:]
    replacements = {
        'EntityExtendParameterInfoParser entityExtendParameterInfoParser = Parsers.Instance.EntityExtendParameterInfoParser;':
            'IMiniMapMarkDataQueries entityExtendParameterInfoParser = MarkContext.MarkDataQueries;',
        'entityExtendParameterInfoParser.Get(': 'entityExtendParameterInfoParser.GetEntityExtend(',
        'MapRegionEntityInfoManager.Instance.GetContainerEntityInfo(': 'MarkContext.MarkDataQueries.GetContainerEntityInfo(',
        'MapRegionEntityInfoManager.Instance.GetEntityByMapRegion(': 'MarkContext.ManagedQueries.GetEntityByMapRegion(',
        'List<MapWorldLoEntityInfoWrap> entityByMapRegion': 'IEnumerable<MapWorldLoEntityInfoWrap> entityByMapRegion',
    }
    for before, after in replacements.items():
        body = body.replace(before, after)
    for before, after in {'MapWorldLoEntityInfoWrap': 'MiniMapMarkLoEntityInfo',
            'EntityExtendParameterInfo': 'MiniMapMarkExtendInfo'}.items():
        body = re.sub(r'\b' + before + r'\b', after, body)
    installed = ROOT / 'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarkManagers/OfflineMiniMapMarkManager.cs'
    text = installed.read_text('utf8').replace('\r\n', '\n')
    previous = method(text, signature)
    # This shared type keeps its serialized identity. Only the selected original
    # Init body supersedes the earlier source receipt; the other methods stay.
    write(installed, replace_once(text, previous, body).encode('utf8'))
    return original, installed


def build():
    raw = SOURCE.read_bytes()
    if digest(raw) != EXPECTED:
        raise ValueError('Review the selected original MiniMap root identity')
    text = raw.decode('utf8').replace('\r\n', '\n')
    text = text[text.index('public class MiniMap : MonoBehaviour'):]
    text = replace_once(text, 'public class MiniMap : MonoBehaviour',
        'public abstract class MainlineMiniMapRoot : MiniMapManagedContext, IMiniMapClickContext')
    text = replace_once(text, '\tpublic int MapClassId;',
        '\t[SerializeField, FormerlySerializedAs("MapClassId")]\n\tprivate int mMapClassId;\n'
        '\tpublic override int MapClassId => mMapClassId;')
    text = replace_once(text, '\tpublic bool IsFullScreen;',
        '\t[SerializeField, FormerlySerializedAs("IsFullScreen")]\n\tprivate bool mIsFullScreen;\n'
        '\tpublic override bool IsFullScreen => mIsFullScreen;')
    text = text.replace('IsFullScreen = ', 'mIsFullScreen = ')
    text = text.replace('[Rename(', '[Tooltip(')
    text = replace_once(text,
        '\t[Inject]\n\tprivate IScriptableObjectConfig<MiniMapSettings> mMiniMapSettings { get; set; }',
        '\tprotected abstract MiniMapSettings Settings { get; }\n'
        '\tprotected abstract int MinimapConfigId { get; }\n'
        '\tprotected abstract int MapViewRangeExtraSize { get; }\n'
        '\tprotected abstract MiniMapResourcePointContext ResourcePointContext { get; }')
    text = replace_once(text, '\tpublic MiniMapSettings.MiniMapSetting MiniMapSetting',
        '\tpublic override MiniMapSettings.MiniMapSetting MiniMapSetting')
    start = text.index('\t\t\t\tMapInfo mapInfo = ')
    end = text.index('\n\t\t\t}\n\t\t\treturn mMiniMapSetting;', start)
    text = text[:start] + ('\t\t\t\tmMiniMapSetting = Settings.GetMinimapSetting(MinimapConfigId);\n'
        '\t\t\t\tif (mMiniMapSetting == null)\n\t\t\t\t{\n'
        '\t\t\t\t\tDebug.LogError("MiniMapSettings这个SO资源中缺少MinimapConfigId[" + MinimapConfigId + "]的配置");\n'
        '\t\t\t\t}') + text[end:]
    text = replace_once(text, '\t[Inject]\n\tprivate IServiceProvider ServiceProvider { get; set; }',
        '\tprotected abstract IMiniMapCharacterManager MapCharacters { get; }\n'
        '\tpublic override MiniMapKeepSizeHandler SizingHandler => KeepSizeHandler;\n'
        '\tpublic override GAction ShowFullScreenAction => OnShowFullScreen;\n'
        '\tpublic override GAction HideFullScreenAction => OnHideFullScreen;\n'
        '\tObject IMiniMapClickContext.MyGridVisibilityAgent => VisibilityAgent;')
    text = replace_once(text, '\tpublic IMapViewRangeHandler ViewRangeHandler { get; private set; }',
        '\tprivate IMapViewRangeHandler mViewRangeHandler;\n'
        '\tpublic override IMapViewRangeHandler ViewRangeHandler => mViewRangeHandler;')
    text = text.replace('\t\tViewRangeHandler = ', '\t\tmViewRangeHandler = ')
    text = replace_once(text, '\tpublic event Action<IMapViewRangeHandler> OnVisibleRangeUpdate;',
        '\tpublic override event Action<IMapViewRangeHandler> OnVisibleRangeUpdate;')
    text = replace_once(text, 'ResourcePointManager.Init(this);', 'ResourcePointManager.Init(ResourcePointContext);')
    text = replace_once(text, 'public void FireToCloseFullScreen()', 'public override void FireToCloseFullScreen()')
    text = replace_once(text, 'public bool IsEntityInViewRange(EcEntity ent)',
        'public override bool IsEntityInViewRange(IMiniMapSpawnEntity ent)')
    text = replace_once(text, 'ViewRangeHandler.IsEntityInViewRange(ent.GridTransform)',
        'ViewRangeHandler.IsEntityInViewRange(new EntityBody(ent.GridTransform))')
    text = replace_once(text, 'public bool IsRangeInViewRange(Pos2d minPos, Pos2d maxPos)',
        'public override bool IsRangeInViewRange(Pos2d minPos, Pos2d maxPos)')
    text = replace_once(text, 'CameraUtils.CalcClientViewRange(Character.UiCamera, Parsers.GameSettings.MapViewRangeExtraSize)',
        'CalcClientViewRange(Character.UiCamera, MapViewRangeExtraSize)')
    text = replace_once(text, 'ServiceProvider.CreateInstance<MapCharacterViewRangeHandler>(new object[1] { pos2d })',
        'new MapCharacterViewRangeHandler(MapCharacters, pos2d)')
    footer = '''

    // Original CameraUtils camera overload; the existing native camera module
    // supplies the unchanged orthographic bounds calculation.
    private static Pos2d CalcClientViewRange(Camera camera, int extraRange)
    {
        Bounds bounds = NativeCameraUtils.GetOrthographicCameraViewBounds(camera);
        Vector3 size = bounds.size;
        return new Pos2d((int)Math.Ceiling(size.x / 1f) + extraRange,
            (int)Math.Ceiling(size.y / 1f) + extraRange);
    }

    private sealed class EntityBody : IMiniMapEntityBody
    {
        private readonly IMiniMapMarkGrid grid;
        public EntityBody(IMiniMapMarkGrid grid) { this.grid = grid; }
        public Pos2d Pos => grid.Pos;
        public IEnumerable<MiniMapBodyOffset> BodyRangeInfos
        {
            get
            {
                foreach (var body in grid.Info.BodyRangeInfos)
                    yield return new MiniMapBodyOffset { Pos = body.Pos };
            }
        }
    }
'''
    text = text.rstrip()[:-1] + footer + '}\n'
    prefix = '''// Migrated selected original MiniMap root; see mainline-minimap-root source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
using GAction = Zjcs.Official.StoryPlayables.GAction;
using NativeCameraUtils = Zjcs.Official.World.NativeCamera.CameraUtils;
using Object = UnityEngine.Object;

namespace Zjcs.Official.World.Map13MiniMap
{
'''
    runtime = DESTINATION / 'MainlineMiniMapRoot.cs'
    write(runtime, (prefix + text + '}\n').encode('utf8'))
    script_meta(runtime, 'MonoImporter')
    references = ['Zjcs.Official.World.' + name for name in (
        'Map13MiniMapMarkManagers', 'Map13MiniMapMarkSpawn', 'Map13MiniMapMarkData',
        'Map13MiniMapMarks', 'Map13MiniMapMarkClick', 'Map13MiniMapCharacter',
        'Map13MiniMapNonMaskable', 'Map13MiniMapResourcePoints', 'Map13MiniMapMasks',
        'Map13MiniMapDescriptions', 'Map13MiniMapSizing', 'Map13MiniMapRegions',
        'Map13MiniMapView', 'Map13MiniMapCamera', 'Map13GridVisibility',
        'NativeMap13Labels', 'NativeTerrain', 'NativeCamera')]
    references += ['Zjcs.Official.StoryPlayables', 'Zjcs.Official.WorldUI', 'Cinemachine']
    assembly = DESTINATION / 'Zjcs.Official.World.MainlineMiniMapRoot.asmdef'
    write(assembly, encode(dict(name='Zjcs.Official.World.MainlineMiniMapRoot', references=references, autoReferenced=True)))
    script_meta(assembly, 'AssemblyDefinitionImporter')
    meta(DESTINATION, folder=True)
    offline_original, offline_installed = port_offline_initialization()
    camera = SOURCE.parent / 'CameraUtils.cs'
    if digest(camera.read_bytes()) != 'c221f4c958c75ce978fe4a166c6234b6f9dce26c04985ed56739e19a54edb303':
        raise ValueError('Review the selected camera view-range formula')
    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=digest(path.read_bytes().replace(b'\r\n', b'\n')))
    paths = [runtime, Path(str(runtime) + '.meta'), assembly, Path(str(assembly) + '.meta'),
        Path(str(DESTINATION) + '.meta'), offline_installed, Path(str(offline_installed) + '.meta')]
    write(RECEIPT, encode(dict(format=1, manifest='884_156692',
        inputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes()))
            for p in (SOURCE, camera, offline_original)], outputs=[record(p) for p in paths],
        historical_offline_initialization='assets/reference/official-map/map13-minimap-mark-managers-code-source-manifest.json',
        operation='Copy the complete original MiniMap root lifecycle, range ownership, full-screen ordering, camera positioning, mark lookup and close events. Replace only existing local state/configuration/resource ports, serialized field/property bridges and diagnostics. Preserve the camera view formula and entity body-offset iteration. Supersede only the historical offline marker Init body with the selected original null-data/container/region-list branches; retain its serialized identity and other methods.',
        limitations=['The original root remains an abstract local-state seam until the production map adapter and original prefab are bound. No formal UI, input, map-save or Player completion is asserted.'],
        hashing='Repository text outputs use UTF-8 LF.', tool=record(Path(__file__)))))
    print('Migrated selected original MiniMap root:', runtime.relative_to(ROOT))


if __name__ == '__main__':
    build()
