"""Recover the original detail talk controller and its exact native subtree."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import yaml

from official_apk import ROOT
from prepare_partner_card_native import guid, sha, script_id, Dumper, identity
from prepare_partner_roster_native import PartnerRosterBinding
from prepare_partner_profile_native import PartnerProfileBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_input_dialog import metadata
from prepare_current_partner_native import documents
from validation_workspace import validation_directory


CODE = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Detail'
RECEIPT = ROOT / 'assets/reference/partner-detail-talk-source-manifest.json'


def controllers(work):
    inputs, outputs = {}, {}
    for name in ('NpcTalkUI', 'NpcCustomSizeController'):
        source = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source' / (name + '.cs')
        inputs[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
        text = source.read_text('utf-8-sig').replace('\r\n', '\n')
        text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = '// Migrated original detail presentation; see partner-detail-talk-source-manifest.json.\n' + \
            'namespace Zjcs.Official.PartnersUI\n{\nusing Zjcs.Official.WorldUI;\nusing Zjcs.Official.ProfessionUI;\n' + \
            'using Zjcs.Official.StoryBubbles;\nusing MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n' + \
            'using Object = UnityEngine.Object;\n' + text + '\n}\n'
        text = '\n'.join(line.expandtabs(4).rstrip() for line in text.splitlines()) + '\n'
        path = CODE / (name + '.cs')
        if not path.is_file() or path.read_text('utf8') != text: path.write_text(text, 'utf8', newline='\n')
        meta = metadata(path)
        meta.write_bytes(('\n'.join(line.rstrip() for line in meta.read_text('utf8').splitlines()) + '\n').encode())
        for entry in (path, meta): outputs[entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
    for relative in ('unity/Assets/Game/Runtime/Official/StoryBubbles/TMP_TypeWriterEffect.cs',
            'unity/Assets/Game/Runtime/Official/ProfessionUI/MonoDictionary.cs'):
        inputs[relative] = sha((ROOT / relative).read_bytes())
    assembly = CODE / 'Zjcs.Official.PartnerDetailUI.asmdef'
    data = json.loads(assembly.read_bytes())
    if 'Zjcs.Official.StoryBubbles' not in data['references']:
        data['references'].append('Zjcs.Official.StoryBubbles')
        assembly.write_text(json.dumps(data, indent=2) + '\n', 'utf8', newline='\n')
    outputs[assembly.relative_to(ROOT).as_posix()] = sha(assembly.read_bytes())
    return dict(inputs=inputs, outputs=outputs)


class DetailTalkBinding(PartnerProfileBinding):
    def load(self, source_guid):
        obj = self.objects.get(source_guid)
        if source_guid not in self.redirects and obj is not None and obj.type.name == 'AnimationClip':
            original = self.recovery.fields(obj)
            if original['m_Legacy']:
                # These are legacy Animation clips: the APK retains their actual
                # curve paths, rather than compiled Animator path hashes.
                result = PartnerGiftBinding.load(self, source_guid)
                _, _, docs = next(row for row in self.queue if row[1] == result['guid'])
                clip = docs[result['fileID']]['AnimationClip']
                if original['m_ClipBindingConstant']['genericBindings'] or clip['m_ClipBindingConstant']['genericBindings']:
                    raise ValueError('Review mixed legacy/compiled detail talk clip')
                for field in ('m_RotationCurves', 'm_CompressedRotationCurves', 'm_EulerCurves', 'm_PositionCurves',
                        'm_ScaleCurves', 'm_FloatCurves', 'm_PPtrCurves', 'm_EditorCurves', 'm_EulerEditorCurves'):
                    converted, source = clip.get(field, []), original.get(field, [])
                    if len(converted) != len(source): raise ValueError('Original legacy curve count differs: ' + field)
                    for index, (curve, raw) in enumerate(zip(converted, source)):
                        if curve.get('path', '') is None:
                            if raw.get('path') != '': raise ValueError('Legacy root is not the original empty path')
                            curve['path'] = ''
                            self.animation_root_paths.append(dict(original=identity(obj), field=field, curve_index=index,
                                original_path=raw['path'], restored='', evidence='APK legacy AnimationClip uncompressed curve path'))
                        elif curve.get('path', '') != raw.get('path', ''):
                            raise ValueError('Original legacy hierarchy path differs')
                return result
        return super().load(source_guid)

    def replace(self, value):
        if value == {'class': 'StateAnimationConfig', 'ns': '', 'asm': 'Framework'}:
            return {'class': 'StateAnimationConfig', 'ns': 'Zjcs.Official.PartnersUI', 'asm': 'Zjcs.Official.PartnersUI'}
        if value == {'class': 'StateAnimationConfig', 'ns': 'Zjcs.Official.PartnersUI', 'asm': 'Zjcs.Official.PartnersUI'}:
            return value
        return super().replace(value)

    def register_scripts(self):
        PartnerRosterBinding.register_scripts(self)
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {'NpcTalkUI': CODE / 'NpcTalkUI.cs',
            'TMP_TypeWriterEffect': runtime / 'StoryBubbles/TMP_TypeWriterEffect.cs',
            'UIBloomGraphicElement': runtime / 'ProfessionUI/UIBloomGraphicElement.cs'}
        for full_name, path in paths.items():
            namespace, _, name = full_name.rpartition('.')
            meta = Path(str(path) + '.meta')
            ref = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[full_name] = ref
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            self.class_ids[script_id(namespace, name)] = ref
            self.script_guids.add(ref['guid'])

    def friendship_templates(self):
        self.recovery.recover('NpcDetailPanel', self.work / 'detail-talk-native')
        self.recovery.finalize()
        path, _, data, receipt = self.recovery.outputs[0]
        consumer, = [row for row in data['scripts'] if row['type'] == 'NpcTalkUI']
        root = data['documents'][consumer['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']
        selected = set()

        def visit(go):
            selected.add(go)
            components = data['documents'][go]['GameObject']['m_Component']
            selected.update(row['component']['fileID'] for row in components)
            transform = next(iter(data['documents'][components[0]['component']['fileID']].values()))
            for child in transform['m_Children']:
                visit(next(iter(data['documents'][child['fileID']].values()))['m_GameObject']['fileID'])

        visit(root)
        panel = copy.deepcopy(data)
        panel['root'] = root
        panel['documents'] = {key: value for key, value in panel['documents'].items() if key in selected}
        panel['scripts'] = [row for row in panel['scripts'] if row['nativeFileID'] in selected]
        panel['objectMap'] = {key: value for key, value in panel['objectMap'].items() if value in selected}
        transform_id = panel['documents'][root]['GameObject']['m_Component'][0]['component']['fileID']
        transform = next(iter(panel['documents'][transform_id].values()))
        old_parent = transform['m_Father']; transform['m_Father'] = dict(fileID=0)

        def check(value):
            if isinstance(value, dict):
                if set(value) == {'fileID'} and value['fileID'] and value['fileID'] not in selected:
                    raise ValueError('Original talk reference leaves subtree: ' + repr(value))
                for entry in value.values(): check(entry)
            elif isinstance(value, list):
                for entry in value: check(entry)

        check(panel['documents'])
        self.template_selection = dict(consumer=consumer['original'], original_parent=old_parent,
            root=root, mapped_objects=len(selected), adaptation='Detach original talk root parent only; preserve every child and binding.')
        self.inputs[receipt['nativeSource']] = sha(path.read_bytes())
        self.inputs[receipt['nativeSource'] + '.meta'] = sha(Path(str(path) + '.meta').read_bytes())
        cache = self.recovery.proof['component_cache']; self.inputs[cache['path']] = cache['sha256']
        return [('NpcDetailTalk', panel)]


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    if json.loads((ROOT / 'official-source.json').read_bytes())['active'] != '884_156692':
        raise ValueError('Review selected detail talk version')
    if not args.install:
        code = controllers(work)
        binding = DetailTalkBinding(work, CODE)
        binding.build()
        proof = json.loads((work / 'partner-gift-native-source-manifest.json').read_bytes())
        prefab = work / 'gift-assets/NpcDetailTalk.prefab'
        raw = prefab.read_bytes()
        original, = [ROOT / relative for relative in proof['inputs'] if relative.endswith('/NpcDetailPanel.prefab')]
        native = documents(original.read_text('utf8'))
        fields = [int(fid) for fid, body in re.findall(rb'^--- !u!114 &(-?\d+)\n(.*?)(?=^--- !u!|\Z)', raw, re.M | re.S)
            if b'  myKey: null\n' in body]
        if any(native[fid]['MonoBehaviour']['myKey'] != '' for fid in fields): raise ValueError('Only restore original empty text keys')
        raw = raw.replace(b'  myKey: null\n', b"  myKey: ''\n")
        prefab.write_bytes(raw); proof['outputs'][prefab.name] = sha(raw)
        proof.update(controllers=code, animation_root_path_repairs=binding.animation_root_paths,
            empty_language_key_restoration=fields, generator_sha256=sha(Path(__file__).read_bytes()))
        for name in ('prepare_partner_card_native.py', 'prepare_partner_gift_native.py', 'prepare_partner_roster_native.py',
                'prepare_partner_profile_native.py', 'prepare_partner_input_dialog.py'):
            relative = 'scripts/' + name
            proof['inputs'][relative] = sha((ROOT / relative).read_bytes())
        (work / 'partner-detail-talk-native-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Restored original detail talk controller and exact native subtree')
        return
    proof = json.loads((work / 'partner-detail-talk-native-source-manifest.json').read_bytes())
    if proof['generator_sha256'] != sha(Path(__file__).read_bytes()): raise ValueError('Rebuild detail talk generator')
    for relative, expected in dict(proof['inputs'], **proof['controllers']['inputs']).items():
        if sha((ROOT / relative).read_bytes()) != expected: raise ValueError('Detail talk source changed: ' + relative)
    existing = {guid(meta): Path(str(meta)[:-5]) for meta in (ROOT / 'unity/Assets/Official/Partners').rglob('*.meta') if Path(str(meta)[:-5]).is_file()}
    previous = json.loads(RECEIPT.read_bytes()).get('installed_output_sha256', {}) if RECEIPT.is_file() else {}
    installed, reused, canonical = {}, [], []
    destination = ROOT / 'unity/Assets/Official/Partners/UI/DetailTalk'
    for relative, expected in proof['outputs'].items():
        if relative.endswith('.meta') or relative == 'partner-gift-texts.json': continue
        source = work / 'gift-assets' / relative
        raw = source.read_bytes()
        if sha(raw) != expected: raise ValueError('Staged detail talk asset changed: ' + relative)
        key = guid(Path(str(source) + '.meta'))
        target = existing.get(key, destination / relative)
        if target.is_file():
            old = target.read_bytes()
            if old != raw:
                if target.suffix != '.controller' or old.replace(b'  m_Name: null\n', b"  m_Name: ''\n") != raw:
                    if sha(old) != previous.get(target.relative_to(ROOT).as_posix()): raise ValueError('Preserve shared detail talk asset: ' + str(target))
                    target.write_bytes(raw)
                else: canonical.append(dict(path=target.relative_to(ROOT).as_posix(), staged_sha256=sha(raw), installed_sha256=sha(old)))
            reused.append(target.relative_to(ROOT).as_posix())
        else:
            target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(raw)
            Path(str(target) + '.meta').write_bytes(Path(str(source) + '.meta').read_bytes())
        for entry in (target, Path(str(target) + '.meta')): installed[entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
        existing[key] = target
    for relative, expected in proof['controllers']['outputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected: raise ValueError('Detail talk controller changed: ' + relative)
        installed[relative] = expected
    def write(path, raw):
        relative = path.relative_to(ROOT).as_posix()
        if path.is_file() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(relative):
            raise ValueError('Preserve unrecorded detail talk output: ' + relative)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.is_file() or path.read_bytes() != raw: path.write_bytes(raw)
        installed[relative] = sha(path.read_bytes())

    rules_path = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    rules = json.loads(rules_path.read_bytes())
    if rules['manifest'] != '884_156692': raise ValueError('Original NPC talk configuration version differs')
    proof['inputs'][rules_path.relative_to(ROOT).as_posix()] = sha(rules_path.read_bytes())
    original = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/NpcDetailPanelDetailContent.cs'
    if '"npc_talk_{num}"' not in original.read_text('utf-8-sig'): raise ValueError('Original detail talk key changed')
    proof['inputs'][original.relative_to(ROOT).as_posix()] = sha(original.read_bytes())
    texts = destination / 'partner-detail-talk-texts.json'
    write(texts, (json.dumps(dict(manifest=rules['manifest'], texts=[dict(key='npc_talk_' + str(row['Id']), text=row['Text'])
        for row in rules['tables']['npc_talk']]), ensure_ascii=False, indent=2) + '\n').encode())
    meta = metadata(texts); installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerDetailTalkAssetCatalog.cs'
    script_meta = metadata(script)
    script_meta.write_bytes(('\n'.join(line.rstrip() for line in script_meta.read_text('utf8').splitlines()) + '\n').encode())
    body = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(script_meta), type=3), m_Name='OfficialPartnerDetailTalkAssetCatalog',
        m_EditorClassIdentifier='', talkPrefab=proof['roots']['NpcDetailTalk'],
        texts=dict(fileID=4900000, guid=guid(meta), type=3))
    catalog = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerDetailTalkAssetCatalog.asset'
    write(catalog, ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' +
        yaml.dump(dict(MonoBehaviour=body), Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode())
    for entry in (script, script_meta, metadata(catalog, 'NativeFormatImporter')):
        installed[entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
    for folder in (destination, *[p for p in destination.rglob('*') if p.is_dir()]):
        meta = metadata(folder, 'DefaultImporter', True); installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    proof.update(installed_output_sha256=installed, reused=reused, canonical=canonical,
        scope='Exact original detail talk region/controller and custom NPC size controller. Full detail host and live portrait remain pending.')
    proof['validation_inputs'] = {relative: sha((ROOT / relative).read_bytes()) for relative in (
        'scripts/prepare_partner_detail_talk_validation.py', 'scripts/fixtures/partner_detail_talk_validation.cs')}
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original detail talk:', len(installed), 'paths')


if __name__ == '__main__': main()
