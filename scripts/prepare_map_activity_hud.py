"""Resolve native music HUD prefabs and their serialized bindings."""
import hashlib
import json
from UnityPy.classes.PPtr import PPtr
from official_manifest import read_asset_manifest
from official_tables import Constructors, SOURCE
from prepare_map_regions import Regions, ART, REF, ROOT, save


def bindings(e, root):
    paths = {}
    components = []

    def walk(obj, parent='', suffix=''):
        go = obj.read()
        path = parent + '/' + go.m_Name + suffix
        paths[(obj.assets_file.name, obj.path_id)] = path
        cs = [v.component.deref() for v in go.m_Component]
        for c in cs:
            paths[(c.assets_file.name, c.path_id)] = path
            if c.type.name == 'MonoBehaviour':components.append((path, c))
        t = next(c.read() for c in cs if c.type.name == 'RectTransform')
        seen = {}
        for child in t.m_Children:
            child = child.read().m_GameObject.deref()
            name = child.read().m_Name
            index = seen.get(name, 0)
            seen[name] = index + 1
            walk(child, path, ' (' + str(index) + ')' if index else '')

    walk(root)
    wanted = {'ActivityMusicLiveTopBoard', 'ActivityMusicLiveScoreBoard',
              'MapActivityMusicFestivalStageLeaveBtn', 'ActivityGroupLimitedRewardItem',
              'ActivityMusicFestivalSignUpPanel', 'ActivityMusicLightBangItem'}
    result = {}
    for path, obj in components:
        cls = e.mono(obj)[0]
        if cls not in wanted:continue
        e.input(obj)
        data = e.schemas.read(obj)
        refs = {str(r['rid']):r for r in data.get('references', {}).get('RefIds', []) if 'data' in r}

        def resolve(value, depth=0):
            if depth > 30:raise ValueError('Cyclic native HUD binding')
            if isinstance(value, list):return [resolve(v, depth + 1) for v in value]
            if not isinstance(value, dict):return value
            if set(value) == {'rid'}:
                ref = refs.get(str(value['rid']))
                return dict(type=ref['type']['class'], data=resolve(ref['data'], depth + 1)) if ref else {}
            if set(value) == {'m_FileID', 'm_PathID'} and value['m_PathID']:
                target = PPtr(**value, assetsfile=obj.assets_file).deref()
                return paths.get((target.assets_file.name, target.path_id), value)
            return {k:resolve(v, depth + 1) for k,v in value.items() if k != 'references'}

        result.setdefault(cls, []).append(dict(path=path, data=resolve(data)))
    return result


def build():
    e = Regions()
    assets = [r for name in e.apk.archive.namelist() if name.startswith('assets/yoo/')
              and name.endswith('_854_156383.v3.bytes') for r in read_asset_manifest(e.apk.archive.read(name))]
    by_guid = {r['guid']:r for r in assets if r['guid']}
    tables = {}
    records = {}
    constructors = Constructors()
    for name, cls in [('activity_live', 'TbActivityLiveInfo'),
                      ('activity_award_limit', 'TbActivityAwardLimitInfo'),
                      ('append_appearance', 'TbAppendAppearanceInfo'),
                      ('spine_skin', 'TbSpineSkinInfo')]:
        obj = e.apk.asset('config_luban', '/' + name + '.g.bytes')
        e.input(obj)
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        tables[name] = constructors.table(raw, cls)
        records[name] = dict(e.apk.provenance(obj), bytes=len(raw), consumed_bytes=len(raw),
                            sha256=hashlib.sha256(raw).hexdigest(), constructor=cls,
                            constructor_sha256=hashlib.sha256((SOURCE / (cls + '.cs')).read_bytes()).hexdigest())
    cfg = e.apk.asset('scriptableconfig_modules_activitymusic', '/ActivityMusicLiveConfig.asset')
    e.input(cfg)
    settings = e.schemas.read(cfg)['ActivitySettings']['mSerializeBuffer']
    panels = json.loads((ART / 'map-ui.json').read_text(encoding='utf8'))
    receipt = json.loads((REF / 'ui-source-manifest.json').read_text(encoding='utf8'))
    music = {}
    panel_bindings = {}
    supporter = []
    support_bundle = 'defaultpackage_assets_prefabs_ecentity_mapactivitymusicfestival_activitymusicfestivacommon.bundle'
    for obj in e.apk.objects(support_bundle):
        if obj.type.name != 'MonoBehaviour' or e.mono(obj)[0] != 'ECViewDriver':continue
        data = e.schemas.read(obj)
        for ref in data.get('references', {}).get('RefIds', []):
            if ref.get('type', {}).get('class') != 'EcActivityMusicFestivalSupporterControllerView':continue
            e.input(obj)
            for action in ref['data']['mSupportActionPlayableDatas']:
                clip = PPtr(**action['AnimClip'], assetsfile=obj.assets_file).deref()
                e.input(clip)
                supporter.append(dict(weight=action['Weight'], animation=clip.read().m_Name,
                    state=action['StateName'], duration=[action['MinDurationS'], action['MaxDurationS']]))
    if len(supporter) != 4:raise ValueError('Unexpected native supporter actions: ' + str(supporter))
    for entry in settings:
        live = next(r for r in tables['activity_live'] if r['Id'] == entry['Key'])
        row = by_guid[entry['Value']['mTopBoardPrefab']['mAssetGuid']]
        key = row['path'].rsplit('/', 1)[-1].removesuffix('.prefab')
        root = e.apk.asset(row['bundle'], '/' + row['path'].rsplit('/', 1)[-1])
        panel_bindings[key] = bindings(e, root)
        music[str(live['MapClassId'])] = dict(activity=entry['Key'], panel=key,
            leave_navigate=entry['Value']['mLeaveMapNavigateKey'], live=live,
            limits=[r for r in tables['activity_award_limit'] if r['ActivityType'] == 25 and r['ActivityId'] == entry['Key']])
    for key in ['ActivityMusicLiveSignUpPanel', 'LuoTianyiLiveSignUpPanel']:
        source = receipt['prefabs'][key]
        row = next(r for r in assets if r['path'].lower() == source['path'].lower())
        root = e.apk.asset(row['bundle'], '/' + row['path'].rsplit('/', 1)[-1])
        panel_bindings[key] = bindings(e, root)
    panels.pop('ChatActionItem', None)
    receipt['prefabs'].pop('ChatActionItem', None)
    ids = {item for cfg in music.values() for values in cfg['live']['LightBangConfig'].values() for item in values}
    appearances = {str(row['ClassId']):row for row in tables['append_appearance'] if row['ClassId'] in ids}
    skins = {str(row['Id']):row for row in tables['spine_skin']}
    output = dict(music=music, bindings=panel_bindings, supporter=supporter, appearances=appearances,
                  skins={str(skin):skins[str(skin)] for row in appearances.values() for skin in row['SkinDict'].values()})
    save(ART / 'map-activity-hud.json', output)
    save(ART / 'map-ui.json', panels)
    receipt['output_sha256'] = hashlib.sha256((ART / 'map-ui.json').read_bytes()).hexdigest()
    save(REF / 'ui-source-manifest.json', receipt)
    save(REF / 'activity-hud-source-manifest.json', dict(manifest='854_156383', tables=records,
        inputs=e.source_records, resources={}, schemas=e.schemas.records,
        script_sha256=hashlib.sha256((ROOT / 'scripts/prepare_map_activity_hud.py').read_bytes()).hexdigest(),
        outputs={'map-activity-hud.json':hashlib.sha256((ART / 'map-activity-hud.json').read_bytes()).hexdigest()},
        operation='Original activity-to-map and GUID-to-prefab links, serialized HUD references and boolean/event switches.'))
    print('Native music HUD maps', len(music), flush=True)


if __name__ == '__main__':build()
