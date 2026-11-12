"""Recover original task reward attachments selected by ItemIconAttachmentConfig."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess

import yaml
from UnityPy.enums import ClassIDType
from official_manifest import read_asset_manifest
from prepare_home_currency_windows_native import HomeCurrencyWindowsBinding
from prepare_partner_card_native import ROOT, Dumper, guid, sha
from prepare_home_bottom_bar_validation import equivalent
from prepare_partner_gift_configuration import normalize
from validation_workspace import validation_directory
from npc_travel_language import language

PROOF = ROOT / 'assets/reference/home-task-reward-attachments-source-manifest.json'
CATALOG = 'Assets/Game/Resources/Home/OfflineHomeTaskRewardAttachments.asset'


class TaskAttachments(HomeCurrencyWindowsBinding):
    def register_scripts(self):
        super().register_scripts()
        from prepare_partner_card_native import script_id
        path = ROOT / 'unity/Assets/Game/Runtime/Official/HomeTaskUI/MultiLanguageTransformControler.cs'
        source = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/MultiLanguageTransformControler.cs'
        original = source.read_text('utf-8-sig')
        text = normalize(original).replace('using Framework;\n', '').replace('using Framework.DependencyInjection;\n', '')
        text = text.replace('    [Inject]\n', '').replace('MonoDictionary<LanguageGroup, TransformSetting>', 'MonoDictionary<int, TransformSetting>')
        text = text.replace('private IScriptableObjectConfig<LanguageGroupConfig> LanguageGroupConfig { get; set; }',
            'public Func<LanguageType, int> GetLanguageGroup { get; set; }')
        text = text.replace('protected ILanguageTypeService LanguageTypeService', 'public ILanguageTypeService LanguageTypeService')
        text = text.replace('LanguageGroup languageGroupByType = LanguageGroupConfig.Value.GetLanguageGroupByType(LanguageTypeService.CurrentLanguage);',
            'int languageGroupByType = GetLanguageGroup(LanguageTypeService.CurrentLanguage);')
        assert 'LanguageGroupConfig' not in text and '[Inject]' not in text
        path.write_text('using Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.World.NativeLabelUI.Framework;\n'
            + 'namespace Zjcs.Official.HomeTaskUI\n{\nusing Object = UnityEngine.Object;\n' + text + '\n}\n', 'utf8', newline='\n')
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:task-attachment-language-controller').hexdigest()
                + '\nMonoImporter:\n  serializedVersion: 2\n  externalObjects: {}\n', 'utf8', newline='\n')
        pointer = dict(fileID=11500000, guid=guid(meta), type=3)
        self.scripts['MultiLanguageTransformControler'] = pointer
        self.class_ids[script_id('', 'MultiLanguageTransformControler')] = pointer
        self.script_guids.add(pointer['guid'])
        self.script_sources['MultiLanguageTransformControler'] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
            sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
        self.inputs[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())

    def build(self):
        for relative in ('scripts/prepare_home_currency_windows_native.py', 'scripts/prepare_home_bottom_bar_native.py',
                'scripts/prepare_partner_gift_native.py', 'scripts/prepare_partner_gift_configuration.py',
                'scripts/prepare_partner_card_native.py', 'scripts/prepare_home_bottom_bar_validation.py',
                'scripts/npc_travel_language.py'):
            self.inputs[relative] = sha((ROOT / relative).read_bytes())
        original_groups = ROOT / '.tools/source-packages/current/884_156692/official-code/launch-source/LanguageGroupConfig.cs'
        assert 'return LanguageGroup.Zh;' in original_groups.read_text('utf-8-sig')
        self.inputs[original_groups.relative_to(ROOT).as_posix()] = sha(original_groups.read_bytes())
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        group_asset, = [row for row in records if Path(row['path']).name == 'LanguageGroupConfig.asset']
        groups, = [ref.deref() for path, ref in self.recovery.apk.container(group_asset['bundle'])
            if path.casefold() == group_asset['path'].casefold() and ref.deref().type.name == 'MonoBehaviour']
        group_fields = self.recovery.fields(groups)
        texts, text_sources = language({'ui_ItemIcon_Probability'})
        assert set(texts) == {'ui_ItemIcon_Probability'}
        self.inputs.update(text_sources)
        configs = {obj.read(check_read=False).m_Name: obj for obj in self.recovery.apk.objects(
            'defaultpackage_assets_scriptableconfig_modules_itemicon.bundle') if obj.type.name == 'MonoBehaviour'}
        config = configs['ItemIconAttachmentConfig']
        definitions = self.recovery.fields(config)['Templates']
        attachments = {}
        for kind in (2, 7):
            definition, = [row for row in definitions if row['Type'] == kind]
            record, = [row for row in records if row['guid'] == definition['GuidProxy']['mAssetGuid']]
            obj, = [ref.deref() for path, ref in self.recovery.apk.container(record['bundle'])
                if path.casefold() == record['path'].casefold() and ref.deref().type.name == 'GameObject']
            name = obj.read().m_Name
            self.recovery.recover(name, self.work / 'task-reward-attachments-native',
                bundle=record['bundle'], native_relative=record['path'].removeprefix('Assets/'))
            attachments[str(kind)] = dict(definition=definition, asset=record, original=self.recovery.apk.provenance(obj), name=name)
        self.recovery.finalize()
        prefabs = []
        for native, _, panel, _ in self.recovery.outputs:
            for path in (native, Path(str(native) + '.meta')):
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            for key, external in panel['external'].items():
                self.objects[external['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
            for script in panel['scripts']:
                name = '.'.join(filter(None, (script['namespace'], script['type'])))
                panel['documents'][script['nativeFileID']]['MonoBehaviour']['m_Script'] = self.scripts[name]
            name = native.stem
            key = hashlib.md5(('zjcs:task-reward-attachment:' + name).encode()).hexdigest()
            prefabs.append((Path(name + '.prefab'), key, self.replace(panel['documents'])))
            selected, = [row for row in attachments.values() if row['name'] == name]
            selected['pointer'] = dict(fileID=panel['root'], guid=key, type=3)
        for _, _, payload in self.queue:
            if isinstance(payload, dict):
                fixed = self.replace(payload); payload.clear(); payload.update(fixed)
        self.queue.extend(prefabs)
        folder = self.work / 'task-reward-attachments'
        outputs = {}
        for target, key, payload in self.queue:
            path = folder / target
            path.parent.mkdir(parents=True, exist_ok=True)
            if isinstance(payload, dict):
                text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id, content in payload.items():
                    kind, = content
                    text += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                    text += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
                raw = text.encode('utf8')
            else:
                raw = payload
            path.write_bytes(raw)
            Path(str(path) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + key
                + '\nNativeFormatImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
            for value in (path, Path(str(path) + '.meta')):
                outputs[value.relative_to(folder).as_posix()] = sha(value.read_bytes())
        used = set()
        for name in outputs:
            if name.endswith('.meta'): continue
            used.update(re.findall(rb'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', (folder / name).read_bytes()))
        scripts = {name: row for name, row in self.script_sources.items() if row['pointer']['guid'].encode() in used}
        receipt = dict(manifest=self.recovery.apk.version, generator_sha256=sha(Path(__file__).read_bytes()),
            inputs=self.inputs, outputs=outputs, attachments=attachments, objects=self.proof, script_bindings=scripts,
            configuration=dict(source=self.recovery.apk.provenance(config), sha256=sha(config.get_raw_data())),
            recovery=self.recovery.proof, texts=texts,
            adaptations=['Original language transform controller retains fields, lifecycle, default snapshot, comparisons and layout updates.',
                'Inject language service and a group lookup delegate. Serialized group enums retain their original integer values.',
                'Original language group table and Zh fallback are preserved; the icon pool forwards host language changes.',
                'Attachment prefabs, original graphic dependencies and language label use explicit packaged bindings.'],
            language_groups=dict(source=self.recovery.apk.provenance(groups), fields=group_fields,
                sha256=sha(groups.get_raw_data())))
        (self.work / PROOF.name).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Recovered task reward attachments:', len(outputs), flush=True)


def install(work, project, record):
    receipt = json.loads((work / PROOF.name).read_bytes())
    assert receipt['generator_sha256'] == sha(Path(__file__).read_bytes())
    keys = {guid(work / 'task-reward-attachments' / name) for name in receipt['outputs'] if name.endswith('.meta')}
    found = subprocess.run(['rg', '--files-with-matches', '--null', '--no-ignore', '--glob', '*.meta', '-e',
        '^guid: (' + '|'.join(sorted(keys)) + ')\r?$', str(project / 'Assets')], stdout=subprocess.PIPE, check=False)
    assert found.returncode in (0, 1)
    existing = {}
    for name in found.stdout.decode('utf8').split('\0'):
        if not name: continue
        meta = Path(name); key = guid(meta)
        assert key not in existing, key
        existing[key] = Path(str(meta)[:-5])
    outputs, reused = {}, {}
    destination = project / 'Assets/Official/Home/UI/TaskRewardAttachments'
    for name, expected in receipt['outputs'].items():
        source = work / 'task-reward-attachments' / name
        assert sha(source.read_bytes()) == expected
        if name.endswith('.meta'): continue
        target = existing.get(guid(Path(str(source) + '.meta')), destination / name)
        if target.exists(): assert equivalent(source.read_bytes(), target.read_bytes()), target
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target); shutil.copy2(Path(str(source) + '.meta'), Path(str(target) + '.meta'))
        for value in (target, Path(str(target) + '.meta')):
            (outputs if value.is_relative_to(destination) else reused)['unity/' + value.relative_to(project).as_posix()] = sha(value.read_bytes())
    for folder in (destination, *sorted(p for p in destination.rglob('*') if p.is_dir())):
        meta = Path(str(folder) + '.meta')
        if not meta.exists():
            meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:task-attachments-folder:'
                + folder.relative_to(project).as_posix()).encode()).hexdigest() + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        outputs['unity/' + meta.relative_to(project).as_posix()] = sha(meta.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs'
    body = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(script) + '.meta')), type=3),
        m_Name='OfflineHomeTaskRewardAttachments', m_EditorClassIdentifier='', Entries=[dict(
            Path=row['asset']['path'], AssetGuid=row['asset']['guid'], Asset=row['pointer']) for row in receipt['attachments'].values()])
    path = project / CATALOG
    path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
        + yaml.safe_dump(dict(MonoBehaviour=body), sort_keys=False, allow_unicode=True), 'utf8', newline='\n')
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:task-reward-attachments-catalog').hexdigest()
            + '\nNativeFormatImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
    for value in (path, meta): outputs['unity/' + value.relative_to(project).as_posix()] = sha(value.read_bytes())
    path = project / 'Assets/Game/Resources/Home/home-task-attachment-language.json'
    path.write_text(json.dumps(dict(configuration=receipt['language_groups']['fields'], texts=receipt['texts']), ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:task-attachment-language').hexdigest()
            + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
    for value in (path, meta): outputs['unity/' + value.relative_to(project).as_posix()] = sha(value.read_bytes())
    if record:
        receipt.update(installed_output_sha256=outputs, reused_output_sha256=reused)
        PROOF.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed task reward attachments:', len(outputs), 'outputs;', len(reused), 'reused', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    TaskAttachments(work).build()
    if args.install: install(work, ROOT / 'unity', True)
