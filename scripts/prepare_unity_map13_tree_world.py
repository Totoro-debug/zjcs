"""Bind the original complete tree entities to Map13's actual local inputs."""
from pathlib import Path
import hashlib
import json
import re

from official_apk import ROOT
from prepare_unity_map13_prop_parts import meta, dump, ref

RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap13TreeWorld'
SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source'
DEST = ROOT / 'unity/Assets/Game/Resources/Map13/Native/tree-world-assets.asset'
RECEIPT = ROOT / 'assets/reference/official-map/map13-native-tree-world-source-manifest.json'


def sha(raw): return hashlib.sha256(raw).hexdigest()


def record(path): return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def write(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != raw: path.write_bytes(raw)


def build():
    dependencies = [SOURCE/'templates.json', SOURCE/'entities.json',
        ROOT/'unity/Assets/Game/Resources/Map13/entity-presentation.json',
        ROOT/'assets/reference/official-map/map13-native-tree-entities-source-manifest.json',
        ROOT/'assets/reference/official-map/map13-native-tree-model-animator-source-manifest.json',
        ROOT/'assets/reference/official-map/map13-native-prop-animators-source-manifest.json']
    templates = json.loads(dependencies[0].read_bytes())
    entities = json.loads(dependencies[1].read_bytes())
    source_receipt = json.loads(dependencies[3].read_bytes())
    for item in source_receipt['outputs']:
        if item['path'].endswith('.prefab'):
            path = ROOT/item['path']
            if sha(path.read_bytes()) != item['sha256']: raise ValueError('Original tree prefab changed: '+str(path))
            dependencies.append(path)
    names = ['Zjcs.Official.EntityViews', 'Zjcs.Official.EntityWorldStages',
        'Zjcs.Official.EntityViewPlayables', 'Zjcs.Official.StoryPlayableManager', 'Zjcs.Official.StoryPlayables',
        'Zjcs.Official.StorySourceFade', 'Zjcs.Official.World.Map13EntityEvents',
        'Zjcs.Official.World', 'Zjcs.Official.World.NativeTerrain',
        'Zjcs.Official.World.Map13EntityStateViews', 'Zjcs.Official.World.Map13EntityVisibilityViews',
        'Zjcs.Official.World.Map13EntityGridViews', 'Zjcs.Official.World.Map13EntityFadeHandlers',
        'Zjcs.Official.World.Map13EntityRendering', 'Zjcs.Official.World.Map13TreePlayables',
        'Zjcs.Official.World.Map13GridVisibility', 'Zjcs.Official.World.Map13Entities', 'Zjcs.Offline.World.Map13Entities',
        'Zjcs.Offline.World.NativeMap13EntityAssets', 'Zjcs.Offline.World.NativeMap13EntityViews',
        'Zjcs.Offline.World.NativeMap13Skeletons', 'Zjcs.Offline.World.NativeMap13TreeEntities',
        'Zjcs.Offline.World.NativeMap13StoryPlayables']
    assembly = RUNTIME/'Zjcs.Offline.World.NativeMap13TreeWorld.asmdef'
    write(assembly, (json.dumps(dict(name=assembly.stem,references=names,autoReferenced=True),indent=2)+'\n').encode())
    meta(RUNTIME, folder=True); meta(assembly,'AssemblyDefinitionImporter')
    for path in RUNTIME.glob('*.cs'): meta(path,'MonoImporter')
    script = meta(RUNTIME/'NativeMap13TreeWorldAssets.cs','MonoImporter')
    def asset(path, file_id=4900000):
        dependencies.append(path)
        return ref(file_id,meta(path),3 if path.suffix=='.prefab' or file_id==4900000 else 2)
    prop = ROOT/'unity/Assets/Official/World/Map/13/NativePropAnimators'
    dissolve = ROOT/'unity/Assets/Official/World/Map/13/NativeTreeModelAnimator'
    binary = prop/'map13-prop-animators.windows.bundle.bytes'
    body = dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=ref(),m_PrefabInstance=ref(),m_PrefabAsset=ref(),
        m_GameObject=ref(),m_Enabled=1,m_EditorHideFlags=0,m_Script=ref(11500000,script,3),m_Name=DEST.stem,m_EditorClassIdentifier='',
        manifest='871_156598',animationSha256=sha(binary.read_bytes()),
        animationBinary=asset(binary),animationCatalog=asset(prop/'execution-cases.json'),
        dissolveBinary=asset(dissolve/'model-animator.windows.bundle.bytes'),dissolveCatalog=asset(dissolve/'catalog.json'),
        spriteCatalogResource='Map13/Native/entity-sprite-catalog',displays=[],placements=[])
    dependencies.append(ROOT/'unity/Assets/Game/Resources/Map13/Native/entity-sprite-catalog.asset')
    display_rows=json.loads(dependencies[2].read_bytes())['Displays']
    for name in ['8363_view','8363_snow_view']:
        paths = {v['DisplayPath'] for v in display_rows if v['EntityClassId']==8363 and Path(v['DisplayPath']).stem==name}
        if len(paths)!=1: raise ValueError('Original tree display path is ambiguous: '+name)
        prefab=ROOT/'unity/Assets/Game/Resources/Map13/Native/TreeEntities'/(name+'.prefab')
        text=prefab.read_text(encoding='utf-8-sig')
        transforms=re.findall(r'(?ms)^--- !u!4 &\d+\n(.*?)(?=^--- !u!|\Z)', text)
        roots=[part for part in transforms if re.search(r'(?m)^  m_Father:\n    fileID: 0$', part)]
        if len(roots)!=1: raise ValueError('Original complete tree has no unique root transform: '+name)
        root_id=int(re.search(r'(?m)^  m_GameObject:\n    fileID: (\d+)$',roots[0])[1])
        body['displays'].append(dict(path=paths.pop(),prefab=asset(prefab,root_id)))
    for identity, value in entities.items():
        entity_id=value['LoEntityComponent']['SpanwInfo']['ClassId']
        if entity_id!=8363: continue
        template=templates[str(entity_id)]; transform=value['LoGridTransformComponent']
        grid=template['GridTransformComponent'].copy();grid['RealCenter']=dict(zip(['x','y'],grid['RealCenter']))
        stage=template['WorldStageComponent']
        body['placements'].append(dict(placementId=int(identity),entityClassId=entity_id,position=transform['Pos'],
            direction=transform['Dir'],flipX=transform['FlipX'],tag=value['LoEntityComponent']['Tag'],
            body=grid,stage=stage,stageQuery=dict(StateInfos=stage['StateInfos']),
            awards=value.get('LoStageResultAwardItemComponent',template['StageResultAwardItemComponent'])))
    if len(body['placements'])!=3: raise ValueError('Original Map13 tree placement inventory differs')
    # Unity's YAML reader does not support PyYAML's shared-container aliases.
    body = json.loads(json.dumps(body))
    write(DEST, ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+dump(dict(MonoBehaviour=body))).encode())
    meta(DEST,'NativeFormatImporter')
    outputs=[DEST,Path(str(DEST)+'.meta'),Path(str(RUNTIME)+'.meta')]+[p for p in RUNTIME.iterdir() if p.is_file()]
    proof=dict(manifest='871_156598',mapClassId=13,placements=3,
        operation='Explicit local inputs and root-prefab references for the original complete 8363 entity, ordered views, source body/placement/state/award queries and native real-time geometry. Original display selection and stage/fade/playable implementations are reused. Executed formal-map checks are recorded separately; Windows Player and the remaining map entities require further integration.',
        inputs=[record(p) for p in sorted(set(dependencies))],outputs=[record(p) for p in sorted(set(outputs))],tools=[record(Path(__file__).resolve())])
    write(RECEIPT,(json.dumps(proof,ensure_ascii=False,indent=2)+'\n').encode())
    print('Bound 3 original Map13 tree placements, 2 complete source prefabs and original native animation libraries')


if __name__=='__main__':build()
