"""Register the original ColorBlend programs and the actual background materials."""
import hashlib
import json
from official_apk import ROOT
from prepare_home_scene import SceneExporter, save
from prepare_home_decoration import components
from official_shader_programs import gles_programs


def build():
    e = SceneExporter()
    materials = {}; shaders = {}
    for file in sorted((ROOT/'assets/official/home-decor/entities').glob('*.json')):
        entity = json.loads(file.read_text(encoding='utf8'))
        if not any('ColorBlend' in m['shader'] for l in entity['layers'] for m in l['materials']):continue
        bundle = entity['source']['bundle']
        root = e.apk.asset(bundle, '/'+entity['name']+'.prefab')
        for _,path,_,cs in components(e, root):
            for c in cs:
                if c.type.name not in ['SpriteRenderer', 'MeshRenderer']:continue
                for ptr in c.read().m_Materials:
                    if not ptr.path_id:continue
                    mat = ptr.deref(); value = mat.read(); shader = value.m_Shader.deref()
                    if 'ColorBlend' not in shader.read().m_ParsedForm.m_Name:continue
                    e.input(mat); e.input(shader)
                    native = e.material(ptr)
                    assert native['floats']['_UseMask'] == 0
                    materials[native['name']] = dict(native, source=e.apk.provenance(mat))
                    shaders[shader.read().m_ParsedForm.m_Name] = dict(source=e.apk.provenance(shader),
                         sha256=hashlib.sha256(shader.get_raw_data()).hexdigest(), programs=gles_programs(shader.read()))
    assert len(materials) == 8 and len(shaders) == 1
    save(ROOT/'assets/reference/home-environment/sky-source-manifest.json', dict(manifest='854_156383',
         inputs=e.source_records, materials=materials, shaders=shaders,
         generator_sha256=hashlib.sha256((ROOT/'scripts/prepare_home_sky.py').read_bytes()).hexdigest(),
         operation='Original compiled GLES programs and all eight actual ColorBlend background materials; all use _UseMask=0. Renderer2D light texture migration is separate.'))
    print('Original ColorBlend:', len(materials), 'materials,', len(shaders), 'shader')


if __name__ == '__main__': build()
