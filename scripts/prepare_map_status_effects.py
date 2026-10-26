"""Convert native effect scopes and explicit percentages in official entry text."""
import hashlib,json,re
from official_apk import Apk
from official_tables import Constructors,SOURCE
from prepare_map_regions import ART,REF,save


FIELDS={'攻击':'attack','防御':'defence','最大生命值':'hp','生命':'hp','速度':'speed','伤害加成':'damage','自身受到伤害':'incoming','伤害抵抗':'resistance','会心率':'critical','会心':'critical','会心伤害':'critical_damage','精准':'accuracy','格挡':'block','治疗加成':'healing'}


def parameters(text):
    if not text:return {}
    parts=re.split(r'("(?:\\.|[^"\\])*")',text)
    for i in range(0,len(parts),2):parts[i]=re.sub(r'([{,]\s*)([A-Za-z_][A-Za-z_0-9]*)\s*:',r'\1"\2":',parts[i])
    return json.loads(''.join(parts))


def build():
    apk=Apk();constructors=Constructors();obj=apk.asset('defaultpackage_assets_config_luban.bundle','/status_effect_scope.g.bytes')
    raw=obj.read().m_Script.encode('utf8','surrogateescape');scopes=constructors.table(raw,'TbStatusEffectScopeInfo')
    effects=json.loads((ART/'adventure.json').read_text(encoding='utf8'))['tables']['status_effect']
    strings=json.loads((ART/'map-localization.json').read_text(encoding='utf8'));output={};unmapped={}
    pattern=re.compile('('+'|'.join(sorted(FIELDS,key=len,reverse=True))+r'(?:、(?:'+'|'.join(sorted(FIELDS,key=len,reverse=True))+r'))*)([+-]\d+)%')
    for cfg in effects:
        name=strings.get(f"entry_{cfg['EntryId']}_name",'');desc=strings.get(f"entry_{cfg['EntryId']}_desc",'');modifiers={}
        for match in pattern.finditer(desc):
            for field in match[1].split('、'):modifiers[FIELDS[field]]=int(match[2])
        handlers={name:parameters(value) for name,value in parameters(cfg['Params']).get('SubHandlers',{}).items()}
        output[str(cfg['ClassId'])]=dict(cfg,name=name,description=desc,modifiers=modifiers,handlers=handlers)
        if cfg['FightStatusIds'] and not modifiers:unmapped[str(cfg['ClassId'])]=desc
    templates=json.loads((ART/'map-entities.json').read_text(encoding='utf8'))['templates'];actions={}
    pending=[int(i) for r in effects for i in r['MapStatusIds'] or []]
    while pending:
        identity=str(pending.pop())
        if identity in actions:continue
        actions[identity]=templates.get(identity,{})
        def visit(value):
            if isinstance(value,dict):
                for key,item in value.items():
                    if key in ['HitAction','DamageAction','ActionId'] and isinstance(item,int) and item:pending.append(item)
                    else:visit(item)
            elif isinstance(value,list):
                for item in value:visit(item)
        visit(actions[identity])
    save(ART/'map-status.json',{'effects':output,'scopes':{str(r['Id']):r for r in scopes},'map_actions':actions})
    save(REF/'status-source-manifest.json',{'manifest':'854_156383','scope_source':dict(apk.provenance(obj),sha256=hashlib.sha256(raw).hexdigest(),bytes=len(raw),consumed_bytes=len(raw),constructor='TbStatusEffectScopeInfo',constructor_sha256=hashlib.sha256((SOURCE/'TbStatusEffectScopeInfo.cs').read_bytes()).hexdigest()),'effects_source':'story-source-manifest.json','text_source':'localization-source-manifest.json','map_actions_source':'entities-source-manifest.json','unmapped_combat_descriptions':unmapped,'outputs':{'map-status.json':hashlib.sha256((ART/'map-status.json').read_bytes()).hexdigest()},'operation':'Original effect scopes, durations, activation counts, leave-map parameters and recursively referenced native map action components. Explicit signed percentages are read from the original entry descriptions and applied by the existing local combat simulator; not represented as extracted server combat formulas. Unmapped combat descriptions remain explicit.'})
    print('Status effects',len(output),'scopes',len(scopes),'without numeric combat percentages',unmapped,flush=True)


if __name__=='__main__':build()
