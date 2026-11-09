"""Restore the selected official Bond Adventure prefab component bindings."""
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'scripts'))
sys.path.insert(0, str(ROOT / '.tools/character-art-python'))

from official_apk import Apk
from official_unity_schema import ScriptSchemas
from prepare_character_creation_opening import SpriteCache
from prepare_dungeon_ui_bindings import CODE, RUNTIME, VERSION, prefab_bindings, relative_transform_path, sprite_binding
from official_tables import Constructors


def build():
    shared_path = RUNTIME / 'official-bindings.json'
    shared = json.loads(shared_path.read_text(encoding='utf-8'))
    if shared['manifest'] != VERSION:
        raise ValueError('Travel entry bindings must use the selected official version')
    travel = next(value for value in shared['prefabs'] if value['name'] == 'TravelNotesPanel')
    if not travel.get('safeAreas') or len(travel.get('entryButtons', [])) != 3:
        raise ValueError('Run prepare_travel_entry_layout.py before extracting the travel entry')
    shared_receipt = ROOT / 'assets/reference/dungeon-ui/884_156692/prefab-binding-source-manifest.json'
    travel_source = next(value for value in json.loads(shared_receipt.read_text(encoding='utf-8'))['prefabs']
                         if value['name'] == 'TravelNotesPanel')
    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError('NPC Adventure UI must use the selected official version')
    schemas, sprites = ScriptSchemas(code_root=CODE), SpriteCache(apk)
    prefabs, sources = [], []
    for name, bundle in [
        ('NpcAdventureSelectNpcDialog', 'prefabs_ui_npcadventure'),
        ('NpcAdventureSelectNpcItem', 'prefabs_ui_npcadventure'),
        ('NpcAdventureNodeRewardItem', 'prefabs_ui_npcadventure'),
        ('GameEndPanelNpcAdventure', 'prefabs_ui_fight_gameendpanelnpcadventure'),
    ]:
        binding, source = prefab_bindings(apk, schemas, sprites, name, bundle, '/' + name + '.prefab')
        root = apk.asset(bundle, '/' + name + '.prefab')
        for game_object, path, names, _ in relative_transform_path(root.read()):
            for pair in game_object.m_Component:
                obj = pair.component.deref()
                if obj.type.name != 'MonoBehaviour':
                    continue
                script = obj.read(check_read=False).m_Script.read().m_ClassName
                if script not in ('NoDrawingRayCast', 'SafeArea'):
                    continue
                fields = schemas.read(obj)
                if script == 'SafeArea':
                    binding['safeAreas'].append(dict(path='/'.join(map(str, path)), objects=names,
                        enabled=bool(fields['m_Enabled']), paddingTop=float(fields['PaddingTop']),
                        paddingBottom=float(fields['PaddingBottom']),
                        disableTopAdapt=bool(fields['DisableTopAdapt']),
                        disableBottomAdapt=bool(fields['DisableBottomAdapt'])))
                    continue
                if any(value['path'] == '/'.join(map(str, path))
                       for value in binding.get('raycastGraphics', [])):
                    continue
                binding.setdefault('raycastGraphics', []).append(dict(
                    path='/'.join(map(str, path)), objects=names,
                    enabled=bool(fields['m_Enabled']), raycastTarget=bool(fields['m_RaycastTarget']),
                    color=[fields['m_Color'][key] for key in ('r', 'g', 'b', 'a')],
                    raycastPadding=[fields['m_RaycastPadding'][key] for key in ('x', 'y', 'z', 'w')]))
        prefabs.append(binding)
        sources.append(dict(name=name, source=source))
    # Reuse the shared extractor's strict SafeArea / entry-button restoration,
    # keeping this runtime payload independent of unrelated dungeon panels.
    prefabs.append(travel)
    sources.append(travel_source)
    battles = json.loads((ROOT / 'unity/Assets/Game/Resources/NpcAdventure/battle-stages.json').read_bytes())
    reward_ids = {item for rule in battles['rewardRules'] for item in rule['IdList']}
    item_object = apk.asset('config_luban', '/item.g.bytes')
    item_raw = item_object.read().m_Script.encode('utf8', 'surrogateescape')
    items = {row['ClassId']: row for row in Constructors(CODE).table(item_raw, 'TbItemInfo')}
    wanted = {items[item]['RawIcon'].rsplit('/', 1)[-1] for item in reward_ids}
    categories = {items[item]['RawIcon'].split('/')[1].lower() for item in reward_ids}
    icons = {}
    for bundle in apk.index:
        if not any('image_itemicon_' + category in bundle for category in categories):
            continue
        for obj in apk.objects(bundle):
            if obj.type.name != 'Sprite' or obj.peek_name() not in wanted:
                continue
            name = obj.peek_name()
            if name in icons:
                raise ValueError('Ambiguous official NPC Adventure reward sprite: ' + name)
            icons[name] = sprite_binding(sprites, obj.assets_file, 0, obj.path_id)
    if wanted - icons.keys():
        raise ValueError('Unresolved official NPC Adventure reward sprites: ' + str(wanted - icons.keys()))
    rewards = [dict(itemId=item, quality=items[item]['Quality'], itemType=items[item]['ItemType'],
                    sprite=icons[items[item]['RawIcon'].rsplit('/', 1)[-1]]) for item in sorted(reward_ids)]
    target = RUNTIME / 'npc-adventure-bindings.json'
    payload = (json.dumps(dict(manifest=VERSION, prefabs=prefabs, rewardItems=rewards), ensure_ascii=False,
                          sort_keys=True, indent=2) + '\n').encode('utf-8')
    target.write_bytes(payload)
    meta = target.with_name(target.name + '.meta')
    if not meta.exists():
        guid = hashlib.sha256(b'zjcs-npc-adventure-bindings:884_156692').hexdigest()[:32]
        meta.write_text('fileFormatVersion: 2\nguid: ' + guid + '\nTextScriptImporter:\n  externalObjects: {}\n', encoding='ascii')
    receipt = ROOT / 'assets/reference/partner-travel/npc-adventure-ui-source-manifest.json'
    sprite_files = sorted({row['sprite']['file'] for binding in prefabs
                           for row in binding['images'] if row.get('sprite')}
                          | {row['sprite']['file'] for row in rewards})
    sprite_outputs = [dict(path=(RUNTIME / file).relative_to(ROOT).as_posix(),
                           sha256=hashlib.sha256((RUNTIME / file).read_bytes()).hexdigest())
                      for file in sprite_files]
    tools = [Path(__file__), ROOT / 'scripts/prepare_dungeon_ui_bindings.py',
             ROOT / 'scripts/prepare_travel_entry_layout.py',
             ROOT / 'scripts/official_unity_schema.py']
    native_paths = [ROOT / 'unity/Assets/Game/Resources/Dungeons/UI/884_156692' / (value['name'] + '.prefab')
                    for value in prefabs if value['name'] != 'TravelNotesPanel']
    native_paths.append(ROOT / 'unity/Assets/Game/Resources/Travel/884_156692/TravelNotesPanel.prefab')
    receipt.write_text(json.dumps(dict(manifest=VERSION, prefabs=sources, sprites=sprite_outputs,
        rewardItemsSource=dict(source=apk.provenance(item_object), sha256=hashlib.sha256(item_raw).hexdigest(),
                               itemIds=sorted(reward_ids)),
        native_prefabs=[dict(path=file.relative_to(ROOT).as_posix(),
                             sha256=hashlib.sha256(file.read_bytes()).hexdigest()) for file in native_paths],
        tools=[dict(path=file.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(file.read_bytes()).hexdigest())
               for file in tools],
        output=dict(path=target.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(payload).hexdigest()),
        operation='Read original prefab transforms, Image, TMP, raycast, layout and localization fields using the selected official assembly schema; restore original atlas sprites.'),
        ensure_ascii=False, sort_keys=True, indent=2) + '\n', encoding='utf-8')
    print(target.relative_to(ROOT))


if __name__ == '__main__':
    build()
