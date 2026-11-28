"""Project selected-source treasure detail item/NPC/format metadata and original Chinese text."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from official_apk import Apk
from official_source import ROOT,OfficialSource
from official_tables import Constructors
from npc_travel_language import language
from prepare_pet_item_detail_native import enum_names
from current_component_schemas import CurrentSchemas
from official_manifest import read_asset_manifest

DEST=ROOT/'unity/Assets/Game/Resources/Treasure/treasure-detail.json'
RECEIPT=ROOT/'assets/reference/treasure-detail-configuration-source-manifest.json'


def sha(raw):return hashlib.sha256(raw).hexdigest()


def prepare(check=False):
    source=OfficialSource();code=source.prepared_path/'official-code';apk=Apk(source.version,reuse_current_cache=True)
    constructors=Constructors(code);tables={};table_sources={}
    try:
        mapping={name.rsplit('/',1)[-1]:kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',constructors.source('Tables'))}
        for name in ('item','npc','game_settings','treasure','prop_cfg','entry'):
            obj=apk.asset('defaultpackage_assets_config_luban.bundle','/'+name+'.g.bytes')
            raw=obj.read().m_Script.encode('utf8','surrogateescape');tables[name]=constructors.table(raw,mapping[name])
            table_sources[name]=dict(source=apk.provenance(obj),decoded_sha256=sha(raw),constructor=mapping[name])
        properties=json.loads((ROOT/'unity/Assets/Game/Resources/Treasure/treasure-properties.json').read_bytes())
        rules=json.loads((ROOT/'unity/Assets/Game/Resources/Treasure/treasure-rules.json').read_bytes())
        treasure_ids={row['ClassId'] for row in tables['treasure']}
        wanted=treasure_ids|{row['TreasurePieceId'] for row in tables['treasure']}
        wanted.update(row['ItemId'] for rule in rules['UpgradeMaterials'] for row in rule['Materials'])
        wanted.update(row['ItemId'] for rule in rules['LevelUpMaterials'] for row in rule['Materials'])
        type_names=enum_names(code/'common-source/Common/ItemType.cs')
        quality_names=enum_names(code/'common-source/Common/Quality.cs')
        treasure_by_item={}
        for row in tables['treasure']:
            treasure_by_item[row['ClassId']]=row;treasure_by_item[row['TreasurePieceId']]=row
        items=[]
        for row in tables['item']:
            if row['ClassId'] not in wanted:continue
            name=type_names[row['ItemType']];limited=treasure_by_item.get(row['ClassId'],{}).get('IsLimited',False)
            item={k:row[k] for k in ('ClassId','Quality','ElementType','ItemType','RawIcon','IconEffect','CanOverlap','ShowPlayerItemCount')}
            item.update(TypeKey='ItemType.'+('Limited' if limited and name in ('Treasure','TreasurePiece') else '')+name,
                DescriptionKey='treasure_piece_desc' if name=='TreasurePiece' else 'item_'+str(row['ClassId'])+'_desc',
                FunctionDescriptionKey='item_'+str(row['ClassId'])+'_func_desc')
            items.append(item)
        npcs=[{k:row[k] for k in ('Id','TreasureClassId','Icon','PartnerAdditionBg','NameKey')}
            for row in tables['npc'] if row['TreasureClassId'] in treasure_ids]
        prop_types=set(properties['ElementPropTypes'])
        for row in properties['Treasures']:
            for name in ('StarPropTypes','LevelPropTypes','EffectPropTypes'):prop_types.update(row[name] or [])
        props=[row for row in tables['prop_cfg'] if row['PropType'] in prop_types]
        entries=[row for row in tables['entry'] if row['Id'] in {p['EntryId'] for p in props if p['EntryId']>0}]
        image_paths={'Assets/Image/'+row['RawIcon'] for row in items if row['RawIcon']}
        image_paths.update('Assets/Image/'+row[key] for row in npcs for key in ('Icon','PartnerAdditionBg') if row[key])
        image_paths.update('Assets/Image/'+row['SmallIcon'] for row in props if row['SmallIcon'])
        image_paths.update('Assets/Image/'+row['Icon'] for row in entries if row['Icon'])
        image_paths.update('Assets/Image/UI/ItemDetailDialog/Img'+quality_names[row['Quality']]+'_PopUps' for row in items)
        keys={'{0}','{0}/{1}','{0}{1}','+{0}','treasuredetail_piece_count','treasure_func_npc_desc',
            'ui_space_before_value','ui_common_prop_plus','ui_blessing_level','ui_blessing_treasure_level_tip_0',
            'ui_blessing_treasure_level_tip_1','ui_blessing_treasure_level_tip_3','ui_blessing_player_level_tip_2',
            'ui_bless_prop_not_effect','ui_daily_equip_suit_extra_rank_desc','ui_equip_attribuate_prop_ PropArg',
            'ui_common_free','common_format_args1'}
        for item in items:
            keys.update(('item_'+str(item['ClassId'])+'_name',item['DescriptionKey'],item['FunctionDescriptionKey'],item['TypeKey'],'Quality.'+quality_names[item['Quality']]))
        keys.update('treasure_'+str(row['ClassId'])+'_func_desc' for row in tables['treasure'])
        keys.update(row['NameKey'] for row in npcs)
        names=enum_names(code/'common-source/Common/PropType.cs')
        keys.update('PropType.'+names[prop] for prop in prop_types)
        keys.update(row['Unit'] for row in props if row['Unit'])
        # YAML can parse numeric text keys as numbers. Read every authored key from the original component bytes.
        original=ROOT/'.tools/source-packages/current'/source.version/'home-treasure-ui-native/native-export/ExportedProject/Assets/Prefabs/UI/ItemDetail/TreasureDetailDialog.prefab'
        managed=source.prepared_path/'home-treasure-ui-native/inputs/Managed'
        schemas=CurrentSchemas(managed_directory=managed);labels=[]
        bundle='defaultpackage_assets_prefabs_ui_itemdetail.bundle'
        root,=[p.deref() for n,p in apk.container(bundle) if n.casefold()=='assets/prefabs/ui/itemdetail/treasuredetaildialog.prefab']
        def visit(go):
            components=[p.component.deref() for p in go.read().m_Component]
            for component in components:
                if component.type.name=='MonoBehaviour' and component.read(check_read=False).m_Script.read().m_ClassName=='MultiLanguageText':
                    key=schemas.read(component)['myKey']
                    if key:keys.add(key)
                    labels.append(dict(source=apk.provenance(component),sha256=sha(component.get_raw_data()),key=key))
            for child in components[0].read().m_Children:visit(child.read().m_GameObject.deref())
        visit(root)
        records=read_asset_manifest(source.manifest_bytes('DefaultPackage',apk.archive))
        color_row,=[row for row in records if row['path'].endswith('/GlobalColors.asset')]
        color_object=apk.asset(color_row['bundle'],color_row['path']);palette=schemas.read(color_object)
        texts,language_proof=language(keys,source)
        # Original TextReplacer resolves nonnumeric braces through the same language dictionary.
        while True:
            aliases={key for text in texts.values() for key in re.findall(r'\{(.*?)\}',text)
                if key!='c' and not key.isdigit()}-texts.keys()
            if not aliases:break
            extra,_=language(aliases,source)
            if aliases-extra.keys():raise ValueError('Bind original dynamic detail text owners: '+repr(sorted(aliases-extra.keys())))
            keys.update(aliases);texts.update(extra)
        texts,language_proof=language(keys,source)
        payload=dict(Manifest=source.version,Items=items,Npcs=npcs,GlobalColors=palette,
            FriendshipQuality=[dict(Quality=int(q),Value=v) for q,v in tables['game_settings']['TreasureNpcFriendshipQuality'].items()],
            Texts=[dict(Key=k,Value=v) for k,v in sorted(texts.items())],
            PropertyConfiguration=dict(prop_cfg=props,entry=entries),ImagePaths=sorted(image_paths))
        raw=(json.dumps(payload,ensure_ascii=False,indent=2)+'\n').encode()
        token=hashlib.md5(b'zjcs:treasure-detail-configuration').hexdigest()
        meta=('fileFormatVersion: 2\nguid: '+token+'\nTextScriptImporter:\n  externalObjects: {}\n').encode()
        paths=[constructors.source_root/(name+'.cs') for name,text in constructors.sources.items()
            if text and (constructors.source_root/(name+'.cs')).is_file()]
        paths += [code/'common-source/Common'/(name+'.cs') for name in ('ItemType','Quality','PropType','LanguageUtils','TreasureInfoParser')]
        inputs=source.verify_code_files(paths)
        for path in (Path(__file__),ROOT/'scripts/npc_travel_language.py',original,
            ROOT/'unity/Assets/Game/Resources/Treasure/treasure-properties.json',ROOT/'unity/Assets/Game/Resources/Treasure/treasure-rules.json'):
            inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        outputs={DEST:raw,Path(str(DEST)+'.meta'):meta}
        inputs.update({p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in managed.glob('*.dll')})
        record=dict(manifest=source.version,inputs=inputs,tables=table_sources,authored_labels=labels,language=language_proof,
            global_colors=dict(source=apk.provenance(color_object),object_sha256=sha(color_object.get_raw_data())),
            outputs={p.relative_to(ROOT).as_posix():sha(v) for p,v in outputs.items()})
        prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
        outputs[RECEIPT]=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode()
        for path,value in outputs.items():
            if check:
                if not path.is_file() or path.read_bytes()!=value:raise ValueError('Original detail configuration differs: '+str(path))
            else:
                if path!=RECEIPT and path.exists() and path.read_bytes()!=value and sha(path.read_bytes())!=prior.get('outputs',{}).get(path.relative_to(ROOT).as_posix()):
                    raise ValueError('Preserve another detail configuration edit: '+str(path))
                path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(value)
        print('Prepared selected original detail metadata, texts and dynamic image addresses',flush=True)
    finally:apk.reader.close()


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');prepare(p.parse_args().check)
