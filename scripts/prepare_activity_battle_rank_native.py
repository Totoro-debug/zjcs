"""Restore the entire original activity rank subtree, including original actors."""
from pathlib import Path
import hashlib
import json

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_activity_battle_health_native import HealthBinding
from prepare_activity_battle_team_native import build, install
from prepare_partner_card_native import script_id
from prepare_partner_card_native import sha
from prepare_partner_detail_actor_native import PartnerDetailActorBinding
from npc_travel_language import language
from validation_workspace import validation_directory


class RankBinding(PartnerDetailActorBinding, HealthBinding):
    resource_group = 'Rank'

    def record_actor_receipt(self, path, stage):
        receipt = json.loads(path.read_bytes())
        receipt['actor_decoder'] = self.actor_proof['decoder']
        receipt['actor_original_sources'] = self.actor_proof['sources']
        receipt['actor_payloads'] = {row['source']: dict(row,
            payload_sha256=sha((self.work / 'detail-actor-data' / row['package']).read_bytes()),
            materials_sha256=sha((self.work / 'detail-actor-data' / row['materials']).read_bytes()),
            regions_sha256=sha((self.work / 'detail-actor-data' / row['regions']).read_bytes()))
            for row in self.actor_rows.values() if row['source'] in self.native_actors}
        receipt['stage'] = stage
        path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')

    def replace(self, value):
        # The complete prefab pass may revisit already rebound managed types
        # after newly discovered native weapon dependencies have been queued.
        if isinstance(value, dict) and set(value) == {'class', 'ns', 'asm'}:
            if (value['class'], value['ns'], value['asm']) in {
                ('WorldStateGameObjectConfig', 'Zjcs.Official.WorldUI', 'Zjcs.Official.WorldUI'),
                ('WorldStateUIColorConfig', 'Zjcs.Official.WorldUI', 'Zjcs.Official.WorldUI'),
                ('StateMultiLanguageConfig', 'Zjcs.Official.PartnersUI', 'Zjcs.Official.PartnerDetailUI'),
                ('StateAnimatorConfig', 'Zjcs.Official.PartnersUI', 'Zjcs.Official.PartnersUI'),
            }:
                return value
        return super().replace(value)

    def load(self, source_guid):
        try:
            return super().load(source_guid)
        except ValueError as error:
            obj = self.objects.get(source_guid)
            if obj is not None:
                print('Rank dependency:', obj.type.name, obj.read(check_read=False).m_Name, flush=True)
            raise

    def register_scripts(self):
        super().register_scripts()
        base = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: base / 'ActivityRankUI' / (name + '.cs') for name in (
            'ActivityDomainBossRankPanel', 'TeamTopContent', 'TeamTopContentDataActivityDomainBoss',
            'TeamTopContentFirstItem', 'TopRankTeamItem', 'SwitchTopRangeBtn', 'RuleBtn',
            'UIBloomPlayerElementPart', 'UIParticleFlipFix')}
        paths.update({name: base / 'ActivityRankUI' / (name + '.cs') for name in
            ('DynamicHeightPanelRuleDialog', 'ScrollRectWithMaxHeight')})
        paths.update({
            'TeamMemberIcon': base / 'TeamUI/TeamMemberIcon.cs',
            'NpcIconUI': base / 'TeamUI/TeamNpcIconUI.cs',
            'UIBloomPlayerElement': base / 'ProfessionUI/UIBloomPlayerElement.cs',
            'AppearanceSystem.NpcAppearance': base / 'ActivityRankUI/NpcAppearance.cs',
            'RuleDialog': base / 'ProfessionUI/RuleDialog.cs',
            'TipInfo': base / 'ProfessionUI/TipInfo.cs',
            'AppearanceSkeletonPart': base / 'ProfessionUI/AppearanceSkeletonPart.cs',
        })
        for name, path in paths.items():
            pointer = self.script_pointer(path)
            self.scripts[name] = pointer
            namespace, _, leaf = name.rpartition('.')
            self.class_ids[script_id(namespace, leaf or name)] = pointer

    def complete_weapon_parts(self, selection):
        from prepare_partner_card_native import guid
        table = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        roots = []
        for skin in ('pWeapon_100_G_007', 'pWeapon_200_G_007'):
            name = skin + '_UI_Integrated'
            path = 'Assets/Prefabs/Appearance/PlayerPart/' + skin + '/' + name + '.prefab'
            entry, = [row for row in table if row['path'] == path]
            self.recovery.recover(name, self.work / 'activity-rank-native', bundle=entry['bundle'], native_relative=path.removeprefix('Assets/'))
            native, _, data, record = self.recovery.outputs[-1]
            for source in (native, Path(str(native) + '.meta')):
                if sha(source.read_bytes()) != self.checked[source.relative_to(self.cache / 'native-export-all').as_posix()]:
                    raise ValueError('Original UI weapon prefab changed')
                self.inputs[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
            for key, pointer in data['external'].items(): self.objects[pointer['guid']] = self.original(key)
            for key, file_id in data['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(data['documents'][file_id].values())), owner)
            for row in data['scripts']:
                full_name = '.'.join(filter(None, [row['namespace'], row['type']]))
                data['documents'][row['nativeFileID']]['MonoBehaviour']['m_Script'] = self.scripts[full_name]
            pointer = dict(fileID=data['root'], guid=hashlib.md5(('zjcs:activity-rank-part:' + name).encode()).hexdigest(), type=3)
            self.queue.append((Path('Resources/DailyChallenges') / self.resource_group / 'Parts' / (name + '.prefab'), pointer['guid'], self.replace(data['documents'])))
            roots.append(dict(Path=path.removeprefix('Assets/Prefabs/').removesuffix('.prefab'), Prefab=pointer))
        requests = [row['textureLoader']['path'] for row in self.actor_rows.values()
                    if row['npcId'] == 0 and row['textureLoader']]
        requests += [row['path'] for row in table if row['path'].endswith('/AppearanceConfig.asset')]
        dynamic = []
        for path in requests:
            entry, = [row for row in table if row['path'] == path]
            obj = self.recovery.apk.asset(entry['bundle'], entry['path'])
            source_guid = guid(Path(str(self.native / path.removeprefix('Assets/')) + '.meta'))
            self.objects[source_guid] = obj
            dynamic.append(dict(Path=path, Asset=self.load(source_guid)))
        textures = []
        for row in self.actor_rows.values():
            if row['npcId'] != 0: continue
            for material in json.loads((self.work / 'detail-actor-data' / row['materials']).read_bytes()):
                for texture in material['runtimeTextures']:
                    pointer = self.load_original(texture['original'])
                    resource = 'DailyChallenges/' + self.resource_group + '/Parts/Textures/' + texture['guid']
                    self.queue = [(Path('Resources') / (resource + '.asset') if value == pointer['guid'] else relative, value, data)
                                  for relative, value, data in self.queue]
                    textures.append(dict(Guid=texture['guid'], Resource=resource))
        config, = [row['Asset'] for row in dynamic if row['Path'].endswith('/AppearanceConfig.asset')]
        catalog = self.local_asset('activity-rank-parts', 'Original UI weapon parts', dict(config=config,
            assets=dynamic, prefabs=roots, textures=textures, atlases=list(self.native_atlases.values()), atlasSourceGuids=[]),
            ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativeDetailActorAssets.cs')
        self.queue = [(Path('Resources/DailyChallenges') / self.resource_group / 'PartAssets.asset' if value == catalog['guid'] else relative, value, data)
                      for relative, value, data in self.queue]
        selection['weapon_parts'] = roots

    def complete_rank_resources(self, font, selection):
        self.complete_weapon_parts(selection)
        rules_path = ROOT / '.tools/source-packages/current/884_156692/official-data/tables.json'
        tables = json.loads(rules_path.read_bytes())
        self.inputs[rules_path.relative_to(ROOT).as_posix()] = sha(rules_path.read_bytes())
        rules = []
        for key in ('ActivityDomainBoss', 'SecretAreaTop'):
            orders = sorted(row['Order'] for row in tables['rule_tip'] if row['RuleKey'] == key)
            if orders[0] != 0: raise ValueError('Original rule title is missing')
            rules.append(dict(Key=key, TitleKey='rule_tip_' + key + '_0',
                TipKeys=['rule_tip_' + key + '_' + str(order) for order in orders[1:]]))
        selection['rule_prefabs'] = []
        for name in ('RuleDialog', 'DynamicHeightPanelRuleDialog'):
            self.recovery.recover(name, self.work / 'activity-rank-native',
                bundle='defaultpackage_assets_prefabs_ui_ruledialog.bundle',
                native_relative='Prefabs/UI/RuleDialog/' + name + '.prefab')
            native, _, data, record = self.recovery.outputs[-1]
            for path in (native, Path(str(native) + '.meta')):
                if sha(path.read_bytes()) != self.checked[path.relative_to(self.cache / 'native-export-all').as_posix()]:
                    raise ValueError('Original rule prefab changed')
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            for key, pointer in data['external'].items(): self.objects[pointer['guid']] = self.original(key)
            for key, file_id in data['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(data['documents'][file_id].values())), owner)
            for row in data['scripts']:
                fields = data['documents'][row['nativeFileID']]['MonoBehaviour']
                full_name = '.'.join(filter(None, [row['namespace'], row['type']]))
                fields['m_Script'] = self.scripts[full_name]
                if row['type'] == 'ButtonSound':
                    owner = self.original(row['original'])
                    fields['ClickEvent'] = self.sound(self.recovery.fields(owner)['ClickEvent'], owner)
            docs = self.replace(data['documents'])
            self.queue.append((Path('Resources/DailyChallenges') / self.resource_group / (name + '.prefab'),
                hashlib.md5(('zjcs:activity-rank:' + name).encode()).hexdigest(), docs))
            selection['rule_prefabs'].append(record)
        # All embedded and dynamically instantiated native rows share the exact
        # configured CN font, as original MultiLanguageText.RefreshFont does.
        font_docs, = [data for _, value, data in self.queue if value == font['font_asset']['guid']]
        material = font_docs[font['font_asset']['fileID']]['MonoBehaviour']['material']
        language_guid = self.scripts['MultiLanguageText']['guid']
        graphic_guid = self.scripts['Spine.Unity.SkeletonGraphic']['guid']
        bootstrap = self.script_pointer(ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativeUiActor.cs')
        text_keys = {rule['TitleKey'] for rule in rules} | {key for rule in rules for key in rule['TipKeys']}
        for relative, _, data in self.queue:
            if relative.as_posix() == 'Resources/DailyChallenges/' + self.resource_group + '/texts.json':
                text_keys.update(row['key'] for row in json.loads(data)['texts'])
        selection['native_actor_bootstraps'] = []
        for relative, _, docs in list(self.queue):
            if not isinstance(docs, dict): continue
            texts = [fields['MonoBehaviour'] for fields in docs.values() if 'MonoBehaviour' in fields
                and fields['MonoBehaviour'].get('m_Script', {}).get('guid') == language_guid]
            language_objects = {item['m_GameObject']['fileID'] for item in texts}
            text_keys.update(item['myKey'] for item in texts if item['myKey'])
            for file_id, row in list(docs.items()):
                fields = row.get('MonoBehaviour')
                if not fields: continue
                if 'm_fontAsset' in fields and fields['m_GameObject']['fileID'] in language_objects:
                    fields['m_fontAsset'] = font['font_asset']; fields['m_sharedMaterial'] = material
                if fields.get('m_Script', {}).get('guid') != graphic_guid or not fields.get('skeletonDataAsset', {}).get('fileID'): continue
                go = fields['m_GameObject']['fileID']
                transform = docs[go]['GameObject']['m_Component'][0]['component']['fileID']
                added = max(docs) + 1
                docs[added] = dict(MonoBehaviour=dict(m_ObjectHideFlags=0,
                    m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0),
                    m_GameObject=dict(fileID=go), m_Enabled=1, m_EditorHideFlags=0, m_Script=bootstrap,
                    m_Name='', m_EditorClassIdentifier='', graphic=dict(fileID=file_id), renderRoot=dict(fileID=transform)))
                docs[go]['GameObject']['m_Component'].insert(1, dict(component=dict(fileID=added)))
                selection['native_actor_bootstraps'].append(dict(prefab=relative.as_posix(), graphic=file_id, added=added))
        texts, sources = language(text_keys)
        for key in ('{0}', 'ui_null', '-', 'NormalMaterial.Info1'):
            if key in text_keys and key not in texts: texts[key] = key
        if set(texts) != text_keys: raise ValueError('Unresolved rank/rule text: ' + repr(text_keys - set(texts)))
        self.inputs.update(sources)
        for relative, _, data in self.queue:
            if relative.as_posix() == 'Resources/DailyChallenges/' + self.resource_group + '/texts.json':
                content = (json.dumps(dict(texts=[dict(key=k, text=texts[k]) for k in sorted(texts)]), ensure_ascii=False, indent=2) + '\n').encode()
                break
        self.queue = [(r, g, content if r.as_posix() == 'Resources/DailyChallenges/' + self.resource_group + '/texts.json' else d) for r,g,d in self.queue]
        config = dict(ShowPlayerCountLimit=tables['game_settings']['TopPanelShowPlayerCountLimit'], Rules=rules)
        self.queue.append((Path('Resources/DailyChallenges') / self.resource_group / 'config.json', hashlib.md5(b'zjcs:activity-rank:config').hexdigest(),
            (json.dumps(config, ensure_ascii=False, indent=2) + '\n').encode()))
        self.queue.extend((path, value, raw) for path, (value, raw) in self.audio_outputs.items()
            if not any(existing == path for existing, _, _ in self.queue))


def main():
    binding = RankBinding(validation_directory())
    try:
        result = build(binding, consumer_type='ActivityDomainBossPanel', field='mRankPanel',
            prefab_name='ActivityDomainBossRankPanel', group='Rank',
            complete_template=dict(name='ActivityDomainBossTopPanel',
                bundle='defaultpackage_assets_prefabs_ui_top.bundle',
                native_relative='Prefabs/UI/Top/ActivityDomainBossTopPanel.prefab'),
            literal_text_keys=('ui_null',),
            extra_text_keys=('SecretAreeRankPanel_no_join', 'SecretAreeRankPanel_first_rank',
                'SecretAreeRankPanel_wait_desc', 'SecretAreeRankItem_damage_desc',
                'TopRangeType.Planes', 'TopRangeType.Domain', 'TopRangeType.Global'))
        binding.complete_rank_resources(result[2], result[1])
        # Weapon loaders/configuration are discovered after the base prefab pass.
        # Bind their original DLL script pointers as well as their asset closure.
        binding.script_guids.update(value for _, value, _ in binding.queue)
        for _, _, data in binding.queue:
            if isinstance(data, dict):
                resolved = binding.replace(data)
                data.clear(); data.update(resolved)
        install(binding, *result, group='Rank', receipt_name='activity-rank-native-source-manifest.json',
            extra_helpers=('prepare_activity_battle_rank_native.py', 'prepare_partner_detail_actor_native.py',
                'prepare_partner_detail_window_native.py', 'prepare_partner_panel_native.py',
                'prepare_partner_detail_actor_data.py', 'convert_official_skeletons.py', 'prepare_partner_native_battle.py'))
        path = ROOT / 'assets/reference/daily-challenges/activity-rank-native-source-manifest.json'
        binding.record_actor_receipt(path,
            'Complete original activity ranking subtree and rule dialogs bound. Production activity-tab integration and Player validation pending.')
    finally:
        binding.recovery.apk.archive.close()


if __name__ == '__main__': main()
