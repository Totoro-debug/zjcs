"""Bind the complete original output-addition popup and referenced row prefabs."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from official_source import OfficialSource
from official_tables import Constructors
from prepare_map16_story_source import decode_localization
from prepare_current_partner_native import PartnerNativeRecovery, identity
from prepare_partner_card_native import PartnerCardBinding, Dumper, guid, script_id, sha
from prepare_home_output_detail_runtime import metadata
from validation_workspace import validation_directory

DEST = ROOT / 'unity/Assets/Official/Home/UI/OutputDetail'
RECEIPT = ROOT / 'assets/reference/home-output-detail-native-source-manifest.json'
PREFABS = [('PopTipOutputAddition', 'Tips/PopTip', 'tips'),
           ('OutputAdditionItem', 'Item', 'item'), ('OutputAdditionPropItem', 'Item', 'item')]


class OutputDetailBinding(PartnerCardBinding):
    def register_scripts(self):
        super().register_scripts()
        paths = {name: ROOT / 'unity/Assets/Game/Runtime/Official/HomeOutputDetailUI' / (name + '.cs')
                 for name, _, _ in PREFABS}
        paths.update({name: ROOT / 'unity/Assets/Game/Runtime/Official' / relative for name, relative in {
            'LoopVerticalScrollWithMaxHeight': 'SkillsUI/LoopVerticalScrollWithMaxHeight.cs',
            'PopupWindow': 'WorldUI/WorldPopupWindow.cs', 'UIBloomGroup': 'WorldUI/WorldUIBloomGroup.cs',
            'MirrorImage': 'WorldUI/MirrorImage.cs', 'PreviewTool': 'ProfessionUI/PreviewTool.cs',
            'ShowRemainTimeInfo': 'ProfessionUI/ShowRemainTimeInfo.cs'}.items()})
        for name, path in paths.items():
            ref = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = ref
            self.class_ids[script_id('', name)] = ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def build(self):
        recovery = self.recovery
        for name, area, bundle in PREFABS:
            recovery.recover(name, self.work / 'native', bundle='defaultpackage_assets_prefabs_ui_' + bundle + '.bundle',
                native_relative='Prefabs/UI/' + area + '/' + name + '.prefab')
        recovery.finalize()
        roots = {}
        for native, _, panel, proof in recovery.outputs:
            name = proof['name']
            root_guid = hashlib.md5(('zjcs:home-output-detail:' + name).encode()).hexdigest()
            roots[name] = dict(fileID=panel['root'], guid=root_guid, type=3)
            self.redirects[guid(Path(str(native) + '.meta'))] = roots[name]
            self.script_guids.add(root_guid)
            # A serialized reference can target the row controller, not only its
            # root GameObject. Retain each exact original component identity.
            for key, file_id in panel['objectMap'].items():
                self.original_refs[key] = dict(fileID=file_id, guid=root_guid, type=3)
            for key, pointer in panel['external'].items():
                self.objects[pointer['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
        prefabs = []
        used = set()
        for _, _, panel, proof in recovery.outputs:
            for script in panel['scripts']:
                full = '.'.join(filter(None, (script['namespace'], script['type'])))
                used.add(full)
                panel['documents'][script['nativeFileID']]['MonoBehaviour']['m_Script'] = self.scripts[full]
            prefabs.append((Path(proof['name'] + '.prefab'), roots[proof['name']]['guid'], self.replace(panel['documents'])))
        # Queue expansion resolves the complete resource dependency closure.
        for _, _, data in self.queue:
            if isinstance(data, dict):
                updated = self.replace(data)
                data.clear(); data.update(updated)
        self.queue.extend(prefabs)

        code = ROOT / '.tools/source-packages/current' / recovery.apk.version / 'official-code'
        constructors = Constructors(code)
        obj = recovery.apk.asset('config_luban', '/output_addition.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        rows = constructors.table(raw, 'TbOutputAdditionInfo')
        keys = {doc['MonoBehaviour']['myKey'] for _, _, panel, _ in recovery.outputs
                for doc in panel['documents'].values() if doc.get('MonoBehaviour', {}).get('myKey')}
        keys.update(('DayCountdown', 'DayCountdown2', 'time_hour', 'time_minute', 'time_second', 'time_end'))
        enum_names = {}
        for name in ('OutputActionType', 'OutputGroupType', 'OutputAdditionPropType'):
            path = code / 'common-source/Common' / (name + '.cs')
            text = path.read_text('utf-8-sig')
            members, value = {}, 0
            for member, number in re.findall(r'^\s*(\w+)(?:\s*=\s*(\d+))?,?\s*$', text.split('{', 1)[1].rsplit('}', 1)[0], re.M):
                value = int(number) if number else value
                members[value] = member; value += 1
            enum_names[name] = members
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        for row in rows:
            keys.add('OutputActionType.' + enum_names['OutputActionType'][row['OutputActionType']] + '.Name')
            keys.add('OutputAdditionPropType.' + enum_names['OutputAdditionPropType'][row['OutputAdditionPropType']])
            if row['ShowKey']:
                keys.add('OutputGroupType.' + enum_names['OutputGroupType'][row['OutputGroupType']] + '_' + row['ShowKey'])
        literals = {'{0}', '{0}<color=#e37d38>+{1}%</color>'}
        texts, language = decode_localization(recovery.apk, keys - literals, set())
        # Optional/missing source text follows the original localization fallback;
        # it is recorded explicitly, never replaced with invented UI copy.
        payload = dict(texts=[dict(key=key, text=value) for key, value in sorted(texts.items())])
        self.queue.append((Path('texts.json'), hashlib.md5(b'zjcs:home-output-detail:texts').hexdigest(),
            (json.dumps(payload, ensure_ascii=False, indent=2) + '\n').encode('utf8')))
        folder = self.work / 'assets'
        outputs = {}
        for relative, asset_guid, data in self.queue:
            path = folder / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            if isinstance(data, dict):
                text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id, document in data.items():
                    kind, = document
                    text += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                    text += yaml.dump(document, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
                data = text.encode('utf8')
            path.write_bytes(data)
            importer = ('PrefabImporter' if path.suffix == '.prefab' else 'ShaderImporter' if path.suffix == '.shader'
                else 'TextScriptImporter' if path.suffix == '.json' else 'TrueTypeFontImporter' if path.suffix in ('.ttf', '.otf') else 'NativeFormatImporter')
            Path(str(path) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + asset_guid + '\n' + importer + ':\n  externalObjects: {}\n', 'utf8', newline='\n')
            for item in (path, Path(str(path) + '.meta')):
                outputs[item.relative_to(folder).as_posix()] = sha(item.read_bytes())
        for path in (Path(__file__), ROOT / 'scripts/prepare_partner_card_native.py', ROOT / 'scripts/current_component_schemas.py'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        proof = dict(manifest=recovery.apk.version, generator_sha256=sha(Path(__file__).read_bytes()),
            inputs=self.inputs, native_recovery=recovery.proof, objects=self.proof,
            script_bindings={key: self.script_sources[key] for key in sorted(used)},
            roots=roots, outputs=outputs, language=language, absent_text_keys=sorted(keys - literals - texts.keys()),
            configuration=dict(source=recovery.apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), payload_sha256=sha(raw)),
            scope='Complete original popup and row hierarchy with active-source dependencies; runtime and Player verification remain separate.')
        (self.work / RECEIPT.name).write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Bound complete original output detail:', len(outputs), 'files', flush=True)


def install(work):
    proof = json.loads((work / RECEIPT.name).read_bytes())
    previous = json.loads(RECEIPT.read_bytes()).get('installed_output_sha256', {}) if RECEIPT.exists() else {}
    if proof['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild the changed output-detail binding')
    for relative, digest in proof['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != digest:
            raise ValueError('Original output-detail input changed: ' + relative)
    # Existing GUIDs can only be reused with equivalent serialized values.
    from prepare_current_partner_native import documents
    existing = {}
    asset_root = ROOT / 'unity/Assets'
    scan = subprocess.run(['rg', '--hidden', '--no-ignore', '--with-filename', '--no-heading', '--color', 'never',
        '--max-count', '1', '--glob', '*.meta', r'^guid: [a-f0-9]{32}\r?$', str(asset_root)],
        capture_output=True, text=True, encoding='utf8', check=True)
    for line in scan.stdout.splitlines():
        name, key = line.rsplit(':guid: ', 1)
        meta = Path(name)
        path = Path(str(meta)[:-5])
        if path.is_file():
            key = key.strip()
            if key in existing:
                raise ValueError('Existing Unity GUID collision: ' + key)
            existing[key] = path
    installed, reused, plan = {}, [], []
    for relative, digest in proof['outputs'].items():
        source = work / 'assets' / relative
        if sha(source.read_bytes()) != digest:
            raise ValueError('Output-detail staging changed: ' + relative)
        if relative.endswith('.meta'):
            continue
        key = guid(Path(str(source) + '.meta'))
        target = existing.get(key, DEST / relative)
        replace_owned = False
        if target.exists():
            left, right = target.read_bytes(), source.read_bytes()
            equal = left.replace(b'\r\n', b'\n') == right.replace(b'\r\n', b'\n')
            if not equal and left.startswith(b'%YAML') and right.startswith(b'%YAML'):
                equal = documents(left.decode('utf8')) == documents(right.decode('utf8'))
            if not equal:
                if not target.is_relative_to(DEST) or sha(left) != previous.get(target.relative_to(ROOT).as_posix()):
                    raise ValueError('Preserve different existing output-detail dependency: ' + str(target))
                replace_owned = True
                plan.extend([(source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))])
            else:
                reused.append(dict(path=target.relative_to(ROOT).as_posix(), guid=key, sha256=sha(left), staged_sha256=digest))
        else:
            plan.extend([(source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))])
        if target.is_relative_to(DEST):
            for item, origin in ((target, source), (Path(str(target) + '.meta'), Path(str(source) + '.meta'))):
                installed[item.relative_to(ROOT).as_posix()] = sha((item if item.exists() and not replace_owned else origin).read_bytes())
        existing[key] = target
    for source, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
    for folder in (DEST, *(path for path in DEST.rglob('*') if path.is_dir())):
        path = metadata(folder, True)
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Official/HomeOutputDetailUI/OfficialHomeOutputDetailAssets.cs'
    catalog = ROOT / 'unity/Assets/Game/Resources/Home/OfficialHomeOutputDetailAssets.asset'
    popup = proof['roots']['PopTipOutputAddition']
    text_guid = hashlib.md5(b'zjcs:home-output-detail:texts').hexdigest()
    raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
    raw += '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
    raw += '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
    raw += '  m_Script: {fileID: 11500000, guid: ' + guid(Path(str(script) + '.meta')) + ', type: 3}\n'
    raw += '  m_Name: OfficialHomeOutputDetailAssets\n  m_EditorClassIdentifier: \n'
    raw += '  popup: {fileID: ' + str(popup['fileID']) + ', guid: ' + popup['guid'] + ', type: 3}\n'
    raw += '  texts: {fileID: 4900000, guid: ' + text_guid + ', type: 3}\n'
    # Existing installed catalog changes must remain attributable to this module.
    if catalog.exists() and sha(catalog.read_bytes()) != previous.get(catalog.relative_to(ROOT).as_posix()):
        raise ValueError('Preserve changed output-detail catalog')
    catalog.write_text(raw, 'utf8', newline='\n')
    for path in (catalog, metadata(catalog)):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    proof.update(installed_output_sha256=installed, reused=reused)
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original output detail:', len(installed), 'files; reused', len(reused), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    if args.install:
        install(work)
    else:
        cache = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'home-output-detail'
        recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle='defaultpackage_assets_prefabs_ui_tips.bundle')
        try:
            OutputDetailBinding(work, recovery=recovery).build()
        finally:
            recovery.apk.reader.close()
