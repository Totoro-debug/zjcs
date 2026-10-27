"""Record the original Map12 world binding and APK rendering settings.

Restores no invented scene data: Unity's NativeWorldTools binds the four
verified native catalogs. This receipt checks their sources and local bridge.
"""
from pathlib import Path
import re
import yaml
from official_apk import Apk, ROOT, UnityPy
from prepare_unity_map12_sources import encode, sha, source_path, write


def build():
    apk = Apk.current()
    entry = 'assets/bin/Data/data.unity3d'
    raw = apk.archive.read(entry)
    objects = list(UnityPy.load(raw).objects)
    players = [o for o in objects if o.type.name == 'PlayerSettings']
    if len(players) != 1:
        raise ValueError('APK PlayerSettings object must be unique')
    player = players[0]
    tree = player.read_typetree()
    if tree['m_ActiveColorSpace'] != 1 or tree['m_ColorGamuts'] != [0]:
        raise ValueError('Official rendering settings changed')
    settings = (ROOT / 'unity/ProjectSettings/ProjectSettings.asset').read_text(encoding='utf8')
    if re.findall(r'^  m_ActiveColorSpace: (\d+)$', settings, re.M) != ['1']:
        raise ValueError('Unity project does not preserve original Linear color space')
    tags = next(o for o in objects if o.type.name == 'TagManager')
    original_layers = tags.read_typetree()['m_SortingLayers']
    tag_text = (ROOT / 'unity/ProjectSettings/TagManager.asset').read_text(encoding='utf8')
    local_layers = yaml.safe_load('\n'.join(line for line in tag_text.splitlines()
                                 if not line.startswith(('%', '---'))))['TagManager']['m_SortingLayers']
    if [(v['name'], v['uniqueID']) for v in local_layers] != [(v['name'], v['uniqueID']) for v in original_layers]:
        raise ValueError('Original ordered sorting layer IDs were not preserved')
    source_root = ROOT / '.tools/source-packages/current/871_156598/official-code'
    sources = [source_root / 'client-source' / (n + '.cs') for n in
               ('PartitionController', 'PartitionLoader', 'MapEnvEffect', 'MapRegionLight', 'MapMyEntityUtils',
                'MapSpineAttachmentMeshBackerRenderer', 'FollowSceneActiveSelfController')]
    receipts = [ROOT / 'assets/reference/official-map' / name for name in (
        'map12-native-terrain-binding-source-manifest.json',
        'map12-native-scene-binding-source-manifest.json',
        'map12-native-environment-scene-binding-source-manifest.json',
        'map12-native-region-labels-binding-source-manifest.json',
        'map12-shared-environment-source-manifest.json',
        'map13-partition-code-source-manifest.json',
        'map13-environment-shader-source-manifest.json')]
    game = ROOT / 'unity/Assets/Game'
    paths = sorted((game / 'Runtime/Presentation/World/NativeMap12').rglob('*'))
    paths += [game / name for name in (
        'Runtime/Presentation/World/NativeMap12.meta',
        'Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.NativeMap12.cs',
        'Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.NativeMap12.cs.meta',
        'Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs',
        'Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.PlayerView.cs',
        'Runtime/Presentation/World/Mainline/OfficialMainlineStatuesModule.Goddess.cs',
        'Runtime/Presentation/World/OfficialMap10EntityView.cs',
        'Runtime/Presentation/World/OfficialMap10MonsterView.cs',
        'Runtime/Presentation/World/Zjcs.Presentation.World.asmdef',
        'Editor/Map12/OfficialMap12NativeWorldTools.cs',
        'Editor/Map12/OfficialMap12NativeWorldTools.cs.meta',
        'Editor/Map12/OfficialMap12NativeWorldAcceptance.cs',
        'Editor/Map12/OfficialMap12NativeWorldAcceptance.cs.meta',
        'Editor/Map12/Zjcs.Editor.Map12.asmdef',
        'Editor/OfficialMap12PlayModeAcceptance.NativeWorld.cs',
        'Editor/OfficialMap12PlayModeAcceptance.NativeWorld.cs.meta',
        'Editor/OfficialMap12PlayModeAcceptance.cs',
        'Editor/OfficialMap12PlayModeAcceptance.Runtime.cs',
        'Resources/Map12/Native/world-assets.asset',
        'Resources/Map12/Native/world-assets.asset.meta')]
    paths += [ROOT / name for name in (
        'unity/Assets/Official/World/Map/12/Resources/Map12/BaseGround/player-view.json',
        'unity/Assets/Official/World/Map/12/base-ground-source-manifest.json',
        'scripts/prepare_unity_map11_base_ground.py',
        'scripts/prepare_unity_map13_environment_shader.py')]
    def record(p):
        return dict(path=source_path(p), sha256=sha(p.read_bytes()))
    write(ROOT / 'assets/reference/official-map/map12-native-world-source-manifest.json', encode(dict(
        manifest='871_156598', generator=record(Path(__file__)),
        apk=dict(path=source_path(apk.source.apk), sha256=apk.source.apk_sha256, entry=entry,
                 entrySha256=sha(raw), objectPathId=player.path_id,
                 objectSha256=sha(player.get_raw_data())),
        originalPlayerSettings=dict(m_ActiveColorSpace=1, m_ColorGamuts=[0]),
        originalSortingLayers=dict(objectPathId=tags.path_id, objectSha256=sha(tags.get_raw_data()),
                                   layers=original_layers),
        sources=[record(p) for p in sources], sourceReceipts=[record(p) for p in receipts],
        bindings=[record(p) for p in paths if p.is_file()],
        resource='Map12/Native/world-assets', pipelineObject='globalgamemanagers.assets:22209',
        adaptations=['Use local profile level, subrank, combat rating, raw region progress and saved goddess activation.',
                     'Keep original unfiltered PartitionData and loader camera/region callbacks.',
                     'Clone original dynamic fonts before Awake; restore previous pipeline when leaving the map.',
                     'Restore converted entity layer/order from original separate values; world character attachments use original Object layer.',
                     'Keep Map12 in the active Unity scene so original FollowSceneActiveSelfController controls its global light.',
                     'Release only registered partition assets through original Remove on local map destruction.'],
        limitations=['Full map progression, original camera behavior and complete UI/animation equivalence remain separate.'])))
    apk.archive.close()
    print('Recorded Map12 native world bindings and original APK Linear color space')


if __name__ == '__main__':
    build()
