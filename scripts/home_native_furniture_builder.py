"""Shared native bundle cache and prefab binding builder for Home furniture buildings."""
import hashlib
import json
from pathlib import Path
import shutil
import sys
import uuid

from home_native_assets import install_staged_assets, write_staged_assets
from native_spine_assets import NativeSpineBinding, guid, script_id, sha
from official_apk import ROOT
from official_source import OfficialSource
from prepare_bag_tip_dots import NODES as BAG_NODES
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity
from prepare_home_tip_dots import NODES as HOME_TIP_NODES
from prepare_selected_wwise_inputs import prepare as prepare_audio


def prepare_native_cache(cache: Path, bundles: list[str], *, workers: int = 2) -> Path:
    """Build or refresh a per-building native Unity conversion cache from current official inputs."""
    from convert_current_resource_project import convert as convert_project
    from convert_current_texture_mips import build as build_textures
    from convert_current_windows_shaders import build as build_shaders
    from prepare_current_asset_inventory import build as build_inventory
    from prepare_current_resource_inputs import prepare as prepare_inputs
    from prepare_current_windows_engine import build as build_engine

    cache = cache.resolve()
    prepare_inputs(cache, bundles)
    build_inventory(cache)
    build_engine(cache)
    build_textures(cache, workers=workers)
    build_shaders(cache)
    convert_project(cache, 'inputs', 'native-export')
    return cache


class HomeNativeFurnitureBinding(NativeSpineBinding):
    """Reusable NativeSpine + HomeFurniture prefab/component/resource binder."""

    def __init__(self, work: Path, *, recovery: PartnerNativeRecovery, guid_prefix: str,
                 audio_cache_name: str = 'home-storage-audio',
                 extra_scripts: dict[str, Path] | None = None):
        self.guid_prefix = guid_prefix
        self.audio_cache_name = audio_cache_name
        self.extra_scripts = dict(extra_scripts or {})
        self.user_script_guids: set[str] = set()
        self.namespaced_tokens: set[str] = set()
        self.guid_rewrites: dict[str, str] = {}
        super().__init__(work, recovery=recovery)

    def register_scripts(self):
        super().register_scripts()
        self.user_script_guids.update(self.script_guids)
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        paths = {name: runtime / 'Official/HomeFurniture' / (name + '.cs') for name in (
            'EntityClickSound', 'StoreBoxFurniture', 'HomeNotice', 'HomeWeaponRack',
            'HomeViewModeControl', 'HomeSwitcherActionEventHandler', 'BaseHangUpFurniture', 'BedFurniture',
            'CartFurniture', 'DeskFurniture', 'HomeDayNight', 'HomeNpcChatEntry', 'DiningTableNpcPopClick', 'HomeNpcBubbleTip')}
        paths.update({
            'Home.ActorBehaviour.HomeBindingAuthoring': runtime / 'Official/HomeEntityBindings/HomeBindingAuthoring.cs',
            'HomeEntityDataProvider': runtime / 'Official/HomeEntityBindings/HomeEntityDataProvider.cs',
            'Home.Furniture.HomeEntityNodeRegister': runtime / 'Official/HomeEntityBindings/HomeEntityNodeRegister.cs',
            'HomePlayable.HomePlayableClipBind': runtime / 'Official/HomeEntityBindings/HomePlayableClipBind.cs',
            'StoryNodeRegister': runtime / 'Official/StoryNodes/StoryNodeRegister.cs',
            'HomeScreenShootActive': runtime / 'Official/HomeSceneUiControls/HomeScreenShootActive.cs',
            'Home.HomeFadeProxy': runtime / 'Official/HomeSceneUiControls/HomeFadeProxy.cs',
            'HomeTimeDriveAnimationElement': runtime / 'Official/HomeNativeTime/HomeTimeDriveAnimationElement.cs',
            'HomeTimeDriveSkeletonElement': runtime / 'Official/HomeNativeTime/HomeTimeDriveSkeletonElement.cs',
            'MaintainGlobalScale': runtime / 'Official/WorldUI/WorldMaintainGlobalScale.cs',
            'TextMeshProColorProxy': runtime / 'Official/World/NativeLabelUI/TextMeshProColorProxy.cs',
            'MultiLanguageTMPControler': runtime / 'Official/World/MainlineMiniMapHud/MultiLanguageTMPControler.cs',
            'PreviewTool': runtime / 'Official/WorldUI/WorldPreviewTool.cs',
            'UnityEngine.Rendering.Universal.ShadowCaster2D':
                ROOT / 'unity/Packages/com.unity.render-pipelines.universal/Runtime/2D/Shadows/ShadowCaster2D.cs',
            'UnityEngine.Rendering.Universal.Light2D':
                ROOT / 'unity/Packages/com.unity.render-pipelines.universal/Runtime/2D/Light2D.cs',
        })
        paths['TipDotObject'] = runtime / 'Official/HomeMainUiControls/TipDotObject.cs'
        paths['HomeMutex'] = runtime / 'Official/HomeRelations/HomeMutex.cs'
        paths.update({name: runtime / 'Official/BagTipDots' / (name + '.cs') for name in BAG_NODES})
        paths.update({name: runtime / 'Official/HomeTipDots' / (name + '.cs') for name in HOME_TIP_NODES})
        paths.update(self.extra_scripts)
        for full_name, path in paths.items():
            namespace, _, name = full_name.rpartition('.')
            if not path.is_file():
                raise ValueError('Migrate original native furniture component: ' + full_name + ' -> ' + str(path))
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[full_name] = pointer
            self.class_ids[script_id(namespace, name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.user_script_guids.add(pointer['guid'])
            self.script_sources[full_name] = dict(
                path=path.relative_to(ROOT).as_posix(),
                pointer=pointer,
                sha256=sha(path.read_bytes()),
                meta_sha256=sha(Path(str(path) + '.meta').read_bytes()),
            )

    def sound(self, payload, owner):
        pointer = payload.get('WwiseObjectReference') or {}
        if not pointer.get('m_PathID') and not payload.get('idInternal') and not payload.get('valueGuidInternal'):
            return dict(fileID=0)
        if not hasattr(self, 'audio_cache'):
            self.audio_cache = prepare_audio(
                self.cache.parent / self.audio_cache_name,
                ['wwisebnk_assets_wwisedata_android_minimum_pck.rawfile'],
            )
            receipt = self.audio_cache / 'raw-resources-source-manifest.json'
            self.inputs[receipt.relative_to(ROOT).as_posix()] = sha(receipt.read_bytes())
        native_cache = self.cache
        try:
            self.cache = self.audio_cache
            result = super().sound(payload, owner)
            if result.get('fileID') and not getattr(self, 'sound_namespaced', False):
                old = result['guid']
                token = hashlib.md5((self.guid_prefix + 'audio:' + old).encode()).hexdigest()
                result['guid'] = token
                self.audio_outputs = {
                    path: (token if key == old else key, raw)
                    for path, (key, raw) in self.audio_outputs.items()
                }
                self.script_guids.add(token)
                self.namespaced_tokens.add(token)
                self.sound_namespaced = True
            return result
        finally:
            self.cache = native_cache

    def _namespace_guid(self, old: str) -> str:
        if old in self.guid_rewrites:
            return self.guid_rewrites[old]
        if (old.startswith('0000000000000000')
                or old in self.user_script_guids
                or old in self.namespaced_tokens):
            return old
        token = hashlib.md5((self.guid_prefix + old).encode()).hexdigest()
        self.guid_rewrites[old] = token
        self.queue = [(target, token if key == old else key, value) for target, key, value in self.queue]
        for proof in self.proof.values():
            if proof.get('guid') == old:
                proof['guid'] = token
        self.script_guids.add(token)
        self.namespaced_tokens.add(token)
        return token

    def _apply_guid_rewrites(self, value):
        if isinstance(value, dict):
            updated = {}
            for key, item in value.items():
                if key == 'guid' and isinstance(item, str) and item in self.guid_rewrites:
                    updated[key] = self.guid_rewrites[item]
                else:
                    updated[key] = self._apply_guid_rewrites(item)
            return updated
        if isinstance(value, list):
            return [self._apply_guid_rewrites(item) for item in value]
        return value

    def original_asset(self, obj):
        result = super().original_asset(obj)
        if isinstance(result, dict) and isinstance(result.get('guid'), str):
            result['guid'] = self._namespace_guid(result['guid'])
        return result

    def load(self, source_guid):
        obj = self.objects.get(source_guid)
        if source_guid not in self.redirects and obj is not None and obj.type.name == 'AnimatorController':
            from home_native_prefab import load_controller
            result = load_controller(self, source_guid, obj)
        else:
            result = super().load(source_guid)
        if isinstance(result, dict) and isinstance(result.get('guid'), str):
            result['guid'] = self._namespace_guid(result['guid'])
        return result

    def replace(self, value):
        if isinstance(value, dict) and set(value) == {'class', 'ns', 'asm'}:
            if value['class'] in BAG_NODES:
                return dict(value, ns='Zjcs.Official.BagTipDots', asm='Zjcs.Official.BagTipDots')
            if value['class'] in HOME_TIP_NODES:
                return dict(value, ns='Zjcs.Official.HomeTipDots', asm='Zjcs.Official.HomeTipDots')
            if value['class'] == 'HomeMutex':
                return dict(value, ns='Zjcs.Official.HomeRelations', asm='Zjcs.Official.HomeRelations')
            if value['class'] == 'TipDotAchievementConditionNode':
                return dict(value, ns='Zjcs.Official.ProfessionUI', asm='Zjcs.Official.ProfessionUI')
        return super().replace(value)

    def build_furniture_prefabs(self, bundle: str, prefab_relatives: tuple[str, ...], dest: Path,
                                *, allow_empty_initial_skin: bool = True):
        by_path = {
            path.casefold(): pointer.deref()
            for path, pointer in self.recovery.apk.container(bundle)
            if pointer.deref().type.name == 'GameObject'
        }
        roots = []
        for relative in prefab_relatives:
            root = by_path[('Assets/' + relative).casefold()]
            name = root.read().m_Name
            native = documents((self.native / relative).read_text('utf8'))
            native_root, = [
                key for key, value in native.items()
                if value.get('GameObject', {}).get('m_Name') == name
                and native[value['GameObject']['m_Component'][0]['component']['fileID']]['Transform']['m_Father']['fileID'] == 0
            ]
            self.recovery.recover(
                name, self.work / ('recovered-' + name),
                bundle=bundle, native_relative=relative,
                source_root=root, native_root_id=native_root,
            )
            roots.append((relative, name, root))
        self.recovery.finalize()

        for (_, name, _), (_, _, panel, _) in zip(roots, self.recovery.outputs):
            if any(value['type'] != 'MonoBehaviour' for value in panel['unresolved'].values()):
                raise ValueError('Resolve additional non-MonoBehaviour prefab references: ' + name + ' -> ' + repr(panel['unresolved']))
            for key, pointer in panel['external'].items():
                self.objects[pointer['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)

        prefab_queue, prefabs, selections = [], [], {}
        bridge_script = self.scripts['Zjcs.Offline.NativeSpine.NativeSpineAppearanceAdapter']
        for (relative, name, root), (_, _, panel, _) in zip(roots, self.recovery.outputs):
            docs = panel['documents']
            renderers, sdk_bridges = {}, {}
            for script in panel['scripts']:
                full_name = '.'.join(filter(None, [script['namespace'], script['type']]))
                if full_name not in self.scripts:
                    raise ValueError('Bind the actual original furniture controller: ' + full_name)
                fields = docs[script['nativeFileID']]['MonoBehaviour']
                fields['m_Script'] = self.scripts[full_name]
                if full_name == 'EntityClickSound':
                    owner = self.original(script['original'])
                    fields['Sound'] = self.sound(self.recovery.fields(owner)['Sound'], owner)
                elif full_name == 'Spine.Unity.SkeletonAnimation':
                    bridge_id = max(map(int, docs)) + 1
                    bridge = {
                        key: fields[key]
                        for key in ('m_ObjectHideFlags', 'm_CorrespondingSourceObject', 'm_PrefabInstance',
                                    'm_PrefabAsset', 'm_GameObject', 'm_Enabled', 'm_EditorHideFlags',
                                    'm_Name', 'm_EditorClassIdentifier')
                    }
                    bridge.update(m_Script=bridge_script, source=dict(fileID=script['nativeFileID']))
                    docs[bridge_id] = dict(MonoBehaviour=bridge)
                    docs[fields['m_GameObject']['fileID']]['GameObject']['m_Component'].append(
                        dict(component=dict(fileID=bridge_id))
                    )
                    renderers[script['nativeFileID']] = bridge_id
                    sdk_bridges[script['original']] = bridge_id

            for script in panel['scripts']:
                if script['type'] in ('AppearanceRendererLite', 'SkeletonRendererFollower'):
                    fields = docs[script['nativeFileID']]['MonoBehaviour']
                    original_renderer = fields['SkeletonRenderer']['fileID']
                    if original_renderer == 0:
                        continue
                    if original_renderer not in renderers:
                        raise ValueError('Bind renderer bridge for ' + relative + ' / ' + script['type'])
                    fields['SkeletonRenderer'] = dict(fileID=renderers[original_renderer])
                elif script['type'] == 'SkeletonRendererHighTexturePreview':
                    fields = docs[script['nativeFileID']]['MonoBehaviour']
                    original_renderer = fields['mSkeletonRenderer']['fileID']
                    if original_renderer == 0:
                        continue
                    if original_renderer not in renderers:
                        raise ValueError('Bind preview renderer bridge for ' + relative)
                    if not allow_empty_initial_skin and not docs[original_renderer]['MonoBehaviour']['initialSkinName']:
                        raise ValueError('Original preview renderer missing explicit starting skin: ' + relative)
                    fields['mSkeletonRenderer'] = dict(fileID=renderers[original_renderer])

            resolved = self.replace(docs)
            prefab_guid = hashlib.md5((self.guid_prefix + 'prefab:' + identity(root)).encode()).hexdigest()
            pointer = dict(fileID=panel['root'], guid=prefab_guid, type=3)
            self.script_guids.add(pointer['guid'])
            self.user_script_guids.add(pointer['guid'])
            prefab_queue.append((Path(name + '.prefab'), pointer['guid'], resolved))
            location = relative.removeprefix('Prefabs/').removesuffix('.prefab')
            prefabs.append(dict(
                name=name,
                location=location,
                prefab=(dest / (name + '.prefab')).relative_to(ROOT).as_posix(),
                pointer=pointer,
            ))
            selections[name] = dict(
                root=identity(root),
                objects=panel['objectMap'],
                scripts=panel['scripts'],
                sdk_bridges=sdk_bridges,
                resolved_graph_references=panel['unresolved'],
            )

        self.resolve_all()
        self.queue = [
            (
                target,
                self.guid_rewrites.get(token, token),
                self._apply_guid_rewrites(data) if isinstance(data, dict) else data,
            )
            for target, token, data in self.queue
        ]
        for proof in self.proof.values():
            if isinstance(proof, dict) and isinstance(proof.get('guid'), str) and proof['guid'] in self.guid_rewrites:
                proof['guid'] = self.guid_rewrites[proof['guid']]
        prefab_queue = [
            (target, token, self._apply_guid_rewrites(data))
            for target, token, data in prefab_queue
        ]
        self.queue.extend(prefab_queue)
        self.queue.extend((path, token, raw) for path, (token, raw) in getattr(self, 'audio_outputs', {}).items())
        for _, token, _ in self.queue:
            if token not in self.namespaced_tokens and token not in self.user_script_guids:
                raise ValueError('Un-namespaced native asset GUID remained in queue: ' + token)
        staged = self.work / 'assets'
        outputs = write_staged_assets(self.queue, staged)
        return outputs, staged, prefabs, selections


def rebuild_furniture_bundle(*, bundle, prefabs, dest, receipt_path, guid_prefix, install_prefix, script_path, scope):
    """Rebind an existing building from its verified source without replacing assets."""
    import re
    from retained_native_binding import rebuild

    prior = json.loads(receipt_path.read_bytes())
    version = prior['manifest']
    export_key, = [key for key in prior['inputs'] if key.endswith('/native-export-source-manifest.json')]
    cache = (ROOT / export_key).parent
    for relative, expected in prior['outputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Owned furniture resource changed: ' + relative)
    work = ROOT / 'output/validation' / (install_prefix + '-rebuild-' + uuid.uuid4().hex)
    work.mkdir(parents=True)
    (work / '.zjcs-validation.json').write_text(json.dumps(dict(format=1, repository=str(ROOT))), 'utf8')

    class FurnitureReplay(HomeNativeFurnitureBinding):
        def build(self):
            outputs, staged, rows, selections = self.build_furniture_prefabs(bundle, prefabs, dest)
            mapping = prior.get('replacement', {}).get('guid_rewrites', {})

            def stable_pointers(value):
                if isinstance(value, dict):
                    return {key: mapping.get(item, item) if key == 'guid' and isinstance(item, str)
                            else stable_pointers(item) for key, item in value.items()}
                if isinstance(value, list):
                    return [stable_pointers(item) for item in value]
                return value

            rebuilt = {}
            for relative, expected in outputs.items():
                path = dest / relative
                key = path.relative_to(ROOT).as_posix()
                raw = (staged / relative).read_bytes()
                if sha(raw) != expected:
                    raise ValueError('Staged furniture resource changed: ' + key)
                if mapping:
                    if relative.endswith('.meta'):
                        original_guid = guid(staged / relative)
                        if guid(path) != mapping.get(original_guid, original_guid):
                            raise ValueError('Installed furniture importer identity changed: ' + key)
                        raw = path.read_bytes()
                    elif raw.startswith(b'%YAML'):
                        raw = re.sub(rb'guid: ([a-f0-9]{32})',
                            lambda match: ('guid: ' + mapping.get(match[1].decode(), match[1].decode())).encode(), raw)
                    (staged / relative).write_bytes(raw)
                    expected = sha(raw)
                if prior['outputs'].get(key) != expected:
                    raise ValueError('Original furniture rebuild differs: ' + key)
                rebuilt[key] = expected
            if mapping:
                rows = stable_pointers(rows)
                self.proof = stable_pointers(self.proof)
            # Folder importer metadata is installed separately from the native
            # payload. Every other prior output must have been rebuilt above.
            for relative, expected in prior['outputs'].items():
                if sha((ROOT / relative).read_bytes()) != expected:
                    raise ValueError('Furniture resource changed during rebuild: ' + relative)
                if relative in rebuilt:
                    continue
                path = ROOT / relative
                if path.suffix != '.meta' or not path.with_suffix('').is_dir() or 'folderAsset: yes' not in path.read_text('utf8'):
                    raise ValueError('Prior furniture payload was not rebuilt: ' + relative)
                rebuilt[relative] = expected
            for path in (script_path, Path(__file__), ROOT / 'scripts/home_native_assets.py', ROOT / 'scripts/native_spine_assets.py'):
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            result = dict(manifest=version, inputs=self.inputs, selection=selections[rows[0]['name']],
                selections=selections, recovery=self.recovery.proof, native_scripts=self.script_sources,
                resources=self.proof, audio=getattr(self, 'audio_proof', {}), prefab=rows[0]['pointer'],
                prefabs=rows, outputs=rebuilt, scope=scope)
            for key in ('replacement', 'retired_previous_outputs', 'retirement_generator_sha256', 'retirement_reference_scope'):
                if key in prior:
                    result[key] = prior[key]
            (work / receipt_path.name).write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')

    def factory(stage):
        recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=bundle)
        try:
            return FurnitureReplay(stage, recovery=recovery, guid_prefix=guid_prefix)
        except BaseException:
            recovery.apk.reader.close()
            raise

    rebuild(work, receipt_path, factory, generator=script_path, record_unchanged=True,
        export_name='native-export', identity_sections=('prefabs', 'selections'), script_section='native_scripts')
    return json.loads(receipt_path.read_bytes())


def install_furniture_bundle(*, cache_name: str, bundle: str, prefabs: tuple[str, ...],
                             dest: Path, receipt_path: Path, guid_prefix: str,
                             install_prefix: str, script_path: Path, scope: str,
                             extra_scripts: dict[str, Path] | None = None,
                             ensure_cache: bool = False) -> dict:
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / cache_name
    if ensure_cache and not (cache / 'native-export-source-manifest.json').is_file():
        prepare_native_cache(cache, [bundle])

    work = ROOT / 'output/validation' / (install_prefix + '-' + uuid.uuid4().hex)
    work.mkdir(parents=True)
    (work / '.zjcs-validation.json').write_text(json.dumps(dict(format=1, repository=str(ROOT))), 'utf8')
    recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=bundle)
    try:
        binding = HomeNativeFurnitureBinding(
            work,
            recovery=recovery,
            guid_prefix=guid_prefix,
            extra_scripts=extra_scripts,
        )
        outputs, staged, prefab_rows, selections = binding.build_furniture_prefabs(bundle, prefabs, dest)
        previous = json.loads(receipt_path.read_bytes())['outputs'] if receipt_path.exists() else {}
        installed = install_staged_assets(work, staged, outputs, dest, install_prefix, previous_outputs=previous)
        for path in (script_path, Path(__file__), ROOT / 'scripts/home_native_assets.py', ROOT / 'scripts/native_spine_assets.py'):
            binding.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt = dict(
            manifest=source.version,
            inputs=binding.inputs,
            selection=selections[prefab_rows[0]['name']],
            selections=selections,
            recovery=recovery.proof,
            native_scripts=binding.script_sources,
            resources=binding.proof,
            audio=getattr(binding, 'audio_proof', {}),
            prefab=prefab_rows[0]['pointer'],
            prefabs=prefab_rows,
            outputs=installed,
            scope=scope,
        )
        stage_receipt = work / 'bound-source-manifest.json'
        stage_receipt.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        receipt_path.parent.mkdir(parents=True, exist_ok=True)
        stage_receipt.replace(receipt_path)
        print('Bound complete original furniture prefabs:', len(prefab_rows), 'skins;', len(installed), 'files')
        return receipt
    finally:
        recovery.apk.reader.close()
        shutil.rmtree(work, ignore_errors=True)


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['cache'])
    parser.add_argument('--destination', type=Path, required=True)
    parser.add_argument('--bundle', action='append', required=True)
    args = parser.parse_args()
    if args.action == 'cache':
        prepare_native_cache(args.destination, args.bundle)
