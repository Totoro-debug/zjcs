"""Bind the complete original storage weapon-rack subtree and native resources."""
import hashlib
import json
from pathlib import Path
import uuid

from home_native_assets import write_staged_assets, install_staged_assets
from official_apk import ROOT
from official_source import OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity
from prepare_partner_card_native import PartnerCardBinding, guid, script_id, sha


DEST = ROOT / 'unity/Assets/Official/Home/StorageBox/WeaponRack'
RECEIPT = ROOT / 'assets/reference/home-storage-weapon-rack-source-manifest.json'
BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_furniture_storagebox.bundle'
PREFAB = 'Prefabs/Home/Dynamic/Furniture/StorageBox/StorageBox.prefab'


class WeaponRackBinding(PartnerCardBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        paths = {name: runtime / 'Official/HomeFurniture' / (name + '.cs') for name in (
            'HomeWeaponRack', 'HomeViewModeControl', 'HomeSwitcherActionEventHandler')}
        paths['PreviewTool'] = runtime / 'Official/WorldUI/WorldPreviewTool.cs'
        for name, path in paths.items():
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = pointer
            self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def load(self, source_guid):
        result = super().load(source_guid)
        if result['guid'].startswith('0000000000000000') or result['guid'] in self.script_guids:
            return result
        old = result['guid']
        value = hashlib.md5(('zjcs:home-storage-weapon-rack:' + old).encode()).hexdigest()
        result['guid'] = value
        self.queue = [(target, value if key == old else key, data) for target, key, data in self.queue]
        for proof in self.proof.values():
            if proof.get('guid') == old:
                proof['guid'] = value
        self.script_guids.add(value)
        return result

    def build(self):
        storage, = [pointer.deref() for name, pointer in self.recovery.apk.container(BUNDLE)
            if name.casefold() == ('Assets/' + PREFAB).casefold()]
        original = []

        def visit(game_object):
            for component in game_object.read(check_read=False).m_Component:
                obj = component.component.deref()
                if obj.type.name == 'Transform':
                    for child in obj.read().m_Children:
                        visit(child.deref().read().m_GameObject.deref())
                elif obj.type.name == 'MonoBehaviour':
                    script = obj.read(check_read=False).m_Script.deref().read(check_read=False)
                    if script.m_ClassName == 'HomeWeaponRack':
                        original.append(obj)

        visit(storage)
        controller, = original
        root = controller.read(check_read=False).m_GameObject.deref()
        native = self.native / PREFAB
        rows = documents(native.read_text('utf8'))
        native_root, = [key for key, value in rows.items()
            if value.get('GameObject', {}).get('m_Name') == root.read().m_Name]
        self.recovery.recover('StorageWeaponRack', self.work / 'rack-recovered', bundle=BUNDLE,
            native_relative=PREFAB, source_root=root, native_root_id=native_root)
        self.recovery.finalize()
        _, _, panel, _ = self.recovery.outputs[0]
        expected = {'HomeWeaponRack', 'HomeViewModeControl', 'HomeSwitcherActionEventHandler', 'PreviewTool'}
        if {script['type'] for script in panel['scripts']} != expected:
            raise ValueError('Bind additional original weapon-rack components explicitly')
        for key, pointer in panel['external'].items():
            self.objects[pointer['guid']] = self.original(key)
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
        for script in panel['scripts']:
            fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
            fields['m_Script'] = self.scripts[script['type']]
        resolved = self.replace(panel['documents'])
        for _, _, data in self.queue:
            if isinstance(data, dict):
                replacement = self.replace(data)
                data.clear()
                data.update(replacement)
        prefab_guid = hashlib.md5(('zjcs:home-storage-weapon-rack:' + identity(root)).encode()).hexdigest()
        self.queue.append((Path('StorageWeaponRack.prefab'), prefab_guid, resolved))
        staged = self.work / 'rack-assets'
        outputs = write_staged_assets(self.queue, staged)
        return outputs, staged, dict(fileID=panel['root'], guid=prefab_guid, type=3), dict(
            root=identity(root), controller=identity(controller), native_root=native_root,
            objects=panel['objectMap'], scripts=panel['scripts'])


def build():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'home-storage-native'
    work = ROOT / 'output/validation' / ('home-storage-rack-' + uuid.uuid4().hex)
    work.mkdir(parents=True)
    (work / '.zjcs-validation.json').write_text(json.dumps(dict(format=1, repository=str(ROOT))), 'utf8')
    recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=BUNDLE)
    binding = WeaponRackBinding(work, recovery=recovery)
    outputs, staged, prefab, selection = binding.build()
    installed = install_staged_assets(work, staged, outputs, DEST, 'home-storage-rack')
    for name in ('StorageBox', 'Home'):
        path = DEST.parent if name == 'StorageBox' else DEST.parent.parent
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            token = hashlib.md5(('zjcs:home-native-folder:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
            meta.write_text('fileFormatVersion: 2\nguid: ' + token + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    RECEIPT.write_text(json.dumps(dict(manifest=source.version, generator_sha256=sha(Path(__file__).read_bytes()),
        inputs=binding.inputs, recovery=recovery.proof, selection=selection,
        native_scripts=binding.script_sources, resources=binding.proof, prefab=prefab, outputs=installed,
        scope='Exact original weapon-rack subtree, state bindings and native render resources. Weapon-part appearance/material bindings and the full storage building remain separate.'),
        ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Original storage weapon-rack subtree bound:', len(installed), 'files; work', work, flush=True)


if __name__ == '__main__':
    build()
