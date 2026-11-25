"""Recover 903_156948 Cart UI windows, AwardPanel, TipDot graph, audio, sprites, CartRule, and skin 4266 metadata."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import yaml
from UnityPy.enums import ClassIDType

from deduplicate_unity_textures import reuse_staged_shared_textures
from home_bottom_bar_animator_binding import verify_controller
from official_apk import ROOT
from official_manifest import read_asset_manifest
from official_source import OfficialSource
from official_tables import Constructors
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity, pointer
from prepare_map16_story_source import decode_localization
from prepare_partner_card_native import Dumper, PartnerCardBinding, PPtr, guid, script_id, sha
from prepare_partner_input_dialog import metadata
from prepare_selected_wwise_inputs import prepare as prepare_audio
from validation_workspace import validation_directory

RECEIPT = ROOT / 'assets/reference/home-cart-windows-native-source-manifest.json'
DESTINATION = ROOT / 'unity/Assets/Official/Home/UI/CartWindows'
CATALOG = ROOT / 'unity/Assets/Game/Resources/Home/OfficialHomeCartWindowAssetCatalog.asset'

PREFABS = (
    ('FurnitureCartDialog', 'defaultpackage_assets_prefabs_ui_furniture.bundle', 'Prefabs/UI/Furniture/FurnitureCartDialog.prefab'),
    ('CartHangUpItem', 'defaultpackage_assets_prefabs_ui_furniture.bundle', 'Prefabs/UI/Furniture/CartHangUpItem.prefab'),
    ('CartOutputItem', 'defaultpackage_assets_prefabs_ui_furniture.bundle', 'Prefabs/UI/Furniture/CartOutputItem.prefab'),
    ('HangUpCartItemDetailDialog', 'defaultpackage_assets_prefabs_ui_furniture.bundle', 'Prefabs/UI/Furniture/HangUpCartItemDetailDialog.prefab'),
    ('HangUpLevelUpPanel', 'defaultpackage_assets_prefabs_ui_furniture.bundle', 'Prefabs/UI/Furniture/HangUpLevelUpPanel.prefab'),
    ('HangUpLevelUpSwitchItem', 'defaultpackage_assets_prefabs_ui_furniture.bundle', 'Prefabs/UI/Furniture/HangUpLevelUpSwitchItem.prefab'),
    ('HangUpLevelUpPanelPropItem', 'defaultpackage_assets_prefabs_ui_furniture.bundle', 'Prefabs/UI/Furniture/HangUpLevelUpPanelPropItem.prefab'),
    ('FurnitureCartRuleDialog', 'defaultpackage_assets_prefabs_ui_furniture.bundle', 'Prefabs/UI/Furniture/FurnitureCartRuleDialog.prefab'),
    ('FurnitureCartPreviewDialogItem', 'defaultpackage_assets_prefabs_ui_furniture.bundle', 'Prefabs/UI/Furniture/FurnitureCartPreviewDialogItem.prefab'),
    ('AwardPanel', 'defaultpackage_assets_prefabs_ui_award.bundle', 'Prefabs/UI/Award/AwardPanel.prefab'),
    ('AwardPanelItem', 'defaultpackage_assets_prefabs_ui_award.bundle', 'Prefabs/UI/Award/AwardPanelItem.prefab'),
)

HOME_TIP_NODES = {
    path.stem
    for path in (ROOT / 'unity/Assets/Game/Runtime/Official/HomeTipDots').glob('TipDot*.cs')
}


class HomeCartWindowsBinding(PartnerCardBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        off = runtime / 'Official'
        paths = {}

        for name in (
            'FurnitureCartDialog',
            'FurnitureCartHangUpItem',
            'FurnitureCartHangUpOutputItem',
            'FurnitureCartRuleButton',
            'FurnitureCartRuleDialog',
            'HangUpCartItemDetailDialog',
            'HangUpLevelUpPanel',
            'HangUpLevelUpSwitchItem',
            'RewardPreviewDialog',
            'RewardPreviewDialogItem',
            'AwardPanel',
            'AwardContent',
            'AwardPanelItem',
            'MultiLanguageGroupStateControler',
            'LayoutElementMirrorForLoopRect',
        ):
            paths[name] = off / 'HomeCartWindowsUI' / (name + '.cs')

        for name in ('FurnitureHangUpQuickReceive', 'FurnitureOutputInfo', 'OutputAdditionDetailButton', 'RuleTipNumer'):
            paths[name] = off / 'HomeHangUpWindowsUI' / (name + '.cs')

        paths['FurnitureIconWidget'] = off / 'HomeUI/FurnitureIconWidget.cs'
        paths['HomeNotice'] = off / 'HomeFurniture/HomeNotice.cs'
        paths['TipDotObject'] = off / 'HomeMainUiControls/TipDotObject.cs'
        for node_name in HOME_TIP_NODES:
            paths[node_name] = off / 'HomeTipDots' / (node_name + '.cs')

        for path in (off / 'ProfessionUI').glob('ItemIcon*.cs'):
            paths[path.stem] = path
        for name in (
            'AkEvent',
            'AkGameObj',
            'WwiseEventReference',
            'UIClickSound',
            'CloseWindowBtn',
            'PanelTitle',
            'PropUpgradeEffectAgent',
            'PlayerUseItemContent',
            'RuleDialog',
            'TipInfo',
            'TopBarHandler',
        ):
            paths[name] = off / 'ProfessionUI' / (name + '.cs')
        paths['PlayerPropItem'] = off / 'ProfessionUI/ProfessionPlayerPropItem.cs'
        paths['StaticHyperLinkClickListener'] = off / 'PartnersUI/DialoguePlayback/StaticHyperLinkClickListener.cs'
        paths['AwardLoopScrollRect'] = off / 'BattleResultUI/AwardLoopScrollRect.cs'

        world_map = {
            'AwardPanelLayoutElement': 'WorldUI/AwardPanelLayoutElement.cs',
            'BgDialogElement': 'WorldUI/WorldBgDialogElement.cs',
            'PopupWindow': 'WorldUI/WorldPopupWindow.cs',
            'PopupWindowSound': 'WorldUI/WorldPopupWindowSound.cs',
            'PreviewTool': 'WorldUI/WorldPreviewTool.cs',
            'ItemProgress': 'WorldUI/WorldItemProgress.cs',
            'LayoutElementClamper': 'WorldUI/WorldLayoutElementClamper.cs',
            'CountRedDotImage': 'WorldUI/WorldCountRedDotImage.cs',
            'UIBloomGroup': 'WorldUI/WorldUIBloomGroup.cs',
            'MirrorImage': 'WorldUI/MirrorImage.cs',
            'UIMesh': 'WorldUI/UIMesh.cs',
            'UGUIJszx.HorizontalLayoutGroupJSZX': 'WorldUI/HorizontalLayoutGroupJSZX.cs',
            'UGUIJszx.VerticalLayoutGroupJSZX': 'WorldUI/VerticalLayoutGroupJSZX.cs',
            'UnityEngine.UI.LoopVerticalScrollRect': 'WorldUI/LoopScroll/LoopVerticalScrollRect.cs',
            'UnityEngine.UI.LoopScrollRect': 'WorldUI/LoopScroll/LoopScrollRect.cs',
            'LoopScrollGridLayoutGroup': 'WorldUI/LoopScroll/LoopScrollGridLayoutGroup.cs',
            'UnityEngine.UI.LoopScrollGridLayoutGroup': 'WorldUI/LoopScroll/LoopScrollGridLayoutGroup.cs',
            'TMPColorGradient': 'World/NativeLabelUI/TMPColorGradient.cs',
            'MultiLanguageTMPControler': 'World/MainlineMiniMapHud/MultiLanguageTMPControler.cs',
            'UIToggle': 'SkillsUI/UIToggle.cs',
            'SafeArea': 'HomeScreenUI/SafeArea.cs',
            'StoryNodeRegister': 'StoryNodes/StoryNodeRegister.cs',
        }
        for full_name, rel in world_map.items():
            paths[full_name] = off / rel

        for full_name, path in paths.items():
            meta = Path(str(path) + '.meta')
            if not path.is_file() or not meta.is_file():
                raise ValueError('Migrated Cart UI component is missing: ' + full_name + ' -> ' + str(path))
            pointer_ref = dict(fileID=11500000, guid=guid(meta), type=3)
            namespace, _, short = full_name.rpartition('.')
            self.scripts[full_name] = pointer_ref
            self.class_ids[script_id(namespace, short)] = pointer_ref
            self.script_guids.add(pointer_ref['guid'])
            self.script_sources[full_name] = dict(
                path=path.relative_to(ROOT).as_posix(),
                pointer=pointer_ref,
                sha256=sha(path.read_bytes()),
                meta_sha256=sha(meta.read_bytes()),
            )

    def sound(self, payload, owner):
        if not hasattr(self, 'audio_cache'):
            self.audio_cache = prepare_audio(
                self.cache.parent / 'home-ui-audio',
                [
                    'wwisebnk_assets_wwisedata_android_minimum_pck.rawfile',
                    'wwisebnk_assets_wwisedata_android_ui_sfx_01_pck.rawfile',
                ],
            )
            receipt = self.audio_cache / 'raw-resources-source-manifest.json'
            self.inputs[receipt.relative_to(ROOT).as_posix()] = sha(receipt.read_bytes())
        native_cache = self.cache
        try:
            self.cache = self.audio_cache
            result = super().sound(payload, owner)
            remapped = getattr(self, '_cart_audio_guids', set())
            if result.get('fileID') and result['guid'] not in remapped:
                old_guid = result['guid']
                new_guid = hashlib.md5(('zjcs:home-cart-windows-audio:' + old_guid).encode()).hexdigest()
                # Remap consistently across audio_outputs and audio_proof so audio never collides.
                result['guid'] = new_guid
                for proof in self.audio_proof.values():
                    if proof.get('pointer', {}).get('guid') == old_guid:
                        proof['pointer']['guid'] = new_guid
                self.audio_outputs = {
                    target: (new_guid if g == old_guid else g, raw)
                    for target, (g, raw) in self.audio_outputs.items()
                }
                self.script_guids.add(new_guid)
                remapped.add(new_guid)
                self._cart_audio_guids = remapped
            return result
        finally:
            self.cache = native_cache

    def original_asset(self, obj):
        key = identity(obj)
        if key in self.original_refs:
            return self.original_refs[key]
        if obj.type.name != 'MonoBehaviour':
            raise ValueError('Bind original non-native resource: ' + key)
        script = obj.read(check_read=False).m_Script.read()
        full_name = '.'.join(filter(None, [script.m_Namespace, script.m_ClassName]))
        script_pointer = self.scripts[full_name]
        target_guid = hashlib.md5(('zjcs:home-cart-windows-original:' + key).encode()).hexdigest()
        result = dict(fileID=11400000, guid=target_guid, type=2)
        self.original_refs[key] = result
        self.script_guids.add(target_guid)
        fields = self.recovery.fields(obj)

        def convert(value):
            if isinstance(value, dict):
                if 'm_FileID' in value and 'm_PathID' in value:
                    if not value['m_PathID']:
                        return dict(fileID=0)
                    linked = PPtr(**value, assetsfile=obj.assets_file).deref()
                    return self.original_asset(linked)
                return {k: script_pointer if k == 'm_Script' else convert(v) for k, v in value.items()}
            if isinstance(value, list):
                return [convert(v) for v in value]
            return value

        target = Path('Graph') / (sha(key.encode())[:16] + '.asset')
        data = {11400000: dict(MonoBehaviour=convert(fields))}
        self.queue.append((target, target_guid, data))
        self.proof[key] = dict(
            source=self.recovery.apk.provenance(obj),
            type=full_name,
            name=fields.get('m_Name', ''),
            original_sha256=sha(obj.get_raw_data()),
            output=target.as_posix(),
            guid=target_guid,
        )
        return result

    def _load_controller(self, source_guid, obj):
        native = self.native_path(source_guid, obj)
        raw = native.read_bytes()
        meta = Path(str(native) + '.meta')
        key = native.relative_to(self.cache / self.recovery.export_name).as_posix()
        if sha(raw) != self.checked[key] or sha(meta.read_bytes()) != self.checked[key + '.meta']:
            raise ValueError('Cart UI AnimatorController cache changed: ' + key)
        if guid(meta) != source_guid:
            raise ValueError('Cart UI AnimatorController GUID changed: ' + key)
        self.inputs[native.relative_to(ROOT).as_posix()] = sha(raw)
        docs = documents(raw.decode('utf8'))
        root, = [fid for fid, doc in docs.items() if 'AnimatorController' in doc]
        visited = verify_controller(self, obj, docs, root)
        target = native.relative_to(self.native)
        target_guid = hashlib.md5(('zjcs:home-cart-windows:' + source_guid).encode()).hexdigest()
        ptr = dict(fileID=root, guid=target_guid, type=2)
        self.redirects[source_guid] = ptr
        self.script_guids.add(target_guid)
        self._cart_remapped = getattr(self, '_cart_remapped', set())
        self._cart_remapped.add(source_guid)
        self.queue.append((target, target_guid, docs))
        self.proof[identity(obj)] = dict(
            source=self.recovery.apk.provenance(obj),
            type='AnimatorController',
            original_sha256=sha(obj.get_raw_data()),
            output=target.as_posix(),
            guid=target_guid,
            verified_runtime_subassets=visited,
        )
        return ptr

    def load(self, source_guid):
        if source_guid in self.redirects:
            return self.redirects[source_guid]
        obj = self.objects.get(source_guid)
        if obj is not None and obj.type.name == 'AnimatorController':
            return self._load_controller(source_guid, obj)
        result = super().load(source_guid)
        if obj is not None and obj.type.name == 'AnimatorOverrideController':
            result['type'] = 2
        if result['guid'].startswith('0000000000000000') or source_guid in getattr(self, '_cart_remapped', set()):
            return result
        old_guid = result['guid']
        if not old_guid.startswith(self._cart_guid_prefix_tag if hasattr(self, '_cart_guid_prefix_tag') else '__never__'):
            token = hashlib.md5(('zjcs:home-cart-windows:' + source_guid).encode()).hexdigest()
            if token != old_guid:
                self.redirects[source_guid]['guid'] = token
                self.queue = [(t, token if g == old_guid else g, d) for t, g, d in self.queue]
                for row in self.proof.values():
                    if row.get('guid') == old_guid:
                        row['guid'] = token
                self.script_guids.add(token)
                self.redirects[source_guid]['guid'] = token
                result = self.redirects[source_guid]
        self._cart_remapped = getattr(self, '_cart_remapped', set())
        self._cart_remapped.add(source_guid)
        return result

    def learn(self, source, native, owner):
        if (
            isinstance(source, dict)
            and source.get('m_PathID')
            and 'm_FileID' in source
            and isinstance(native, dict)
            and native.get('guid')
            and native['guid'] != '0000000000000000e000000000000000'
        ):
            obj = PPtr(**source, assetsfile=owner.assets_file).deref()
            if obj.type.name == 'MonoScript':
                script = obj.read(check_read=False)
                name = '.'.join(filter(None, (script.m_Namespace, script.m_ClassName)))
                expected = script_id(script.m_Namespace, script.m_ClassName)
                if native['fileID'] not in (11500000, expected) or name not in self.scripts:
                    raise ValueError('Bind original animated script explicitly: ' + name + ' / ' + repr(native))
                self.native_script_bindings = getattr(self, 'native_script_bindings', {})
                key = (native['guid'], native['fileID'])
                mapped = self.scripts[name]
                if key in self.native_script_bindings and self.native_script_bindings[key] != mapped:
                    raise ValueError('Conflicting original script subasset identity: ' + repr(key))
                self.native_script_bindings[key] = mapped
                return
            old = self.objects.get(native['guid'])
            if old is not None and identity(old) != identity(obj):
                raise ValueError(
                    f'One native GUID {native["guid"]} maps to two objects: {identity(old)} ({old.type.name}) vs {identity(obj)} ({obj.type.name}) inside {owner.read(check_read=False).m_Name}'
                )
            self.objects[native['guid']] = obj
            return
        return super().learn(source, native, owner)

    def replace(self, value):
        if isinstance(value, dict) and value.get('guid') and value.get('fileID'):
            mapped = getattr(self, 'native_script_bindings', {}).get((value['guid'], value['fileID']))
            if mapped is not None:
                return mapped
        if isinstance(value, dict) and set(value) == {'class', 'ns', 'asm'} and value['class']:
            if value['class'] in HOME_TIP_NODES:
                return dict(value, ns='Zjcs.Official.HomeTipDots', asm='Zjcs.Official.HomeTipDots')
            if value['class'] == 'TipDotCommonNode':
                return dict(value, ns='Zjcs.Official.ProfessionUI', asm='Zjcs.Official.ProfessionUI')
        return super().replace(value)

    def bind_dynamic_sprite(self, asset_guid):
        if not hasattr(self, 'dynamic_sprites_by_guid'):
            self.dynamic_sprites_by_guid = {}
            self.asset_manifest = read_asset_manifest(
                self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive)
            )
        if asset_guid in self.dynamic_sprites_by_guid:
            return
        record, = [row for row in self.asset_manifest if row['guid'] == asset_guid]
        obj, = [
            ptr.deref()
            for p, ptr in self.recovery.apk.container(record['bundle'])
            if p.casefold() == record['path'].casefold() and ptr.deref().type.name == 'Sprite'
        ]
        key_bytes, source_id = self.recovery.fields(obj)['m_RenderDataKey']
        render_guid = ''.join(format(key_bytes['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
        if render_guid != asset_guid:
            raise ValueError('Dynamic Cart sprite GUID mismatch: ' + record['path'])
        name = self.name_key(obj.read(check_read=False).m_Name)
        matches = set()
        for k in [name] + [name + str(i) for i in range(100)]:
            for candidate in self.by_name.get(k, []):
                if candidate.suffix == '.asset':
                    docs = documents(candidate.read_text('utf8'))
                    if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: source_id}:
                        matches.add(candidate)
        referenced = [
            p
            for p in matches
            if guid(Path(str(p) + '.meta')) in self.objects
            and identity(self.objects[guid(Path(str(p) + '.meta'))]) == identity(obj)
        ]
        if not referenced:
            canonical = self.recovery.native / Path(record['path'].split('Assets/', 1)[-1]).with_suffix('.asset')
            if canonical in matches:
                referenced = [canonical]
        if len(referenced) != 1:
            raise ValueError('Resolve unique Cart sprite export for ' + record['path'] + ': ' + repr(sorted(str(p) for p in matches)))
        candidate, = referenced
        source_guid = guid(Path(str(candidate) + '.meta'))
        previous = self.objects.get(source_guid)
        if previous is not None and identity(previous) != identity(obj):
            raise ValueError('Cart sprite identity collision: ' + record['path'])
        self.objects[source_guid] = obj
        ptr = self.load(source_guid)
        self.dynamic_sprites_by_guid[asset_guid] = dict(
            pointer=ptr,
            manifest_asset=record,
            original=identity(obj),
            original_sha256=sha(obj.get_raw_data()),
            source=self.recovery.apk.provenance(obj),
        )

    def build(self):
        recovery = self.recovery
        for name, bundle, native_rel in PREFABS:
            recovery.recover(name, self.work / 'cart-native', bundle=bundle, native_relative=native_rel)
        recovery.finalize()

        roots = {}
        for native, _, panel, proof in recovery.outputs:
            name = proof['name']
            root_guid = hashlib.md5(('zjcs:home-cart-windows:' + name + '.prefab').encode()).hexdigest()
            roots[name] = dict(fileID=panel['root'], guid=root_guid, type=3)
            self.redirects[guid(Path(str(native) + '.meta'))] = roots[name]
            self.script_guids.add(root_guid)
            for key, file_id in panel['objectMap'].items():
                self.original_refs[key] = dict(fileID=file_id, guid=root_guid, type=3)
            for key, ext_ptr in panel['external'].items():
                self.objects[ext_ptr['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)

        # Recover HangUpItem TipDot graph before prefab replacement so CartHangUpItem and
        # HangUpLevelUpSwitchItem link to the emitted node asset inside Graph/.
        graph_bundle = 'defaultpackage_assets_scriptableconfig_tipdotgraphassets_home.bundle'
        hangup_graph_obj, = [
            ptr.deref()
            for p, ptr in recovery.apk.container(graph_bundle)
            if p.casefold() == 'assets/scriptableconfig/tipdotgraphassets/home/hangupitem.asset'
            and ptr.deref().type.name == 'MonoBehaviour'
            and ptr.deref().read(check_read=False).m_Script.read().m_ClassName == 'TipDotNodeGraph'
        ]
        hangup_graph_ptr = self.original_asset(hangup_graph_obj)

        for _, _, panel, proof in recovery.outputs:
            for script in panel['scripts']:
                full = '.'.join(filter(None, (script['namespace'], script['type'])))
                fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
                fields['m_Script'] = self.scripts[full]
                owner = self.original(script['original'])
                original = recovery.fields(owner)
                if script['type'] == 'PopupWindowSound':
                    for field in ('ShowSound', 'HideSound'):
                        fields[field] = self.sound(original[field], owner)
                elif script['type'] == 'ButtonSound':
                    fields['ClickEvent'] = self.sound(original['ClickEvent'], owner)
                elif script['type'] == 'UIClickSound':
                    self.sound(original['AkEvent'], owner)
                elif script['type'] == 'AkEvent':
                    self.sound(original['data'], owner)

        missing = {
            '.'.join(filter(None, (s['namespace'], s['type'])))
            for _, _, panel, _ in recovery.outputs
            for s in panel['scripts']
        } - self.scripts.keys()
        if missing:
            raise ValueError('Unmapped scripts in Cart UI prefabs: ' + repr(sorted(missing)))

        switch_comp_file_id = next(
            int(s['nativeFileID'])
            for _, _, p, pr in recovery.outputs if pr['name'] == 'HangUpLevelUpSwitchItem'
            for s in p['scripts'] if s['type'] == 'HangUpLevelUpSwitchItem'
        )
        switch_root_guid = roots['HangUpLevelUpSwitchItem']['guid']

        prefabs = []
        used_scripts = set()
        for _, _, panel, proof in recovery.outputs:
            used_scripts.update('.'.join(filter(None, (s['namespace'], s['type']))) for s in panel['scripts'])
            docs = self.replace(panel['documents'])
            if proof['name'] == 'HangUpLevelUpPanel':
                for doc in docs.values():
                    mb = doc.get('MonoBehaviour')
                    if isinstance(mb, dict) and isinstance(mb.get('mSwitchItemPrefab'), dict):
                        mb['mSwitchItemPrefab'] = dict(fileID=switch_comp_file_id, guid=switch_root_guid, type=3)
            prefabs.append((Path(proof['name'] + '.prefab'), roots[proof['name']]['guid'], docs))

        # Dynamic Cart UI sprites + skin 4266 visuals
        records = read_asset_manifest(recovery.apk.source.manifest_bytes('DefaultPackage', recovery.apk.archive))
        by_path = {row['path'].casefold(): row for row in records}
        sprite_bindings = []
        sprite_keys = []
        for idx in (0, 2, 3, 4, 5, 6, 7, 8, 9):
            p = f'Assets/Image/UI/Furniture/Cart_NoAtlas/ImgItem_Cart_{idx}.png'
            sprite_keys.append((f'UI/Furniture/ImgItem_Cart_{idx}', p))
        for idx in range(2, 10):
            p = f'Assets/Image/UI/Furniture/BgCost_Atlas/ImgBgCost_{idx}.png'
            sprite_keys.append((f'UI/Furniture/ImgBgCost_{idx}', p))

        for stripped_key, full_path in sprite_keys:
            rec = by_path[full_path.casefold()]
            self.bind_dynamic_sprite(rec['guid'])
            ptr = self.dynamic_sprites_by_guid[rec['guid']]['pointer']
            sprite_bindings.append(dict(path=stripped_key, pointer=ptr))
            sprite_bindings.append(dict(path=full_path, pointer=ptr))

        skin4266_icon_rec = by_path['assets/image/itemicon/home/item_4266.png']
        self.bind_dynamic_sprite(skin4266_icon_rec['guid'])
        skin4266_icon_ptr = self.dynamic_sprites_by_guid[skin4266_icon_rec['guid']]['pointer']

        skin4266_prev_rec = by_path['assets/image/ui/homeskin/homeskinimg/img_furniture_cart_03.png']
        self.bind_dynamic_sprite(skin4266_prev_rec['guid'])
        skin4266_prev_ptr = self.dynamic_sprites_by_guid[skin4266_prev_rec['guid']]['pointer']

        # Fixed-point resolution over self.queue including materials, fonts, and Graph/.
        idx = 0
        while idx < len(self.queue):
            _, _, data = self.queue[idx]
            if isinstance(data, dict):
                updated = self.replace(data)
                data.clear()
                data.update(updated)
            idx += 1

        self.queue.extend(prefabs)

        # Luban tables & 903_156948 localization extraction
        code = ROOT / '.tools/source-packages/current' / recovery.apk.version / 'official-code'
        constructors = Constructors(code)
        mapping = {
            n.rsplit('/', 1)[-1]: k
            for k, n in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))
        }
        wanted_tables = {
            'hang_up', 'hang_up_reward', 'reward_rule', 'rule_tip',
            'furniture', 'furniture_skin', 'home_skin_weather_override_prefab', 'item',
        }
        tables = {}
        table_proofs = {}
        for obj in recovery.apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name != 'TextAsset':
                continue
            n = obj.read().m_Name.rsplit('/', 1)[-1].removesuffix('.g')
            if n not in wanted_tables:
                continue
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            tables[n] = constructors.table(raw, mapping[n])
            table_proofs[n] = dict(
                source=recovery.apk.provenance(obj),
                object_sha256=sha(obj.get_raw_data()),
                payload_sha256=sha(raw),
            )

        cart_rule_rows = sorted(
            [row for row in tables['rule_tip'] if row['RuleKey'] == 'CartRule'],
            key=lambda row: row['Order'],
        )
        assert sum(r['Order'] == 0 for r in cart_rule_rows) == 1 and len(cart_rule_rows) == 6
        cart_rule = dict(
            Key='CartRule',
            TitleKey='rule_tip_CartRule_0',
            TipKeys=[f'rule_tip_CartRule_{r["Order"]}' for r in cart_rule_rows if r['Order'] != 0],
        )

        # Evaluate 903_156948 reward_rule 11100 preview table cleanly (all equip preview rows)
        # Note: Equip probabilities depend only on Condition(SubRank, Level) and WeightList/CountList.
        # Compute deterministic (hangUpId, itemId, probability, amount) per row and store raw
        # 11100 entries OR resolve exact probabilities in runtime via OfficialHomeRules.
        # Here we also keep reward_rule_11100 item names in texts.
        equip_previews = []
        cart_reward_items = set()
        for row in tables['reward_rule']:
            if row['Id'] != 11100:
                continue
            for item_id in row.get('IdList', []):
                cart_reward_items.add(item_id)

        skin4266_row, = [r for r in tables['furniture_skin'] if r['SkinItemId'] == 4266]
        weather4266_row, = [r for r in tables['home_skin_weather_override_prefab'] if r['SkinId'] == 4266]
        skin4266_meta = dict(
            skinItemId=int(skin4266_row['SkinItemId']),
            furnitureType=int(skin4266_row['FurnitureType']),
            weatherSkinId=int(weather4266_row['WeatherSkinId']),
            order=int(skin4266_row['Order']),
            prefabName=skin4266_row['PrefabName'],
            weatherPrefabName=weather4266_row['PrefabName'],
            itemNameKey='item_4266_name',
            itemDescKey='item_4266_desc',
        )

        text_keys = {
            doc['MonoBehaviour']['myKey']
            for _, _, panel, _ in recovery.outputs
            for doc in panel['documents'].values()
            if doc.get('MonoBehaviour', {}).get('myKey')
        }
        text_keys.update((
            'item_1_name',
            'item_4011_name',
            'item_4011_desc',
            'item_4266_name',
            'item_4266_desc',
            'hang_up_output_hour',
            'hang_up_output_tip',
            'ui_common_level_style_11',
            'fast_hang_up_no_award',
            'AwardPanel.CommonTitle',
            'function_limit_homelevel',
            'ui_level_up_item_not_enough',
            'ui_rule_hours',
            'ui_common_percent',
            'hang_up_prop_add_sum_speed_and_percent',
            'hangup_output_add_title',
            'hangup_output_add_loss_goddess',
            'hangup_output_add_loss_goddess_nums',
            'hangup_output_add_feeder_explore',
            'hangup_output_add_treasure',
            'UI_Pay_PeriodicCard',
            'hangup_output_add_astrological',
            'ui_ItemIcon_count_key',
            'ui_ItemIcon_count_overflow_key',
            'ui_output_addition_equip_text',
            'ui_common_world_level',
            'item_desc_bed',
            cart_rule['TitleKey'],
            *cart_rule['TipKeys'],
        ))
        prop_cs = (code / 'common-source/Common/PropType.cs').read_text('utf-8-sig')
        prop_names = {int(v): n for n, v in re.findall(r'^\s*(\w+)\s*=\s*(\d+),?$', prop_cs, re.M)}
        self.inputs[Path(code / 'common-source/Common/PropType.cs').relative_to(ROOT).as_posix()] = sha(
            (code / 'common-source/Common/PropType.cs').read_bytes()
        )
        for row in tables['hang_up']:
            if row.get('BindFurniture') != 10:
                continue
            hid = row['Id']
            text_keys.add(f'hang_up_{hid}_name')
            text_keys.add(f'hang_up_{hid}_base_output')
            for field in ('EffectFixedPropType', 'EffectPercentPropType', 'MaxStorePropType', 'FinallyEffectPercentPropType'):
                pid = row.get(field, 0)
                if pid in prop_names:
                    text_keys.add('PropType.' + prop_names[pid])
        for item_id in cart_reward_items:
            text_keys.add(f'item_{item_id}_name')
            text_keys.add(f'item_{item_id}_desc')

        literals = {'{0}', '{0}/{1}', '+{0}%'}
        texts, language_meta = decode_localization(recovery.apk, text_keys - literals, set())
        item1_name = texts.get('item_1_name', '')
        for k, v in list(texts.items()):
            if '{item_1_name}' in v and item1_name:
                texts[k] = v.replace('{item_1_name}', item1_name)

        absent = sorted((text_keys - literals) - texts.keys())
        # Ensure explicit empty string if an official optional desc key like item_4011_desc is absent.
        if 'item_4011_desc' in absent:
            texts['item_4011_desc'] = ''

        payload = dict(
            manifest=recovery.apk.version,
            texts=[dict(key=k, text=v) for k, v in sorted(texts.items())],
            cartRule=cart_rule,
            equipPreviews=equip_previews,
            skin4266=skin4266_meta,
        )
        data_json_guid = hashlib.md5(b'zjcs:home-cart-windows:data-json').hexdigest()
        self.queue.append((
            Path('cart-windows-data.json'),
            data_json_guid,
            (json.dumps(payload, ensure_ascii=False, indent=2) + '\n').encode('utf8'),
        ))

        folder = self.work / 'cart-assets'
        outputs = {}
        for target, target_guid, data in self.queue:
            path = folder / target
            path.parent.mkdir(parents=True, exist_ok=True)
            if isinstance(data, dict):
                text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id, document in data.items():
                    kind, = document
                    if kind == 'Mesh' and isinstance(document.get('Mesh'), dict):
                        sd = document['Mesh'].get('m_StreamData')
                        if isinstance(sd, dict) and sd.get('path') is None:
                            sd['path'] = ''
                    text += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                    text += yaml.dump(document, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
                raw = text.encode('utf8')
            else:
                raw = data
            path.write_bytes(raw)
            ext = path.suffix.lower()
            importer = (
                'PrefabImporter' if ext == '.prefab' else
                'ShaderImporter' if ext == '.shader' else
                'TextScriptImporter' if ext == '.json' else
                'TrueTypeFontImporter' if ext in ('.ttf', '.otf') else
                'NativeFormatImporter'
            )
            meta_path = Path(str(path) + '.meta')
            meta_path.write_text(
                f'fileFormatVersion: 2\nguid: {target_guid}\n{importer}:\n  externalObjects: {{}}\n',
                'utf8',
                newline='\n',
            )
            for item in (path, meta_path):
                outputs[item.relative_to(folder).as_posix()] = sha(item.read_bytes())

        for target, (target_guid, raw) in self.audio_outputs.items():
            path = folder / target
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            meta_path = Path(str(path) + '.meta')
            meta_path.write_text(
                f'fileFormatVersion: 2\nguid: {target_guid}\nAudioImporter:\n  externalObjects: {{}}\n'
                '  serializedVersion: 7\n  defaultSettings:\n    loadType: 0\n    sampleRateSetting: 0\n'
                '    sampleRateOverride: 0\n    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n'
                '  forceToMono: 0\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 0\n',
                'utf8',
                newline='\n',
            )
            for item in (path, meta_path):
                outputs[item.relative_to(folder).as_posix()] = sha(item.read_bytes())

        for helper_script in (Path(__file__), ROOT / 'scripts/deduplicate_unity_textures.py', ROOT / 'scripts/prepare_partner_card_native.py'):
            self.inputs[helper_script.relative_to(ROOT).as_posix()] = sha(helper_script.read_bytes())

        used_guids = set()
        for rel in outputs:
            p = folder / rel
            if not rel.endswith('.meta') and p.read_bytes().startswith(b'%YAML'):
                used_guids.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', p.read_text('utf8')))

        receipt = dict(
            manifest=recovery.apk.version,
            generator_sha256=sha(Path(__file__).read_bytes()),
            inputs=self.inputs,
            outputs=outputs,
            native_recovery=recovery.proof,
            objects=self.proof,
            roots=roots,
            hang_up_item_graph=hangup_graph_ptr,
            sprite_bindings=sprite_bindings,
            skin4266_icon=skin4266_icon_ptr,
            skin4266_preview=skin4266_prev_ptr,
            data_json_guid=data_json_guid,
            audio=self.audio_proof,
            script_bindings={k: v for k, v in self.script_sources.items() if v['pointer']['guid'] in used_guids or k in HOME_TIP_NODES or k in ('TipDotNodeGraph', 'TipDotGraphNode', 'TipDotGraphConditionNode')},
            graph_bindings=[
                dict(guid=v['guid'], name=v['name'], type=v['type'])
                for v in self.proof.values()
                if v.get('output', '').startswith('Graph/')
            ],
            language=language_meta,
            absent_text_keys=absent,
            tables=table_proofs,
            skin4266=skin4266_meta,
            scope='Complete 903_156948 Cart UI windows, AwardPanel, HangUpItem TipDot graph, audio, sprites, CartRule, and skin 4266 metadata.',
        )
        reuse_staged_shared_textures(folder, receipt)
        # Also deduplicate immutable Texture2Ds whose payload+importer match a proved
        # canonical texture in shared-textures-source-manifest.json under different source GUIDs.
        shared_path = ROOT / 'assets/reference/shared-textures-source-manifest.json'
        if shared_path.is_file():
            shared_rows = json.loads(shared_path.read_bytes()).get('textures', [])
            canon_by_key = {}
            for row in shared_rows:
                canon = row['canonical']
                t_path = ROOT / canon['path']
                t_meta = Path(str(t_path) + '.meta')
                if t_path.is_file() and t_meta.is_file() and sha(t_path.read_bytes()) == canon['sha256'] and sha(t_meta.read_bytes()) == canon['meta_sha256']:
                    imp = re.sub(rb'(?m)^guid: [0-9a-f]{32}', b'guid: GUID', t_meta.read_bytes()).replace(b'\r\n', b'\n')
                    canon_by_key[(canon['sha256'], hashlib.sha256(imp).hexdigest())] = canon
            extra_aliases = {}
            extra_reused = []
            for rel, expected in list(receipt['outputs'].items()):
                if not rel.endswith('.asset'):
                    continue
                p = folder / rel
                raw = p.read_bytes()
                if b'\nTexture2D:' not in raw[:4096] or not re.search(rb'm_IsReadable: 0\s', raw[:4096]):
                    continue
                pm = Path(str(p) + '.meta')
                imp = re.sub(rb'(?m)^guid: [0-9a-f]{32}', b'guid: GUID', pm.read_bytes()).replace(b'\r\n', b'\n')
                match_canon = canon_by_key.get((expected, hashlib.sha256(imp).hexdigest()))
                if match_canon is not None:
                    src_guid = guid(pm)
                    extra_aliases[src_guid.encode()] = match_canon['guid'].encode()
                    extra_reused.append(dict(staged_path=rel, staged_sha256=expected, canonical=match_canon))
            if extra_reused:
                for er in extra_reused:
                    receipt['outputs'].pop(er['staged_path'])
                    receipt['outputs'].pop(er['staged_path'] + '.meta')
                    c = er['canonical']
                    receipt['inputs'][c['path']] = c['sha256']
                    receipt['inputs'][c['path'] + '.meta'] = c['meta_sha256']
                pptr_re = re.compile(rb'guid: ([0-9a-f]{32})')
                for rel in list(receipt['outputs']):
                    rp = folder / rel
                    rraw = rp.read_bytes()
                    if rel.endswith('.meta') or rraw.startswith(b'%YAML'):
                        updated = pptr_re.sub(lambda m: b'guid: ' + extra_aliases.get(m[1], m[1]), rraw)
                        if updated != rraw:
                            rp.write_bytes(updated)
                            receipt['outputs'][rel] = sha(updated)
                receipt['shared_textures'] = receipt.get('shared_textures', []) + extra_reused
        (self.work / RECEIPT.name).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print(f'Staged Cart UI native assets: {len(receipt["outputs"])} outputs, reused {len(receipt.get("shared_textures", []))} shared textures', flush=True)


def install(work):
    receipt = json.loads((work / RECEIPT.name).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild changed Cart UI native generator.')
    for rel, expected in receipt['inputs'].items():
        if sha((ROOT / rel).read_bytes()) != expected:
            raise ValueError('Cart UI input changed: ' + rel)
    previous = json.loads(RECEIPT.read_bytes()).get('installed_output_sha256', {}) if RECEIPT.exists() else {}

    folder = work / 'cart-assets'
    staged_guids = set()
    for rel in receipt['outputs']:
        if rel.endswith('.meta'):
            staged_guids.add(guid(folder / rel))

    existing = {}
    asset_root = ROOT / 'unity/Assets'
    guid_head_re = re.compile(rb'(?m)^guid: ([0-9a-f]{32})')
    for base, _, files in os.walk(asset_root):
        for fname in files:
            if not fname.endswith('.meta'):
                continue
            meta_p = Path(base) / fname
            with meta_p.open('rb') as stream:
                match = guid_head_re.search(stream.read(256))
            if not match:
                continue
            k = match[1].decode()
            if k not in staged_guids:
                continue
            target_p = Path(str(meta_p)[:-5])
            if target_p.is_file():
                if k in existing:
                    raise ValueError('Existing Unity GUID collision for staged asset: ' + k)
                existing[k] = target_p

    plan, installed, reused = [], {}, []
    folder = work / 'cart-assets'
    for rel, expected in receipt['outputs'].items():
        src = folder / rel
        if sha(src.read_bytes()) != expected:
            raise ValueError('Staged Cart output changed: ' + rel)
        if rel.endswith('.meta'):
            continue
        k = guid(Path(str(src) + '.meta'))
        target = existing.get(k, DESTINATION / rel)
        same = target.exists() and target.read_bytes().replace(b'\r\n', b'\n') == src.read_bytes().replace(b'\r\n', b'\n')
        if target.exists() and not same:
            if not target.is_relative_to(DESTINATION) or sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve different existing resource: ' + str(target))
        if same:
            reused.append(dict(path=target.relative_to(ROOT).as_posix(), guid=k, sha256=sha(target.read_bytes())))
        else:
            plan.extend(((src, target), (Path(str(src) + '.meta'), Path(str(target) + '.meta'))))
        if target.is_relative_to(DESTINATION):
            installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes()) if same else expected
            installed[Path(str(target) + '.meta').relative_to(ROOT).as_posix()] = sha(Path(str(src) + '.meta').read_bytes())
        existing[k] = target

    for src, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(src.read_bytes())
    for d in [DESTINATION, *(p for p in DESTINATION.rglob('*') if p.is_dir())]:
        m = metadata(d, 'DefaultImporter', True)
        installed[m.relative_to(ROOT).as_posix()] = sha(m.read_bytes())

    def fmt_ptr(ptr):
        return f'{{fileID: {ptr["fileID"]}, guid: {ptr["guid"]}, type: {ptr["type"]}}}'

    catalog_script = ROOT / 'unity/Assets/Game/Runtime/Official/HomeCartWindowsUI/OfficialHomeCartWindowAssetCatalog.cs'
    roots = receipt['roots']
    lines = [
        '%YAML 1.1',
        '%TAG !u! tag:unity3d.com,2011:',
        '--- !u!114 &11400000',
        'MonoBehaviour:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        '  m_GameObject: {fileID: 0}',
        '  m_Enabled: 1',
        '  m_EditorHideFlags: 0',
        f'  m_Script: {{fileID: 11500000, guid: {guid(Path(str(catalog_script) + ".meta"))}, type: 3}}',
        '  m_Name: OfficialHomeCartWindowAssetCatalog',
        "  m_EditorClassIdentifier: ''",
        f'  cartDialogPrefab: {fmt_ptr(roots["FurnitureCartDialog"])}',
        f'  hangUpItemPrefab: {fmt_ptr(roots["CartHangUpItem"])}',
        f'  hangUpOutputItemPrefab: {fmt_ptr(roots["CartOutputItem"])}',
        f'  itemDetailDialogPrefab: {fmt_ptr(roots["HangUpCartItemDetailDialog"])}',
        f'  levelUpPanelPrefab: {fmt_ptr(roots["HangUpLevelUpPanel"])}',
        f'  levelUpSwitchItemPrefab: {fmt_ptr(roots["HangUpLevelUpSwitchItem"])}',
        f'  levelUpPropItemPrefab: {fmt_ptr(roots["HangUpLevelUpPanelPropItem"])}',
        f'  ruleDialogPrefab: {fmt_ptr(roots["FurnitureCartRuleDialog"])}',
        f'  previewDialogItemPrefab: {fmt_ptr(roots["FurnitureCartPreviewDialogItem"])}',
        f'  awardPanelPrefab: {fmt_ptr(roots["AwardPanel"])}',
        f'  awardPanelItemPrefab: {fmt_ptr(roots["AwardPanelItem"])}',
        f'  hangUpItemGraphAsset: {fmt_ptr(receipt["hang_up_item_graph"])}',
        '  sprites:',
    ]
    for row in receipt['sprite_bindings']:
        lines.append(f'  - path: {row["path"]}')
        lines.append(f'    sprite: {fmt_ptr(row["pointer"])}')
    lines.append(f'  cartSkin4266Icon: {fmt_ptr(receipt["skin4266_icon"])}')
    lines.append(f'  cartSkin4266Preview: {fmt_ptr(receipt["skin4266_preview"])}')
    lines.append('  sounds:')
    for row in sorted(receipt['audio'].values(), key=lambda x: x['event']['id']):
        lines.append(f'  - eventId: {row["event"]["id"]}')
        lines.append(f'    clip: {fmt_ptr(row["pointer"])}')
    lines.append(f'  texts: {{fileID: 4900000, guid: {receipt["data_json_guid"]}, type: 3}}')

    catalog_text = '\n'.join(lines) + '\n'
    if CATALOG.exists() and CATALOG.read_text('utf8') != catalog_text and sha(CATALOG.read_bytes()) != previous.get(CATALOG.relative_to(ROOT).as_posix()):
        raise ValueError('Preserve changed Cart window catalog: ' + str(CATALOG))
    CATALOG.parent.mkdir(parents=True, exist_ok=True)
    CATALOG.write_text(catalog_text, 'utf8', newline='\n')
    for item in (CATALOG, metadata(CATALOG, 'NativeFormatImporter')):
        installed[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())

    receipt.update(installed_output_sha256=installed, reused=reused)
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print(f'Installed Cart UI native assets: {len(installed)} files; reused {len(reused)}', flush=True)


if __name__ == '__main__':
    from validation_workspace import temporary_validation_directory
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    parser.add_argument('--stage-only', action='store_true')
    parser.add_argument('--work', type=Path, default=None)
    args = parser.parse_args()

    def run_in(target_work):
        marker = target_work / '.zjcs-validation.json'
        if not marker.exists():
            marker.write_text('{"owner":"prepare_home_cart_windows_native"}\n', 'utf8')
        if args.install:
            install(target_work)
            return
        cache_dir = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'home-cart-windows-native'
        rec = PartnerNativeRecovery(
            native_cache=cache_dir,
            export_name='native-export',
            bundle='defaultpackage_assets_prefabs_ui_furniture.bundle',
        )
        HomeCartWindowsBinding(target_work, recovery=rec).build()
        if not args.stage_only:
            install(target_work)

    if args.work is not None:
        args.work.mkdir(parents=True, exist_ok=True)
        run_in(args.work.resolve())
    else:
        with temporary_validation_directory('cart-windows-') as tmp:
            run_in(Path(tmp))
