"""Extend the APK UI catalog with its runtime panels and legacy animation curves."""
import hashlib
import json
import math
import re
import struct

from prepare_official_city import Exporter, ART, REFERENCE, ROOT
from UnityPy.classes.PPtr import PPtr
from official_animation import mecanim_tracks


def unique_sibling_paths(node):
    """Unity allows repeated sibling names; retain both comparison dialogs."""
    seen = {}
    def rebase(value, old, new):
        if isinstance(value, dict):
            for key, entry in value.items():
                if key in ['path', 'owner'] and isinstance(entry, str) and (entry == old or entry.startswith(old + '/')):
                    value[key] = new + entry[len(old):]
                else:
                    rebase(entry, old, new)
        elif isinstance(value, list):
            for entry in value:
                rebase(entry, old, new)
    for child in node['children']:
        name = child['name']
        index = seen.get(name, 0)
        seen[name] = index + 1
        if index:
            rebase(child, child['path'], child['path'] + f' ({index})')
        unique_sibling_paths(child)


class PanelExporter(Exporter):
    def guid_sprite(self, guid):
        if not hasattr(self, 'guid_paths'):
            entry=next(n for n in self.apk.archive.namelist() if 'PackageManifest_DefaultPackage_' in n and n.endswith('.bytes'))
            self.guid_paths={m[2].decode():m[1].decode() for m in re.finditer(rb'(Assets/[^\x00-\x1f]+?)\x20\x00([a-f0-9]{32})',self.apk.archive.read(entry))}
            self.guid_sprites={}
        if guid in self.guid_sprites:return self.guid_sprites[guid]
        if guid not in self.guid_paths:
            self.unsupported.setdefault('state_images_not_in_manifest',{})[guid]='Original state sprite GUID is absent from the pinned manifest'
            return None
        path=self.guid_paths[guid]
        asset_key='defaultpackage_'+path.replace('/','_').lower()
        candidates=sorted((b for b in self.apk.index if asset_key.startswith(b.removesuffix('.bundle'))),key=len,reverse=True)
        for bundle in candidates:
            try:
                for asset,ptr in self.apk.container(bundle):
                    if asset.lower()==path.lower() and ptr.type.name=='Sprite':
                        result=self.sprite(ptr);self.guid_sprites[guid]=result
                        return result
            except FileNotFoundError:continue
        self.unsupported.setdefault('state_images_not_packaged',{})[guid]=path
        return None

    def object_path(self, ptr):
        go=ptr.read();names=[go.m_Name]
        transform=next(c.component.read() for c in go.m_Component if c.component.type.name=='RectTransform')
        while transform.m_Father.path_id:
            transform=transform.m_Father.read();names.insert(0,transform.m_GameObject.read().m_Name)
        return '/'+ '/'.join(names)

    def state_images(self, obj, raw):
        bindings=[]
        for match in re.finditer(rb'\x1b\x00\x00\x00StateSpriteAssetProxyConfig',raw):
            offset=match.start()
            for _ in range(3):
                length=struct.unpack_from('<i',raw,offset)[0]
                offset=(offset+4+length+3)&~3
            count=struct.unpack_from('<i',raw,offset)[0];offset+=4
            if not 0<=count<=100:raise ValueError('Invalid state image list')
            for _ in range(count):
                file_id,path_id=struct.unpack_from('<iq',raw,offset);offset+=12
                image=PPtr(m_FileID=file_id,m_PathID=path_id,assetsfile=obj.assets_file).deref().read(check_read=False)
                target=self.object_path(image.m_GameObject)
                entries=struct.unpack_from('<i',raw,offset)[0];offset+=4
                states={}
                for _ in range(entries):
                    state,length=struct.unpack_from('<ii',raw,offset);offset+=8
                    guid=raw[offset:offset+length].decode();offset=(offset+length+3)&~3
                    offset+=12 # ResourceAssetProxy.ContainerSource
                    states[str(state)]=self.guid_sprite(guid)
                bindings.append({'path':target,'states':states})
        return bindings

    def clip(self, obj, path, tree=None):
        clip = obj.read()
        tracks = []
        for curve in clip.m_FloatCurves:
            keys = [[k.time, k.value,
                     k.inSlope if math.isfinite(k.inSlope) else None,
                     k.outSlope if math.isfinite(k.outSlope) else None] for k in curve.curve.m_Curve]
            if keys:
                tracks.append({'path': path + ('/' + curve.path if curve.path else ''),
                               'property': curve.attribute, 'keys': keys})
        for field, prop in [('m_ScaleCurves', 'scale'), ('m_PositionCurves', 'position'),
                            ('m_EulerCurves', 'rotation')]:
            for curve in getattr(clip, field):
                for axis in 'xy' if prop != 'rotation' else 'z':
                    keys = [[k.time, getattr(k.value, axis),
                             getattr(k.inSlope, axis) if math.isfinite(getattr(k.inSlope, axis)) else None,
                             getattr(k.outSlope, axis) if math.isfinite(getattr(k.outSlope, axis)) else None]
                            for k in curve.curve.m_Curve]
                    if keys:
                        tracks.append({'path': path + ('/' + curve.path if curve.path else ''),
                                       'property': prop + '.' + axis, 'keys': keys})
        duration = max((t['keys'][-1][0] for t in tracks), default=0)
        loop = clip.m_WrapMode == 2
        unknown = []
        if not tracks and tree is not None:
            tracks, duration, loop, unknown = mecanim_tracks(obj, tree)
        return {'name': clip.m_Name, 'owner': path, 'source': self.apk.provenance(obj), 'tracks': tracks,
                'duration': duration, 'loop': loop, 'unconverted_bindings': unknown}

    def controller_clips(self, ptr):
        controller = ptr.read()
        if ptr.type.name == 'AnimatorOverrideController':
            return [p.m_OverrideClip if p.m_OverrideClip.path_id else p.m_OriginalClip
                    for p in controller.m_Clips]
        return controller.m_AnimationClips

    def ui_node(self, obj, path=''):
        node = super().ui_node(obj, path)
        if node is None:
            return None
        clips = {}
        for component in obj.read().m_Component:
            c = component.component.deref()
            if c.type.name == 'Animator' and c.read().m_Controller.path_id:
                for ptr in self.controller_clips(c.read().m_Controller):
                    if ptr.path_id:
                        clips[ptr.path_id] = self.clip(ptr.deref(), node['path'], node)
            elif c.type.name == 'Animation':
                animation = c.read()
                for ptr in animation.m_Animations:
                    if ptr.path_id:
                        clips[ptr.path_id] = self.clip(ptr.deref(), node['path'])
                if animation.m_Animation.path_id:
                    node['default_animation'] = animation.m_Animation.read().m_Name
                    node['animation_play_automatically'] = bool(animation.m_PlayAutomatically)
            elif c.type.name == 'MonoBehaviour':
                cls, raw, _ = self.mono(c)
                if b'StateSpriteAssetProxyConfig' in raw:
                    node.setdefault('state_images',[]).extend(self.state_images(c,raw))
                if cls == 'TabItem':
                    node['tab_on'] = bool(raw[len(raw)-20])
                if cls == 'GraphicCapsuleMeshClipper':
                    node['capsule_clip'] = {'enabled': bool(raw[32]),
                        'size': list(struct.unpack_from('<2f', raw, 36)),
                        'offset': list(struct.unpack_from('<2f', raw, 44)),
                        'left': bool(raw[52]), 'right': bool(raw[56]),
                        'segments': struct.unpack_from('<i', raw, 60)[0]}
                if cls in ['LoopVerticalScrollRect', 'LoopHorizontalScrollRect']:
                    node['scroll'] = {'horizontal': cls == 'LoopHorizontalScrollRect',
                                      'vertical': cls == 'LoopVerticalScrollRect', 'sensitivity': 90}
                if cls != 'SimpleAnimation':
                    continue
                # SimpleAnimation serializes clip pointers inside its state array.
                # Resolve only valid AnimationClip objects; never execute client code.
                for offset in range(32, len(raw)-11, 4):
                    file_id, path_id = struct.unpack_from('<iq', raw, offset)
                    if not path_id or not 0 <= file_id <= len(c.assets_file.externals):
                        continue
                    try:
                        asset = PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=c.assets_file).deref()
                    except (ValueError, KeyError, FileNotFoundError):
                        continue
                    if asset.type.name == 'AnimationClip':
                        clips[path_id] = self.clip(asset, node['path'], node)
        if clips:
            node['animations'] = list(clips.values())
        return node


def build():
    exporter = PanelExporter()  # Checks the fixed APK hash before any extraction.
    manifest = json.loads((REFERENCE/'source-manifest.json').read_text(encoding='utf8'))
    screens = json.loads((ART/'ui.json').read_text(encoding='utf8'))
    for unused in ['equip_row', 'skill_row', 'gacha_reward']:
        screens.pop(unused, None)
    # Embedded fragments have dedicated extraction scripts and must not be
    # replaced with their entire source prefab during a catalog rebuild.
    specs = {key: (entry['source']['bundle'], entry['source']['prefab'].removeprefix('/').removesuffix('.prefab'))
             for key, entry in screens.items() if not entry['source'].get('embedded_path')}
    specs.update({
        'home': ('home', 'HomeUI'),
        'top_bar': ('public', 'PublicTopBar'),
        'bottom_bar': ('public', 'PublicBottomBar'),
        'player_icon': ('public', 'PublicPlayerIconHome'),
        'bed': ('furniture', 'FurnitureBedDialog'),
        'cart': ('furniture', 'FurnitureCartDialog'),
        'decor': ('home', 'HomeSkinPanel'),
        'decor_furniture': ('home', 'HomeSkinFurnitureContent'),
        'decor_item': ('home', 'HomeSkinItem'),
        'creation': ('createplayer', 'CreatePlayerPanel'),
        'creation_diy': ('createplayer', 'CreatePlayerPanelDIYContent'),
        'initial_profession': ('login', 'CreateRoleSelectProfessionPanel'),
        'treasure_detail': ('itemdetail', 'TreasureDetailDialog'),
        'cooking_detail': ('itemdetail', 'CookingUseDetailDialog'),
        'rank_upgrade': ('characterupgrade', 'CharacterUpgradePanel'),
        'chest_choose': ('itemdetail', 'ItemChooseDialog'),
        'pet_potential': ('pet', 'PetPotentialPanel'),
        'pet_evolution': ('pet', 'PetEvolutionPanel'),
        'character': ('character', 'CharacterPanel'),
        'character_skills': ('character', 'CharacterPanelSkillPanel'),
        'equipment': ('character', 'CharacterPanelEquipListPanel'),
        'equip_detail': ('character', 'CharacterPanelEquipDetailPanel'),
        'equip_compare': ('character', 'CharacterPanelEquipComparePanel'),
        'equip_row': ('character', 'CharacterPanelEquipListItem'),
        'item_skill': ('item', 'ItemIconSkill'),
        'item_skill_piece': ('item', 'ItemIconSkillPiece'),
        'item_equip': ('item', 'ItemIconEquip'),
        'item_empty': ('item', 'ItemIconEmpty'),
        'item_cooking': ('item', 'ItemIconCooking'),
        'item_gem': ('item', 'ItemIconGem'),
        'item_treasure': ('item', 'ItemIconTreasurePanel'),
        'item_treasure_piece': ('item', 'ItemIconTreasurePiecePanel'),
        'count_slider': ('common', 'Slider/SetCountSlider'),
        'character_props': ('character', 'CharacterDetailPropsDialog'),
        'skills': ('skill', 'SkillContentPanel'),
        'skill_detail': ('skill', 'SkillDetailDialogPanel'),
        'gacha_shell': ('gachapanel', 'GachaPanel'),
        'goddess': ('gachapanel', 'SkillGachaPanel'),
        'gacha_senior': ('gachapanel', 'SkilSeniorlGachaPanel'),
        'gacha_background': ('gachapanel', 'NormalGachaBgImg'),
        'gacha_senior_background': ('gachapanel', 'NormalSeniorGachaBgImg'),
        'gacha_result': ('gachapanel', 'GachaResultPanel'),
        'gacha_history': ('gachapanel', 'GachaHistoryContent'),
        'gacha_history_row': ('gachapanel', 'GachaHistoryItem'),
        'gacha_rules': ('gachapanel', 'GachaDetailDialog'),
        'pet': ('pet', 'PetPanel'),
        'pet_row': ('pet', 'PetListItem'),
        'pet_detail': ('pet', 'PetDetailPanel'),
        'pet_gacha': ('pet', 'PetGachaSubPanel'),
        'bag': ('playerbagpanel', 'PlayerBagPanel'),
        'quests': ('task', 'TaskPanel'),
        'task_row': ('task', 'TaskPanelListItemDaily'),
        'book': ('home', 'TravelNotesPanel'),
        'shop_item': ('shop','ShopGoodsItemInfoCommon'),
        'gacha_profession_item': ('gachapanel','SkillGachaPanelDropdownItem'),
        'gacha_professions': ('gachapanel','SkillGachaCustomProfessionSelectionPanel'),
        'gacha_profession_choice': ('gachapanel','SkillGachaCustomProfessionSelectionPanelItem'),
        'gacha_score': ('gachapanel','ScoreRewardDialog'),
        'gacha_score_item': ('gachapanel','ScoreRewardDialogItem'),
        'pay_item': ('pay','PayDirectPayItem'),
        'pay_card': ('pay','PayPeriodicCardItem'),
        'pay_button': ('pay','CommonPayBtn'),
        'activity_panel': ('activityentry','ActivityEntryPanel'),
        'pay_panel': ('activityentry','ShopActivityEntryPanel'),
        'activity_tab': ('activityentry','ActivityEntryTabItem'),
        'activity_entry': ('activityentry','Common/ActivityCommonEntry'),
    })
    for key, (bundle, name) in specs.items():
        screens[key] = exporter.ui(bundle if bundle.endswith('.bundle') else 'prefabs_ui_'+bundle, '/'+name+'.prefab')
        unique_sibling_paths(screens[key]['root'])
        print('Panel', key, flush=True)
    exporter.save('ui.json', screens)
    manifest['resources'].update(exporter.records)
    manifest['ui_material_inputs'] = {**manifest.get('ui_material_inputs', {}), **exporter.material_inputs}
    manifest['unsupported'].update(exporter.unsupported)
    manifest['data_sha256']['ui.json'] = hashlib.sha256((ART/'ui.json').read_bytes()).hexdigest()
    manifest['panel_prefabs'] = {key: entry['source'] for key, entry in screens.items()}
    (REFERENCE/'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf8')
    entries = sorted(manifest['resources'])
    __import__("official_resource_manifest").write_resources(entries)
    print('Original UI prefabs:', len(screens), 'additional resources:', len(exporter.records))
    # Restore profession-specific fields/GUIDs after the generic prefab pass.
    from prepare_profession_ui import build as build_profession
    build_profession()
    from prepare_skill_ui import build as build_skills
    build_skills()


if __name__ == '__main__':
    build()
