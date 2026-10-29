"""Package original property/entry tables and their consumed native Sprite images."""
import hashlib
import json
from pathlib import Path
import re
import yaml

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_unity_profession_skill_icons import ProfessionSprites
from prepare_unity_profession_reward_sprites import Dumper, source_header
from prepare_unity_profession_character_resources import meta, record, guid


class PropertySprites(ProfessionSprites):
    def __init__(self, apk):
        super().__init__(apk)
        self.folder = ROOT/'unity/Assets/Official/ProfessionUI/PropertyResources'

    def write(self, kind, obj, native):
        row = super().write(kind, obj, native)
        path = ROOT/row['path']; metadata = path.with_name(path.name+'.meta')
        identity = hashlib.md5(('zjcs:profession:property-full:'+row['identity']).encode()).hexdigest()
        self.outputs[metadata] = self.outputs[metadata].replace(row['guid'].encode(),identity.encode())
        row['guid'] = identity
        return row


def build():
    apk = Apk.current()
    if apk.version != '871_156598': raise ValueError('Wrong property source version')
    code = ROOT/'.tools/source-packages/current/871_156598/official-code'
    constructors = Constructors(code)
    tables, inputs = {}, []
    for name, table in [('prop_cfg','TbPropCfgInfo'),('entry','TbEntryInfo')]:
        obj = apk.asset('config_luban','/'+name+'.g.bytes')
        raw = obj.read().m_Script.encode('utf-8','surrogateescape')
        tables[name] = json.loads(json.dumps(constructors.table(raw,table),ensure_ascii=False))
        inputs.append(dict(name=name, source=apk.provenance(obj), object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), raw_sha256=hashlib.sha256(raw).hexdigest(), rows=len(tables[name])))
    manifest = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage',apk.archive))
    entries_by_id = {e['Id']:e for e in tables['entry']}
    wanted = {p['SmallIcon'] for p in tables['prop_cfg'] if p['SmallIcon']}
    wanted.update(entries_by_id[p['EntryId']]['Icon'] for p in tables['prop_cfg'] if p['EntryId'] > 0 and entries_by_id[p['EntryId']]['Icon'])
    native = PropertySprites(apk)
    entries, bindings = [], []
    for icon in sorted(wanted):
        address = 'Assets/Image/'+icon
        matches = [r for r in manifest if r['address'] == address]
        if not matches: matches = [r for r in manifest if r['path'].removesuffix('.png') == address]
        if len(matches) != 1: raise ValueError('Unresolved original property image: '+icon)
        row, = matches
        obj, = [ptr.deref() for path,ptr in apk.container(row['bundle']) if path == row['path'] and ptr.deref().type.name == 'Sprite']
        sprite = native.sprite(obj)
        entries.append(dict(Path=address, AssetGuid=row['guid'], Asset=dict(fileID=21300000,guid=sprite['guid'],type=2)))
        bindings.append(dict(icon=icon,original_asset=row,sprite=sprite['path'],identity=sprite['identity']))
    native.folder.mkdir(parents=True,exist_ok=True);meta(native.folder,folder=True)
    outputs = [record(native.folder.with_name(native.folder.name+'.meta'))]
    for path, raw in native.outputs.items():
        path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw);outputs.append(record(path))
    config = ROOT/'unity/Assets/Game/Resources/ProfessionUI/property-config.json'
    config.write_text(json.dumps(tables,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n');meta(config)
    outputs.extend((record(config),record(config.with_name(config.name+'.meta'))))
    values = source_header(); values.update(m_Enabled=1,m_EditorHideFlags=0,m_Name='ProfessionPropertyImages',m_EditorClassIdentifier='',
        m_Script=dict(fileID=11500000,guid=guid(ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs'),type=3),Entries=entries)
    catalog = ROOT/'unity/Assets/Game/Resources/ProfessionUI/property-images.asset'
    catalog.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.dump({'MonoBehaviour':values},Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000),encoding='utf-8',newline='\n');meta(catalog,native=True)
    outputs.extend((record(catalog),record(catalog.with_name(catalog.name+'.meta'))))
    proof = dict(format=1,manifest=apk.version,tables=inputs,outputs=outputs,bindings=bindings,
        sprites=list(native.sprites.values()),textures=list(native.textures.values()),config=record(config),catalog=record(catalog),
        generator=record(Path(__file__)), sprite_writer=record(ROOT/'scripts/prepare_unity_profession_skill_icons.py'),
        geometry_converter=record(ROOT/'scripts/prepare_unity_map12_team_panel_sprites.py'),
        source_constructors=[record(code/'common-source/Common'/(name+'.cs')) for name in ['PropCfgInfo','TbPropCfgInfo','EntryInfo','TbEntryInfo']],
        operation='Fully decode original prop_cfg and entry tables. Package every property SmallIcon and the entry icon referenced by each property; preserve original addresses/GUIDs, Sprite geometry/UV and full native texture pixels/mips/settings using the existing independently implemented native asset converter. Empty icon paths retain original null behavior. Keep all table columns and rows, including unused entry definitions.',
        validated_scope='Pending actual Unity import and native property-row validation.',
        limitations=['Only property-consumed entry images are packaged; unrelated entry popup images are outside this closure.', 'Full entry/blessing popup prefabs and profession page/player/save integration remain incomplete.'])
    (ROOT/'assets/reference/profession-ui/property-resources-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    fixture=dict(config=config.relative_to(ROOT/'unity').as_posix(),catalog=catalog.relative_to(ROOT/'unity').as_posix(),
        sprites=[s['expected'] for s in native.sprites.values()],
        textures=[dict(path=t['path'].removeprefix('unity/'),**{k:t[k] for k in ('format','mips','pixels_sha256')},
            width=t['original_fields']['m_Width'],height=t['original_fields']['m_Height'],settings=t['original_fields']['m_TextureSettings']) for t in native.textures.values()])
    (ROOT/'.tools/source-packages/current/871_156598/profession-ui/typed/property-rows-fixture.json').write_text(json.dumps(fixture,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Packaged original property configuration:',len(tables['prop_cfg']),'properties,',len(tables['entry']),'entries,',len(native.sprites),'sprites,',len(native.textures),'full textures')


if __name__ == '__main__':
    build()
