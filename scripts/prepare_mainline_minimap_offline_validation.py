"""Stage the original offline-minimap coordinator and its native fog integration fixture."""
import json,re,shutil,subprocess
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from official_apk import ROOT
from validation_workspace import validation_directory
from prepare_partner_star_god_validation import copy_modules


def main():
    work=validation_directory()
    if not (work/'.zjcs-validation.json').is_file():raise ValueError('Owned validation workspace required')
    project=work/'minimap-unity'
    if project.exists():raise ValueError('Preserve existing minimap validation project')
    source=ROOT/'unity';runtime=source/'Assets/Game/Runtime'
    # This native rendering fixture deliberately reuses the installed Map13 source,
    # while the coordinated request and dispatch code comes from selected 884.
    x=json.loads((source/'Assets/Official/World/Map/13/map13.json').read_bytes());n=x['Navigation']
    regions=[json.loads(row['Json']) for row in x['Configs'] if row['TableName']=='TbMapRegionInfo']
    if n['Width']!=240 or n['Height']!=240 or len(regions)!=106:raise ValueError('Review installed Map13 fixture identity')
    fixture_data=dict(width=n['Width'],height=n['Height'],origin=[n['Origin'][a] for a in 'XYZ'],cells=[dict(area=c['RawValues'][0],ground=c['RawValues'][1]) for row in n['Rows'] for c in row['Cells']],areas=[dict(x=a['Cell'][0],y=a['Cell'][1],count=a['Value']) for a in n['AreaMappings']],regions=regions)
    if len(fixture_data['cells'])!=57600:raise ValueError('Incomplete original navigation fixture')
    (work/'minimap-description-fixture.json').write_text(json.dumps(fixture_data,separators=(',',':')),'utf8')
    modules={json.loads(p.read_bytes())['name']:p for p in runtime.rglob('*.asmdef')};selected=set()
    def stage_modules(pending):
     added=set()
     while pending:
      n=pending.pop()
      if n in selected or n not in modules:continue
      selected.add(n);added.add(n);pending.extend(json.loads(modules[n].read_bytes()).get('references',[]))
     copy_modules(project,runtime,modules,added)
    fixture=source/'Assets/Game/Editor/Map13MiniMap';stage_modules(json.loads((fixture/'Zjcs.Editor.Map13.MiniMap.asmdef').read_bytes())['references']);shutil.copytree(fixture,project/'Assets/Game/Editor/Map13MiniMap')
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
    pending=[p for name in ['NativeMiniMapRegions','NativeMiniMapSettings','NativeMiniMapMasks','NativeMiniMapDescriptions'] for p in (base/name).rglob('*') if p.is_file() and p.suffix!='.meta']
    pending += [base/'NativePipeline/UniversalRenderPipeline.asset',base/'NativeText/FZY4K-Dynamic.asset',base/'Resources/Map13/Native/region-label-data.json',source/'Assets/Game/Resources/Map13/Native/combat-rating-config.asset',base/'Resources/Map13/grid-visibility.json',source/'Assets/TextMesh Pro/Resources/TMP Settings.asset']
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
    shutil.copytree(source/'Packages',project/'Packages')
    shutil.copytree(source/'Assets/StreamingAssets/Map13/NativeText',project/'Assets/StreamingAssets/Map13/NativeText')
    settings=project/'ProjectSettings';settings.mkdir()
    for n in ['ProjectVersion.txt','ProjectSettings.asset','TagManager.asset']:shutil.copy2(source/'ProjectSettings'/n,settings/n)
    print('Prepared isolated actual minimap coordinator:',len(selected),'assemblies,',len(seen),'original assets',flush=True)


if __name__ == "__main__":
    main()
