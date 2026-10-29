"""Restore all profession skill/skin and equipment-type Sprite atlas bindings."""
import hashlib
import json
import re
from pathlib import Path
import yaml

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_unity_map12_team_panel_sprites import NativeTeamSprites
from prepare_unity_profession_reward_sprites import Dumper, source_header
from prepare_unity_profession_character_resources import meta, record


class ProfessionSprites(NativeTeamSprites):
    def __init__(self, apk):
        self.apk = apk
        self.outputs, self.textures, self.sprites = {}, {}, {}
        self.folder = ROOT/'unity/Assets/Official/ProfessionUI/SkillResources'

    def write(self, kind, obj, native):
        identity = obj.assets_file.name + ':' + str(obj.path_id)
        name = obj.read().m_Name + '-' + hashlib.sha256(identity.encode()).hexdigest()[:10]
        path = self.folder/(name+('.sprite.asset' if kind == 'Sprite' else '.texture2d.asset'))
        guid = hashlib.md5(('zjcs:profession:skill-full:'+identity).encode()).hexdigest()
        main_id, class_id = (21300000,213) if kind == 'Sprite' else (2800000,28)
        # Unity's typeless payload is already hexadecimal. Avoid asking the
        # generic YAML scalar analyser to scan tens of megabytes per atlas.
        if kind == 'Texture2D':
            fields = dict(native); pixels = fields['_typelessdata']
            fields['_typelessdata'] = 'PROFESSION_NATIVE_TEXTURE_HEX'
            body = yaml.dump({kind:fields}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
            if body.count('PROFESSION_NATIVE_TEXTURE_HEX') != 1 or not re.fullmatch('[0-9a-f]*', pixels):
                raise ValueError('Native skill texture payload serialization differs')
            body = body.replace('PROFESSION_NATIVE_TEXTURE_HEX', pixels)
        else:
            body = yaml.dump({kind:native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
        raw = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!'+str(class_id)+' &'+str(main_id)+'\n'+body).encode()
        self.outputs[path] = raw
        self.outputs[path.with_name(path.name+'.meta')] = ('fileFormatVersion: 2\nguid: '+guid+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: '+str(main_id)+'\n').encode()
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(raw).hexdigest(), guid=guid,
            identity=identity, source=self.apk.provenance(obj), object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())


def build():
    apk = Apk.current()
    if apk.version != '871_156598':
        raise ValueError('Original profession skill source version differs')
    code = ROOT/'.tools/source-packages/current/871_156598/official-code'
    constructors = Constructors(code)
    types = {m[2]:m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    tables, inputs = {}, []
    rules = json.loads((ROOT/'assets/official/rules.json').read_bytes())
    for name in ('profession_base','item','skill_rank','skill_skin','equip_type'):
        obj = apk.asset('config_luban','/'+name+'.g.bytes')
        raw = obj.read().m_Script.encode('utf-8','surrogateescape')
        tables[name] = json.loads(json.dumps(constructors.table(raw,types[name]),ensure_ascii=False))
        if name != 'item' and tables[name] != rules[name]:
            raise ValueError('Packaged profession information table differs: '+name)
        inputs.append(dict(name=name,source=apk.provenance(obj),object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),sha256=hashlib.sha256(raw).hexdigest(),rows=len(tables[name])))
    ids = {i for p in tables['profession_base'] for i in p['Skills']}
    skins = [s for s in tables['skill_skin'] if s['SkillId'] in ids]
    ids.update(s['ClassId'] for s in skins)
    items = [i for i in tables['item'] if i['ClassId'] in ids]
    if len(items) != len(ids):
        raise ValueError('Original profession skill item definition is missing')
    if items != [i for i in rules['item'] if i['ClassId'] in ids]:
        raise ValueError('Packaged profession skill/skin item projection differs')
    manifest = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage',apk.archive))
    native = ProfessionSprites(apk)
    entries, bindings = [], []
    wanted = [(i['RawIcon'], dict(kind='skill',item_id=i['ClassId'])) for i in items]
    # ProfessionInfoPanel requests only Weapon (10) and SecondWeapon (14).
    wanted.extend((e['Icon'],dict(kind='equipment',item_type=e['ItemType'])) for e in tables['equip_type'] if e['EquipPos'] in (10,14))
    consumed = {}
    for icon, use in wanted:
        # RawIcon is used by ItemInfo.GetIconPath when no appearance PartInfo
        # exists. The original ImageLoaderProxy normalizes the Assets/Image path.
        address = 'Assets/Image/'+icon
        matches = [r for r in manifest if r['address'] == address]
        if not matches:
            matches = [r for r in manifest if r['path'].removesuffix('.png') == address]
        if len(matches) != 1:
            raise ValueError('Resolve original icon address: '+icon+'; matches='+str(matches))
        row, = matches
        # Some standalone PNG containers expose both Texture2D and Sprite at
        # the same path. Match the original typed LoadSprite request.
        obj, = [ptr.deref() for path,ptr in apk.container(row['bundle'])
            if path == row['path'] and ptr.deref().type.name == 'Sprite']
        value = native.sprite(obj)
        if row['guid'] in consumed:
            if consumed[row['guid']] != address:
                raise ValueError('Original icon has different logical addresses; bind aliases explicitly')
        else:
            consumed[row['guid']] = address
            entries.append(dict(Path=address,AssetGuid=row['guid'],Asset=dict(fileID=21300000,guid=value['guid'],type=2)))
        bindings.append(dict(**use, icon=icon, original_asset=row, sprite_identity=value['identity'], installed_sprite=value['path'].removeprefix('unity/')))
    native.folder.mkdir(parents=True, exist_ok=True); meta(native.folder,folder=True)
    outputs = [record(native.folder.with_name(native.folder.name+'.meta'))]
    for path, raw in native.outputs.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw); outputs.append(record(path))
    config = dict(professions=tables['profession_base'],items=items,ranks=tables['skill_rank'],skins=skins,equipment=tables['equip_type'])
    config_path = ROOT/'unity/Assets/Game/Resources/ProfessionUI/info-config.json'
    config_path.write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n');meta(config_path)
    outputs.extend((record(config_path),record(config_path.with_name(config_path.name+'.meta'))))
    # Add source-configured dynamic prefabs by original AssetTable GUID. The
    # native prefab closure is restored separately, without rebuilding layout.
    prefab_names = ('UI/Item/ItemIconSkill.prefab','UI/Profession/ProfessionSkillItem.prefab',
        'UI/Item/Attachments/NotGotTip.prefab','UI/Item/Attachments/ProfessionMismatch.prefab',
        'UI/Item/Attachments/GMDisabled.prefab','UI/Item/Attachments/WearingAppendAppearanceGo.prefab')
    for address in prefab_names:
        row, = [r for r in manifest if r['address'] == address]
        path = ROOT/'unity/Assets/Official/ProfessionUI/Native'/row['path'].removeprefix('Assets/')
        root_id = re.search(r'(?m)^--- !u!1 &(\d+)$',path.read_text(encoding='utf-8'))[1]
        from prepare_unity_profession_native_bindings import guid
        entries.append(dict(Path=address,AssetGuid=row['guid'],Asset=dict(fileID=int(root_id),guid=guid(path),type=3)))
    data = source_header(); data.update(m_Enabled=1,m_EditorHideFlags=0,m_Name='ProfessionSkillImages',m_EditorClassIdentifier='',
        m_Script=dict(fileID=11500000,guid=guid(ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs'),type=3),Entries=entries)
    catalog = ROOT/'unity/Assets/Game/Resources/ProfessionUI/skill-images.asset'
    catalog.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.dump({'MonoBehaviour':data},Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000),encoding='utf-8',newline='\n');meta(catalog,native=True)
    outputs.extend((record(catalog),record(catalog.with_name(catalog.name+'.meta'))))
    proof = dict(format=1,manifest=apk.version,tables=inputs,packaged_rules=record(ROOT/'assets/official/rules.json'),
        bindings=bindings,sprites=list(native.sprites.values()),textures=list(native.textures.values()),outputs=outputs,
        catalog=record(catalog),config=record(config_path),generator=record(Path(__file__)),
        atlas_converter=record(ROOT/'scripts/prepare_unity_map12_team_panel_sprites.py'),
        texture_decoder=record(ROOT/'scripts/prepare_unity_map13_native_scene.py'),
        official_code=[record(code/'common-source/Common'/n) for n in ('ItemInfo.cs','SkillRankInfoParser.cs','EquipTypeInfoParser.cs')],
        operation='Fully decode five selected official Luban tables. Compare the four complete profession/rank/skin/equipment tables and consumed skill/skin item rows against packaged projections. Select all profession-listed skills, their configured skins and equipment-type icons. Resolve original SpriteAtlas render-data keys, full texture/sampler/mip payloads, and original Sprite geometry/UV/logical properties through the existing independent native Sprite converter. Bind exact source image addresses/AssetTable GUIDs and six native prefabs in a packaged local catalog.',
        validated_scope='Pending actual native Sprite/GPU and skill-icon Play Mode validation.',
        limitations=['Local player/pool/window host binding and complete original information/upgrade pages remain incomplete.', 'No complete profession page/save verification or Windows release is claimed.'])
    (ROOT/'assets/reference/profession-ui/skill-icons-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    fixture = dict(config='Assets/Game/Resources/ProfessionUI/info-config.json',catalog='Assets/Game/Resources/ProfessionUI/skill-images.asset',
        sprites=[s['expected'] for s in native.sprites.values()],
        textures=[dict(path=t['path'].removeprefix('unity/'),**{k:t[k] for k in ('format','mips','pixels_sha256')},
            width=t['original_fields']['m_Width'],height=t['original_fields']['m_Height'],settings=t['original_fields']['m_TextureSettings']) for t in native.textures.values()])
    target = ROOT/'.tools/source-packages/current/871_156598/profession-ui/typed/skill-icons-fixture.json'
    target.write_text(json.dumps(fixture,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Restored',len(items),'skill/skin items,',len(bindings),'icons,',len(native.textures),'full native textures.')


if __name__ == '__main__':
    build()
