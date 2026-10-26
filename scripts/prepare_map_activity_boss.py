"""Decode the original map activity boss tables with full byte consumption."""
import hashlib
import re
from pathlib import Path
from official_apk import Apk
from official_tables import Constructors, Reader, SOURCE
from prepare_map_regions import ART, REF, save


def monster_groups(raw):
    """Pinned TbMonsterGroupInfo's six shared pools, then its row indices."""
    r = Reader(raw); count = r.uint(); pools = []
    resilience = [None]
    for _ in range(r.uint()):
        resilience.append(dict(DisappearStatusIds=r.collection('int'),
            **{key:r.integer() for key in ('DisappearSkillId','SkillMaxHpPercent','SkillAtkPercent','ResetRound')}))
    pools.append(resilience)
    for _ in range(3):
        pool = [None]
        for _ in range(r.uint()): pool.append([r.integer() for _ in range(r.uint())])
        pools.append(pool)
    pool = [None]
    for _ in range(r.uint()):
        group=r.integer(); factors={}
        if r.boolean():factors={str(r.integer()):r.long() for _ in range(r.uint())}
        pool.append(dict(PropGroupId=group,PropFactors=factors))
    pools.append(pool); pool = [None]
    for _ in range(r.uint()):pool.append({str(r.integer()):r.long() for _ in range(r.uint())})
    pools.append(pool); rows = []
    for _ in range(count):
        row = dict(MonsterId=r.integer(),GroupNo=r.integer())
        for name, pool in zip(('ResilienceInfo','ActiveSkills','PassiveSkills','DeactivePassiveSkills'),pools[:4]):row[name]=pool[r.uint()]
        row['HpPliesId']=r.integer();row['PropCfg']=pools[4][r.uint()];row['ExtraProps']=pools[5][r.uint()] or {}
        rows.append(row)
    if r.pos != len(raw):raise ValueError(f'monster_group trailing bytes: {r.pos}/{len(raw)}')
    return rows


def build():
    apk = Apk(); constructors = Constructors(); tables = {}; records = {}
    mapping = {m[2]: m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    for name, cls in mapping.items():
        if not name.startswith(('ActivityDomainBoss/', 'ActivityDomainFunnyBoss/')) and name not in ('monster_group','step_award','chest'): continue
        bundle = 'config_luban_' + name.rsplit('/', 1)[0].replace('/', '_').lower() if '/' in name else 'config_luban'
        obj = apk.asset(bundle, '/' + name + '.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        tables[name] = monster_groups(raw) if name == 'monster_group' else constructors.table(raw, cls)
        records[name] = dict(apk.provenance(obj), bytes=len(raw), consumed_bytes=len(raw),
            sha256=hashlib.sha256(raw).hexdigest(), constructor=cls,
            constructor_sha256=hashlib.sha256((SOURCE / (cls + '.cs')).read_bytes()).hexdigest())
        print(name, 'rows', len(tables[name]), flush=True)
    save(ART / 'map-activity-boss.json', tables)
    save(REF / 'activity-boss-source-manifest.json', dict(manifest='854_156383', tables=records,
        decoding={'script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            'monster_pool_constructor_sha256':hashlib.sha256((SOURCE/'MonsterGroupInfo.cs').read_bytes()).hexdigest(),
            'property_formula_sha256':hashlib.sha256((SOURCE/'ActivityDomainBossUtils.cs').read_bytes()).hexdigest()},
        outputs={'map-activity-boss.json': hashlib.sha256((ART / 'map-activity-boss.json').read_bytes()).hexdigest()}))


if __name__ == '__main__': build()
