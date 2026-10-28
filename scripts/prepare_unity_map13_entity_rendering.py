"""Port the game's original world-entity sliced renderer (not a UI redesign)."""
from __future__ import annotations
import json
import re
from pathlib import Path

from official_apk import ROOT, Apk
from prepare_unity_map13_prop_hierarchies import key, sha, write, encode
from prepare_unity_map13_prop_parts import meta

DEST = ROOT / 'unity/Assets/Game/Runtime/Official/World/Map13EntityRendering'
SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source'
RECEIPT = ROOT / 'assets/reference/official-map/map13-entity-rendering-source-manifest.json'


def build():
    apk=Apk.current(); code=ROOT/'.tools/source-packages/current'/apk.version/'official-code/client-source'
    original=code/'SlicedFilledRender.cs'
    text=original.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    text=re.sub(r'^\s*//IL_[^\n]*\n','',text,flags=re.M)
    text=text.replace('using Framework.DependencyInjection;','using Object = UnityEngine.Object;')
    text=text.replace('[ExecuteInEditMode]','namespace Zjcs.Official.World.Map13EntityRendering\n{\n[ExecuteInEditMode]')+'\n}\n'
    text=text.replace('[Inject]\n\tprivate SlicedFilledRenderService mSlicedFilledRenderService { get; set; }',
        'private SlicedFilledRenderService mSlicedFilledRenderService => SlicedFilledRenderService.Current;')
    text=text.replace('Span<Vector2>.op_Implicit(new Span<Vector2>((void*)ptr, 4))','new ReadOnlySpan<Vector2>((void*)ptr, 4)')
    text=text.replace('Span<Vector2>.op_Implicit(new Span<Vector2>((void*)ptr2, 4))','new ReadOnlySpan<Vector2>((void*)ptr2, 4)')
    text=text.replace('Span<Vector3>.op_Implicit(new Span<Vector3>((void*)ptr, 4))','new ReadOnlySpan<Vector3>((void*)ptr, 4)')
    text=text.replace('Span<Vector3>.op_Implicit(new Span<Vector3>((void*)ptr2, 4))','new ReadOnlySpan<Vector3>((void*)ptr2, 4)')
    text=re.sub(r'Vector2\.op_Implicit\((new Vector2\([^)]*\))\)',r'(Vector3)\1',text)
    text=text.replace('ReadOnlySpan<Vector3>.op_Implicit(array)','new ReadOnlySpan<Vector3>(array)')
    text=text.replace('ReadOnlySpan<Vector3>.op_Implicit(array2)','new ReadOnlySpan<Vector3>(array2)')
    text=text.replace('Color32.op_Implicit(mLinearColor)','(Color32)mLinearColor')
    text=text.replace('System.Runtime.CompilerServices.Unsafe.Read<Vector3>((void*)quadPositions[i])','quadPositions[i]')
    text=text.replace('Vector4.op_Implicit(System.Runtime.CompilerServices.Unsafe.Read<Vector3>((void*)quadUVs[i]))','(Vector4)quadUVs[i]')
    text=text.replace('\t\t((UIBehaviour)this)._002Ector();\n','')
    if 'op_Implicit' in text or '_002Ector' in text or 'Unsafe.Read' in text:raise ValueError('Unrepaired decompiled C#')
    # Explicit asset injection occurs while the owning prefab is inactive.
    # Mesh generation and all fill formulae remain in the original methods.
    text=text.replace('\tpublic float FillAmount\n', '\tpublic void BindOriginalSprite(Sprite sprite)\n\t{\n\t\tif (mInited) throw new InvalidOperationException("Bind original Sprite before entity activation.");\n\t\tmSprite = sprite;\n\t}\n\n\tpublic float FillAmount\n',1)
    text='\n'.join(line.rstrip() for line in text.splitlines())+'\n'
    write(DEST/'SlicedFilledRender.cs',text.encode())
    service=code/'SlicedFilledRenderService.cs'
    text=service.read_text(encoding='utf-8-sig').replace('\r\n','\n').replace('using Framework;','')
    text=text.replace('public class SlicedFilledRenderService : BackgroundServiceBase, ILateUpdatable',
        'namespace Zjcs.Official.World.Map13EntityRendering\n{\npublic class SlicedFilledRenderService')+'\n}\n'
    text=text.replace('void ILateUpdatable.LateUpdate()','public void LateUpdate()')
    text=text.replace('private readonly HashSet', 'public static readonly SlicedFilledRenderService Current = new SlicedFilledRenderService();\n\n\tprivate readonly HashSet',1)
    write(DEST/'SlicedFilledRenderService.cs',text.encode())
    write(DEST/'Zjcs.Official.World.Map13EntityRendering.asmdef',encode(dict(name='Zjcs.Official.World.Map13EntityRendering',rootNamespace='Zjcs.Official.World.Map13EntityRendering',references=['Unity.ugui'],allowUnsafeCode=True,autoReferenced=False)))
    meta(DEST,folder=True)
    outputs=[Path(str(DEST)+'.meta')]
    for p in DEST.iterdir():
        if p.suffix in ('.cs','.asmdef'):
            meta(p,'MonoImporter' if p.suffix=='.cs' else 'AssemblyDefinitionImporter');outputs.extend([p,Path(str(p)+'.meta')])
    graph=json.loads((SOURCE/'native-entity-object-graph.json').read_bytes())
    components=json.loads((SOURCE/'native-entity-components.json').read_bytes())
    materials={key(m['cab'],m['path_id']):m['output']['path'] for m in json.loads((SOURCE/'native-entity-material-bindings.json').read_bytes())['entries']}
    images=json.loads(RECEIPT.with_name('map13-native-entity-sprites-source-manifest.json').read_bytes())
    image_objects={o['identity']:(p['key'],o) for p in images['packages'] for o in p['native_objects']}
    indexed={key(o['cab'],o['path_id']):o for o in graph['objects']}
    records=[]
    for c in components['components']:
        if c['type']!='SlicedFilledRender':continue
        identity=key(c['cab'],c['path_id'])
        if identity not in indexed:continue
        o=indexed[identity]
        refs={ref['field']:key(ref['cab'],ref['path_id']) for ref in o['references']}
        sprite=refs['.mSprite'];package,native=image_objects[sprite]
        if native['type']!='Sprite':raise ValueError('Sliced image is not an original Sprite')
        records.append(dict(key=identity,gameObject=refs['.m_GameObject'],fields=c['fields'],material=materials[refs['.mMaterial']],sprite=sprite,package=package,assetName='__zjcs/map13/native/'+sha(sprite.encode())[:24]))
    if len(records)!=84:raise ValueError('Original sliced entity component selection differs')
    path=SOURCE/'native-entity-sliced-renderers.json';write(path,encode(dict(manifest=apk.version,mapClassId=13,renderers=records)));meta(path,'TextScriptImporter');outputs.extend([path,Path(str(path)+'.meta')])
    inputs=[original,service,Path(__file__).resolve(),SOURCE/'native-entity-object-graph.json',SOURCE/'native-entity-components.json',SOURCE/'native-entity-material-bindings.json',RECEIPT.with_name('map13-native-entity-sprites-source-manifest.json')]
    write(RECEIPT,encode(dict(manifest=apk.version,mapClassId=13,
        method='Official application mesh generation, fill branches, radial cuts, color and dirty-refresh order retained. Namespace and C# decompilation syntax repaired; original injected background service registry is explicitly shared and ticked by the offline owner. Sprite injection must precede Awake. No SDK code or replacement art.',
        components=len(records),coverage='Entity sliced renderer source/asset recovery. Full original prefabs, EC view lifecycle, map input/save and Player require separate verification.',
        inputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in inputs],outputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in sorted(outputs)])))
    print('Original entity sliced renderer restored:',len(records),'components')


if __name__=='__main__':build()
