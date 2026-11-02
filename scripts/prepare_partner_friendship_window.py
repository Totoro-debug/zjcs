"""Restore the selected friendship popup, original blur mask and screen-blur program."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_shader_programs import gles_programs
from prepare_battle_hud_materials import stage
from prepare_current_partner_native import documents, pointer as source_pointer
from prepare_partner_card_native import guid, identity, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_input_dialog import PartnerInputBinding, metadata
from validation_workspace import validation_directory

RECEIPT = ROOT / 'assets/reference/partner-friendship-window-source-manifest.json'
DESTINATION = ROOT / 'unity/Assets/Official/Partners/UI/FriendshipWindow'
CATALOG = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerFriendshipWindowAssetCatalog.asset'
NEW_SCRIPTS = ('Official/WorldUI/WorldWindowBlurMask.cs',
    'Offline/PartnersUI/OfficialPartnerFriendshipWindowAssetCatalog.cs')


class PartnerFriendshipWindowBinding(PartnerInputBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {
            'WindowBlurMask': runtime / 'WorldUI/WorldWindowBlurMask.cs',
            'NpcFriendshipDialog': runtime / 'PartnersUI/FriendshipDialog/NpcFriendshipDialog.cs',
            'PreviewTool': runtime / 'WorldUI/WorldPreviewTool.cs',
            'UGUIJszx.VerticalLayoutGroupJSZX': runtime / 'WorldUI/VerticalLayoutGroupJSZX.cs',
        }
        for full_name, path in paths.items():
            namespace, _, name = full_name.rpartition('.')
            meta = Path(str(path) + '.meta')
            reference = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[full_name] = reference
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=reference,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            self.class_ids[script_id(namespace, name)] = reference
            self.script_guids.add(reference['guid'])

    def friendship_templates(self):
        self.recovery.recover('NpcFriendshipDialog', self.work / 'partner-native')
        bundle = 'defaultpackage_assets_prefabs_ui_main.bundle'
        containers = []
        for obj in self.recovery.apk.objects(bundle):
            if obj.type.name == 'MonoBehaviour':
                script = obj.read(check_read=False).m_Script.read()
                if script.m_ClassName == 'WindowContainer' and self.recovery.fields(obj)['mContainerType'] == 0:
                    containers.append((obj, script))
        container, script = containers[0]
        if len(containers) != 1:
            raise ValueError('Inspect original default window container')
        original = self.recovery.fields(container)
        masks = [(i, source_pointer(value, container)) for i, value in enumerate(original['MaskList'])]
        index, mask = next((i, obj) for i, obj in masks if self.recovery.fields(obj)['mMaskType'] == 3)
        native = self.native / 'Prefabs/UI/Main/MainCanvas.prefab'
        native_docs = documents(native.read_text('utf8'))
        native_container, = [doc['MonoBehaviour'] for doc in native_docs.values() if 'MonoBehaviour' in doc
            and doc['MonoBehaviour']['m_Script']['fileID'] == script_id(script.m_Namespace, script.m_ClassName)
            and doc['MonoBehaviour'].get('mContainerType') == 0]
        native_mask = native_docs[native_container['MaskList'][index]['fileID']]['MonoBehaviour']
        if native_mask['mMaskType'] != 3:
            raise ValueError('Original/native blur window mask differs')
        self.recovery.recover('PartnerFriendshipBlurMask', self.work / 'partner-native', bundle=bundle,
            native_relative='Prefabs/UI/Main/MainCanvas.prefab',
            source_root=source_pointer(self.recovery.fields(mask)['m_GameObject'], mask),
            native_root_id=native_mask['m_GameObject']['fileID'])
        self.mask_selection = dict(container=identity(container), source=identity(mask), list_index=index,
            original_container_fields=original, native_source=native.relative_to(ROOT).as_posix())
        self.recovery.finalize()
        self.empty_values = []

        def restore(source, converted, address, owner):
            if isinstance(source, dict) and isinstance(converted, dict):
                for key in source.keys() & converted.keys():
                    if converted[key] is None and (source[key] == '' or source[key] == [] or source[key] == {}):
                        converted[key] = source[key]
                        self.empty_values.append(dict(original=owner, field=address + '/' + key, value=source[key]))
                    else:
                        restore(source[key], converted[key], address + '/' + key, owner)
            elif isinstance(source, list) and isinstance(converted, list) and len(source) == len(converted):
                for i, (left, right) in enumerate(zip(source, converted)):
                    restore(left, right, address + '/' + str(i), owner)

        result = []
        for native, _, panel, record in self.recovery.outputs:
            for path in (native, Path(str(native) + '.meta')):
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            for key, ref in panel['external'].items():
                self.objects[ref['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
            for script in panel['scripts']:
                owner = self.original(script['original'])
                fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
                original_fields = self.recovery.fields(owner)
                restore(original_fields, fields, '', script['original'])
                if script['type'] == 'PopupWindowSound':
                    for field in ('ShowSound', 'HideSound'):
                        fields[field] = self.sound(original_fields[field], owner)
                if script['type'] == 'PopupWindow' and record['name'] == 'NpcFriendshipDialog':
                    if fields['mMaskType'] != 3:
                        raise ValueError('Original friendship window no longer requests blur mask')
            result.append((record['name'], panel))
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.template_selection = dict(scope='Complete original friendship popup and its requested blur mask.')
        return result

    def build(self):
        PartnerGiftBinding.build(self)
        path = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(path.read_bytes())
        folder = self.work / 'gift-assets'
        root_guids = {}
        for root in receipt['roots'].values():
            old = root['guid']
            root['guid'] = hashlib.md5(('zjcs:partner-friendship-window:' + old).encode()).hexdigest()
            root_guids[old] = root['guid']
        for name in receipt['outputs']:
            item = folder / name
            raw = item.read_bytes()
            if item.suffix == '.meta' or raw.startswith(b'%YAML'):
                for old, new in root_guids.items():
                    raw = raw.replace(old.encode(), new.encode())
                item.write_bytes(raw)
                receipt['outputs'][name] = sha(raw)
        for name in ('partner-gift-texts.json', 'partner-gift-texts.json.meta'):
            receipt['outputs'].pop(name)
            (folder / name).unlink()
        self.recovery.apk.load_builtin('assets/bin/Data/data.unity3d')
        shader = next(o for o in self.recovery.apk.env.objects if o.type.name == 'Shader'
            and o.read().m_ParsedForm.m_Name == 'CustomRenderTexture/ScreenBlur')
        programs = gles_programs(shader.read(), with_keywords=True)
        if len(programs) != 1 or programs[0]['keywords']:
            raise ValueError('Inspect original ScreenBlur program variants')
        fragment, uniforms = stage(programs[0]['source'], False)
        text = '''Shader "Zjcs/Official/UI/ScreenBlur" {
SubShader { Cull Off ZWrite Off ZTest Always
Pass { CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
Varying vert(appdata_img input) { Varying output; output.position=UnityObjectToClipPos(input.vertex); output.uv=input.texcoord; return output; }
''' + '\n'.join(uniforms) + '\n' + fragment + '\nENDCG\n}\n}\n}\n'
        existing = ROOT / 'unity/Assets/Game/Runtime/Presentation/World/Shaders/ScreenBlur.shader'
        material = ROOT / 'unity/Assets/Game/Resources/World/Mainline/ScreenBlur.mat'
        shader_guid = hashlib.md5(('zjcs:partner-screen-blur:' + identity(shader)).encode()).hexdigest()
        material_guid = hashlib.md5(b'zjcs:partner-screen-blur-material').hexdigest()
        shader_path = folder / 'PartnerScreenBlur.shader'
        shader_path.write_text(text.replace('Zjcs/Official/UI/ScreenBlur', 'Zjcs/Official/Partners/ScreenBlur'),
            'utf8', newline='\n')
        Path(str(shader_path) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + shader_guid
            + '\nShaderImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        material_path = folder / 'PartnerScreenBlur.mat'
        original_material = material.read_text('utf8')
        material_path.write_text(re.sub(r'(m_Shader: \{fileID: 4800000, guid: )[0-9a-f]{32}',
            lambda match: match[1] + shader_guid, original_material), 'utf8', newline='\n')
        Path(str(material_path) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + material_guid
            + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n', 'utf8', newline='\n')
        for item in (shader_path, Path(str(shader_path) + '.meta'), material_path, Path(str(material_path) + '.meta')):
            receipt['outputs'][item.name] = sha(item.read_bytes())
        receipt['blur_material'] = dict(fileID=2100000, guid=material_guid, type=2)
        card_receipt = ROOT / 'assets/reference/partner-card-binding-source-manifest.json'
        audio, = [row for row in json.loads(card_receipt.read_bytes())['audio'].values()
            if row['event']['id'] == 3772241779]
        click_clip = ROOT / 'unity/Assets/Official/Partners/UI/Card' / audio['output']
        if sha(click_clip.read_bytes()) != audio['output_sha256']:
            raise ValueError('Existing original partner cursor audio changed')
        receipt['click_audio'] = audio
        for item in (card_receipt, click_clip, Path(str(click_clip) + '.meta')):
            receipt['inputs'][item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        receipt['screen_blur'] = dict(source=self.recovery.apk.provenance(shader), original=identity(shader),
            original_sha256=sha(shader.get_raw_data()), programs=programs,
            windows_shader=shader_path.name, windows_sha256=sha(shader_path.read_bytes()),
            material=material_path.name, material_sha256=sha(material_path.read_bytes()),
            differs_from_previous_program=text.encode() != existing.read_bytes(),
            operation='Convert current original GLSL separately; retain original seven-tap expressions, uniforms and pass. Use original new-Material defaults.')
        for item in (existing, Path(str(existing) + '.meta'), material, Path(str(material) + '.meta'),
                ROOT / 'unity/Assets/Official/Partners/UI/FriendshipDialog/partner-friendship-dialog-texts.json'):
            receipt['inputs'][item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        for name in ('prepare_partner_input_dialog.py', 'prepare_partner_roster_native.py', 'prepare_partner_gift_native.py',
                'partner_roster_animator_binding.py', 'official_shader_programs.py', 'prepare_battle_hud_materials.py'):
            item = ROOT / 'scripts' / name
            receipt['inputs'][item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        for relative in ('client-source/NpcFriendshipDialog.cs', 'framework-firstpass-source/WindowBlurMask.cs',
                'framework-firstpass-source/WindowMaskBase.cs', 'framework-firstpass-source/BlurManager.cs',
                'framework-firstpass-source/ScreenBlurPass.cs', 'framework-firstpass-source/PopupWindow.cs'):
            item = ROOT / '.tools/source-packages/official-code' / relative
            selected = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code' / relative
            if item.read_bytes() != selected.read_bytes():
                raise ValueError('Selected friendship source differs: ' + relative)
            receipt['inputs'][item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        used = set()
        for name in receipt['outputs']:
            item = folder / name
            if item.suffix != '.meta' and item.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', item.read_text('utf8')))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items()
            if row['pointer']['guid'] in used}
        receipt.update(scope='Original friendship popup, original blur mask, verified current Windows screen-blur shader.',
            generator_sha256=sha(Path(__file__).read_bytes()), mask_selection=self.mask_selection,
            original_empty_value_restorations=self.empty_values, animation_root_path_repairs=self.animation_root_paths)
        for key in ('gradient_aot_methods', 'gradient_observations'):
            receipt.pop(key)
        (self.work / RECEIPT.name).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        path.unlink()


def install(work):
    receipt = json.loads((work / RECEIPT.name).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild changed friendship window generator')
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Friendship window source changed: ' + relative)
    for row in receipt['script_bindings'].values():
        source = ROOT / row['path']
        if sha(source.read_bytes()) != row['sha256'] or sha(Path(str(source) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Friendship window script changed: ' + row['path'])
    previous = json.loads(RECEIPT.read_bytes()).get('installed_output_sha256', {}) if RECEIPT.is_file() else {}
    existing = {}
    for meta in (ROOT / 'unity/Assets/Official/Partners').rglob('*.meta'):
        source = Path(str(meta)[:-5])
        if source.is_file():
            key = guid(meta)
            if key in existing:
                raise ValueError('Existing partner GUID collision: ' + key)
            existing[key] = source
    plan, installed, reused = [], {}, []
    for relative, expected in receipt['outputs'].items():
        source = work / 'gift-assets' / relative
        if sha(source.read_bytes()) != expected:
            raise ValueError('Staged friendship resource changed: ' + relative)
        if relative.endswith('.meta'):
            continue
        key = guid(Path(str(source) + '.meta'))
        target = existing.get(key, DESTINATION / relative)
        if target.exists() and target.read_bytes() != source.read_bytes():
            if not target.is_relative_to(DESTINATION) or sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve different existing partner resource: ' + str(target))
        if target.exists() and target.read_bytes() == source.read_bytes():
            reused.append(target.relative_to(ROOT).as_posix())
        else:
            plan.extend(((source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))))
        if target.is_relative_to(DESTINATION):
            installed[target.relative_to(ROOT).as_posix()] = expected
            installed[Path(str(target) + '.meta').relative_to(ROOT).as_posix()] = sha(Path(str(source) + '.meta').read_bytes())
        existing[key] = target
    for source, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
    for folder in [DESTINATION, *[p for p in DESTINATION.rglob('*') if p.is_dir()]]:
        meta = metadata(folder, 'DefaultImporter', True)
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerFriendshipWindowAssetCatalog.cs'
    raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
    raw += '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
    raw += '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
    raw += '  m_Script: {fileID: 11500000, guid: ' + guid(Path(str(script) + '.meta')) + ', type: 3}\n'
    raw += "  m_Name: OfficialPartnerFriendshipWindowAssetCatalog\n  m_EditorClassIdentifier: ''\n"
    for field, ref in (('windowPrefab', receipt['roots']['NpcFriendshipDialog']),
            ('blurMaskPrefab', receipt['roots']['PartnerFriendshipBlurMask']), ('blurMaterial', receipt['blur_material']),
            ('clickClip', receipt['click_audio']['pointer'])):
        raw += '  ' + field + ': {fileID: ' + str(ref['fileID']) + ', guid: ' + ref['guid'] + ', type: ' + str(ref['type']) + '}\n'
    raw += '  clickEventId: ' + str(receipt['click_audio']['event']['id']) + '\n'
    CATALOG.write_text(raw, 'utf8', newline='\n')
    metadata(CATALOG, 'NativeFormatImporter')
    for source in (CATALOG, Path(str(CATALOG) + '.meta')):
        installed[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
    receipt.update(installed_output_sha256=installed, reused_assets=reused)
    code = [str(ROOT / 'unity/Assets/Game/Runtime' / p) for p in NEW_SCRIPTS]
    code += [str(ROOT / p) for p in (
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldWindowMask.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldPopupWindow.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/PartnerWindowBlurCapture.cs',
        'unity/Assets/Game/Runtime/Presentation/World/OfficialDungeonPopupMask.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Partners.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.cs')]
    code += [str(ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI' / name) for name in (
        'OfflinePartnerUiSoundScope.cs', 'PartnerGiftPanelView.cs', 'PartnerDetailPanelView.cs')]
    receipt['runtime_code'] = {}
    for name in code:
        for source in (Path(name), Path(name + '.meta')):
            receipt['runtime_code'][source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original friendship window:', len(installed), 'files; reused', len(reused), 'assets', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    for relative in NEW_SCRIPTS:
        metadata(ROOT / 'unity/Assets/Game/Runtime' / relative)
    work = validation_directory()
    if args.install:
        install(work)
    else:
        PartnerFriendshipWindowBinding(work).build()


if __name__ == '__main__':
    main()
