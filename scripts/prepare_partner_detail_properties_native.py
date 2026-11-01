"""Bind the exact property subtree of the selected official NPC detail prefab."""
import copy
import argparse
import hashlib
import json
from pathlib import Path

from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_gift_controller_native import PartnerGiftControllerBinding
from prepare_partner_card_native import ROOT, guid, sha, script_id
from validation_workspace import validation_directory


class PartnerDetailPropertiesBinding(PartnerGiftBinding):
    def __init__(self, work, code):
        self.code = code
        super().__init__(work)

    def load(self, source_guid):
        try:
            obj = self.objects.get(source_guid)
            if source_guid not in self.redirects and obj is not None and obj.type.name == 'AnimatorController':
                return PartnerGiftControllerBinding.controller(self, source_guid, obj)
            return super().load(source_guid)
        except (ValueError, KeyError) as error:
            obj = self.objects.get(source_guid)
            detail = '' if obj is None else obj.type.name + ' ' + str(obj.read(check_read=False).m_Name)
            raise ValueError('Detail dependency ' + source_guid + ' ' + detail + ': ' + str(error)) from error

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {
            'NpcDetailPanelPropContent': self.code / 'NpcDetailPanelPropContent.cs',
            'UIBloomGraphicElement': runtime / 'ProfessionUI/UIBloomGraphicElement.cs',
            'PreviewTool': runtime / 'World/Map13MiniMapWindows/PreviewTool.cs',
            'ClampingScrollRect': runtime / 'WorldUI/WorldClampingScrollRect.cs',
        }
        for name, path in paths.items():
            reference = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = reference
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=reference,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))
            self.class_ids[script_id('', name)] = reference
            self.script_guids.add(reference['guid'])

    def friendship_templates(self):
        self.recovery.recover('NpcDetailPanel', self.work / 'partner-native')
        self.recovery.recover('NpcFriendshipPropItemGroup', self.work / 'partner-native')
        self.recovery.finalize()
        detail, group = [entry[2] for entry in self.recovery.outputs]
        consumer, = [row for row in detail['scripts'] if row['type'] == 'NpcDetailPanelPropContent']
        root = detail['documents'][consumer['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']
        selected = set()

        def visit(go_id):
            selected.add(go_id)
            components = detail['documents'][go_id]['GameObject']['m_Component']
            selected.update(row['component']['fileID'] for row in components)
            transform = next(iter(detail['documents'][components[0]['component']['fileID']].values()))
            for child in transform['m_Children']:
                visit(next(iter(detail['documents'][child['fileID']].values()))['m_GameObject']['fileID'])

        visit(root)
        panel = copy.deepcopy(detail)
        panel['root'] = root
        panel['documents'] = {key: value for key, value in panel['documents'].items() if key in selected}
        panel['scripts'] = [row for row in panel['scripts'] if row['nativeFileID'] in selected]
        panel['objectMap'] = {key: value for key, value in panel['objectMap'].items() if value in selected}
        transform_id = panel['documents'][root]['GameObject']['m_Component'][0]['component']['fileID']
        transform = next(iter(panel['documents'][transform_id].values()))
        old_parent = transform['m_Father']
        transform['m_Father'] = dict(fileID=0)

        def check(value):
            if isinstance(value, dict):
                if set(value) == {'fileID'} and value['fileID'] and value['fileID'] not in selected:
                    raise ValueError('Property subtree references outside its original hierarchy: ' + repr(value))
                for item in value.values():
                    check(item)
            elif isinstance(value, list):
                for item in value:
                    check(item)

        check(panel['documents'])
        self.template_selection = dict(consumer=consumer['original'], source_name=panel['documents'][root]['GameObject']['m_Name'],
            native_root=root, original_parent=old_parent, detached_parent=dict(fileID=0), mapped_objects=len(selected),
            adaptation='Exact original property subtree; detach root parent only. Preserve geometry, components and references.')
        for native, _, data, record in self.recovery.outputs:
            self.inputs[record['nativeSource']] = sha(native.read_bytes())
            self.inputs[record['nativeSource'] + '.meta'] = sha(Path(str(native) + '.meta').read_bytes())
            if data is group:
                self.redirects[guid(Path(str(native) + '.meta'))] = dict(fileID=group['root'],
                    guid=hashlib.md5(b'zjcs:partner-current:NpcFriendshipPropItemGroup.prefab').hexdigest(), type=3)
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        return [('NpcDetailPropContent', panel), ('NpcFriendshipPropItemGroup', group)]


def finish_staged(work):
    proof = json.loads((work / 'partner-gift-native-source-manifest.json').read_bytes())
    repairs = {}
    from prepare_current_partner_native import documents
    import re
    for name, original in (('NpcDetailPropContent', 'NpcDetailPanel'), ('NpcFriendshipPropItemGroup', 'NpcFriendshipPropItemGroup')):
        path = work / 'gift-assets' / (name + '.prefab')
        raw = path.read_bytes()
        if b'  myKey: null\n' not in raw:
            continue
        candidates = [ROOT / relative for relative in proof['inputs'] if relative.endswith('/' + original + '.prefab')]
        native_path, = candidates
        if sha(native_path.read_bytes()) != proof['inputs'][native_path.relative_to(ROOT).as_posix()]:
            raise ValueError('Original detail text input changed')
        native = documents(native_path.read_text('utf8'))
        fields = [int(file_id) for file_id, body in re.findall(rb'^--- !u!114 &(-?\d+)\n(.*?)(?=^--- !u!|\Z)', raw, re.M | re.S)
            if b'  myKey: null\n' in body]
        if any(native[file_id]['MonoBehaviour']['myKey'] != '' for file_id in fields):
            raise ValueError('Only restore original empty language keys')
        repaired = raw.replace(b'  myKey: null\n', b"  myKey: ''\n")
        path.write_bytes(repaired)
        repairs[path.name] = dict(previous_sha256=sha(raw), sha256=sha(repaired), fields=fields,
            source=native_path.relative_to(ROOT).as_posix())
        proof['outputs'][path.name] = sha(repaired)
    proof['empty_language_key_repairs'] = repairs
    proof['base_generator_sha256'] = proof['generator_sha256']
    proof['generator_sha256'] = sha(Path(__file__).read_bytes())
    for name in ('prepare_partner_gift_controller_native.py', 'partner_gift_animator_binding.py'):
        path = ROOT / 'scripts' / name
        proof['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    proof['scope'] = 'Exact current property subtree and original dependencies staged; local service and runtime verification pending.'
    (work / 'partner-detail-properties-native-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')


def install_staged(work):
    native = json.loads((work / 'partner-detail-properties-native-source-manifest.json').read_bytes())
    code = json.loads((work / 'partner-detail-properties-ui-source-manifest.json').read_bytes())
    installed_proof = ROOT/'assets/reference/partner-detail-properties-native-source-manifest.json'
    previous = json.loads(installed_proof.read_bytes()).get('installed_output_sha256', {}) if installed_proof.is_file() else {}
    for relative, expected in dict(native['inputs'], **code['inputs']).items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Detail property input changed: ' + relative)
    if native['generator_sha256'] != sha(Path(__file__).read_bytes()) or code['generator_sha256'] != sha((ROOT/'scripts/prepare_partner_detail_properties_ui.py').read_bytes()):
        raise ValueError('Rebuild detail property generators before installing')
    for name, binding in native['script_bindings'].items():
        path = ROOT/binding['path']
        if sha(path.read_bytes()) != binding['sha256'] or sha(Path(str(path)+'.meta').read_bytes()) != binding['meta_sha256']:
            raise ValueError('Rebuild the native detail binding after its script changes: '+name)
    existing = {}
    for metadata in (ROOT/'unity/Assets/Official/Partners').rglob('*.meta'):
        path = Path(str(metadata)[:-5])
        if path.is_file():
            key = guid(metadata)
            if key in existing:
                raise ValueError('Existing partner asset GUID collision')
            existing[key] = path
    destination = ROOT/'unity/Assets/Official/Partners/UI/DetailProperties'
    plan, installed, reused, folders = [], {}, [], {}
    for relative, expected in native['outputs'].items():
        source = work/'gift-assets'/relative
        if sha(source.read_bytes()) != expected:
            raise ValueError('Staged detail asset differs: ' + relative)
        if relative.endswith('.meta'):
            continue
        target = existing.get(guid(Path(str(source)+'.meta')), destination/relative)
        if target.is_file() and target.read_bytes() != source.read_bytes():
            raise ValueError('Existing detail dependency has different bytes: ' + str(target))
        if target.is_file():
            reused.append(target.relative_to(ROOT).as_posix())
        plan.append((source, target))
        plan.append((Path(str(source)+'.meta'), Path(str(target)+'.meta')))
    for relative, expected in code['outputs'].items():
        source = work/'detail-property-code'/Path(relative).name
        if sha(source.read_bytes()) != expected:
            raise ValueError('Staged detail code differs: ' + relative)
        plan.append((source, ROOT/relative))
    for source, target in plan:
        if target.is_file() and target.read_bytes().replace(b'\r\n',b'\n') != source.read_bytes().replace(b'\r\n',b'\n'):
            if target.relative_to(ROOT).as_posix() not in code['outputs'] or sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
                raise ValueError('Refusing to overwrite a detail file: ' + str(target))
    for source, target in plan:
        if not target.is_file() or target.relative_to(ROOT).as_posix() in code['outputs']:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read_bytes())
        installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes())
        parent = target.parent
        while parent.is_relative_to(destination) or parent == ROOT/'unity/Assets/Game/Runtime/Official/PartnersUI/Detail':
            metadata = Path(str(parent)+'.meta')
            if not metadata.is_file():
                metadata.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('zjcs:partner-detail-folder:'+parent.relative_to(ROOT).as_posix()).encode()).hexdigest()
                    +'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
            folders[metadata.relative_to(ROOT).as_posix()] = sha(metadata.read_bytes())
            parent = parent.parent
    native.update(installed_output_sha256=installed, installed_folder_sha256=folders, reused=reused, controller_source=code,
        validation_source_sha256={path.relative_to(ROOT).as_posix():sha(path.read_bytes()) for path in (
            ROOT/'scripts/prepare_partner_detail_properties_validation.py', ROOT/'scripts/fixtures/partner_detail_properties_validation.cs')},
        local_dependencies_sha256={path.relative_to(ROOT).as_posix():sha(path.read_bytes()) for path in (
            ROOT/'assets/reference/partner-properties-source-manifest.json',
            ROOT/'unity/Assets/Game/Runtime/Offline/PartnersUI/Zjcs.Offline.PartnersUI.asmdef')},
        scope='Original current detail-property region and selected formula installed; formal complete detail routing and Player remain pending.')
    (ROOT/'assets/reference/partner-detail-properties-native-source-manifest.json').write_text(
        json.dumps(native, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Installed detail property code/native region:',len(installed),'files; reused',len(reused),'assets')


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--finish-staged', action='store_true')
    parser.add_argument('--install-staged', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    if args.install_staged:
        install_staged(work)
    else:
        if not args.finish_staged:
            binding = PartnerDetailPropertiesBinding(work, work / 'detail-property-code')
            binding.build()
        finish_staged(work)


if __name__ == '__main__':
    main()
