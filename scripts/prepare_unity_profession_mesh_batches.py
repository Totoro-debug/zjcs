"""Record original profession geometry settings and independent native mesh batches."""
import json
from pathlib import Path
import re

from prepare_unity_profession_native_inputs import ROOT,CACHE
from prepare_unity_profession_native_curves import record


def build():
    base=CACHE/'typed/export/ExportedProject/Assets';inputs=[];settings=[]
    closure_path=CACHE/'typed/native-bindings-main-dynamic.json';closure=json.loads(closure_path.read_bytes())
    if closure['manifest']!='871_156598' or record(ROOT/closure['verified_export']['path'])!=closure['verified_export']:
        raise ValueError('Verified original prefab export changed')
    for name in ('CharacterPanelProfessionPanel','PlayerModel_ProfessionUI'):
        relative='Prefabs/UI/Profession/'+name+'.prefab'
        selected=closure['assets'][relative]
        path=ROOT/selected['path'];source=path.read_text(encoding='utf-8')
        if record(path)!=dict(path=selected['path'],sha256=selected['sha256']):raise ValueError('Original graphic prefab differs from verified export')
        blocks=[b for b in source.split('--- ') if '\n  meshGenerator:\n' in b]
        if len(blocks)!=1:raise ValueError('Original graphic geometry settings are ambiguous')
        block=blocks[0];fields={}
        for key in ('useClipping','pmaVertexColors','zSpacing','useCustomSpacing'):
            fields[key]=float(re.search(r'(?m)^      '+key+r': ([^\n]+)$',block)[1])
        if fields!=dict(useClipping=1,pmaVertexColors=0,zSpacing=0,useCustomSpacing=1):raise ValueError('Restore changed original geometry parameters')
        inputs.append(dict(**record(path),component=block.splitlines()[0],fields=fields))
        settings.append(dict(useClipping=bool(fields['useClipping']),pmaVertexColors=bool(fields['pmaVertexColors']),zSpacing=fields['zSpacing']))
    shader_path=ROOT/'assets/reference/profession-ui/shader-bindings-source-manifest.json';shader=json.loads(shader_path.read_bytes())
    binding=next(r['native'] for r in shader['bindings'] if r['native_relative_path']=='Shaders/Spine/Spine-SkeletonGraphicDye.shader')
    fixture=CACHE/'typed/native-mesh-batches-validation.json';fixture.write_text(json.dumps(dict(shader=binding['path'],settings=settings),ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeMeshBatches.cs';validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeMeshBatchesValidation.cs'
    proof=dict(format=1,manifest='871_156598',inputs=inputs,verified_export=closure['verified_export'],shader=record(shader_path),dependencies=[record(ROOT/'assets/reference/profession-ui'/name) for name in ('native-attachments-source-manifest.json','native-clipping-source-manifest.json')],
        fixture=record(fixture),generator=record(Path(__file__)),outputs=[record(p) for file in (runtime,validation) for p in (file,file.with_name(file.name+'.meta'))],
        behavior_reference='https://esotericsoftware.com/spine-clipping#End-slot',
        operation='Independent native mesh batching preserves the supplied complete source draw-order permutation, contiguous page/blend groups, explicit separator boundaries, per-slot colors, source zero Z spacing and original clipping/PMA settings. Clipping ends inclusively on source slot identity even if the end slot is empty/inactive; a self end runs to the end of draw order. Meshes are reused, stale geometry cleared, and only owned meshes are disposed. Original UI appearance code remains responsible for actual materials, parts and CanvasRenderer assignment; this creates no UI layout or substitute art.',
        validated_scope='Isolated Unity Play Mode and D3D11 passed: both source prefab geometry settings, inclusive/empty/inactive/reordered/self end slots, contiguous page/blend groups, explicit separators, slot colors, inactive skin bones, pooled mesh clearing/reuse and disposal. Original converted UI shader GPU output shows clipped red geometry, unclipped blue geometry after the end slot and transparent exterior; runtime image inspected. Geometry and white texture are synthetic fixtures. Full source character and profession-page services remain unbound.',
        limitations=['Geometry validation uses synthetic slots, poses and a white texture fixture. Original source constraints, production material/part binding and full page input/save/release still require integration. Overlapping clipping ranges fail explicitly; source clipping ranges must not overlap.'])
    (ROOT/'assets/reference/profession-ui/mesh-batches-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Recorded original geometry parameters from both profession graphic prefabs.')


if __name__=='__main__':build()
