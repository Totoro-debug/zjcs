"""Recover original current-version atlas packing and attachment geometry references."""
import hashlib
import json
import math
from pathlib import Path

from official_apk import Apk
from prepare_native_character_geometry import atlas_regions
from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record, f32


def uv_reference(region, uv):
    left, top, width, height = region['bounds']
    ox, oy, original_width, original_height = region['offsets']
    x, y = uv[0]*original_width-ox, uv[1]*original_height-(original_height-oy-height)
    # Rotate image coordinates about the rectangle centre, then translate to
    # the packed rectangle centre. This reference does not use branch UV code.
    c, s = round(math.cos(math.radians(region['rotate']))), round(math.sin(math.radians(region['rotate'])))
    packed_width, packed_height = (height,width) if s else (width,height)
    dx, dy = x-width/2, y-height/2
    page_x = left+packed_width/2+c*dx+s*dy
    page_y = top+packed_height/2-s*dx+c*dy
    return dict(x=f32(page_x/region['size'][0]),y=f32(1-page_y/region['size'][1]))


def corner_uvs(region):
    _,_,w,h = region['bounds']; ox,oy,ow,oh = region['offsets']
    return [(f32(x/ow),f32(1-y/oh)) for x,y in ((ox,oy),(ox,oy+h),(ox+w,oy+h),(ox+w,oy))]


def corner_reference(region, attachment):
    _,_,w,h = region['bounds']; ox,oy,ow,oh = region['offsets']
    c,s = math.cos(math.radians(attachment['rotation'])),math.sin(math.radians(attachment['rotation']))
    corners=[]
    for px,py in ((ox,oy),(ox,oy+h),(ox+w,oy+h),(ox+w,oy)):
        x=(px-ow/2)*(attachment['width']/ow)*attachment['scaleX']
        y=(py-oh/2)*(attachment['height']/oh)*attachment['scaleY']
        corners.append(dict(x=f32(attachment['x']+c*x-s*y),y=f32(attachment['y']+s*x+c*y)))
    return corners


def build():
    apk=Apk.current()
    if apk.version!='871_156598': raise ValueError('Original profession atlas version changed')
    bone_path=ROOT/'assets/reference/profession-ui/bone-pose-source-manifest.json'
    bone_proof=json.loads(bone_path.read_bytes())
    inputs=[record(bone_path),record(ROOT/'scripts/prepare_native_character_geometry.py'),record(ROOT/'scripts/official_apk.py')]
    cases=[];counts=dict(regions=0,pages=0,mesh=0,region=0,uvs=0,corners=0)
    for sex in ('male','female'):
        path=CACHE/'skeletons'/sex/'skeleton.json'
        if record(path) not in bone_proof['inputs']:raise ValueError('Source skeleton changed')
        skeleton=json.loads(path.read_bytes());inputs.append(record(path))
        objects=apk.objects('appearanceassets_character_model_'+sex+'_1')
        atlases=[o for o in objects if o.type.name=='TextAsset' and o.read().m_Name=='character_'+sex.title()+'_1.atlas']
        if len(atlases)!=1:raise ValueError('Expected one original character atlas')
        original=atlases[0];data=original.read().m_Script.encode('utf-8','surrogateescape')
        atlas_path=path.with_name('atlas.txt');atlas_path.write_bytes(data)
        receipt_path=path.with_name('atlas-source-manifest.json')
        receipt=dict(manifest=apk.version,source=apk.provenance(original),output_sha256=hashlib.sha256(data).hexdigest())
        receipt_path.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
        inputs.extend([record(atlas_path),record(receipt_path)])
        regions=atlas_regions(data.decode('utf-8','surrogateescape'))
        indices={r['name']:i for i,r in enumerate(regions)}
        if len(indices)!=len(regions):raise ValueError('Original region names require frame indices')
        for r in regions:
            _,_,w,h=r['bounds'];ox,oy,ow,oh=r['offsets']
            if not (0<=ox<=ow-w and 0<=oy<=oh-h):raise ValueError('Original trim exceeds untrimmed size')
            r['cornerUvs']=[uv_reference(r,p) for p in corner_uvs(r)]
        attachments=[]
        for skin in skeleton['skins']:
            for slot,entries in skin.get('attachments',{}).items():
                for key,a in entries.items():
                    kind=a.get('type','region')
                    if kind not in ('mesh','region'):continue
                    image=a.get('path',a.get('name',key))
                    if image not in indices:raise ValueError('Original atlas path unresolved: '+image)
                    region=regions[indices[image]]
                    entry=dict(name='/'.join((skin['name'],slot,key)),region=indices[image],mesh=kind=='mesh')
                    if kind=='mesh':
                        uv=[f32(v) for v in a['uvs']]
                        entry.update(uvs=uv,expectedUvs=[uv_reference(region,uv[i:i+2]) for i in range(0,len(uv),2)])
                    else:
                        params={k:f32(a.get(k,1 if k in ('scaleX','scaleY') else 0)) for k in ('width','height','x','y','scaleX','scaleY','rotation')}
                        if 'width' not in a or 'height' not in a:raise ValueError('Original region attachment size is absent')
                        entry.update(**params,expectedCorners=corner_reference(region,params))
                    attachments.append(entry);counts[kind]+=1
                    counts['uvs']+=len(entry.get('expectedUvs',[]));counts['corners']+=len(entry.get('expectedCorners',[]))
        counts['regions']+=len(regions);counts['pages']+=len({r['page'] for r in regions})
        cases.append(dict(sex=sex,regions=regions,attachments=attachments))
    output=CACHE/'typed/atlas-geometry-validation.json'
    output.write_text(json.dumps(dict(cases=cases),ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAtlasRegion.cs'
    validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeAtlasGeometryValidation.cs'
    proof=dict(format=1,manifest=apk.version,inputs=inputs,fixture=record(output),generator=record(Path(__file__)),
        outputs=[record(p) for path in (runtime,validation) for p in (path,path.with_name(path.name+'.meta'))],counts=counts,
        format_references=['https://esotericsoftware.com/spine-atlas-format','https://esotericsoftware.com/spine-json-format#Attachments'],
        operation='Read the APK-first, pinned-current bundle via the verified local CDN resolver only when needed. Reuse the independent atlas text parser. Independently map untrimmed source UVs through trim and quarter-turn packing to Unity page UVs; retain original attachment transforms and region trim geometry. References use a centred affine rotation rather than the runtime branch mapping. No skeleton SDK implementation or baked prefab is used.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode: 19805 original atlas regions, all 11716 source meshes / 165309 UVs and 22230 textured rectangle attachments / 88920 corners match independent centred-affine references. Maximum UV difference 5.96046448E-08 within 0.000002; maximum source-coordinate difference 0.0000305175781 within 0.0004 + 0.000007 * abs(expected). All quarter turns, original trim and attachment transforms, reflected geometry and invalid inputs pass. No real character or complete profession page is rendered by this stage.',
        limitations=['GPU sampling of production atlas textures, attachment material resolution, original constraints and full character/page rendering remain required.'])
    (ROOT/'assets/reference/profession-ui/atlas-geometry-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Recovered original atlas geometry:',counts)


if __name__=='__main__':build()
