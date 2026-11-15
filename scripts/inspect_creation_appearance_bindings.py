"""Record original creation icon, selection and title-effect bindings from the APK."""
import hashlib
import json

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from prepare_official_city import PanelSchemas
from UnityPy.classes.PPtr import PPtr


def build():
    with Apk() as apk:
        native, effects = PanelSchemas(), ScriptSchemas()
        records = []
        title_shader = None
        for obj in apk.objects('defaultpackage_assets_prefabs_ui_createplayer.bundle'):
            if obj.type.name != 'MonoBehaviour':
                continue
            mono = obj.read(check_read=False)
            owner = mono.m_GameObject.deref().read().m_Name
            kind = mono.m_Script.deref().read().m_ClassName
            if not ((owner == 'HairTypeItem' and kind in ('TypeSelectItem', 'UIToggleObject'))
                    or (owner == 'SelectSexTitle' and kind == 'TMPEffect')):
                continue
            fields = (effects if kind == 'TMPEffect' else native).read(obj)
            if kind == 'TMPEffect':
                for ptr in mono.m_GameObject.deref().read().m_Component:
                    component = ptr.component.deref()
                    if component.type.name != 'MonoBehaviour':
                        continue
                    if component.read(check_read=False).m_Script.deref().read().m_ClassName != 'TextMeshProUGUI':
                        continue
                    text = native.read(component)
                    material = PPtr(**text['m_sharedMaterial'], assetsfile=obj.assets_file).deref()
                    shader = material.read().m_Shader.deref()
                    title_shader = dict(source=apk.provenance(shader),
                                        name=shader.read().m_ParsedForm.m_Name,
                                        object_sha256=hashlib.sha256(shader.get_raw_data()).hexdigest(),
                                        material_source=apk.provenance(material),
                                        runtime_material='Assets/Game/Resources/SaveSelection/Materials/25.mat',
                                        runtime_shader='Assets/Game/Runtime/Official/Startup/StartupTextMeshPro.shader')
            resolved = {}
            for name in ('Icon', 'myActiveObj'):
                pointers = fields.get(name, [])
                if isinstance(pointers, dict):
                    pointers = [pointers]
                resolved[name] = []
                for ptr in pointers:
                    target = PPtr(**ptr, assetsfile=obj.assets_file).deref()
                    value = target.read(check_read=False)
                    if target.type.name != 'GameObject':
                        value = value.m_GameObject.deref().read()
                    resolved[name].append(value.m_Name)
            records.append(dict(owner=owner, component=kind, source=apk.provenance(obj),
                                object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                                fields=fields, resolved=resolved))
        if not any(r['component'] == 'TMPEffect' for r in records):
            raise ValueError('Original title effect not found')
        destination = ROOT / 'assets/reference/character-creation/appearance-bindings-source-manifest.json'
        destination.write_text(json.dumps(dict(manifest=apk.version, records=records, title_shader=title_shader),
                                          ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print(destination)


if __name__ == '__main__':
    build()
