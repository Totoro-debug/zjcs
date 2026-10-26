"""Resolve original EC managed references without losing 64-bit identities."""
import hashlib
import json
from prepare_map_regions import ART, REF, save


def normalize(value):
    if isinstance(value, list):return [normalize(v) for v in value]
    if isinstance(value, dict):return {k:str(v) if k=='rid' else normalize(v) for k,v in value.items()}
    return value


def build():
    path=ART/'map-models/catalog.json';catalog=json.loads(path.read_text(encoding='utf8'));count=0
    for model in catalog['models'].values():
        states={}
        overlap_display=False
        for driver in model.get('views',[]):
            refs={str(r['rid']):r for r in driver.get('references',{}).get('RefIds',[]) if 'data' in r}
            def resolve(value,depth=0):
                if depth>20:raise ValueError('Cyclic interaction reference')
                if isinstance(value,list):return [resolve(v,depth+1) for v in value]
                if not isinstance(value,dict):return value
                if set(value)=={'rid'}:
                    ref=refs.get(str(value['rid']))
                    return dict(type=ref['type']['class'],data=resolve(ref['data'],depth+1)) if ref else {}
                return {k:resolve(v,depth+1) for k,v in value.items()}
            for ref in refs.values():
                if ref['type']['class']=='ECEntityOverlapDisplayView':overlap_display=True
                if ref['type']['class']!='ECWorldStageView':continue
                pointer=ref['data'].get('mWorldStageCfgProviderData',{}).get('ECWorldStageCfgProvider',{})
                provider=refs.get(str(pointer.get('rid','')),{});data=provider.get('data',{})
                for state,cfg in enumerate(data.get('mStateCfgs',[])):
                    states[str(state)]=[dict(match=-1,operation=resolve(cfg.get('InteractionOperation',{}).get('InteractionOperation',{})))]
                for state,cfg in enumerate(data.get('mConditionStateCfgsList',[])):
                    states[str(state)]=[dict(match=row['MapEntityBehMatchId'],operation=resolve(row['StageStateCfg'].get('InteractionOperation',{}).get('InteractionOperation',{}))) for row in cfg['Configs']]
        model['interactions']=states
        if overlap_display:model['overlap_display']=True
        model['views']=normalize(model.get('views',[]));count+=bool(states)
    save(path,catalog)
    save(ART/'map-interactions.json',{path:model['interactions'] for path,model in catalog['models'].items() if model.get('interactions')})
    save(REF/'interactions-source-manifest.json',dict(manifest='854_156383',source='views-source-manifest.json',models=count,output_sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    print('Interaction bindings',count,flush=True)


if __name__=='__main__':build()
