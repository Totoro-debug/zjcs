"""Stage the original offline-minimap coordinator and its native fog integration fixture."""
import argparse,json,re,shutil,subprocess
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from official_apk import ROOT
from validation_workspace import validation_directory
from prepare_partner_star_god_validation import copy_modules


def main():
    parser=argparse.ArgumentParser(__doc__)
    parser.add_argument('--refresh',action='store_true',help='Refresh this owned, stopped validation project while retaining its import cache')
    args=parser.parse_args()
    work=validation_directory()
    if not (work/'.zjcs-validation.json').is_file():raise ValueError('Owned validation workspace required')
    project=work/'minimap-unity'
    if project.exists() and not args.refresh:raise ValueError('Preserve existing minimap validation project')
    source=ROOT/'unity';runtime=source/'Assets/Game/Runtime'
    # This native rendering fixture deliberately reuses the installed Map13 source,
    # while the coordinated request and dispatch code comes from selected 884.
    x=json.loads((source/'Assets/Official/World/Map/13/map13.json').read_bytes());n=x['Navigation']
    regions=[json.loads(row['Json']) for row in x['Configs'] if row['TableName']=='TbMapRegionInfo']
    if n['Width']!=240 or n['Height']!=240 or len(regions)!=106:raise ValueError('Review installed Map13 fixture identity')
    fixture_data=dict(width=n['Width'],height=n['Height'],origin=[n['Origin'][a] for a in 'XYZ'],cells=[dict(area=c['RawValues'][0],ground=c['RawValues'][1]) for row in n['Rows'] for c in row['Cells']],areas=[dict(x=a['Cell'][0],y=a['Cell'][1],count=a['Value']) for a in n['AreaMappings']],regions=regions)
    if len(fixture_data['cells'])!=57600:raise ValueError('Incomplete original navigation fixture')
    (work/'minimap-description-fixture.json').write_text(json.dumps(fixture_data,separators=(',',':')),'utf8')
    for map_id,expected in {10:(60,60,5),11:(240,180,77),12:(280,210,99),14:(280,280,111),16:(140,120,43)}.items():
     installed=json.loads((source/f'Assets/Official/World/Map/{map_id}/map{map_id}.json').read_bytes());nav=installed['Navigation']
     map_regions=[json.loads(row['Json']) for row in installed['Configs'] if row['TableName']=='TbMapRegionInfo']
     info,=[json.loads(row['Json']) for row in installed['Configs'] if row['TableName']=='TbMapInfo']
     if (nav['Width'],nav['Height'],len(map_regions))!=expected or info['ClassId']!=map_id:raise ValueError('Review installed map fixture identity: '+str(map_id))
     map_data=dict(mapClassId=map_id,configId=info['MinimapConfigId'],width=nav['Width'],height=nav['Height'],origin=[nav['Origin'][a] for a in 'XYZ'],cells=[dict(area=c['RawValues'][0],ground=c['RawValues'][1]) for row in nav['Rows'] for c in row['Cells']],areas=[dict(x=a['Cell'][0],y=a['Cell'][1],count=a['Value']) for a in nav['AreaMappings']],regions=map_regions)
     if len(map_data['cells'])!=expected[0]*expected[1]:raise ValueError('Incomplete original navigation fixture: '+str(map_id))
     (work/f'minimap-description-fixture-{map_id}.json').write_text(json.dumps(map_data,separators=(',',':')),'utf8')
    modules={json.loads(p.read_bytes())['name']:p for p in runtime.rglob('*.asmdef')};selected=set()
    def stage_modules(pending):
     added=set()
     while pending:
      n=pending.pop()
      if n in selected or n not in modules:continue
      selected.add(n);added.add(n);pending.extend(json.loads(modules[n].read_bytes()).get('references',[]))
     copy_modules(project,runtime,modules,added)
    fixture=source/'Assets/Game/Editor/Map13MiniMap';stage_modules(json.loads((fixture/'Zjcs.Editor.Map13.MiniMap.asmdef').read_bytes())['references']);shutil.copytree(fixture,project/'Assets/Game/Editor/Map13MiniMap',dirs_exist_ok=args.refresh)
    files=subprocess.check_output(['git','ls-files','-co','--exclude-standard','-z','--','unity/Assets'],cwd=ROOT).split(b'\0')
    metas=[ROOT/n.decode() for n in set(files) if n.endswith(b'.meta')]
    def identity(meta):
     p=Path(str(meta)[:-5]);m=re.search(rb'(?m)^guid: ([a-f0-9]{32})',meta.read_bytes());return (m[1],p) if m and p.is_file() else None
    with ThreadPoolExecutor(max_workers=16) as pool:index=dict(x for x in pool.map(identity,metas) if x)
    package_guids=set()
    for folder in (source/'Packages',source/'Library/PackageCache'):
     for package in folder.iterdir():
      if package.is_dir() and package.name.startswith(('com.unity.textmeshpro','com.unity.ugui','com.unity.render-pipelines.','com.unity.shadergraph','com.unity.cinemachine')):
       for meta in package.rglob('*.meta'):
        m=re.search(rb'(?m)^guid: ([a-f0-9]{32})',meta.read_bytes())
        if m:package_guids.add(m[1])
    base=source/'Assets/Official/World/Map/13'
    shared_hud=source/'Assets/Official/World/Shared/NativeMiniMapHudCurrent'
    if not shared_hud.is_dir():raise ValueError('Prepare the complete selected minimap HUD')
    pending=[p for name in ['NativeMiniMapRegions','NativeMiniMapSettings','NativeMiniMapMasks','NativeMiniMapDescriptions','NativeMiniMapCurrent','NativeMiniMapRootCurrent'] for p in (base/name).rglob('*') if p.is_file() and p.suffix!='.meta']
    pending += [p for name in ['NativeMiniMapCurrent','NativeMiniMapRootCurrent'] for p in (source/'Assets/Official/World/Map/16'/name).rglob('*') if p.is_file() and p.suffix!='.meta']
    pending += [p for p in shared_hud.rglob('*') if p.is_file() and p.suffix!='.meta']
    shared_panel=source/'Assets/Official/World/Shared/NativeMiniMapPanelCurrent'
    if not shared_panel.is_dir():raise ValueError('Prepare the complete selected fullscreen minimap window')
    pending += [p for p in shared_panel.rglob('*') if p.is_file() and p.suffix!='.meta']
    pending.append(source/'Assets/Game/Resources/Map13/Native/minimap-resource-point-assets.asset')
    pending.append(source/'Assets/Game/Resources/Map14/Native/shared-assembly-catalog.asset')
    pending.extend((source/'Assets/Official/World/Map/14/SharedSerialization').glob('*.dll'))
    for folder in ('SharedConfig', 'SharedRoot', 'SharedSerialization'):
     pending.extend(p for p in (source/'Assets/Official/World/Map/14'/folder).rglob('*') if p.is_file() and p.suffix!='.meta')
    pending.extend(p for p in (source/'Assets/Official/World/Shared/SharedWorldDataCurrent').rglob('*') if p.is_file() and p.suffix!='.meta')
    pending.append(source/'Assets/Official/World/Map/15/map15.json')
    pending.extend(source/f'Assets/Official/World/Map/{mid}/NativeMiniMapResourcesCurrent/Resources/Map{mid}/Native/current-resource-point-assets.asset' for mid in (10,11,12,13,14,16))
    current_markers=source/'Assets/Official/World/Shared/NativeMiniMapMarkersCurrent/Resources/MainlineMiniMap/current-marker-assets.asset'
    if current_markers.is_file():
     pending.append(current_markers)
     pending.append(source/'Assets/Game/Resources/MainlineMiniMap/current-marker-data.json')
     pending.append(source/'Assets/Game/Resources/MainlineMiniMap/current-region-displays.json')
     pending.append(source/'Assets/Game/Resources/MainlineMiniMap/current-entity-data.json')
     pending.extend(source/f'Assets/Official/World/Map/{mid}/map{mid}.json' for mid in (10,11,12,13,14,16))
    shared_renderer=source/'Assets/Official/World/Shared/NativeMiniMapRendererCurrent'
    if not shared_renderer.is_dir():raise ValueError('Prepare the complete selected shared minimap renderer')
    pending += [p for p in shared_renderer.rglob('*') if p.is_file() and p.suffix!='.meta']
    for map_id in (10,11,12,14):
     native=source/f'Assets/Official/World/Map/{map_id}/NativeMiniMapRootCurrent'
     if not native.is_dir():raise ValueError('Prepare the complete selected minimap root: '+str(map_id))
     pending += [p for p in native.rglob('*') if p.is_file() and p.suffix!='.meta']
    pending += [base/'NativePipeline/UniversalRenderPipeline.asset',base/'NativeText/FZY4K-Dynamic.asset',base/'Resources/Map13/Native/region-label-data.json',source/'Assets/Game/Resources/Map13/Native/combat-rating-config.asset',base/'Resources/Map13/grid-visibility.json',base/'Resources/Map13/grid-entities.json',source/'Assets/TextMesh Pro/Resources/TMP Settings.asset']
    seen=set()
    while pending:
     p=pending.pop()
     if p in seen:continue
     seen.add(p)
     if p.suffix in ('.cs','.asmdef'):
      target=project/p.relative_to(source)
      if not target.is_file():
       folder=p.parent
       while folder!=runtime and not list(folder.glob('*.asmdef')) and not list(folder.glob('*.asmref')):folder=folder.parent
       definitions=list(folder.glob('*.asmdef'))
       if not definitions:raise ValueError('Unstaged source assembly '+str(p))
       stage_modules([json.loads(definitions[0].read_bytes())['name']])
      continue
     for f in (p,Path(str(p)+'.meta')):
      target=project/f.relative_to(source);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,target)
     raw=p.read_bytes()
     if p.suffix in ('.shader','.cginc'):
      for n in re.findall(rb'#include\s+"([^"\r\n]+)"',raw):
       include=p.parent/n.decode()
       if include.is_file():pending.append(include)
     if raw.startswith(b'%YAML'):
      for guid in set(re.findall(rb'guid: ([a-f0-9]{32})',raw)):
       if guid.startswith(b'000000') or guid in package_guids:continue
       if guid not in index:raise ValueError('Unbound native asset '+guid.decode()+' '+str(p))
       pending.append(index[guid])
    external_refs={ref for name in selected for ref in json.loads(modules[name].read_bytes()).get('references',[])}
    if external_refs & {'spine-unity','spine-csharp'}:
     # Existing original-playable dependencies include the installed Spine ABI,
     # which lives outside Game/Runtime and is not found by stage_modules.
     shutil.copytree(source/'Assets/Spine/Runtime',project/'Assets/Spine/Runtime',dirs_exist_ok=args.refresh)
     for relative in ('Assets/Spine.meta','Assets/Spine/Runtime.meta'):
      shutil.copy2(source/relative,project/relative)
    shutil.copytree(source/'Packages',project/'Packages',dirs_exist_ok=args.refresh)
    shutil.copytree(source/'Assets/StreamingAssets/Map13/NativeText',project/'Assets/StreamingAssets/Map13/NativeText',dirs_exist_ok=args.refresh)
    settings=project/'ProjectSettings';settings.mkdir(exist_ok=args.refresh)
    for n in ['ProjectVersion.txt','ProjectSettings.asset','TagManager.asset']:shutil.copy2(source/'ProjectSettings'/n,settings/n)
    print('Prepared isolated actual minimap coordinator:',len(selected),'assemblies,',len(seen),'original assets',flush=True)


if __name__ == "__main__":
    main()
