"""Original astrological catalog, script bindings and season prefab conversion."""
import hashlib
import re
import struct
from official_unity_schema import ScriptSchemas
from official_tables import SOURCE
from prepare_official_panels import unique_sibling_paths
from UnityPy.classes.PPtr import PPtr


def extract_astrological(e, constructors, tables, sources, panels, text, localize):
    schemas = ScriptSchemas()
    names = {m[2].rsplit('/', 1)[-1]: m[1] for m in re.finditer(
        r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    for obj in e.apk.objects('config_luban_astrologicaldestiny'):
        if obj.type.name != 'TextAsset': continue
        asset = obj.read()
        name = asset.m_Name.removesuffix('.g')
        if name not in ['astrological_level', 'astrological_season_config',
                        'astrological_season_activity', 'astrological_season_top_award',
                        'destiny_score_award']: continue
        raw = asset.m_Script.encode('utf8', 'surrogateescape') if isinstance(asset.m_Script, str) else asset.m_Script
        tables[name] = constructors.table(raw, names[name])
        sources[name] = dict(e.apk.provenance(obj), object=asset.m_Name, bytes=len(raw),
                            sha256=hashlib.sha256(raw).hexdigest(), constructor=names[name],
                            constructor_sha256=hashlib.sha256((SOURCE / (names[name]+'.cs')).read_bytes()).hexdigest())
    config_obj = e.apk.asset('scriptableconfig_modules_astrologicaldestiny', '/AstrologicalDestinyConfig.asset')
    config = schemas.read(config_obj)
    tables['astrological_art'] = {}
    # Initialize the pinned manifest's GUID lookup before resolving season prefabs.
    for group in ['SeasonSprites', 'ContractTagSetting', 'GradeSpritesProxy']:
        tables['astrological_art'][group] = {}
        for pair in config[group]['mSerializeBuffer']:
            value = pair['Value']
            if 'mAssetGuid' in value: result = e.guid_sprite(value['mAssetGuid'])
            else:
                result = {key: e.guid_sprite(field['mAssetGuid']) if isinstance(field, dict) and 'mAssetGuid' in field else field
                          for key, field in value.items()}
            tables['astrological_art'][group][str(pair['Key'])] = result
    sources['astrological_config'] = dict(e.apk.provenance(config_obj), object='AstrologicalDestinyConfig',
                                          sha256=hashlib.sha256(config_obj.get_raw_data()).hexdigest())
    from prepare_official_city import ART
    import json
    rules=json.loads((ART/'rules.json').read_text('utf8'))
    icon=next(row['RawIcon'] for row in rules['item'] if row['ClassId']==61)
    guid=next(guid for guid,path in e.guid_paths.items() if path.lower().removesuffix('.png')=='assets/image/'+icon.lower())
    tables['astrological_art']['source_star_icon']=e.guid_sprite(guid)
    specs = {'astrological_rank': ('prefabs_ui_astrologicaldestiny', '/AstrologicalDestinyRankItem.prefab'),
             'astrological_contract_tag': ('prefabs_ui_astrologicaldestiny', '/AstrologicalContractTag.prefab'),
             'astrological_rule': ('prefabs_ui_astrologicaldestiny', '/AstrologicalDestinyRuleDialog.prefab')}
    for pair in config['SeasonSubPanelsProxy']['mSerializeBuffer']:
        path = e.guid_paths[pair['Value']['mAssetGuid']]
        key = 'defaultpackage_' + path.replace('/', '_').lower()
        bundle = max((b for b in e.apk.index if key.startswith(b.removesuffix('.bundle'))), key=len)
        specs['astrological_season_'+str(pair['Key'])] = (bundle, '/'+path.rsplit('/',1)[-1])

    def object_path(component, pointer):
        ptr = PPtr(m_FileID=pointer['m_FileID'], m_PathID=pointer['m_PathID'], assetsfile=component.assets_file)
        if not ptr.path_id: return None
        if ptr.type.name not in ['GameObject', 'MonoBehaviour', 'RectTransform', 'Transform']: return None
        if ptr.type.name != 'GameObject': ptr = ptr.deref().read(check_read=False).m_GameObject
        return e.object_path(ptr)

    def original_fields(component, cls):
        if cls not in ['AstrologicalDestinyRankItem','AstrologicalContractTag']:return schemas.read(component)
        # These two private enum specializations fail AssetsTools root generation.
        # Reuse the exact StateSwitcher / Event schemas from the entry/contract,
        # and the pinned C# serialized field order; UnityPy still checks full bytes.
        from official_apk import ROOT
        from copy import deepcopy
        source=(ROOT / '.tools/source-packages/official-code/client-source' / (cls+'.cs')).read_text('utf-8-sig')
        schemas.tree('Assembly-CSharp','AstrologicalDestinyEntryPanel')
        schemas.tree('Assembly-CSharp','AstrologicalContractContent')
        entry=schemas.cache['Assembly-CSharp:AstrologicalDestinyEntryPanel']
        contract=schemas.cache['Assembly-CSharp:AstrologicalContractContent']
        def subtree(tree,name):
            at=next(i for i,n in enumerate(tree) if n['level']==1 and n['name']==name)
            end=next((i for i in range(at+1,len(tree)) if tree[i]['level']==1),len(tree))
            return deepcopy(tree[at:end])
        nodes=deepcopy(entry[:9]);nodes[0]['type']=cls
        for kind,name in re.findall(r'\[SerializeField\]\s*(?:\[[^]]+\]\s*)*private ([\w<>]+) (\w+)(?:\s*=.*)?;',source):
            if kind.startswith('StateSwitcher<'):part=subtree(entry,'mTabSwitcher')
            elif kind=='Event':part=subtree(contract,'mOnChargeEndSound')
            elif kind=='ImageProxy':
                schemas.tree('Framework.FirstPass.Runtime.dll','ImageProxy')
                part=[{'level':1,'type':'ImageProxy','name':name,'flags':0}]
                for field in schemas.cache['Framework.FirstPass.Runtime.dll:ImageProxy'][9:]:
                    child=deepcopy(field);child['level']+=1;part.append(child)
            else:part=subtree(entry,'mAnimation');part[0]['type']='PPtr<$'+kind+'>'
            part[0]['name']=name;nodes.extend(part)
        nodes.extend(subtree(entry,'references'))
        schemas.cache['Assembly-CSharp:'+cls]=nodes
        result=schemas.read(component)
        sources.setdefault('astrological_manual_schema',{})[cls]={
            'source_sha256':hashlib.sha256(source.encode('utf8')).hexdigest(),
            'object_sha256':hashlib.sha256(component.get_raw_data()).hexdigest(),
            'operation':'Original serialized field order and exact StateSwitcher/Event schemas; complete read required'}
        return result

    def enrich(node, components):
        for component in components.get(node['path'], []):
            basic = component.read(check_read=False)
            cls = basic.m_Script.read().m_ClassName
            fields = original_fields(component,cls)
            info = {}
            for key, value in fields.items():
                if key in ['m_GameObject', 'm_Script', 'm_Name', 'm_Enabled', 'references']: continue
                if isinstance(value, dict) and 'm_PathID' in value:
                    target = object_path(component, value)
                    if target: info[key] = target
                elif isinstance(value, (int, float, str, bool)): info[key] = value
            # Resolve the original managed StateSwitcher object sets independently
            # of dictionary/enum formatting. No state is inferred from node names.
            states = {}
            ref_states = {}
            for ref in fields.get('references', {}).get('RefIds', []):
                data = ref.get('data', {})
                current = {}
                for name, value in data.items():
                    if not isinstance(value, dict) or 'GoConfig' not in value: continue
                    for state in value['GoConfig']:
                        paths=[object_path(component, target) for target in state['GameObjects'] if target['m_PathID']]
                        current.setdefault(str(state['State']['mValue']), []).extend(paths)
                        states.setdefault(str(state['State']['mValue']), []).extend(paths)
                ref_states[ref['rid']]=current
            node.setdefault('original_bindings', {})[cls] = info
            if states: node.setdefault('original_states', {})[cls] = states
            switches={}
            for name,value in fields.items():
                if not isinstance(value,dict) or 'mStateConfigs' not in value:continue
                result={}
                for ref in value['mStateConfigs']:
                    for state,paths in ref_states.get(ref['rid'],{}).items():result.setdefault(state,[]).extend(paths)
                switches[name]=result
            if switches:node.setdefault('original_switchers',{})[cls]=switches
        for child in node['children']: enrich(child, components)

    for key, (bundle, suffix) in specs.items():
        panels[key] = e.ui(bundle, suffix)
    for key in [k for k in panels if k.startswith('astrological')]:
        source = panels[key]['source']
        components = {}
        autosize = {}
        for obj in e.apk.objects(source['bundle']):
            if obj.type.name != 'MonoBehaviour': continue
            basic = obj.read(check_read=False)
            cls = basic.m_Script.read().m_ClassName
            if cls=='TextMeshProUGUI':
                raw=obj.get_raw_data();length=struct.unpack_from('<i',raw,88)[0]
                aligned=(92+length+3)&~3
                shared=struct.unpack_from('<i',raw,aligned+28)[0]
                materials=struct.unpack_from('<i',raw,aligned+44+12*shared)[0]
                style=aligned+12*(shared+materials)
                if raw[style+204]:autosize[e.object_path(basic.m_GameObject)]=list(struct.unpack_from('<2f',raw,style+208))
            if cls not in ['AstrologicalDestinyEntryPanel', 'AstrologicalContractContent',
                           'AstrologicalContractChargeButton', 'AstrologicalContractTag',
                           'AstrologicalDestinyRankItem', 'AstrologicalDestinyShopContent']: continue
            path = e.object_path(basic.m_GameObject)
            components.setdefault(path, []).append(obj)
        enrich(panels[key]['root'], components)
        def original_autosize(node):
            if node['path'] in autosize and 'text' in node:node['text']['auto_size']=autosize[node['path']]
            for child in node['children']:original_autosize(child)
        original_autosize(panels[key]['root'])
        unique_sibling_paths(panels[key]['root']); localize(panels[key]['root'])
        print('Panel', key, flush=True)
    for key in ['astrological_season_title', 'astrological_season_close_tip',
                'astrological_season_default_rank_item_title', 'destiny_contract_charge_full_tip',
                'destiny_contract_charge_lack_tip']:
        text(key)
    for season in tables['astrological_season_config']:
        text('astrological_season_%d_name' % season['Season'])
