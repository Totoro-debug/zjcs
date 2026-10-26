"""Recover original Chinese strings using the client's XXHash64 key index."""
import hashlib
import json
import re
import struct
from official_apk import ROOT
from prepare_map_regions import ART, REF, save
import xxhash

PUZZLE_41001_KEYS = (
    'activity_puzzle_1_7_name', 'activity_puzzle_1_8_name',
    'GachaPanel.ReplaceRaffleTicket', 'ItemInfoDialog.Limit.QuickBuyConditionNotMet',
    'ui_activity_puzzle_unlock_desc', 'ui_activtiy_puzzle_award',
    'ui_activtiy_puzzle_competed', 'ui_quit_text', 'ui_activity_puzzle_next_level',
    'ui_activity_puzzle_next_level_locked', 'ui_activity_puzzle_award_is_receive',
    'ui_activity_puzzle_back_content', 'confirmation', 'confirm', 'cancel',
)


def task_growth_keys():
    source=ROOT/'.tools/source-packages/official-code/common-source/Common/SubRank.cs'
    names=re.findall(r'^\s*(\w+)\s*,?\s*$',source.read_text(encoding='utf8'),re.M)
    return ['condition_SubRank_desc','jump_to_home_tip0']+['SubRank.'+name for name in names],source


def build(puzzle_41001=False, task_growth=False):
    names=['wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile','wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile']
    directory=ROOT/'.tools/source-packages/WwiseBnk';sources=json.loads((directory/'bundle-sources.json').read_text(encoding='utf8'));texts={};records={}
    manifest_path=REF/'localization-source-manifest.json'
    previous_manifest=json.loads(manifest_path.read_text(encoding='utf8'))
    for name in names:
        raw=(directory/name).read_bytes()
        digest=hashlib.sha256(raw).hexdigest()
        if digest!=previous_manifest['inputs'][name]['sha256']:
            raise ValueError(f'Official language input SHA256 mismatch: {name}')
        count=struct.unpack_from('<i',raw)[0];start=4+12*count
        for i in range(count):
            key,offset=struct.unpack_from('<Qi',raw,4+12*i);pos=start+offset
            size=struct.unpack_from('<H',raw,pos)[0]
            if pos<start or pos+2+size>len(raw):raise ValueError('Invalid localization offset')
            texts[key]=raw[pos+2:pos+2+size].decode('utf8')
        records[name]=dict(source=sources[name],sha256=hashlib.sha256(raw).hexdigest(),entries=count)
    if puzzle_41001 or task_growth:
        path=ART/'map-localization.json';before=path.read_bytes()
        output=json.loads(before.decode('utf8'));added=[]
        keys=PUZZLE_41001_KEYS
        scope='41001 native puzzle UI and original next-level name'
        if task_growth:
            keys,enum_path=task_growth_keys()
            scope='PlayerUpgrade and mainline subrank condition; original SubRank enum names'
        for key in keys:
            value=texts.get(xxhash.xxh64_intdigest(key.encode('utf8')))
            if task_growth and key.startswith('SubRank.') and value is None:continue
            if value is None:raise ValueError(f'Original language key is missing: {key}')
            if key in output and output[key]!=value:raise ValueError(f'Existing original text differs: {key}')
            if key not in output:added.append(key)
            output[key]=value
        save(path,output)
        after=hashlib.sha256(path.read_bytes()).hexdigest()
        previous_manifest.update(keys=len(output),output_sha256=after)
        increments=previous_manifest.setdefault('increments',[])
        if added:
            increments.append(dict(scope=scope,
                keys=added,inputs=records,previous_output_sha256=hashlib.sha256(before).hexdigest(),output_sha256=after))
            if task_growth:
                increments[-1]['enum_source']=dict(path=str(enum_path.relative_to(ROOT)),sha256=hashlib.sha256(enum_path.read_bytes()).hexdigest())
        save(manifest_path,previous_manifest)
        print(scope,'Chinese strings added',len(added),flush=True)
        return
    keys=set()
    def visit(value):
        if isinstance(value,dict):
            for k,v in value.items():visit(k);visit(v)
        elif isinstance(value,list):
            for v in value:visit(v)
        elif isinstance(value,str) and len(value)<300 and re.fullmatch(r'[A-Za-z][A-Za-z0-9_{}.:\- ]*',value):keys.add(value)
    for name in ['adventure.json','map-ui.json','map-entities.json','rules.json','ui.json','map-activity-hud.json']:
        visit(json.loads((ART/name).read_text(encoding='utf8')))
    keys.add('ui_activity_music_reward_limit_max')
    keys.update(task_growth_keys()[0])
    # Entry names/descriptions are computed by LanguageUtils in the client;
    # they are absent from literal prefab strings and still need local text.
    for row in json.loads((ART/'adventure.json').read_text(encoding='utf8'))['tables']['status_effect']:
        keys.update(f"entry_{row['EntryId']}_{suffix}" for suffix in ['name','desc'])
    galaxy=ART/'map-galaxy.json'
    if galaxy.exists():
        for id in json.loads(galaxy.read_text(encoding='utf8'))['books']:
            keys.update(f'story_book_{id}_{suffix}' for suffix in ['title','content','option_title'])
        for row in json.loads((ART/'adventure.json').read_text(encoding='utf8'))['tables']['story_option_dialog']:
            keys.add(f"story_{row['StoryName']}_{row['Id']}_option")
    output={key:texts[h] for key in sorted(keys) if (h:=xxhash.xxh64_intdigest(key.encode('utf8'))) in texts}
    save(ART/'map-localization.json',output)
    save(REF/'localization-source-manifest.json',dict(manifest='854_156383',inputs=records,lookup='XXHash64 UTF-8 seed 0, ushort UTF-8 byte lengths',keys=len(output),output_sha256=hashlib.sha256((ART/'map-localization.json').read_bytes()).hexdigest()))
    print('Original Chinese strings',len(output),flush=True)


if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser();parser.add_argument('--puzzle-41001',action='store_true');parser.add_argument('--task-growth',action='store_true')
    args=parser.parse_args();build(args.puzzle_41001,args.task_growth)
