"""Convert the pinned NPC gift and dialogue prefabs, tables and Chinese text."""
import copy
import hashlib
import json
import struct
from official_apk import ROOT
from official_tables import Constructors
from official_manifest import read_asset_manifest
from prepare_official_panels import PanelExporter, unique_sibling_paths
from official_unity_schema import ScriptSchemas
from UnityPy.classes.PPtr import PPtr
from npc_travel_language import language
from prepare_home_ui import HomeExporter


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False), 'utf8')


def extract_bindings(exporter, panels, sources, classes):
    schemas = ScriptSchemas()
    bindings, receipts = {}, []
    def convert(value, obj):
        if isinstance(value, list): return [convert(v, obj) for v in value]
        if not isinstance(value, dict): return value
        if 'm_PathID' in value:
            if not value['m_PathID']: return None
            ptr = PPtr(m_FileID=value['m_FileID'], m_PathID=value['m_PathID'], assetsfile=obj.assets_file)
            target = ptr.deref().read(check_read=False)
            if ptr.type.name == 'GameObject': return exporter.object_path(ptr)
            if hasattr(target, 'm_GameObject') and target.m_GameObject.path_id: return exporter.object_path(target.m_GameObject)
            return dict(asset_type=ptr.type.name, name=target.m_Name, source=exporter.apk.provenance(ptr.deref()))
        return {k: str(v) if k == 'rid' else convert(v, obj) for k, v in value.items()}
    for bundle in {p['source']['bundle'] for p in panels.values()}:
        for obj in exporter.apk.objects(bundle):
            if obj.type.name != 'MonoBehaviour': continue
            cls, raw, basic = exporter.mono(obj)
            if cls not in classes: continue
            fields = schemas.read(obj)
            path = exporter.object_path(basic.m_GameObject)
            bindings.setdefault(path, {})[cls] = {k: convert(v, obj) for k, v in fields.items()
                if k not in ['m_GameObject', 'm_Script', 'm_Name', 'm_Enabled']}
            receipts.append(dict(exporter.apk.provenance(obj), object_path=path, script=cls,
                sha256=hashlib.sha256(raw).hexdigest(), operation='Complete original schema read including asset references'))
    def enrich(node):
        if node['path'] in bindings: node.setdefault('original_bindings', {}).update(bindings[node['path']])
        for child in node['children']: enrich(child)
    for panel in panels.values(): enrich(panel['root'])
    def descendants(node):
        yield node
        for child in node['children']: yield from descendants(child)
    history = next(n for n in descendants(panels['npc_dialogue_list_item']['root'])
                   if 'NpcPanelDialogueListDialogueItem' in n.get('original_bindings', {}))
    panels['npc_dialogue_history_entry'] = dict(root=copy.deepcopy(history), source=panels['npc_dialogue_list_item']['source'])
    sources['treasure_bindings'] = receipts


def build():
    art = ROOT / 'assets/official'
    base = json.loads((art / 'ui.json').read_text('utf8'))
    detail = json.loads((art / 'partner-detail/panels.json').read_text('utf8'))
    panels = {key: copy.deepcopy(base[key]) for key in ['partners', 'partner_row', 'item_npc_gift', 'partner_reduce']}
    panels['partner_detail'] = detail['prefabs']['partner_detail']
    exporter = HomeExporter()
    # Keep particle conversion isolated to this module's payload directory.
    import prepare_map_regions, official_particle_codec
    prepare_map_regions.ART = art / 'partner-interactions'
    official_particle_codec.ART = art / 'partner-interactions'
    (art / 'partner-interactions/sprites').mkdir(parents=True, exist_ok=True)
    (art / 'partner-interactions/map-particles').mkdir(parents=True, exist_ok=True)
    # Refresh original cards/panel too: the old catalog predates m_Enabled curves.
    for key, name in {'partners':'NpcPanel', 'partner_row':'NpcPanelNpcItem'}.items():
        panels[key] = exporter.ui('prefabs_ui_npc', '/' + name + '.prefab')
        unique_sibling_paths(panels[key]['root'])
    for key, name in {'npc_dialogue': 'NpcDialoguePanel', 'npc_dialogue_item': 'NpcDialoguePanelItem',
                      'npc_dialogue_option': 'NpcDialogueOptionBtn', 'npc_dialogue_emoji_option': 'NpcDialogueEmojiOptionBtn',
                      'npc_dialogue_list_item': 'NpcPanelDialogueItem',
                      'npc_level_notify': 'NpcFriendshipLevelUpNotifyDialog',
                      'npc_level_notify_item': 'NpcFriendshipLevelUpNotifyDialogItem'}.items():
        panels[key] = exporter.ui('prefabs_ui_npc', '/' + name + '.prefab')
        unique_sibling_paths(panels[key]['root'])
    panels['npc_dialogue_got'] = exporter.ui('prefabs_ui_item', '/GotGrayObj.prefab')
    sources = {}
    extract_bindings(exporter, panels, sources, {
        'NpcPanel', 'NpcPanelNpcList', 'NpcPanelNpcItem', 'NpcPanelDialogueList', 'NpcPanelDialogueListNpcItem',
        'NpcPanelDialogueListDialogueItem', 'NpcDialoguePanel', 'NpcDialoguePanelItem',
        'NpcDialoguePanelItemContent', 'NpcDialoguePanelRewardItemContent', 'NpcDialoguePanelOption',
        'NpcDialoguePanelOptionItem', 'NpcDetailPanelGiftContent', 'NpcTalkUI', 'TMP_TypeWriterEffect',
        'ItemIconNpcGift', 'ItemProgress', 'NpcFriendshipLevelContent', 'NpcGiftBlueDotItem',
        'NpcMeetProgressItem', 'NpcFriendshipLevelShow', 'NpcFriendshipTagItem',
        'NpcFriendshipLevelUpNotifyDialog', 'PopupWindow', 'FilterSortPanel',
        'CollectSortGroup', 'CollectSortGroupItem', 'AchievementPanel', 'AchievementPanelBase',
        'AchievementPanelItem', 'PanelTitle', 'TabObjectTransition'})
    # Preserve the existing cultivation bindings when enriching the same node.
    def merge_old(node, old):
        if node['path'] == old['path']:
            bindings = copy.deepcopy(old.get('original_bindings', {}))
            bindings.update(node.get('original_bindings', {}))
            if bindings: node['original_bindings'] = bindings
        old_children = {n['path']: n for n in old['children']}
        for child in node['children']:
            if child['path'] in old_children: merge_old(child, old_children[child['path']])
    merge_old(panels['partner_detail']['root'], detail['prefabs']['partner_detail']['root'])
    layouts, triggers, int_states = {}, {}, {}
    for bundle in {p['source']['bundle'] for p in panels.values()}:
        for obj in exporter.apk.objects(bundle):
            if obj.type.name == 'Animator':
                animator = obj.read()
                path = exporter.object_path(animator.m_GameObject)
                if not (path.startswith('/NpcDialoguePanelItem/') or path.endswith('/FilterSortPanel') and path.startswith('/NpcPanel/')) or not animator.m_Controller.path_id: continue
                controller = animator.m_Controller.read()
                tree = controller.object_reader.read_typetree()
                names = dict(tree['m_TOS'])
                clips = [ptr.read().m_Name for ptr in controller.m_AnimationClips]
                mapping = {}
                integers = {}
                for machine in tree['m_Controller']['m_StateMachineArray']:
                    machine = machine['data']
                    transitions = list(machine['m_AnyStateTransitionConstantArray'])
                    for source_state in machine['m_StateConstantArray']:
                        transitions += source_state['data']['m_TransitionConstantArray']
                    for transition in transitions:
                        transition = transition['data']
                        conditions = transition['m_ConditionConstantArray']
                        if len(conditions) != 1 or conditions[0]['data']['m_ConditionMode'] not in [1,6]: continue
                        state = machine['m_StateConstantArray'][transition['m_DestinationState']]['data']
                        blend = state['m_BlendTreeConstantArray'][0]['data']['m_NodeArray'][0]['data']
                        condition = conditions[0]['data']
                        parameter = names[condition['m_EventID']]
                        if condition['m_ConditionMode'] == 1: mapping[parameter] = clips[blend['m_ClipID']]
                        else: integers.setdefault(parameter,{})[str(int(condition['m_EventThreshold']))] = clips[blend['m_ClipID']]
                triggers[path] = mapping
                int_states[path] = integers
                sources.setdefault('animator_controllers', []).append(dict(exporter.apk.provenance(controller.object_reader),
                    sha256=hashlib.sha256(controller.object_reader.get_raw_data()).hexdigest(), triggers=mapping, integers=integers))
            if obj.type.name != 'MonoBehaviour': continue
            cls, raw, basic = exporter.mono(obj)
            if cls != 'TextMeshProUGUI': continue
            length = struct.unpack_from('<i', raw, 88)[0]
            aligned = (92 + length + 3) & ~3
            shared = struct.unpack_from('<i', raw, aligned + 28)[0]
            materials = struct.unpack_from('<i', raw, aligned + 44 + 12 * shared)[0]
            style = aligned + 12 * (shared + materials)
            wrapping = struct.unpack_from('<i', raw, style + 256)[0]
            overflow = struct.unpack_from('<i', raw, style + 264)[0]
            if wrapping not in (0, 1) or overflow not in range(7): raise ValueError('Unexpected TMP layout')
            path = exporter.object_path(basic.m_GameObject)
            layouts[path] = dict(word_wrap=bool(wrapping), overflow=overflow,
                                 margin=list(struct.unpack_from('<4f', raw, style + 352)))
            if raw[style + 204]: layouts[path]['auto_size'] = list(struct.unpack_from('<2f', raw, style + 208))
    def enrich(node):
        if node['path'] in layouts and 'text' in node: node['text'].update(layouts[node['path']])
        if node['path'] in triggers: node['animator_triggers'] = triggers[node['path']]
        if node['path'] in int_states: node['animator_integers'] = int_states[node['path']]
        for child in node['children']: enrich(child)
    for panel in panels.values(): enrich(panel['root'])
    raw_path = ROOT / '.tools/source-packages/official-data/npc_dialogue.g.bytes'
    rows = Constructors().table(raw_path.read_bytes(), 'TbNpcDialogueInfo')
    keys = {f"npc_dialogue_{r['ClassId']}_{r['Node']}_text" for r in rows}
    options = {f"npc_dialogue_{r['ClassId']}_{n}_option_text" for r in rows if len(r['NextNodes'] or []) > 1 for n in r['NextNodes']}
    ui_keys = set()
    def language_keys(node):
        if node.get('localization_key'): ui_keys.add(node['localization_key'])
        for child in node['children']: language_keys(child)
    for panel in panels.values(): language_keys(panel['root'])
    ui_keys.update({'ui_npc_change_tag', 'ui_npc_input_tag', 'ui_npc_change_tag_success',
                    'PLAYER_NAME_INCLUDE_NONSENSE', 'PLAYER_NPC_CUSTOM_FRIENDSHIP_TAG_IS_EMPTY',
                    'PLAYER_NPC_CUSTOM_FRIENDSHIP_TAG_TOO_LONG',
                    'CollectSortType.Friendship', 'CollectSortType.ActivateTime',
                    'ui_achievement_content_NpcFriendshipLevel', 'ui_achievement_title_NpcFriendshipLevel',
                    'AchievementPanel.CanNotGetReward', 'AchievementPanel.AlreadyGotReward'})
    # Notification titles are serialized fields rather than MultiLanguageText keys.
    def bound_keys(node):
        for fields in node.get('original_bindings', {}).values():
            for name in ['mMeetTitleKey', 'mLevelUpTitleKey']:
                if fields.get(name): ui_keys.add(fields[name])
        for child in node['children']: bound_keys(child)
    for panel in panels.values(): bound_keys(panel['root'])
    texts, language_sources = language(keys | options | ui_keys)
    missing = (keys | options) - texts.keys()
    if missing: raise ValueError('Missing original dialogue text: ' + repr(sorted(missing)[:12]))
    def localize(node):
        key = node.get('localization_key')
        if key in texts and 'text' in node: node['text']['value'] = texts[key]
        for child in node['children']: localize(child)
    for panel in panels.values(): localize(panel['root'])
    tags = json.loads((art / 'rules.json').read_text('utf8'))['npc_friendship_tag']
    tag_backgrounds = {}
    manifest_raw = exporter.apk.archive.read('assets/yoo/DefaultPackage/PackageManifest_DefaultPackage_854_156383.v3.bytes')
    addresses = {r['address'].lower():r for r in read_asset_manifest(manifest_raw) if r['address']}
    for tag in tags:
        address = ('assets/image/' + tag['TagBg']).lower()
        asset = addresses[address];path = asset['path'].lower()
        matches = [ptr for name,ptr in exporter.apk.container(asset['bundle']) if name.lower()==path and ptr.type.name=='Sprite']
        if len(matches)!=1: raise ValueError('Original friendship tag Sprite is not unique: '+path)
        tag_backgrounds[str(tag['ClassId'])] = exporter.sprite(matches[0])
    from home_ui_audio import convert as convert_audio
    native = exporter.apk.asset('prefabs_ui_npc', '/NpcPanelNpcItem.prefab')
    component = next(p.component.deref() for p in native.read().m_Component
                     if p.component.type.name == 'MonoBehaviour' and exporter.mono(p.component.deref())[0] == 'NpcPanelNpcItem')
    audio = convert_audio(exporter, PPtr(**exporter.schemas.read(component)['MeetSound']['WwiseObjectReference'],
                          assetsfile=component.assets_file), art / 'partner-interactions/audio')
    save(art / 'partner-interactions/panels.json', dict(prefabs=panels, texts=texts, tag_backgrounds=tag_backgrounds, meet_sound=audio))
    save(art / 'partner-interactions/dialogues.json', rows)
    code = ROOT / '.tools/source-packages/official-code'
    names = ['client-source/NpcDetailPanelGiftContent.cs', 'client-source/ItemIconNpcGift.cs',
             'client-source/NpcDetailPanelDetailContent.cs', 'client-source/NpcTalkUI.cs',
             'client-source/NpcPanelDialogueList.cs', 'client-source/NpcPanelDialogueListNpcItem.cs',
             'client-source/NpcPanelDialogueListDialogueItem.cs', 'client-source/NpcDialoguePanel.cs',
             'client-source/NpcDialoguePanelItem.cs', 'client-source/NpcDialoguePanelItemContent.cs',
             'client-source/NpcDialoguePanelOption.cs', 'client-source/NpcDialoguePanelOptionItem.cs',
             'client-source/NpcDialoguePanelRewardItemContent.cs', 'client-source/DiningTableNpcPopClick.cs',
             'common-source/NpcGiftItemCompare.cs', 'common-source/Common/NpcGiftInfoParser.cs',
             'common-source/Common/NpcDialogueInfoParser.cs', 'common-source/Common/PlayerNpcDialogueDataWrap.cs',
             'common-source/Common/PlayerNpcDialogueManager.cs', 'common-source/Common/NpcDialogueEndCondition.cs',
             'common-source/Common/Player.cs', 'common-source/Common/LanguageUtils.cs',
             'common-source/NpcDialogueComparer.cs', 'client-source/NpcPanelDialogueListDialogueItemUtils.cs',
             'client-source/NpcPanelDialogueListNpcItemUtils.cs', 'client-source/PlayerInfoUI.cs',
             'client-source/HeadCharacterPortrait.cs', 'client-source/NpcPanelNpcItem.cs',
             'client-source/NpcPanelNpcList.cs', 'client-source/NpcMeetProgressItem.cs',
             'client-source/NpcDetailPanel.cs', 'client-source/NpcFriendshipLevelContent.cs',
             'client-source/NpcFriendshipLevelShow.cs', 'client-source/NpcGiftBlueDotItem.cs',
             'client-source/NpcFriendshipLevelUpNotifyDialog.cs', 'common-source/Common/FormulaHandler.cs',
             'client-source/FilterSortPanel.cs', 'client-source/BaseSortGroup.cs',
             'client-source/CollectSortGroupItem.cs', 'common-source/NpcComparer.cs',
             'client-source/AchievementPanel.cs', 'client-source/AchievementPanelBase.cs',
             'client-source/AchievementPanelItem.cs', 'client-source/AchievementContentNpcFriendshipLevel.cs',
             'common-source/Common/AchievementDataKeyNpcFriendshipLevel.cs',
             'common-source/Common/ErrCode.cs', 'common-source/Common/CJK.cs',
             'client-source/FontCharacterValidChecker.cs', 'framework-firstpass-source/PopupWindow.cs']
    sources.update(manifest='854_156383', resources=exporter.records, inputs=exporter.inputs,
        code={name: hashlib.sha256((code / name).read_bytes()).hexdigest() for name in names},
        language=language_sources, table=dict(file=raw_path.relative_to(ROOT).as_posix(),
        sha256=hashlib.sha256(raw_path.read_bytes()).hexdigest()),
        outputs={name: hashlib.sha256((art / 'partner-interactions' / name).read_bytes()).hexdigest()
                 for name in ['panels.json', 'dialogues.json']},
        operation='Original prefabs, serialized pointers and states, complete Luban dialogue table and Chinese language; no fabricated dialogue')
    save(ROOT / 'assets/reference/partner-interactions/source-manifest.json', sources)
    print('NPC interactions:', len(rows), 'nodes,', len(texts), 'texts,', len(sources['treasure_bindings']), 'bindings')


if __name__ == '__main__':
    build()
