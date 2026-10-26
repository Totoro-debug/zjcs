"""Convert original map tables, dialogue CSVs and both sides of story graphs."""
import csv
import hashlib
import io
import json
import re
import math
import sys
from official_apk import Apk, ROOT
from official_tables import Constructors, SOURCE
from official_unity_schema import ScriptSchemas
from official_map_codec import unpack
from UnityPy.classes.PPtr import PPtr
from prepare_map_regions import save, ART, REF, MAIN


def finite(value):
    if isinstance(value,float) and not math.isfinite(value):return str(value)
    if isinstance(value,dict):return {k:finite(v) for k,v in value.items()}
    if isinstance(value,list):return [finite(v) for v in value]
    return value


def dialog_graphs(tables):
    """Adapt the newer DialogNode table to the same durable story interpreter.

    Node fields and successor IDs are native; only the common graph envelope is
    generated. Localization remains the client's dialog_node_ID_NODE key.
    """
    groups={}
    for row in tables['Dialog/dialog_node']:
        groups.setdefault(row['DialogId'],[]).append(row)
    output={}
    for did,rows in groups.items():
        def ports(ids):return {'Exit':{'connections':[{'node':str(i),'port':'Entry'} for i in ids]}}
        nodes={'start':dict(_type='StartStoryNode',CanStart=True,ports=ports([min(r['NodeId'] for r in rows)]))}
        for row in rows:
            nodes[str(row['NodeId'])]=dict(row,_type='NativeDialogNode',TextKey=f"dialog_node_{did}_{row['NodeId']}",ports=ports(row['NextNodes']))
        output['@dialog/'+str(did)]=dict(nodes=nodes,server=[],properties={'RunAsSubGraph':True,'native_dialog_id':did})
    return output


def build():
    a=Apk();c=Constructors();schemas=ScriptSchemas();records={};tables={};failures={}
    mapping={m[2]:m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',c.source('Tables'))}
    for name,cls in mapping.items():
        if not (name.startswith(('Map/','Dialog/','DoubleKitchen/')) or name in ['EC/entity_grid_mover','story','stage_story','story_entity_listen','story_entity_status','task','story_option_dialog','map_stage','hand_in_item','fixed_reward','def_award_handler','daily_dungeon_battle','daily_dungeon_room','daily_dungeon_map_group','daily_dungeon_event','daily_dungeon_random_event_group','monster','status_effect','fight_status','map_scene_status','monster_reward','monster_segment_reward','bgm_event_key','activity_music_festival_event_batch','activity_music_festival_timeline','level_number','wheel_reward','witch','shop_group','arrest_monster','activity_puzzle','minigame_element_link','minigame_energy_circuit','ruin','stage_append_entity','append_entity_award','bubble_dialog','activity_hide_and_seek']):continue
        key='defaultpackage_assets_config_luban_'+name.rsplit('/',1)[0].replace('/','_').lower()+'.bundle' if '/' in name else 'defaultpackage_assets_config_luban.bundle'
        try:
            obj=a.asset(key,'/'+name+'.g.bytes');raw=obj.read().m_Script.encode('utf8','surrogateescape')
            tables[name]=c.table(raw,cls)
            records[name]=dict(a.provenance(obj),bytes=len(raw),consumed_bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest(),constructor=cls,constructor_sha256=hashlib.sha256((SOURCE/(cls+'.cs')).read_bytes()).hexdigest())
        except Exception as ex:failures[name]=type(ex).__name__+': '+str(ex)
    if '--tables-only' in sys.argv:
        output=json.loads((ART/'adventure.json').read_text(encoding='utf8'))
        output['tables']=tables;output['stories'].update(dialog_graphs(tables));save(ART/'adventure.json',finite(output))
        manifest=json.loads((REF/'story-source-manifest.json').read_text(encoding='utf8'))
        manifest.update(tables=records,table_failures=failures,native_dialog_graphs=len(dialog_graphs(tables)),adventure_sha256=hashlib.sha256((ART/'adventure.json').read_bytes()).hexdigest())
        save(REF/'story-source-manifest.json',manifest)
        print('Tables',len(tables),'failures',failures,flush=True)
        return
    # Legacy dialog CSV contains the complete original spoken text, options,
    # role IDs and voice keys. It is not the unrelated MessageDialog UI text.
    obj=next(o for o in a.objects('defaultpackage_assets_config.bundle') if o.type.name=='TextAsset' and o.read().m_Name=='dialog');raw=obj.read().m_Script.encode('utf8','surrogateescape')
    dialogues={}
    for row in csv.DictReader(io.StringIO(raw.decode('utf-8-sig'))):
        if not row.get('Id','').isdigit():continue
        story=row['StoryName'] or 'default';did=row['Id']
        for key in ['Id','Order','RoleId','EntityLogicId','CreateEntityClassId']:row[key]=int(row.get(key) or 0)
        dialogues.setdefault(story,{}).setdefault(did,[]).append(row)
    for story in dialogues.values():
        for rows in story.values():rows.sort(key=lambda r:r['Order'])
    records['dialog.csv']=dict(a.provenance(obj),bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest(),rows=sum(len(v) for s in dialogues.values() for v in s.values()))
    entries=unpack(a.asset('config_map','/MapEntryInfoPack.g.bytes').read().m_Script.encode('utf8','surrogateescape'))[0]
    transports=unpack(a.asset('config_map','/MapTransportEntityInfoPack.g.bytes').read().m_Script.encode('utf8','surrogateescape'))[0]
    server={}
    for path,ptr in a.container('config_story'):
        if ptr.type.name!='TextAsset':continue
        if not path.endswith('.json'):continue
        name=path.rsplit('/',1)[-1].removesuffix('.json');nodes=[]
        for node in json.loads(ptr.read().m_Script):
            try:d=json.loads(node['Item2'])
            except json.JSONDecodeError:d=json.loads(node['Item2'].replace('\\"','"'))
            nodes.append(dict(d,_type=node['Item1']))
        server[name]=nodes
    stories={};graph_records={};unread={}
    pattern=r'defaultpackage_assets_scriptableconfig_story_'
    for bundle in sorted(n for n in a.index if re.search(pattern,n)):
        objects=a.objects(bundle);graphs=[]
        for obj in objects:
            if obj.type.name!='MonoBehaviour':continue
            cls=obj.read(check_read=False).m_Script.read().m_ClassName
            if cls=='StoryGraph':graphs.append(obj)
        for graph in graphs:
            definition=schemas.read(graph);name=definition['m_Name'];nodes={};by_path={}
            for ptr in definition['nodes']:
                obj=graph.assets_file.objects[ptr['m_PathID']]
                cls=obj.read(check_read=False).m_Script.read().m_ClassName
                try:
                    data=schemas.read(obj);by_path[str(obj.path_id)]=str(data['NodeId']);nodes[str(data['NodeId'])]=dict(data,_type=cls)
                except Exception as ex:unread[bundle+':'+str(obj.path_id)]=dict(story=name,cls=cls,error=type(ex).__name__+': '+str(ex))
            for data in nodes.values():
                ports={}
                for port in data.pop('ports',{}).get('values',[]):
                    ports[port['_fieldName']]=dict(direction=port['_direction'],flow=port.get('_flowConnectionType',0),connections=[dict(node=by_path.get(str(v['node']['m_PathID']),''),port=v['fieldName']) for v in port['connections']])
                data['ports']=ports
                for k in ['m_GameObject','m_Script','m_Enabled','graph','position','Mono','IsMonoShow']:data.pop(k,None)
            for node in nodes.values():
                for field in ['WrappingInputNode','WrappingOutputNode','GoTo']:
                    if field in node:node[field]=by_path.get(str(node[field]['m_PathID']),'')
                if 'Subgraph' in node and node['Subgraph']['m_PathID']:
                    node['subgraph_name']=schemas.read(PPtr(**node['Subgraph'],assetsfile=graph.assets_file).deref())['m_Name']
            stories[name]=dict(nodes=nodes,server=server.get(name,[]),properties={k:v for k,v in definition.items() if k!='nodes'})
            graph_records[name]=dict(a.provenance(graph),nodes=len(definition['nodes']),converted_nodes=len(nodes))
        print('Story bundle',bundle,'graphs',len(graphs),flush=True)
    enums={}
    for name in ['StageComponentType','ConditionType','Profession','StoryStatus','PlayerMapWorldAloneStatus','Rank','SubRank','Sex','ItemType','SkillType']:
        source=SOURCE/(name+'.cs')
        if source.exists():
            current=-1;values={}
            for field in re.findall(r'^\s*(\w+)\s*(?:=\s*(\d+))?\s*,?\s*$',source.read_text(encoding='utf-8-sig'),re.M):
                if field[0] in ['namespace','Common']:continue
                current=int(field[1]) if field[1] else current+1;values[str(current)]=field[0]
            enums[name]=values
    stories.update(dialog_graphs(tables))
    output=dict(tables=tables,dialogues=dialogues,stories=stories,entries=entries,transports=transports,enums=enums)
    save(ART/'adventure.json',finite(output))
    save(REF/'story-source-manifest.json',dict(manifest='854_156383',tables=records,table_failures=failures,graphs=graph_records,unconverted_nodes=unread,schemas=schemas.records,adventure_sha256=hashlib.sha256((ART/'adventure.json').read_bytes()).hexdigest()))
    reference=ROOT/'assets/reference/official-city/source-manifest.json';manifest=json.loads(reference.read_text(encoding='utf8'))
    manifest['data_sha256']['adventure.json']=hashlib.sha256((ART/'adventure.json').read_bytes()).hexdigest();save(reference,manifest)
    print('Stories',len(stories),'dialogs',len(dialogues),'unconverted nodes',len(unread),'table failures',failures,flush=True)


if __name__=='__main__':build()
