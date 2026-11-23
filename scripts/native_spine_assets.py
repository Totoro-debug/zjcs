"""Bind recovered application controllers to the installed native Spine SDK.

Spine data, atlas text and native Unity assets retain their original payloads.
Only MonoScript identities and the explicit application/SDK bridge are changed.
"""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_current_partner_native import documents, identity
from prepare_partner_card_native import PartnerCardBinding, guid, script_id, sha


class NativeSpineBinding(PartnerCardBinding):
    def add_texture_cache(self, cache):
        path = cache / 'all-assets/windows-textures/source-manifest.json'
        receipt = json.loads(path.read_bytes())
        inventory = cache / 'all-assets/inventory.json'
        if receipt['manifest'] != self.recovery.apk.version or not receipt['completed'] or sha(inventory.read_bytes()) != receipt['inventory_sha256']:
            raise ValueError('Native Spine dynamic texture conversion is incomplete or differs')
        self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        self.extra_textures = {row['source']['cab'].lower() + ':' + row['source']['path_id']: (cache, row) for row in receipt['textures']}

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        sdk = ROOT / 'unity/Assets/Spine/Runtime/spine-unity'
        paths = {
            'Spine.Unity.SkeletonAnimation': sdk / 'Components/SkeletonAnimation.cs',
            'Spine.Unity.SkeletonDataAsset': sdk / 'Asset Types/SkeletonDataAsset.cs',
            'Spine.Unity.SpineAtlasAsset': sdk / 'Asset Types/SpineAtlasAsset.cs',
            'AppearanceSystem.AppearanceRendererLite': runtime / 'Official/ProfessionUI/AppearanceRendererLite.cs',
            'SkeletonRendererFollower': runtime / 'Official/ProfessionUI/SkeletonRendererFollower.cs',
            'AppearanceSystem.Runtime.YooAssetTextureLoader': runtime / 'Official/ProfessionUI/YooAssetTextureLoader.cs',
            'AppearanceConfig': runtime / 'Official/ProfessionUI/AppearanceConfig.cs',
        }
        for name in ('HomeWeapon', 'AppearancePartSideSwitcher', 'AppearancePartSideSwitchListener'):
            paths[name] = runtime / 'Official/HomeFurniture' / (name + '.cs')
        paths['Zjcs.Offline.NativeSpine.NativeSpineAppearanceAdapter'] = runtime / 'Offline/NativeSpine/NativeSpineAppearanceAdapter.cs'
        for full_name, path in paths.items():
            namespace, _, name = full_name.rpartition('.')
            if not path.is_file():
                raise ValueError('Bind the installed native Spine/application source: ' + str(path))
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[full_name] = pointer
            self.class_ids[script_id(namespace, name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def checked_native(self, source_guid, obj):
        path = self.native_path(source_guid, obj)
        raw = path.read_bytes()
        relative = path.relative_to(self.cache / self.recovery.export_name).as_posix()
        meta = Path(str(path) + '.meta')
        if sha(raw) != self.checked[relative] or sha(meta.read_bytes()) != self.checked[relative + '.meta'] or guid(meta) != source_guid:
            raise ValueError('Native Spine input differs: ' + str(path))
        self.inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        self.inputs[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
        return path, raw

    def load(self, source_guid):
        if source_guid in self.redirects:
            return self.redirects[source_guid]
        obj = self.objects.get(source_guid)
        if obj is None:
            raise ValueError('Resolve original native Spine identity: ' + source_guid)
        key = identity(obj)
        if obj.type.name == 'Texture2D' and key not in self.textures and key in getattr(self, 'extra_textures', {}):
            cache, row = self.extra_textures[key]
            if sha(obj.get_raw_data()) != row['source']['object_sha256']:
                raise ValueError('Original native Spine dynamic texture identity differs')
            output = row['outputs'][0]
            raw = (cache / 'all-assets/windows-textures' / output['path']).read_bytes()
            if sha(raw) != output['sha256']:
                raise ValueError('Native Spine dynamic texture payload differs')
            target = Path('Converted') / (sha(key.encode())[:16] + Path(output['path']).suffix)
            target_guid = hashlib.md5(('zjcs:native-spine-texture:' + key).encode()).hexdigest()
            result = dict(fileID=2800000, guid=target_guid, type=2)
            self.redirects[source_guid] = result
            self.queue.append((target, target_guid, raw))
            self.proof[key] = dict(source=self.recovery.apk.provenance(obj), type=obj.type.name,
                original_sha256=sha(obj.get_raw_data()), output=target.as_posix(), guid=target_guid)
        elif obj.type.name in ('TextAsset', 'AnimatorController', 'Mesh'):
            path, raw = self.checked_native(source_guid, obj)
            if obj.type.name == 'TextAsset':
                original = obj.read(check_read=False).m_Script
                if isinstance(original, str):
                    original = original.encode('utf8', 'surrogateescape')
                if raw != bytes(original):
                    raise ValueError('Exported Spine text/binary bytes differ from the original TextAsset')
                file_id, data = 4900000, raw
            elif obj.type.name == 'Mesh':
                mesh, = documents(raw.decode('utf8')).values()
                fields = self.recovery.fields(obj)
                native = mesh['Mesh']
                if (bytes.fromhex(native['m_IndexBuffer']) != bytes(fields['m_IndexBuffer']) or
                    native['m_VertexData']['m_VertexCount'] != fields['m_VertexData']['m_VertexCount'] or
                    bytes.fromhex(native['m_VertexData']['_typelessdata']) != bytes(fields['m_VertexData']['m_DataSize'])):
                    raise ValueError('Native Spine prefab mesh geometry differs from the original')
                def has_external(value):
                    if isinstance(value, dict):
                        return bool(value.get('guid')) or any(has_external(child) for child in value.values())
                    return isinstance(value, list) and any(map(has_external, value))
                if has_external(mesh):
                    raise ValueError('Bind native Spine mesh external references explicitly')
                # Preserve original Unity hex/empty scalars. A general YAML
                # round trip quotes numeric hex and writes empty buffers as null.
                file_id, data = 4300000, raw
            else:
                data = documents(raw.decode('utf8'))
                main, = [(key, value['AnimatorController']) for key, value in data.items() if 'AnimatorController' in value]
                file_id, fields = main
                self.learn(self.recovery.fields(obj), fields, obj)
                # This is the original empty Unity state machine accompanying
                # SkeletonAnimation, not a replacement animation controller.
                for value in data.values():
                    for child in value.values():
                        if any(child.get(key) for key in ('m_ChildStates', 'm_ChildStateMachines', 'm_AnyStateTransitions', 'm_EntryTransitions', 'm_StateMachineBehaviours')):
                            raise ValueError('Recover and map native Spine animator subassets explicitly')
            target = path.relative_to(self.native)
            seed = hashlib.md5(('zjcs:partner-current:' + source_guid).encode()).hexdigest() if obj.type.name == 'Mesh' else source_guid
            target_guid = hashlib.md5(('zjcs:native-spine:' + seed).encode()).hexdigest()
            result = dict(fileID=file_id, guid=target_guid, type=3 if obj.type.name == 'TextAsset' else 2)
            self.redirects[source_guid] = result
            self.queue.append((target, target_guid, data))
            self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj), type=obj.type.name,
                original_sha256=sha(obj.get_raw_data()), payload_sha256=sha(raw),
                native_path=path.relative_to(ROOT).as_posix(), output=target.as_posix(), guid=target_guid)
        else:
            if obj.type.name == 'MonoBehaviour':
                script = obj.read(check_read=False).m_Script.deref().read(check_read=False)
                fields = self.recovery.fields(obj)
                if script.m_ClassName == 'SkeletonDataAsset' and fields.get('zSlots'):
                    raise ValueError('Migrate the original custom slot depths before binding this Spine asset')
                if script.m_ClassName == 'SpineAtlasAsset' and (fields.get('textureLoadingMode') or fields.get('onDemandTextureLoader', {}).get('m_PathID')):
                    raise ValueError('Bind the original SDK atlas texture-loading extension explicitly')
            result = super().load(source_guid)
            if result['guid'].startswith('0000000000000000') or result['guid'] in self.script_guids:
                return result
            old = result['guid']
            token = hashlib.md5(('zjcs:native-spine:' + old).encode()).hexdigest()
            result['guid'] = token
            self.queue = [(target, token if key == old else key, data) for target, key, data in self.queue]
            for proof in self.proof.values():
                if proof.get('guid') == old:
                    proof['guid'] = token
        self.script_guids.add(result['guid'])
        return result

    def bind_prefab(self, row):
        root, = [pointer.deref() for path, pointer in self.recovery.apk.container(row['bundle'])
                 if path.casefold() == row['path'].casefold() and pointer.deref().type.name == 'GameObject']
        relative = row['path'].removeprefix('Assets/')
        native = documents((self.native / relative).read_text('utf8'))
        native_root, = [key for key, value in native.items()
                        if value.get('GameObject', {}).get('m_Name') == root.read().m_Name]
        self.recovery.recover(root.read().m_Name, self.work / 'recovered', bundle=row['bundle'],
            native_relative=relative, source_root=root, native_root_id=native_root)
        _, _, panel, _ = self.recovery.outputs[-1]
        if panel['unresolved']:
            raise ValueError('Native Spine prefab still has unresolved original references')
        for key, pointer in panel['external'].items():
            self.objects[pointer['guid']] = self.original(key)
        docs = panel['documents']
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(docs[file_id].values())), owner)
        renderers, redirects = {}, {}
        for script in panel['scripts']:
            name = '.'.join(filter(None, [script['namespace'], script['type']]))
            if name not in self.scripts:
                raise ValueError('Migrate original native Spine controller: ' + name)
            fields = docs[script['nativeFileID']]['MonoBehaviour']
            fields['m_Script'] = self.scripts[name]
            if name == 'Spine.Unity.SkeletonAnimation':
                bridge_id = max(map(int, docs)) + 1
                bridge = {key: fields[key] for key in ('m_ObjectHideFlags', 'm_CorrespondingSourceObject', 'm_PrefabInstance', 'm_PrefabAsset', 'm_GameObject', 'm_Enabled', 'm_EditorHideFlags', 'm_Name', 'm_EditorClassIdentifier')}
                bridge.update(m_Script=self.scripts['Zjcs.Offline.NativeSpine.NativeSpineAppearanceAdapter'], source=dict(fileID=script['nativeFileID']))
                docs[bridge_id] = dict(MonoBehaviour=bridge)
                docs[fields['m_GameObject']['fileID']]['GameObject']['m_Component'].append(dict(component=dict(fileID=bridge_id)))
                renderers[script['nativeFileID']] = bridge_id
                redirects[script['original']] = bridge_id
        for script in panel['scripts']:
            if script['type'] in ('AppearanceRendererLite', 'SkeletonRendererFollower'):
                fields = docs[script['nativeFileID']]['MonoBehaviour']
                original_renderer = fields['SkeletonRenderer']['fileID']
                if original_renderer not in renderers:
                    raise ValueError('Bind the original application renderer to its native SDK bridge')
                fields['SkeletonRenderer'] = dict(fileID=renderers[original_renderer])
        pointer = dict(fileID=panel['root'], guid=hashlib.md5(('zjcs:native-spine-prefab:' + identity(root)).encode()).hexdigest(), type=3)
        self.script_guids.add(pointer['guid'])
        # Resolving SDK dependencies may namespace/reassign the queue. Do not
        # capture list.append before that resolution has completed.
        resolved = self.replace(docs)
        self.queue.append((Path(relative), pointer['guid'], resolved))
        return dict(asset=row, pointer=pointer, original=identity(root), objects=panel['objectMap'],
                    scripts=panel['scripts'], sdk_bridges=redirects)

    def bind_renderer_names(self, selections, appearance_config):
        paths = self.recovery.fields(appearance_config)['mAppearanceSkeletonPaths']
        bridge_script = self.scripts['Zjcs.Offline.NativeSpine.NativeSpineAppearanceAdapter']
        resources = {proof['guid']: proof for proof in self.proof.values() if 'guid' in proof}
        for selection in selections:
            docs, = [data for _, token, data in self.queue if token == selection['pointer']['guid']]
            identities = {}
            for file_id, value in docs.items():
                bridge = value.get('MonoBehaviour', {})
                if bridge.get('m_Script') != bridge_script:
                    continue
                renderer = docs[bridge['source']['fileID']]['MonoBehaviour']
                resource = resources[renderer['skeletonDataAsset']['guid']]
                native = Path(resource['native_path']).relative_to(self.native.relative_to(ROOT)).as_posix()
                match, = [row for row in paths if row['Path'] + '/' + row['SkeletonName'] + '_SkeletonData.asset' == 'Assets/' + native]
                bridge['skeletonDataName'] = match['SkeletonName']
                identities[str(file_id)] = dict(config_entry=match, skeleton_data=native)
            if not identities:
                raise ValueError('Bind at least one original native Spine skeleton identity')
            selection['sdk_skeleton_identities'] = identities

    def resolve_all(self):
        index = 0
        while index < len(self.queue):
            _, _, data = self.queue[index]
            if isinstance(data, dict):
                replaced = self.replace(data)
                data.clear()
                data.update(replaced)
            index += 1
        self.recovery.finalize()
