"""Decode native map player actions without replacing the chosen appearance."""
import hashlib,json,subprocess
from prepare_map_regions import Regions,ART,REF,ROOT,save
from map_player_assets import PlayerAssets,identity

def build():
    e=Regions();assets=PlayerAssets(e)
    bundle='defaultpackage_assets_appearanceassets_character_model_male_1.bundle'
    asset=e.apk.asset(bundle,'/character_Male_1_SkeletonData.asset');e.input(asset);assets.split_definition(asset)
    names=['map/idle_01','map/run_01','map/die_02','map/affirmative_01','map/increase_01','map/portal_01','map/win_03','200/skill_15']
    names += [r['animation'] for r in json.loads((ART/'map-activity-hud.json').read_text(encoding='utf8'))['supporter']]
    cache=ROOT/'.tools/source-packages/character-male'
    job=dict(skeleton=str(cache/'character_Male_1.skel.bytes'),atlas=str(cache/'character_Male_1.atlas.bytes'),animations=names,split_animations={})
    for name in names:
        split=dict(animation=name);assets.prepare_split_animation(identity(asset)+':@'+name,split)
        job['split_animations'].update(split.get('split_animations',{}))
        if split.get('string_table'):job['string_table']=split['string_table']
    controller=e.apk.asset('defaultpackage_assets_appearanceassets_character_model_anim.bundle','/Male_Map_Controller.controller');e.input(controller)
    raw=controller.read_typetree();strings=dict(raw['m_TOS']);clips=controller.read().m_AnimationClips;parameters={}
    for machine in raw['m_Controller']['m_StateMachineArray']:
        states=machine['data']['m_StateConstantArray']
        for transition in machine['data']['m_AnyStateTransitionConstantArray']:
            transition=transition['data'];destination=transition['m_DestinationState']
            if destination>=len(states):continue
            state=states[destination]['data'];blend=state['m_BlendTreeConstantArray']
            ids=[v['data']['m_ClipID'] for v in blend[0]['data']['m_NodeArray']] if blend else []
            for condition in transition['m_ConditionConstantArray']:
                parameter=strings.get(condition['data']['m_EventID'],'')
                if parameter not in ['affirmative','Raise']:continue
                for index in ids:
                    if index<len(clips) and clips[index].read().m_Name in names:parameters[parameter]=clips[index].read().m_Name
    if parameters!={'affirmative':'map/affirmative_01','Raise':'map/increase_01'}:raise ValueError('Unexpected original map parameter transitions: '+str(parameters))
    parameters['idle']='map/idle_01';job['parameters']=parameters
    from official_appearance_catalog import catalog
    parts=catalog()['parts']['male']
    meshes=json.loads((ROOT/'assets/characters/original/meshes.json').read_text(encoding='utf8'))
    job.update(parts=parts,slots={p:[m['slot']for m in meshes[p]] for p in parts})
    save(cache/'map-actions-job.json',job)
    subprocess.run(['node',str(ROOT/'scripts/extract_story_character_actions.mjs'),str(cache/'map-actions-job.json'),str(ART/'map-player-actions.json')],check=True)
    save(REF/'player-actions-source-manifest.json',dict(manifest='854_156383',inputs=e.source_records,schemas=e.schemas.records,parameters=parameters,animations=names,parts=parts,outputs={'map-player-actions.json':hashlib.sha256((ART/'map-player-actions.json').read_bytes()).hexdigest()},operation='Original Male_Map_Controller transitions and matching original Spine animations sampled at 24 fps for every currently supported body, face, hair, eyes, mouth, makeup and ethnicity skin. Tower portal and victory animations are included from the male split-animation definition. Existing dye masks and chosen appearance are retained.'))

if __name__=='__main__':build()
