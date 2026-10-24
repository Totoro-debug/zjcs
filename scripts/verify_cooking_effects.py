"""Validate original cooking data, exact inputs and runtime template bindings."""
import hashlib
import json
from pathlib import Path
from official_apk import ROOT
from official_tables import Constructors
from prepare_map_props import decode


def verify():
    record=json.loads((ROOT/'assets/reference/cooking-effects/source-manifest.json').read_text('utf8'))
    assert record['manifest']=='854_156383'
    target=ROOT/'assets/official/cooking-effects.json'
    assert hashlib.sha256(target.read_bytes()).hexdigest()==record['output_sha256']
    data=json.loads(target.read_text('utf8'))
    for path,digest in {**record['original_code_sha256'],**record['language_sha256']}.items():
        assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==digest,path
    # Code-only HP migration is separate from the converted asset inventory.
    role=ROOT/'.tools/source-packages/official-code/common-source/Common/FightRoleFightComponent.cs'
    assert hashlib.sha256(role.read_bytes()).hexdigest()=='3b842762196f9f5701339f34be02179e9af3f352db79c770d83f0f637994c048'
    raw=(ROOT/'.tools/source-packages/official-data/cooking_prop_effect_full.g.bytes').read_bytes()
    assert hashlib.sha256(raw).hexdigest()==record['table']['sha256']
    assert Constructors().table(raw,'TbCookingPropEffectFullInfo')==data['permanent']
    prop_path=ROOT/record['level_props']['path']
    assert hashlib.sha256(prop_path.read_bytes()).hexdigest()==record['level_props']['sha256']
    assert decode(prop_path.read_bytes(),set(map(int,data['props'])))==data['props']
    assert len(data['permanent'])==284 and len(data['props'])==71 and len(data['templates'])==14
    for name,template in data['templates'].items():
        nodes={}
        def visit(node):
            assert node['path'] not in nodes,(name,node['path'])
            nodes[node['path']]=node
            for child in node['children']:visit(child)
        visit(template['root'])
        for node in nodes.values():
            for cls,fields in node.get('original_bindings',{}).items():
                for key in ['mEffectItemRoot','mTitleTxt','mStageItemRoot','mRoundTitleText','mRemainRoundText',
                            'mRangeTxt','mEfficiencyTxt','mInfoText','myPropTypeText','myPropValueText']:
                    if fields.get(key):assert fields[key] in nodes,(name,cls,key,fields[key])
    rules=json.loads((ROOT/'assets/official/rules.json').read_text('utf8'))
    status=json.loads((ROOT/'assets/official/map-status.json').read_text('utf8'))
    battle=json.loads((ROOT/'assets/official/battle-rules.json').read_text('utf8'))
    food_entities=[]
    for food in rules['cooking']:
        if '1' not in food['EffectInfos']:continue
        cfg=status['effects'][str(food['EffectInfos']['1']['StatusEffectId'])]
        for entity in cfg['FightStatusIds']:
            assert str(entity) in battle['entities'],entity
            assert set(battle['entities'][str(entity)])=={'ActionComponent','FightStatusComponent','FightStatusPropComponent'},entity
            food_entities.append(entity)
    assert len(food_entities)==102
    assert data['entries']['902']['Icon']=='' and data['entries']['902']['Bg']==''
    print('Original cooking verified: 14 templates, 284 thresholds, 71 property classes, all food combat entities')


if __name__=='__main__':verify()
