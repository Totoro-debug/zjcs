"""Recover the selected furniture's original repair particle subtrees."""
import argparse
import hashlib
import json
from pathlib import Path

import yaml

from home_native_assets import install_staged_assets, write_staged_assets
from official_apk import ROOT
from prepare_current_partner_native import documents, identity, pointer
from prepare_home_environment_native import HomeEnvironmentBinding, metadata
from prepare_partner_card_native import Dumper, guid, sha
from validation_workspace import validation_directory

FURNITURE = {'Bed': 0, 'Desk': 1, 'DiningTable': 2, 'Danlu': 3, 'StatueOfGod': 5, 'StorageBox': 7, 'VendingMachine': 8, 'GachaChest': 9,
             'Cart': 10, 'AstrologicalDestiny': 12}
HOUSE_SLOTS = {'Bed': 'BedSlot', 'Desk': 'DeskSlot', 'DiningTable': 'DiningTableSlot', 'Danlu': 'DanluSlot', 'StorageBox': 'StorageBoxSlot'}


class RepairEffectBinding(HomeEnvironmentBinding):
    def build(self):
        def identity_chain(transform):
            chain = []
            while transform:
                value = transform.read()
                pos, scale, rot = value.m_LocalPosition, value.m_LocalScale, value.m_LocalRotation
                if ((scale.x, scale.y, scale.z) != (1, 1, 1)
                        or (rot.x, rot.y, rot.z, rot.w) != (0, 0, 0, 1)):
                    raise ValueError('Restore original slot rotation/scale before binding effects')
                chain.append(dict(name=value.m_GameObject.read().m_Name,
                    position=[pos.x, pos.y, pos.z], source=identity(transform)))
                self.proof[identity(transform)] = dict(source=self.recovery.apk.provenance(transform),
                    original_sha256=sha(transform.get_raw_data()))
                transform = value.m_Father.deref() if value.m_Father.path_id else None
            return chain

        scene_objects = self.recovery.apk.objects('defaultpackage_assets_scenes_main.bundle')
        manager, = [obj for obj in scene_objects if obj.type.name == 'MonoBehaviour'
            and obj.read(check_read=False).m_Script.deref().read(check_read=False).m_ClassName == 'HomeEntityManagerMono']
        manager_fields = self.recovery.fields(manager)
        house_row, = [row for row in manager_fields['HomeThemeSlotDict']['mSerializeBuffer'] if row['Key'] == 1]
        house_chain = identity_chain(pointer(house_row['Value'], manager))
        house_objects = self.recovery.apk.objects('defaultpackage_assets_prefabs_home_dynamic_theme_house.bundle')
        house, = [obj for obj in house_objects if obj.type.name == 'GameObject'
            and obj.read().m_Name == 'House' and not obj.read().m_Component[0].component.read().m_Father.path_id]
        def descendants(transform):
            yield transform
            for child in transform.read().m_Children:
                yield from descendants(child.deref())
        house_transforms = list(descendants(house.read().m_Component[0].component.deref()))
        scene_path = ROOT / 'unity/Assets/StreamingAssets/Home/scene.json'
        scene = json.loads(scene_path.read_bytes())
        slot_chains = {}
        for name, furniture_type in FURNITURE.items():
            if name in HOUSE_SLOTS:
                slot, = [obj for obj in house_transforms
                         if obj.read().m_GameObject.read().m_Name == HOUSE_SLOTS[name]]
                slot_chains[name] = identity_chain(slot) + house_chain
            else:
                slot_row, = [row for row in manager_fields['FurnitureSlotDict']['mSerializeBuffer']
                             if row['Key'] == furniture_type]
                slot_chains[name] = identity_chain(pointer(slot_row['Value'], manager))
        for name, furniture_type in FURNITURE.items():
            chain = slot_chains[name]
            current, = [row for row in scene['furniture'] if row['type'] == furniture_type]
            position = [sum(row['position'][i] for row in chain) for i in range(3)]
            if current['slot'] != position:
                raise ValueError('Historical scene slot differs from selected official transform chain')
        self.inputs[scene_path.relative_to(ROOT).as_posix()] = sha(scene_path.read_bytes())
        self.proof[identity(manager)] = dict(source=self.recovery.apk.provenance(manager),
            original_sha256=sha(manager.get_raw_data()))
        self.proof[identity(house)] = dict(source=self.recovery.apk.provenance(house),
            original_sha256=sha(house.get_raw_data()))
        selections = []
        for name, furniture_type in FURNITURE.items():
            bundle = 'defaultpackage_assets_prefabs_home_dynamic_furniture_' + name.lower() + '.bundle'
            native_relative = 'Prefabs/Home/Dynamic/Furniture/' + name + '/' + name + '.prefab'
            objects = self.recovery.apk.objects(bundle)
            root, = [obj for obj in objects if obj.type.name == 'GameObject'
                and obj.read().m_Name == name and obj.read().m_Component[0].component.read().m_Father.path_id == 0]
            transform = root.read().m_Component[0].component.read()
            pos, scale, rot = transform.m_LocalPosition, transform.m_LocalScale, transform.m_LocalRotation
            if ((scale.x, scale.y, scale.z) != (1, 1, 1) or (rot.x, rot.y, rot.z, rot.w) != (0, 0, 0, 1)):
                raise ValueError('Restore original furniture root rotation/scale: ' + name)
            root_position = [pos.x, pos.y, pos.z]
            for component in root.read().m_Component:
                if component.component.type.name != 'MonoBehaviour':
                    continue
                obj = component.component.deref()
                if obj.read(check_read=False).m_Script.read().m_ClassName == 'HomeEntityCreateData':
                    offset = self.recovery.fields(obj)['mLocalPosOffset']
                    if any(offset[key] != 0 for key in ('x', 'y', 'z')):
                        raise ValueError('Restore original furniture creation offset: ' + name)
            controller, = [entry.component.deref() for entry in root.read().m_Component
                if entry.component.type.name == 'MonoBehaviour'
                and self.recovery.fields(entry.component.deref()).get('mFurniture') == furniture_type
                and 'mRepairEffect' in self.recovery.fields(entry.component.deref())]
            fields = self.recovery.fields(controller)
            effect = pointer(fields['mRepairEffect'], controller)
            source_transform = effect.read().m_Component[0].component.deref().read()
            parent = source_transform.m_Father.read()
            if parent.m_GameObject.read().m_Name.casefold() != 'other' or parent.m_Father.read().m_GameObject.path_id != root.path_id:
                raise ValueError('Preserve the original repair effect ancestor chain: ' + name)
            pos, scale, rot = parent.m_LocalPosition, parent.m_LocalScale, parent.m_LocalRotation
            if ((pos.x, pos.y, pos.z) != (0, 0, 0) or (scale.x, scale.y, scale.z) != (1, 1, 1)
                    or (rot.x, rot.y, rot.z, rot.w) != (0, 0, 0, 1)):
                raise ValueError('Restore the nonidentity repair effect parent: ' + name)
            native_path = self.native / native_relative
            native_docs = documents(native_path.read_text('utf8'))
            native_root, = [key for key, value in native_docs.items()
                if value.get('GameObject', {}).get('m_Name') == effect.read().m_Name]
            self.recovery.recover(effect.read().m_Name, self.work / 'home-repair-effect-recovery',
                bundle=bundle, native_relative=native_relative, source_root=effect, native_root_id=native_root)
            selections.append((name, root, controller, effect, fields, native_path, root_position))
        self.recovery.finalize()
        entries = []
        for selection, recovered in zip(selections, self.recovery.outputs, strict=True):
            name, root, controller, effect, fields, native_path, root_position = selection
            _, _, panel, record = recovered
            if panel['unresolved'] or panel['scripts']:
                raise ValueError('Bind additional original repair effect components: ' + name)
            for key, ref in panel['external'].items():
                self.objects[ref['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
            target_guid = hashlib.md5(('zjcs:home-repair-effect:' + name).encode()).hexdigest()
            self.queue.append((Path(name + '.prefab'), target_guid, panel['documents']))
            entries.append(dict(name=name, root=dict(fileID=panel['root'], guid=target_guid, type=3),
                showTime=fields['mRepairEffectShowTime'], fullTime=fields['mRepairEffectFullTime'],
                rootPosition=root_position,
                sourceRoot=identity(root), sourceController=identity(controller), sourceEffect=identity(effect)))
            for obj in (root, controller, effect):
                self.proof.setdefault(identity(obj), {}).update(source=self.recovery.apk.provenance(obj),
                    original_sha256=sha(obj.get_raw_data()))
            for path in (native_path, Path(str(native_path) + '.meta')):
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        for _, _, data in self.queue:
            if isinstance(data, dict):
                resolved = self.replace(data)
                data.clear()
                data.update(resolved)
        # Other native modules can bind the same original material to their own
        # converted texture GUIDs. Keep this closure self-contained instead of
        # replacing their shared GUID with different bytes.
        remap = {key: hashlib.md5(('zjcs:home-repair-effect-material:' + key).encode()).hexdigest()
            for path, key, _ in self.queue if path.suffix == '.mat'}
        def remap_guids(value):
            if isinstance(value, dict):
                return {key: remap.get(item, item) if key == 'guid' and isinstance(item, str)
                    else remap_guids(item) for key, item in value.items()}
            if isinstance(value, list):
                return [remap_guids(item) for item in value]
            return value
        self.queue = [(path, remap.get(key, key), remap_guids(data)) for path, key, data in self.queue]
        outputs = write_staged_assets(self.queue, self.work / 'home-repair-effect-assets')
        source = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code/client-source/FurnitureBase.cs'
        for path in (source, Path(__file__), ROOT / 'scripts/home_native_assets.py', ROOT / 'scripts/prepare_home_environment_native.py'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt = dict(manifest=self.recovery.apk.version, inputs=self.inputs, objects=self.proof,
            slot_chains=slot_chains, material_guid_remap=remap,
            outputs=outputs, entries=entries, recovery=self.recovery.proof)
        (self.work / 'home-repair-effect-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged original furniture repair effects:', len(outputs), 'files', flush=True)


def install(work):
    receipt = json.loads((work / 'home-repair-effect-source-manifest.json').read_bytes())
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Changed repair effect input: ' + relative)
    installed = install_staged_assets(work, work / 'home-repair-effect-assets', receipt['outputs'],
        ROOT / 'unity/Assets/Official/Home/RepairEffectsNative', 'home-repair-effect')
    script = ROOT / 'unity/Assets/Game/Runtime/Presentation/Home/HomeRepairEffectAssets.cs'
    metadata(script, 'MonoImporter')
    entries = [dict(Type=FURNITURE[row['name']], Prefab=row['root'], RootOffset=dict(zip(('x', 'y', 'z'), row['rootPosition'])),
        ShowTime=row['showTime'], FullTime=row['fullTime']) for row in receipt['entries']]
    data = dict(MonoBehaviour=dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
        m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(script) + '.meta')), type=3),
        m_Name='HomeRepairEffectAssets', m_EditorClassIdentifier='', Entries=entries))
    catalog = ROOT / 'unity/Assets/Game/Resources/Home/HomeRepairEffectAssets.asset'
    catalog.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
        + yaml.dump(data, Dumper=Dumper, sort_keys=False), 'utf8', newline='\n')
    for path in (catalog, metadata(catalog, 'NativeFormatImporter')):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt['installed_output_sha256'] = installed
    runtime = ROOT / 'unity/Assets/Game/Runtime/Presentation/Home'
    bindings = [script, Path(str(script) + '.meta'), runtime / 'HomeRepairEffectPlayer.cs',
        runtime / 'HomeRepairEffectPlayer.cs.meta', runtime / 'HomeSceneView.cs', runtime / 'HomeFeatureRuntime.cs',
        ROOT / 'scripts/prepare_home_repair_effect_validation.py', ROOT / 'scripts/fixtures/home_repair_effect_validation.cs',
        ROOT / 'scripts/prepare_home_repair_effect_host_validation.py', ROOT / 'scripts/fixtures/home_furniture_host_validation.cs']
    receipt['runtime_bindings'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in bindings}
    (ROOT / 'assets/reference/home-repair-effect-native-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    workspace = validation_directory()
    if args.install:
        install(workspace)
    else:
        RepairEffectBinding(workspace).build()
