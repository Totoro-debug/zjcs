"""Pinned CharacterSkinInfo.BuildTypeDic and stable local appearance identifiers.

The option order and random weights come from the original tables. Existing
save identifiers are retained, including the old non-free facePaint_G_010.
"""
import hashlib
import json
from pathlib import Path
from official_tables import Constructors
from official_apk import ROOT

FIELDS = {'hairstyle_id': 'Hair', 'eyebrow_id': 'Brow', 'eye_id': 'Eye',
          'mouth_id': 'Mouth', 'makeup_id': 'FacePaint', 'feature_id': 'Ethnicity'}
TABLES = {'character_skin': 'TbCharacterSkinInfo',
          'character_skin_id_group': 'TbCharacterSkinIdGroupInfo', 'spine_skin': 'TbSpineSkinInfo',
          'character_skin_dye': 'TbCharacterSkinDyeInfo'}


def stable_id(name, field, suffix):
    if name == 'emptySkin': return 'none'
    if field == 'feature_id':
        return {'ethnicity_G_01': 'none', 'ethnicity_G_02': 'cat', 'ethnicity_G_03': 'rabbit',
                'ethnicity_G_04': 'horns', 'ethnicity_G_19': 'ram',
                'ethnicity_G_10': 'elf'}.get(name, name)
    if field == 'makeup_id':
        return {'facePaint_G_002': 'blush', 'facePaint_G_003': 'freckles',
                'facePaint_G_010': 'star'}.get(name, name)
    return name.replace('hair_'+suffix+'_', 'hair_'+suffix.lower()+'_').replace('_'+suffix+'_', '_')


def catalog():
    parser = Constructors()
    tables = {n: parser.table((ROOT/'.tools/source-packages/official-data'/(n+'.g.bytes')).read_bytes(), c)
              for n, c in TABLES.items()}
    skins = {r['Id']: r for r in tables['spine_skin']}
    groups = {r['GroupId']: [i for span in (r['NestedIdList'] or []) for i in range(span[0], span[-1]+1)]
              for r in tables['character_skin_id_group']}
    result = {'bodies': {}, 'parts': {}, 'skins': {str(k): v for k, v in skins.items()},
              'dyes': tables['character_skin_dye']}
    for sex, body, suffix in [(0, 'male', 'M'), (1, 'female', 'F')]:
        row = next(r for r in tables['character_skin'] if r['Gender'] == sex and r['ProfessionLimit'] == 0)
        parts = {body+'_body': 'body_0_'+suffix+'_01', body+'_face': 'faceShape_'+suffix+'_01'}
        options = {}
        for field, column in FIELDS.items():
            options[field] = []
            for sid in groups.get(row[column], []):
                skin = skins.get(sid)
                if skin is None or skin['SkinUseType'] != 0: continue
                key = stable_id(skin['SkinName'], field, suffix)
                part = ('makeup_' if field == 'makeup_id' else 'feature_' if field == 'feature_id' else '')+key
                options[field].append(dict(id=key, skin_id=sid, part=part, weight=skin['RandomWeight'],
                                           name=skin['DisplayName'], bodies=[body]))
                if skin['SkinName'] != 'emptySkin': parts[part] = skin['SkinName']
        # Old save compatibility, deliberately absent from the free selector.
        parts['makeup_star'] = 'facePaint_G_010'
        result['bodies'][body] = options
        result['parts'][body] = parts
    return result


def write_catalog():
    path = ROOT/'assets/characters/appearance/catalog.json'
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(catalog(), ensure_ascii=False, separators=(',', ':'))+'\n', encoding='utf8')
    return path


if __name__ == '__main__':
    write_catalog()
