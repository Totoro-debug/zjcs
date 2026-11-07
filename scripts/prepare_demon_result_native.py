"""Restore the complete official DemonKing result and referenced UI prefabs.

Source object identities and native fields are verified by the shared recovery
pipeline. No UI geometry, controller states or prefab components are synthesized.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re

from UnityPy.enums import ClassIDType
import yaml

from official_apk import ROOT
from prepare_partner_card_native import Dumper, guid, script_id, sha
from prepare_partner_panel_native import PartnerPanelBinding
from prepare_current_partner_native import documents, identity, pointer as source_pointer
from official_manifest import read_asset_manifest
from validation_workspace import validation_directory

NAME = 'demon-result-native-source-manifest.json'
DESTINATION = ROOT / 'unity/Assets/Official/Combat/DemonResult'


class DemonResultBinding(PartnerPanelBinding):
    def restore(self, data):
        super().restore(data)
        # Binary typetrees omit YAML's version tags. In particular Rect v1
        # interprets width/height differently, collapsing the RawImage UVs.
        native = self.recovery.outputs[-1][0]
        relative = native.relative_to(self.cache / 'native-export-all').as_posix()
        if sha(native.read_bytes()) != self.checked[relative]:
            raise ValueError('Native serialization metadata changed: ' + str(native))
        self.inputs[native.relative_to(ROOT).as_posix()] = self.checked[relative]
        def versions(source, target):
            if isinstance(source, dict) and isinstance(target, dict):
                if 'serializedVersion' in source and 'serializedVersion' not in target:
                    target['serializedVersion'] = source['serializedVersion']
                for key in source.keys() & target.keys(): versions(source[key], target[key])
            elif isinstance(source, list) and isinstance(target, list) and len(source) == len(target):
                for left, right in zip(source, target): versions(left, right)
        original = documents(native.read_text('utf8'))
        for file_id, target in data['documents'].items(): versions(original[file_id], target)

    def register_scripts(self):
        super().register_scripts()
        official = ROOT / 'unity/Assets/Game/Runtime/Official'
        names = ('GameEndPanelDemonKing', 'GameEndPanelBoss', 'GameEndPanelBase',
                 'GameEndPlayerSettleGroup', 'GameEndPlayerSettleItem',
                 'EndPanelAnimEventReceiver', 'AwardLoopScrollRect', 'AwardPanelItem',
                 'ItemAwardIcon', 'FightMainPanelLeaveBtnUI', 'FightThumbsUpUI')
        paths = {name: official / 'BattleResultUI' / (name + '.cs') for name in names}
        paths['AwardContent'] = official / 'BattleResultUI/OfficialBattleAwardList.cs'
        paths.update({name: official / 'WorldUI' / filename for name, filename in (
            ('PopupWindow', 'WorldPopupWindow.cs'), ('PopupWindowSound', 'WorldPopupWindowSound.cs'),
            ('UIBloomGroup', 'WorldUIBloomGroup.cs'),
            ('UGUIJszx.VerticalLayoutGroupJSZX', 'VerticalLayoutGroupJSZX.cs'))})
        paths['MePlayerItemUI'] = official / 'ProfessionUI/MePlayerItemUI.cs'
        paths['ItemIconCustomAttachment'] = official / 'ProfessionUI/ItemIconCustomAttachment.cs'
        paths['TempRTUI'] = official / 'ActivityBattleUI/TempRTUI.cs'
        paths.update({name: official / 'ItemDetailUI' / (name + '.cs') for name in (
            'ItemDetailDialog', 'ItemDetailDialogUseResult', 'ItemDetailDialogAddition',
            'ItemSourceGo', 'ItemSourceItem', 'ItemDecomposContent', 'OutputAdditionDetailButton', 'UseResultIcon')})
        paths['GMFunction.ItemDetailGmTools'] = official / 'ItemDetailUI/ItemDetailGmTools.cs'
        paths['LoopVerticalScrollWithMaxHeight'] = official / 'SkillsUI/LoopVerticalScrollWithMaxHeight.cs'
        paths['UIToggleObject'] = official / 'SkillsUI/UIToggleObject.cs'
        paths['ShowRemainTimeInfo'] = official / 'ProfessionUI/ShowRemainTimeInfo.cs'
        paths['BgDialogElement'] = official / 'WorldUI/WorldBgDialogElement.cs'
        paths['AwardPanelLayoutElement'] = official / 'WorldUI/AwardPanelLayoutElement.cs'
        for name in ('ItemIconAttachmentConfig', 'ItemIconAttachmentCell', 'ItemIconAttachmentHint'):
            paths[name] = official / 'ProfessionUI' / (name + '.cs')
        for full_name, path in paths.items():
            metadata = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(metadata), type=3)
            self.scripts[full_name] = pointer
            namespace, _, leaf = full_name.rpartition('.')
            self.class_ids[script_id(namespace, leaf)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(),
                pointer=pointer, sha256=sha(path.read_bytes()), meta_sha256=sha(metadata.read_bytes()))

    def load(self, source_guid):
        obj = self.objects.get(source_guid)
        if obj is not None and obj.type.name == 'GameObject' and obj.read(check_read=False).m_Name == 'MonsterModel_3803_RT':
            proof_path = ROOT / 'assets/reference/battle-animation/demon-result-rt-prefab-source-manifest.json'
            proof = json.loads(proof_path.read_bytes())
            for relative, expected in proof['outputs'].items():
                path = ROOT / 'unity' / relative
                if sha(path.read_bytes()) != expected: raise ValueError('Demon RT dependency changed: ' + relative)
                self.inputs[path.relative_to(ROOT).as_posix()] = expected
            self.inputs[proof_path.relative_to(ROOT).as_posix()] = sha(proof_path.read_bytes())
            original = proof['staged_source']['original']
            path = ROOT / 'unity/Assets/Official/Combat/DemonResultRtPrefab/Resources/Combat/DemonResultRtPrefab/MonsterModel_3803_RT.prefab'
            data = documents(path.read_text('utf8'))
            root, = [file_id for file_id, row in data.items() if row.get('GameObject', {}).get('m_Name') == 'MonsterModel_3803_RT']
            if identity(obj) != original['originalRoot']:
                raise ValueError('Demon RT original root differs')
            result = dict(fileID=root, guid=guid(Path(str(path) + '.meta')), type=3)
            self.redirects[source_guid] = result
            self.prefab_object_pointers[identity(obj)] = result
            self.script_guids.add(result['guid'])
            return result
        return super().load(source_guid)

    def prefab(self, name, bundle, relative):
        self.recovery.recover(name, self.work / 'demon-result-native',
            bundle=bundle, native_relative=relative)
        native, _, panel, record = self.recovery.outputs[-1]
        self.restore(panel)
        for path in (native, Path(str(native) + '.meta')):
            key = path.relative_to(self.cache / 'native-export-all').as_posix()
            if sha(path.read_bytes()) != self.checked[key]:
                raise ValueError('Original demon result source changed: ' + str(path))
            self.inputs[path.relative_to(ROOT).as_posix()] = self.checked[key]
        original, = [key for key, value in panel['objectMap'].items() if value == panel['root']]
        self.selections[name] = dict(source=record, root=panel['root'], original=original, objects=len(panel['documents']))
        for row in panel['scripts']:
            full = '.'.join(filter(None, [row['namespace'], row['type']]))
            if full not in self.scripts:
                raise ValueError('Original result script has not been migrated: ' + full)
            panel['documents'][row['nativeFileID']]['MonoBehaviour']['m_Script'] = self.scripts[full]
            if row['type'] in ('AkEvent', 'UIClickSound'):
                owner = self.original(row['original'])
                field = 'data' if row['type'] == 'AkEvent' else 'AkEvent'
                # Keep the original event reference on the native component;
                # decode its PCM as well for the local event-ID sound host.
                self.sound(self.recovery.fields(owner)[field], owner)
        target_guid = hashlib.md5(('zjcs:official-demon-result:' + name).encode()).hexdigest()
        resolved = self.replace(panel['documents'])
        self.queue.append((Path(name + '.prefab'), target_guid, resolved))
        self.roots[name] = dict(fileID=panel['root'], guid=target_guid, type=3)

    def prepare(self):
        self.selections, self.roots = {}, {}
        code = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code'
        for relative in (['client-source/' + name + '.cs' for name in (
            'GameEndPanelDemonKing', 'GameEndPanelBoss', 'GameEndUIHandler', 'GameEndPlayerSettleGroup', 'GameEndPlayerSettleItem',
            'FightThumbsUpUI', 'AwardContent', 'AwardPanelItem', 'ItemAwardIcon', 'AwardLoopScrollRect',
            'FightMainPanelLeaveBtnUI', 'ItemAwardIconManager', 'ItemIconManager', 'TempRTUI',
            'ItemDetailDialog', 'ItemDetailDialogFactory', 'ItemDetailDialogUseResult', 'ItemDetailDialogAddition',
            'ItemSourceGo', 'ItemSourceItem', 'ItemDecomposContent', 'OutputAdditionDetailButton', 'UseResultIcon')]
            + ['common-source/Common/FightUtilsHandler.cs', 'common-source/Common/LanguageUtils.cs',
               'common-source/Common/StageInfoParser.cs', 'common-source/Common/StageType.cs',
               'common-source/Common/GameBattleEndType.cs', 'common-source/Common/GameSettings.cs',
               'common-source/Common/ItemSourceDataManager.cs', 'common-source/Common/ItemCountLimitInfoParser.cs',
               'common-source/GlobalColors.cs', 'client-source/GMFunction/ItemDetailGmTools.cs']):
            path = code / relative
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        for path in code.rglob('ItemSource*Agent.cs'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        self.prefab('GameEndPanelDemonKing',
            'defaultpackage_assets_prefabs_ui_fight_gameendpaneldemonking.bundle',
            'Prefabs/UI/Fight/GameEndPanelDemonKing.prefab')
        self.prefab('ItemAwardIcon', 'defaultpackage_assets_prefabs_ui_item.bundle',
            'Prefabs/UI/Item/ItemAwardIcon.prefab')
        self.prefab('ItemIconDefault', 'defaultpackage_assets_prefabs_ui_item.bundle',
            'Prefabs/UI/Item/ItemIconDefault.prefab')
        self.prefab('MonsterIcon_3803', 'defaultpackage_assets_prefabs_appearance_monster_3803.bundle',
            'Prefabs/Appearance/Monster/3803/MonsterIcon_3803.prefab')
        self.prefab('ItemDetailDialog', 'defaultpackage_assets_prefabs_ui_itemdetail.bundle',
            'Prefabs/UI/ItemDetail/ItemDetailDialog.prefab')
        self.dynamic()

    def dynamic(self):
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        config_row, = [row for row in records if row['path'].endswith('/ItemIconConfig.asset')]
        obj = self.recovery.apk.asset(config_row['bundle'], config_row['path'])
        native = self.native / config_row['path'].removeprefix('Assets/')
        source_guid = guid(Path(str(native) + '.meta'))
        self.objects[source_guid] = obj
        self.roots['ItemIconConfig'] = self.load(source_guid)
        self.script_guids.add(self.roots['ItemIconConfig']['guid'])
        # Follow the two original factory configurations instead of assuming an
        # item type's prefab. The selected 41021 has ItemType=108.
        factory_proof = {}
        for config, field, selected, lists in (
            ('ItemAwardIconManagerConfig', 'DefAwardPrefab', 'ItemAwardIcon', ('TypeAwardList', 'PrefabAwardList')),
            ('ItemIconManagerConfig', 'DefPrefab', 'ItemIconDefault', ('IdList', 'IdPrefabList'))):
            owner = self.recovery.apk.asset('scriptableconfig_modules_itemicon', '/' + config + '.asset')
            fields = self.recovery.fields(owner)
            if fields[lists[0]] or fields[lists[1]]: raise ValueError('Reinspect original item factory precedence: ' + config)
            if config == 'ItemIconManagerConfig' and 108 in fields['TypeList']:
                raise ValueError('Original demon reward has a specialized item prefab')
            root = source_pointer(fields[field], owner)
            if identity(root) != self.selections[selected]['original']:
                raise ValueError('Original item factory points to a different prefab')
            factory_proof[config] = dict(source=self.recovery.apk.provenance(owner),
                original_sha256=sha(owner.get_raw_data()), fields=fields)
        colors_obj = self.recovery.apk.asset('scriptableconfig_modules_systemsetting', '/GlobalColors.asset')
        colors = self.recovery.fields(colors_obj)
        self.dynamic_proof = dict(factories=factory_proof, colors=dict(source=self.recovery.apk.provenance(colors_obj),
            original_sha256=sha(colors_obj.get_raw_data()), fields=colors))
        attachment = self.recovery.apk.asset('scriptableconfig_modules_itemicon', '/ItemIconAttachmentConfig.asset')
        attachment_fields = self.recovery.fields(attachment)
        config_row, = [row for row in records if row['path'].endswith('/ItemIconAttachmentConfig.asset')]
        native = self.native / config_row['path'].removeprefix('Assets/')
        config_guid = guid(Path(str(native) + '.meta'))
        self.objects[config_guid] = attachment
        self.roots['AttachmentConfig'] = self.load(config_guid)
        self.script_guids.add(self.roots['AttachmentConfig']['guid'])
        attachments = []
        # Ordinary ItemIcon always creates HintTip, even with no hint to show;
        # ItemAwardIcon similarly creates then hides ExtraGetTip.
        for attachment_type in (1, 13):
            definition, = [row for row in attachment_fields['Templates'] if row['Type'] == attachment_type]
            selected, = [row for row in records if row['guid'] == definition['GuidProxy']['mAssetGuid']]
            obj, = [ref.deref() for path, ref in self.recovery.apk.container(selected['bundle'])
                if path.casefold() == selected['path'].casefold() and ref.deref().type.name == 'GameObject']
            name = obj.read().m_Name
            self.prefab(name, selected['bundle'], selected['path'].removeprefix('Assets/'))
            attachments.append(dict(type=attachment_type, guid=selected['guid'], name=name,
                asset=selected, pointer=self.roots[name]))
        self.dynamic_proof['attachments'] = dict(source=self.recovery.apk.provenance(attachment),
            original_sha256=sha(attachment.get_raw_data()), fields=attachment_fields, selected=attachments)
        tables_path = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-data/tables.json'
        tables = json.loads(tables_path.read_bytes())
        self.inputs[tables_path.relative_to(ROOT).as_posix()] = sha(tables_path.read_bytes())
        self.dynamic_proof['result_timing'] = {
            'normal': tables['game_settings']['GameEndDelayTime'],
            'round_limit': tables['game_settings']['GameEndOverRoundDelayTime'],
        }
        if any(value < 0 for value in self.dynamic_proof['result_timing'].values()):
            raise ValueError('Original result delays must be nonnegative')
        item, = [row for row in tables['item'] if row['ClassId'] == 41021]
        if item['ItemType'] != 108 or item['IconEffect'] or not item['CanOverlap']:
            raise ValueError('Reinspect original demon reward icon branch')
        row, = [r for r in records if r['path'] == 'Assets/Image/' + item['RawIcon'] + '.png'
            or r['address'] == 'Assets/Image/' + item['RawIcon']]
        sprite, = [ref.deref() for path, ref in self.recovery.apk.container(row['bundle'])
            if path.casefold() == row['path'].casefold() and ref.deref().type.name == 'Sprite']
        render_key, render_id = self.recovery.fields(sprite)['m_RenderDataKey']
        render_guid = ''.join(format(render_key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
        if render_guid != row['guid']: raise ValueError('Reward sprite identity differs')
        candidates = []
        name = self.name_key(sprite.read(check_read=False).m_Name)
        for key in [name] + [name + str(i) for i in range(100)]:
            for path in self.by_name.get(key, []):
                if path.suffix != '.asset': continue
                docs = documents(path.read_text('utf8'))
                if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: render_id}:
                    candidates.append(path)
        native, = candidates
        source_guid = guid(Path(str(native) + '.meta'))
        self.objects[source_guid] = sprite
        self.roots['RewardIcon'] = self.load(source_guid)
        self.script_guids.add(self.roots['RewardIcon']['guid'])
        self.dynamic_proof['reward_item'] = item
        self.dynamic_proof['reward_sprite'] = dict(source=self.recovery.apk.provenance(sprite),
            original_sha256=sha(sprite.get_raw_data()), manifest_asset=row)
        self.item_detail(records, tables, item)
        self.bind_language_font(records)

    def item_detail(self, records, tables, item):
        # The generic dialog factory is selected by the original CanUse/type
        # branches. Do not silently route another item through this closure.
        if item['ClassId'] != 41021 or item['CanUse'] or item['ShowUseDetail'] or item['RecycleReward']:
            raise ValueError('Reinspect original ordinary-item detail branches')
        if any(row['ClassId'] == item['ClassId'] for row in tables['item_count_limit']):
            raise ValueError('Reinspect original detail count-limit branch')
        groups = tables['game_settings']['OutputAdditionSetting']['ItemOutputGroupTypeDict']
        if str(item['ClassId']) in groups: raise ValueError('Reinspect original detail output-addition branch')
        path = 'Assets/Image/UI/ItemDetailDialog/ImgPurple_PopUps.png'
        selected, = [row for row in records if row['path'] == path]
        sprite, = [ref.deref() for source, ref in self.recovery.apk.container(selected['bundle'])
            if source.casefold() == path.casefold() and ref.deref().type.name == 'Sprite']
        render_key, render_id = self.recovery.fields(sprite)['m_RenderDataKey']
        render_guid = ''.join(format(render_key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
        if render_guid != selected['guid']: raise ValueError('Detail quality sprite identity differs')
        candidates = []
        name = self.name_key(sprite.read(check_read=False).m_Name)
        for key in [name] + [name + str(i) for i in range(100)]:
            for native in self.by_name.get(key, []):
                if native.suffix != '.asset': continue
                docs = documents(native.read_text('utf8'))
                if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: render_id}:
                    candidates.append(native)
        native, = candidates
        source_guid = guid(Path(str(native) + '.meta')); self.objects[source_guid] = sprite
        self.roots['DetailQualityBackground'] = self.load(source_guid)
        self.script_guids.add(self.roots['DetailQualityBackground']['guid'])
        colors = self.dynamic_proof['colors']['fields']['QualityColorConfig']['TextColorLists']
        normal, = [row for row in colors if row['ThemeColorType'] == 0]
        color = normal['TextColors'][normal['TypeList'].index(item['Quality'])]
        if color['EnableGradient']: raise ValueError('Migrate original gradient quality text before showing this item')
        self.dynamic_proof['item_detail'] = dict(
            item_id=item['ClassId'], type_key='ItemType.MonsterProduct', name_key='item_41021_name',
            description_key='item_41021_desc', function_description_key='item_41021_func_desc',
            show_player_item_count=item['ShowPlayerItemCount'], is_count_limit=False, show_use_detail=False,
            has_output_addition_group=False, quality_text_color=color,
            quality_background=dict(source=self.recovery.apk.provenance(sprite),
                original_sha256=sha(sprite.get_raw_data()), manifest_asset=selected))
        self.dynamic_proof['item_detail']['source_analysis'] = self.item_sources(tables, item)

    def item_sources(self, tables, item):
        # Evaluate the selected item's closed branches in every registered
        # ItemSourceDataManager agent. Do not infer map sources from a table
        # missing in the shared System-table export: Map has a separate bundle.
        item_id, item_type = item['ClassId'], item['ItemType']
        columns = {'quick_buy': 'ItemId', 'item_source': 'Id',
            'item_source_redirect': 'SourceItemId', 'item_merge': 'ProductId',
            'cooking': 'Id', 'shop_goods': 'ItemId', 'trading_post_item': 'ClassId',
            'treasure': 'ClassId'}
        for table, column in columns.items():
            if any(row[column] == item_id for row in tables[table]):
                raise ValueError('Migrate original item source agent for ' + table)
        settings = tables['game_settings']
        if str(item_type) in settings['SkillTypes'] or item_type in settings['SkillPieceTypes'] or item_type in (305, 306):
            raise ValueError('Migrate original skill/treasure source agents')
        # All explicit 41021 reward rules belong to DemonInvade/top rewards.
        # Stage and chest source agents consume Awards/RewardRules instead.
        rules = {row['Id'] for row in tables['reward_rule'] if item_id in row['IdList']}
        for table, field in (('stage', 'Awards'), ('chest', 'RewardRules')):
            if any(rules.intersection(row[field] or []) for row in tables[table]):
                raise ValueError('Migrate original reward-derived source agent for ' + table)
        for row in tables['random_reward']:
            selector = row['RandomItemRangeSelector']
            if item_id in (row['IdList'] or []) or item_type in (row['ItemTypes'] or []):
                raise ValueError('Migrate original random reward source expansion')
            if selector and selector['_type'] not in ('RandomItemRangeProfessionSkillSelector', 'RandomItemRangeTreasureGachaSelector'):
                raise ValueError('Inspect new random item range selector')
            if not row['IdList'] and not row['ItemTypes'] and not selector:
                raise ValueError('Inspect unconstrained original random reward')
        from official_tables import Constructors
        code = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code'
        constructors = Constructors(code)
        obj = self.recovery.apk.asset('config_luban_map', '/item_map_location.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        locations = constructors.table(raw, 'TbItemMapLocationInfo')
        if any(row['ItemId'] == item_id for row in locations):
            raise ValueError('Migrate original map-resource item source')
        for name in constructors.sources:
            for folder in ('common-source/Common', 'common-source/Common.LubanGen'):
                path = code / folder / (name + '.cs')
                if path.is_file(): self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        path = ROOT / 'scripts/official_tables.py'
        self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        # PetGacha and Legion agents yield no entries in the official client.
        # ArenaSeasonLimitedTreasure derives only Treasure/Piece item ids.
        return dict(has_sources=False, direct_columns=columns, reward_rule_ids=sorted(rules),
            map_locations=dict(source=self.recovery.apk.provenance(obj),
                payload_sha256=sha(raw), count=len(locations), matching_count=0))

    def bind_language_font(self, records):
        config, = [row for row in records if row['path'] ==
            'Assets/TextMesh Pro/Assets/Fonts & Materials/MultiLanguageFontConfig.json']
        obj = self.recovery.apk.asset(config['bundle'], config['path'])
        name = json.loads(obj.read().m_Script)['RegionFontAssetDict']['CN']['Default']
        selected, = [row for row in records if row['path'] == name or row['address'] == name]
        obj = self.recovery.apk.asset(selected['bundle'], selected['path'])
        native = self.native / selected['path'].removeprefix('Assets/')
        source_guid = guid(Path(str(native) + '.meta'))
        self.objects[source_guid] = obj
        self.load(source_guid)
        font = self.chinese_font(records)
        font_data, = [data for _, key, data in self.queue if key == font['font_asset']['guid']]
        fields = font_data[font['font_asset']['fileID']]['MonoBehaviour']
        for key in ('m_SourceFontFile', 'm_SourceFontFile_EditorRef'): fields[key] = font['source_font']
        fields['m_SourceFontFileGUID'] = font['source_font']['guid']
        # Reproduce MultiLanguageText.RefreshFont's CN runtime selection. The
        # serialized preview font has no raw source font bound on Android.
        font['localized_texts'] = []
        language_script = self.scripts['MultiLanguageText']
        for target, _, data in self.queue:
            if not isinstance(data, dict): continue
            language_objects = {row['MonoBehaviour']['m_GameObject']['fileID']
                for row in data.values() if row.get('MonoBehaviour', {}).get('m_Script') == language_script}
            for file_id, row in data.items():
                text = row.get('MonoBehaviour', {})
                if 'm_fontAsset' not in text or text['m_GameObject']['fileID'] not in language_objects: continue
                font['localized_texts'].append(dict(output=target.as_posix(), component=file_id,
                    preview_font=text['m_fontAsset'], preview_material=text['m_sharedMaterial']))
                text['m_fontAsset'] = font['font_asset']
                text['m_sharedMaterial'] = fields['material']
        if not font['localized_texts']: raise ValueError('No original demon result language fields were bound')
        self.dynamic_proof['font'] = font

    def build(self):
        self.prepare()
        # Resolving a dependency may append another dependency to this queue.
        for target, _, data in self.queue:
            if isinstance(data, dict):
                resolved = self.replace(data)
                data.clear()
                data.update(resolved)
        self.catalog()
        self.queue.extend((path, key, raw) for path, (key, raw) in self.audio_outputs.items())
        aliases = {key: hashlib.md5(('zjcs:demon-result-resource:' + path.as_posix()).encode()).hexdigest()
            for path, key, _ in self.queue}
        def remap(value):
            if isinstance(value, dict):
                return {key: aliases.get(child, child) if key in ('guid', 'm_SourceFontFileGUID') else remap(child)
                    for key, child in value.items()}
            if isinstance(value, list): return [remap(child) for child in value]
            return value
        self.roots = remap(self.roots)
        self.recovery.finalize()
        folder = self.work / 'demon-result-assets'
        outputs = {}
        for target, target_guid, data in self.queue:
            path = folder / target
            target_guid = aliases[target_guid]
            if isinstance(data, dict):
                data = remap(data)
                text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id, content in data.items():
                    kind, = content
                    text += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                    text += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
                if 'NativeMeshes' in target.parts:
                    text = re.sub(r': null(?=\s*(?:\n|$))', ': ', text)
                raw = text.encode('utf8')
            else:
                raw = data
                if raw.startswith(b'%YAML'):
                    raw = re.sub(rb'(?<=guid: )[a-f0-9]{32}',
                        lambda match: aliases.get(match[0].decode(), match[0].decode()).encode(), raw)
            if target.as_posix() in outputs and outputs[target.as_posix()] != sha(raw):
                raise ValueError('Different original payloads resolve to one native output: ' + str(target))
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            suffix = path.suffix.lower()
            importer = ('PrefabImporter' if suffix == '.prefab' else 'ShaderImporter' if suffix == '.shader'
                else 'TextScriptImporter' if suffix == '.json' else 'TrueTypeFontImporter'
                if suffix in ('.ttf', '.otf') else 'AudioImporter' if suffix == '.wav' else 'NativeFormatImporter')
            metadata = Path(str(path) + '.meta')
            metadata.write_text('fileFormatVersion: 2\nguid: ' + target_guid + '\n' + importer +
                ':\n  externalObjects: {}\n', 'utf8', newline='\n')
            outputs[target.as_posix()] = sha(raw)
            outputs[target.as_posix() + '.meta'] = sha(metadata.read_bytes())
        receipt = dict(manifest=self.recovery.apk.version, inputs=self.inputs, objects=self.proof,
            outputs=outputs, native_recovery=self.recovery.proof, selections=self.selections,
            roots=self.roots, guid_aliases=aliases, dynamic=self.dynamic_proof, script_bindings=self.script_sources, audio=self.audio_proof,
            generator_sha256=sha(Path(__file__).read_bytes()))
        (self.work / NAME).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged complete demon result:', len(outputs), 'files', flush=True)

    def catalog(self):
        from npc_travel_language import language
        keys = {'ui_ItemIcon_count_key', 'item_41021_name', 'stage_4800_name'}
        def collect(value):
            if isinstance(value, dict):
                if value.get('myKey') and not value['myKey'].startswith('{'): keys.add(value['myKey'])
                for child in value.values(): collect(child)
            elif isinstance(value, list):
                for child in value: collect(child)
        for _, _, value in self.queue: collect(value)
        # GM fixed-count button labels are literal keys. The original language
        # service returns those keys when no dictionary entry exists.
        literal_keys = {'+1', '+10', '+100', '-1', '-10', '-100'}
        self.dynamic_proof['literal_text_keys'] = sorted(keys & literal_keys)
        keys -= literal_keys
        keys.update(('ItemType.MonsterProduct', 'Quality.Purple'))
        optional, optional_inputs = language({'item_41021_desc', 'item_41021_func_desc'})
        keys.update(optional); self.inputs.update(optional_inputs)
        texts, inputs = language(keys)
        if set(texts) != keys: raise ValueError('Original result language is absent: ' + repr(keys - texts.keys()))
        self.inputs.update(inputs)
        colors = self.dynamic_proof['colors']['fields']
        payload = dict(schema=1, manifest=self.recovery.apk.version, item=self.dynamic_proof['reward_item'],
            end_delays=self.dynamic_proof['result_timing'],
            item_detail=self.dynamic_proof['item_detail'],
            attachments=[dict(type=row['type'], guid=row['guid']) for row in self.dynamic_proof['attachments']['selected']],
            images=[dict(path=self.dynamic_proof['reward_item']['RawIcon'],
                guid=self.dynamic_proof['reward_sprite']['manifest_asset']['guid']),
                dict(path='UI/ItemDetailDialog/ImgPurple_PopUps',
                guid=self.dynamic_proof['item_detail']['quality_background']['manifest_asset']['guid'])],
            texts=[dict(key=k, text=texts[k]) for k in sorted(texts)],
            colors={k: colors[k] for k in ('Green', 'White', 'TextColorConfig', 'QualityColorConfig')})
        bindings_guid = hashlib.md5(b'zjcs:official-demon-result:bindings').hexdigest()
        self.queue.append((Path('bindings.json'), bindings_guid,
            (json.dumps(payload, ensure_ascii=False, indent=2) + '\n').encode('utf8')))
        script = ROOT / 'unity/Assets/Game/Runtime/Presentation/Home/OfficialDemonResultAssets.cs'
        self.inputs[script.relative_to(ROOT).as_posix()] = sha(script.read_bytes())
        self.inputs[script.relative_to(ROOT).as_posix() + '.meta'] = sha(Path(str(script) + '.meta').read_bytes())
        audio_script = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionAudioCatalog.cs'
        for path in (audio_script, Path(str(audio_script) + '.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        audio_guid = hashlib.md5(b'zjcs:official-demon-result:audio-catalog').hexdigest()
        audio_fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
            m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
            m_Script=dict(fileID=11500000, guid=guid(Path(str(audio_script) + '.meta')), type=3),
            m_Name='Original demon result audio', m_EditorClassIdentifier='',
            Events=[dict(EventId=row['event']['id'], Clip=row['pointer'])
                for row in sorted(self.audio_proof.values(), key=lambda row: row['event']['id'])])
        if not audio_fields['Events'] or len({row['EventId'] for row in audio_fields['Events']}) != len(audio_fields['Events']):
            raise ValueError('Original demon result audio catalog is empty or duplicated')
        self.queue.append((Path('Resources/Combat/DemonResult/audio.asset'), audio_guid, {11400000: dict(MonoBehaviour=audio_fields)}))
        fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
            m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
            m_Script=dict(fileID=11500000, guid=guid(Path(str(script) + '.meta')), type=3),
            m_Name='Official demon result assets', m_EditorClassIdentifier='',
            Panel=self.roots['GameEndPanelDemonKing'], AwardIcon=self.roots['ItemAwardIcon'],
            ItemIcon=self.roots['ItemIconDefault'], Boss=self.roots['MonsterIcon_3803'],
            ItemConfig=self.roots['ItemIconConfig'], RewardIcon=self.roots['RewardIcon'],
            Font=self.dynamic_proof['font']['font_asset'],
            Audio=dict(fileID=11400000, guid=audio_guid, type=2),
            ItemDetail=self.roots['ItemDetailDialog'], DetailQualityBackground=self.roots['DetailQualityBackground'],
            AttachmentConfig=self.roots['AttachmentConfig'],
            AttachmentPrefabs=[row['pointer'] for row in self.dynamic_proof['attachments']['selected']],
            Bindings=dict(fileID=4900000, guid=bindings_guid, type=3))
        self.queue.append((Path('Resources/Combat/DemonResult/catalog.asset'),
            hashlib.md5(b'zjcs:official-demon-result:catalog-asset').hexdigest(), {11400000: dict(MonoBehaviour=fields)}))


def install(work):
    receipt = json.loads((work / NAME).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild staged demon result after generator changes')
    for path, expected in receipt['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected:
            raise ValueError('Staged demon result input changed: ' + path)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Staged demon result script changed: ' + str(path))
    staged = work / 'demon-result-assets'
    for path, expected in receipt['outputs'].items():
        if sha((staged / path).read_bytes()) != expected:
            raise ValueError('Staged demon result output changed: ' + path)
    # This native graph owns its asset identities, like the existing activity
    # UI pipeline. It does not overwrite shared partner prefabs or mutable fonts.
    prior_path = ROOT / 'assets/reference' / NAME
    prior = json.loads(prior_path.read_bytes()) if prior_path.exists() else {}
    plan = []
    for relative in receipt['outputs']:
        if relative.endswith('.meta'): continue
        source = staged / relative
        target = DESTINATION / relative
        if target.is_file() and target.read_bytes() != source.read_bytes():
            expected = prior.get('installed_output_sha256', {}).get(target.relative_to(ROOT).as_posix())
            if expected != sha(target.read_bytes()) or Path(str(target) + '.meta').read_bytes() != Path(str(source) + '.meta').read_bytes():
                raise ValueError('Existing dependency has unrecorded different bytes: ' + str(target))
        plan.append((source, target))
    installed, folders = {}, {}
    for source, target in plan:
        metadata = Path(str(target) + '.meta')
        if not target.exists() or target.read_bytes() != source.read_bytes():
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read_bytes())
            metadata.write_bytes(Path(str(source) + '.meta').read_bytes())
        for path in (target, metadata): installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        parent = target.parent
        while parent.is_relative_to(DESTINATION):
            path = Path(str(parent) + '.meta')
            if not path.exists():
                path.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(
                    ('zjcs:demon-result-folder:' + parent.relative_to(ROOT).as_posix()).encode()).hexdigest() +
                    '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
            folders[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            parent = parent.parent
    receipt['installed_output_sha256'] = installed
    receipt['folder_meta_sha256'] = folders
    (ROOT / 'assets/reference' / NAME).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed complete demon result:', len(plan), 'assets')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    if parser.parse_args().install: install(validation_directory())
    else: DemonResultBinding(validation_directory()).build()
