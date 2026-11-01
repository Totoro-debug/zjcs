"""Bind the current original live portrait material/shader for isolated GPU validation."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

from official_apk import ROOT
from prepare_current_partner_native import PartnerNativeRecovery, pointer, identity
from validation_workspace import require_temporary_unity_project, validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project',required=True,type=Path)
    parser.add_argument('--portraits',required=True,type=Path)
    args=parser.parse_args()
    project=require_temporary_unity_project(args.project)
    work=validation_directory()
    portraits=args.portraits.resolve()
    recovery=PartnerNativeRecovery()
    recovery.recover('NpcDetailLiveItem',work/'portrait-live-source',
        bundle='defaultpackage_assets_prefabs_appearance_npclive_npcdetailliveitem.bundle',
        native_relative='Prefabs/Appearance/NpcLive/NpcDetailLiveItem.prefab')
    recovery.finalize()
    data=recovery.outputs[0][2]
    graphic,=[row for row in data['scripts'] if row['namespace']=='Spine.Unity' and row['type']=='SkeletonGraphic']
    objects={identity(obj):obj for obj in recovery.apk.objects('defaultpackage_assets_prefabs_appearance_npclive_npcdetailliveitem.bundle')}
    original=objects[graphic['original']]
    fields=recovery.fields(original)
    material=pointer(fields['m_Material'],original)
    material_fields=recovery.fields(material)
    shader=pointer(material_fields['m_Shader'],material)
    cache=recovery.cache/'all-assets/windows-shaders'
    receipt=json.loads((cache/'source-manifest.json').read_bytes())
    candidates=[row for row in receipt['shaders'] if row['source']['object_sha256']==sha(shader.get_raw_data())]
    if not candidates:raise ValueError('Convert the actual current live UI shader')
    converted=candidates[0]
    shader_path=cache/converted['path']
    if sha(shader_path.read_bytes())!=converted['sha256']:raise ValueError('Current native shader changed')
    destination=project/'Assets/PartnerCurrentValidation/Portrait'
    destination.mkdir(exist_ok=True)
    shader_target=destination/'SkeletonGraphic.shader'
    shutil.copy2(shader_path,shader_target)
    shutil.copy2(Path(str(shader_path)+'.meta'),Path(str(shader_target)+'.meta'))
    native_material=recovery.native/'Materials/Spine/SkeletonGraphicDefault_0.mat'
    native_fields=data['documents'][str(graphic['nativeFileID'])]['MonoBehaviour'] if str(graphic['nativeFileID']) in data['documents'] else data['documents'][graphic['nativeFileID']]['MonoBehaviour']
    metadata=Path(str(native_material)+'.meta').read_text('utf8')
    if re.search(r'^guid: ([0-9a-f]{32})$',metadata,re.M)[1]!=native_fields['m_Material']['guid']:
        raise ValueError('Original live material differs from the native source')
    text=native_material.read_text('utf8')
    text,count=re.subn(r'm_Shader: \{fileID: 4800000, guid: [0-9a-f]{32}, type: 2\}',
        'm_Shader: {fileID: 4800000, guid: '+converted['guid']+', type: 3}',text)
    if count!=1:raise ValueError('Native live material shader binding is not unique')
    (destination/'SkeletonGraphicDefault.mat').write_text(text,'utf8',newline='\n')
    proof=json.loads((portraits/'partner-native-portraits-source-manifest.json').read_bytes())
    portrait=next(row for row in proof['portraits'] if row['npcId']==8401)
    atlases=json.loads((portraits/'native-portraits'/portrait['atlases']).read_bytes())
    textures=[texture for atlas in atlases for mat in atlas['materials'] for texture in mat['textures'] if texture['property']=='_MainTex']
    texture,=textures
    source=portraits/'native-portraits'/texture['png']
    if sha(source.read_bytes())!=proof['outputs'][texture['png']]:raise ValueError('Original portrait pixels changed')
    shutil.copy2(source,portraits/'portrait-8401-texture.png')
    shutil.copy2(ROOT/'scripts/fixtures/partner_portrait_graphics_validation.cs',project/'Assets/PartnerCurrentValidation/Editor/PartnerPortraitGraphicsValidation.cs')
    result=dict(manifest='884_156692',source_graphic=dict(original=identity(original),sha256=sha(original.get_raw_data()),fields=fields),
        source_material=dict(original=identity(material),sha256=sha(material.get_raw_data()),fields=material_fields),
        shader=dict(original=identity(shader),sha256=sha(shader.get_raw_data()),conversion=converted),texture=texture,
        original_native_material=dict(path=native_material.relative_to(ROOT).as_posix(),sha256=sha(native_material.read_bytes())),
        staged_material_sha256=sha((destination/'SkeletonGraphicDefault.mat').read_bytes()),
        scope='Original 8401 isolated mesh/GPU fixture; complete native live prefab and interaction remain required.')
    (portraits/'portrait-graphics-source-manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Staged original live material and byte-verified current shader; 8401 original texture',texture['name'])


if __name__=='__main__':main()
