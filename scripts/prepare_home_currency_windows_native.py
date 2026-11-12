"""Recover the complete selected official quick-buy and pay-change windows."""
import hashlib
import json
from pathlib import Path
import re

from prepare_home_count_controls_native import HomeCountControlsBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_card_native import ROOT, guid, sha, script_id
from prepare_current_partner_native import documents
from prepare_current_partner_native import identity, pointer
from official_manifest import read_asset_manifest
from official_tables import Constructors
from npc_travel_language import language
from validation_workspace import validation_directory


class HomeCurrencyWindowsBinding(HomeCountControlsBinding):
    def register_scripts(self):
        super().register_scripts()
        paths={name:ROOT/'unity/Assets/Game/Runtime/Official/HomeCurrencyWindowsUI'/(name+'.cs')
            for name in ('PlayerCostMoneyContent','PayMoneyChangeDialog','QuickBuyDialog')}
        for name in ('VerticalLayoutGroupJSZX','HorizontalLayoutGroupJSZX'):
            paths['UGUIJszx.'+name]=ROOT/'unity/Assets/Game/Runtime/Official/WorldUI'/(name+'.cs')
            original=ROOT/'.tools/source-packages/current/884_156692/official-code/framework-source/UGUIJszx'/(name+'.cs')
            self.inputs[original.relative_to(ROOT).as_posix()]=sha(original.read_bytes())
        for name in ('ItemIcon','ItemIconAttachment','ItemIconConfig','ItemIconAttachmentConfig','ItemIconAttachmentCell','ItemIconAttachmentHint'):
            paths[name]=ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI'/(name+'.cs')
        paths['ItemProgress']=ROOT/'unity/Assets/Game/Runtime/Official/WorldUI/WorldItemProgress.cs'
        paths['SafeArea']=ROOT/'unity/Assets/Game/Runtime/Official/HomeScreenUI/SafeArea.cs'
        for name,path in paths.items():
            meta=Path(str(path)+'.meta');ref=dict(fileID=11500000,guid=guid(meta),type=3)
            namespace,_,short=name.rpartition('.')
            self.scripts[name]=ref;self.class_ids[script_id(namespace,short)]=ref;self.script_guids.add(ref['guid'])
            self.script_sources[name]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
                sha256=sha(path.read_bytes()),meta_sha256=sha(meta.read_bytes()))

    def friendship_templates(self):
        for name,area in [('ShopQuickBuyDialog','Shop'),('PayMoneyChangeDialog','Pay')]:
            self.recovery.recover(name,self.work/'currency-windows-native',
                bundle='defaultpackage_assets_prefabs_ui_'+area.lower()+'.bundle',
                native_relative='Prefabs/UI/'+area+'/'+name+'.prefab')
        self.recovery.recover('ItemIconDefault',self.work/'currency-windows-native',
            bundle='defaultpackage_assets_prefabs_ui_item.bundle',native_relative='Prefabs/UI/Item/ItemIconDefault.prefab')
        configs={obj.read(check_read=False).m_Name:obj for obj in self.recovery.apk.objects(
            'defaultpackage_assets_scriptableconfig_modules_itemicon.bundle') if obj.type.name=='MonoBehaviour'}
        attachment=self.recovery.fields(configs['ItemIconAttachmentConfig'])
        definition,=[row for row in attachment['Templates'] if row['Type']==13]
        records=read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage',self.recovery.apk.archive))
        record,=[row for row in records if row['guid']==definition['GuidProxy']['mAssetGuid']]
        obj,=[ref.deref() for path,ref in self.recovery.apk.container(record['bundle'])
            if path.casefold()==record['path'].casefold() and ref.deref().type.name=='GameObject']
        self.hint_attachment=dict(type=13,name=obj.read().m_Name,asset=record)
        self.recovery.recover(obj.read().m_Name,self.work/'currency-windows-native',bundle=record['bundle'],
            native_relative=record['path'].removeprefix('Assets/'))
        self.recovery.finalize()
        result=[];self.template_selection={};self.home_template_pointers={};self.empty_restorations=[]

        def restore(original,current,address):
            if isinstance(original,dict) and isinstance(current,dict):
                for key in original.keys()&current.keys():
                    if current[key] is None and original[key] in ('',[],{}):
                        current[key]=original[key];self.empty_restorations.append(address+'/'+key)
                    else:restore(original[key],current[key],address+'/'+key)
            elif isinstance(original,list) and isinstance(current,list) and len(original)==len(current):
                for index,(left,right) in enumerate(zip(original,current)):restore(left,right,address+'/'+str(index))

        for native,_,panel,record in self.recovery.outputs:
            name='HomeCurrency'+native.stem if native.stem in ('ItemIconDefault',self.hint_attachment['name']) else native.stem
            template_guid=hashlib.md5(('zjcs:partner-current:'+name+'.prefab').encode()).hexdigest()
            self.home_template_pointers.update({key:dict(fileID=file_id,guid=template_guid,type=3)
                for key,file_id in panel['objectMap'].items()})
            self.script_guids.add(template_guid)
            for path in (native,Path(str(native)+'.meta')):self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
            for key,external_ref in panel['external'].items():self.objects[external_ref['guid']]=self.original(key)
            for key,file_id in panel['objectMap'].items():
                owner=self.original(key);self.learn(self.recovery.fields(owner),next(iter(panel['documents'][file_id].values())),owner)
            for script in panel['scripts']:
                owner=self.original(script['original']);original=self.recovery.fields(owner)
                fields=panel['documents'][script['nativeFileID']]['MonoBehaviour']
                restore(original,fields,script['original'])
                if script['type']=='PopupWindowSound':
                    for field in ('ShowSound','HideSound'):fields[field]=self.sound(original[field],owner)
                elif script['type']=='UIClickSound':self.sound(original['AkEvent'],owner)
            missing={'.'.join(filter(None,(row['namespace'],row['type']))) for row in panel['scripts']}-self.scripts.keys()
            if missing:raise ValueError('Migrate original currency-window scripts: '+repr(missing))
            self.template_selection[name]=dict(source=record['originalRoot'],mapped_objects=len(panel['objectMap']),
                adaptation='Complete original window hierarchy and bindings; original empty values restored after native conversion.')
            result.append((name,panel))
        cache=self.recovery.proof['component_cache'];self.inputs[cache['path']]=cache['sha256']
        configs={obj.read(check_read=False).m_Name:obj for obj in self.recovery.apk.objects(
            'defaultpackage_assets_scriptableconfig_modules_itemicon.bundle') if obj.type.name=='MonoBehaviour'}
        manager=self.recovery.fields(configs['ItemIconManagerConfig'])
        default=pointer(manager['DefPrefab'],configs['ItemIconManagerConfig'])
        if manager['IdList'] or manager['IdPrefabList'] or default.read().m_Name!='ItemIconDefault':
            raise ValueError('Review original item-icon prefab selection')
        self.icon_manager=dict(default=identity(default),fields=manager,source=self.recovery.apk.provenance(configs['ItemIconManagerConfig']),
            sha256=sha(configs['ItemIconManagerConfig'].get_raw_data()))
        self.icon_config=self.load_config(configs['ItemIconConfig'],'ItemIconConfig')
        self.attachment_config=self.original_asset(configs['ItemIconAttachmentConfig'])
        self.configuration()
        return result

    def load_config(self,obj,name):
        path=self.native/('ScriptableConfig/Modules/ItemIcon/'+name+'.asset')
        key=guid(Path(str(path)+'.meta'));self.objects[key]=obj
        return self.load(key)

    def configuration(self):
        code=ROOT/'.tools/source-packages/current'/self.recovery.apk.version/'official-code'
        constructors=Constructors(code)
        mapping={name.rsplit('/',1)[-1]:kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',constructors.source('Tables'))}
        tables={};self.table_proofs={}
        wanted={'quick_buy','game_settings','item','item_count_limit','level_number'}
        for obj in self.recovery.apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name!='TextAsset':continue
            name=obj.read().m_Name.rsplit('/',1)[-1].removesuffix('.g')
            if name not in wanted:continue
            raw=obj.read().m_Script.encode('utf8','surrogateescape');tables[name]=constructors.table(raw,mapping[name])
            self.table_proofs[name]=dict(source=self.recovery.apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),
                payload_sha256=sha(raw),constructor=mapping[name])
        if tables.keys()!=wanted:raise ValueError('Original currency configuration missing: '+repr(wanted-tables.keys()))
        item_ids={row['ItemId'] for row in tables['quick_buy']}|{row['CurrencyId'] for row in tables['quick_buy']}
        item_ids.update(tables['game_settings'][key] for key in ('MoneyId','DiamonId','FlareStarId'))
        items={row['ClassId']:row for row in tables['item'] if row['ClassId'] in item_ids}
        tables['item']=list(items.values())
        records=read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage',self.recovery.apk.archive))
        self.currency_images={}
        for item_id in (tables['game_settings']['MoneyId'],tables['game_settings']['DiamonId'],tables['game_settings']['FlareStarId']):
            row=items[item_id];path='Assets/Image/'+row['RawIcon']
            record,=[r for r in records if r.get('address','').casefold()==path.casefold()
                or r['path'].casefold()==(path+'.png').casefold()]
            self.bind_dynamic_sprite(record['guid'])
            self.currency_images[str(item_id)]=dict(item=row,**self.dynamic_sprites_by_guid[record['guid']])
        self.tables=tables
        colors,=[obj for obj in self.recovery.apk.objects('defaultpackage_assets_scriptableconfig_modules_systemsetting.bundle')
            if obj.type.name=='MonoBehaviour' and obj.read(check_read=False).m_Name=='GlobalColors']
        self.global_colors=dict(fields=self.recovery.fields(colors),source=self.recovery.apk.provenance(colors),
            original=identity(colors),sha256=sha(colors.get_raw_data()))
        for name in constructors.sources:
            path=code/'common-source/Common'/(name+'.cs')
            if not path.is_file():path=code/'common-source/Common.LubanGen'/(name+'.cs')
            self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        for path in (code/'global-metadata.dat',ROOT/'scripts/official_tables.py'):
            self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())

    def build(self):
        PartnerGiftBinding.build(self)
        receipt=json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes());folder=self.work/'gift-assets'
        keys={doc['MonoBehaviour']['myKey'] for name in ('ShopQuickBuyDialog','PayMoneyChangeDialog')
            for doc in documents((folder/(name+'.prefab')).read_text('utf8')).values()
            if doc.get('MonoBehaviour',{}).get('myKey')}
        keys.update(('ui_common_contrast_1','ui_common_free','common_format_args1','UI_Pay_Change_Item_Not_Enough_Confirm',
            'ui_quick_buy_currency_not_enough','ui_quick_buy_limit_KeepCountLimit',
            'ui_quick_buy_limit_BuyCountLimit_None','ui_quick_buy_limit_BuyCountLimit_Daily','ui_quick_buy_limit_BuyCountLimit_Weekly'))
        keys.update('item_'+str(row['ClassId'])+'_name' for row in self.tables['item'])
        keys.add('ui_ItemIcon_count_key')
        keys.update(('confirmation','confirm','cancel','ui_item_max_count_buy_confirm_text'))
        keys.update('ui_quick_buy_'+kind+'_limited' for kind in ('None','Daily','Weekly'))
        texts,sources=language(keys);missing=keys-texts.keys()
        if missing-{'{0}'}:raise ValueError('Original currency-window language missing: '+repr(missing))
        receipt['inputs'].update(sources);receipt['texts']=texts;receipt['literal_format_keys']=sorted(missing)
        code=ROOT/'.tools/source-packages/current'/self.recovery.apk.version/'official-code/common-source'
        for name in ('GlobalColors','ThemeColorType','ColorType','TextColorConfig','TextColorList',
            'Common/QuickBuyInfoParser','Common/LevelNumberParser','Common/LanguageUtils'):
            source=code/(name+'.cs');receipt['inputs'][source.relative_to(ROOT).as_posix()]=sha(source.read_bytes())
        for name in ('partner-gift-texts.json','partner-gift-texts.json.meta'):
            receipt['outputs'].pop(name);(folder/name).unlink()
        path=folder/'home-currency-windows-texts.json'
        path.write_text(json.dumps(dict(manifest=self.recovery.apk.version,texts=[dict(key=k,text=v) for k,v in sorted(texts.items())]),
            ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        meta=Path(str(path)+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-currency-windows-texts').hexdigest()+
            '\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        for item in (path,meta):receipt['outputs'][item.name]=sha(item.read_bytes())
        path=folder/'home-currency-windows-rules.json'
        path.write_text(json.dumps(dict(manifest=self.recovery.apk.version,tables=self.tables,globalColors=self.global_colors['fields']),ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        meta=Path(str(path)+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-currency-windows-rules').hexdigest()+
            '\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        for item in (path,meta):receipt['outputs'][item.name]=sha(item.read_bytes())
        used=set()
        for name in receipt['outputs']:
            path=folder/name
            if path.suffix!='.meta' and path.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})',path.read_text('utf8')))
        receipt['script_bindings']={name:row for name,row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()),original_empty_value_restorations=self.empty_restorations,
            tables=self.table_proofs,icon_manager=self.icon_manager,icon_config=self.icon_config,
            attachment_config=self.attachment_config,currency_images=self.currency_images,hint_attachment=self.hint_attachment,
            global_colors=self.global_colors,
            scope='Complete original quick-buy/pay-change windows and dependencies. Explicit local services, real transactions and runtime verification remain required.')
        (self.work/'home-currency-windows-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Original currency windows:',len(receipt['outputs']),'native outputs',flush=True)


if __name__=='__main__':HomeCurrencyWindowsBinding(validation_directory()).build()
