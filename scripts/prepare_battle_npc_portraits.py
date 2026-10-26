"""Restore NpcInfo.Icon textures and exact Sprite metadata for the scene timeline.

The fixed battle configuration is 854_156383. APK objects are preferred to the
same version's verified cloud cache by official_apk.Apk. Legacy MapExploreBoss
UI/Boss paths, two empty icons and unresolved configured paths are recorded
separately. Unresolved icons are never replaced by another character's portrait.
"""
from __future__ import annotations
import hashlib
import io
import json
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_unity_battle_monster_portraits import sprite_meta

DEST = ROOT/'unity/Assets/Official/Combat/NpcPortraits'
SOURCE = DEST/'Resources/Combat/battle-npc-portraits.json'
ASSET = DEST/'Resources/Combat/OfficialBattleNpcPortraitCatalog.asset'
SCRIPT = ROOT/'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleNpcPortraitCatalog.cs'
RECEIPT = ROOT/'assets/reference/battle-hud/npc-portraits-source-manifest.json'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def guid(path):
    return hashlib.md5(('zjcs-official:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def verify_unresolved(ids):
    checks = []
    if not ids:
        return checks
    for version in ('854_156383', '871_156598'):
        apk = Apk(version)
        filename = f'PackageManifest_DefaultPackage_{version}.v3.bytes'
        entry = 'assets/yoo/DefaultPackage/'+filename
        manifest = apk.archive.read(entry) if entry in apk.available else (apk.source.cloud_path/'manifests/DefaultPackage'/filename).read_bytes()
        assets = read_asset_manifest(manifest)
        wanted = {'npc_icon_'+str(i) for i in ids}
        paths = [a['path'] for a in assets if Path(a['path']).stem in wanted]
        bundles, found = [], []
        for name in apk.index:
            if 'image_ui_npc_npc_icon_' not in name:
                continue
            objects = apk.objects(name)
            for obj in objects:
                if obj.type.name in ('Sprite', 'Texture2D') and obj.read().m_Name in wanted:
                    found.append(dict(apk.provenance(obj), name=obj.read().m_Name))
            bundles.append(dict(apk.provenance(objects[0]), inspected_object_count=len(objects)))
        if paths or found:
            raise ValueError('Unresolved NPC icons were located; restore their explicit bindings: '+str(paths or found))
        checks.append(dict(manifest=version, asset_manifest_sha256=sha(manifest),
                           npcClassIds=ids, matching_asset_paths=paths, matching_objects=found, inspected_bundles=bundles))
    return checks


def build():
    apk, constructors = Apk(), Constructors()
    inputs, outputs = {}, {}

    def record(obj):
        value = dict(apk.provenance(obj), object_sha256=sha(obj.get_raw_data()))
        inputs[(value['bundle'], value['path_id'])] = value
        return obj

    table = record(apk.asset('config_luban', '/npc.g.bytes'))
    raw = table.read().m_Script.encode('utf8', 'surrogateescape')
    rows = constructors.table(raw, 'TbNpcInfo')
    rules_path = ROOT/'unity/Assets/Official/Combat/battle-rules.json'
    tables = json.loads(rules_path.read_bytes())['tables']
    required = {r['NpcClassId'] for r in tables['stage_npc']} | {r['NpcId'] for r in tables['npc_template']}
    required.discard(0)
    manifest_bytes = apk.archive.read('assets/yoo/DefaultPackage/PackageManifest_DefaultPackage_854_156383.v3.bytes')
    assets = read_asset_manifest(manifest_bytes)
    mapped = {r['path']:r for r in assets}
    sprites, bindings, deferred = {}, [], []
    for row in rows:
        icon = row['Icon']
        binding = dict(npcClassId=row['Id'], icon=icon, sprite='')
        bindings.append(binding)
        if row['NpcType'] == 2 or not icon:
            deferred.append(dict(npcClassId=row['Id'], icon=icon, npcType=row['NpcType'],
                                 reason='MapExploreBoss legacy icon path' if icon else 'Original empty Icon'))
            continue
        path = 'Assets/Image/'+icon+'.png'
        if path not in mapped:
            deferred.append(dict(npcClassId=row['Id'],icon=icon,npcType=row['NpcType'],
                                 reason='Configured image path absent from pinned asset manifest'))
            continue
        entry = mapped[path]
        texture_obj = record(apk.asset(entry['bundle'], '/'+path.rsplit('/', 1)[-1]))
        if texture_obj.type.name != 'Texture2D':
            raise ValueError('NPC Icon must reference the original Texture2D: '+path)
        texture = texture_obj.read()
        sprite_obj = next(o for o in apk.objects(entry['bundle'])
                          if o.type.name == 'Sprite' and o.read().m_Name == texture.m_Name)
        sprite = record(sprite_obj).read()
        tex = texture.m_TextureSettings
        if (sprite.m_RD.texture.path_id != texture_obj.path_id or sprite.m_SpriteAtlas.path_id
            or tuple(getattr(sprite.m_Rect, k) for k in ('x','y','width','height')) != (0,0,texture.m_Width,texture.m_Height)
            or tuple(getattr(sprite.m_Pivot,k) for k in 'xy') != (.5,.5)
            or sprite.m_PixelsToUnits != 100 or any(getattr(sprite.m_Border,k) for k in 'xyzw')
            or (tex.m_FilterMode,tex.m_Aniso,tex.m_MipBias,tex.m_WrapU,tex.m_WrapV,tex.m_WrapW) != (1,1,0,1,1,1)
            or texture.m_ColorSpace != 1 or texture.m_MipCount != 1):
            raise ValueError('Restore the additional original NPC Sprite parameters: '+path)
        target = DEST/'Textures'/(texture.m_Name+'.png')
        stream = io.BytesIO(); texture.image.save(stream,format='PNG')
        write(target,stream.getvalue())
        write(Path(str(target)+'.meta'),sprite_meta(target,guid(target),guid(target)))
        binding['sprite'] = target.relative_to(ROOT/'unity').as_posix()
        sprites[icon] = dict(path=binding['sprite'], name=sprite.m_Name, width=texture.m_Width,height=texture.m_Height,
                             ppu=sprite.m_PixelsToUnits, pivot=[sprite.m_Pivot.x,sprite.m_Pivot.y],
                             sha256=sha(target.read_bytes()),guid=guid(target))
    if required - {r['npcClassId'] for r in bindings}:
        raise ValueError('Battle stage/template references an unknown NpcInfo')
    unresolved = sorted(required - {r['npcClassId'] for r in bindings if r['sprite']})
    missing_checks = verify_unresolved(unresolved)
    source = dict(manifest=apk.version, bindings=bindings, sprites=list(sprites.values()),
                  battleNpcIds=sorted(required), unresolvedBattleNpcIds=unresolved, deferred=deferred)
    write(SOURCE,(json.dumps(source,ensure_ascii=False,separators=(',',':'))+'\n').encode())
    script_meta = Path(str(SCRIPT)+'.meta')
    if not script_meta.exists():
        write(script_meta,('fileFormatVersion: 2\nguid: '+guid(SCRIPT)+'\n').encode())
    script_guid = next(line[6:].strip() for line in script_meta.read_text().splitlines() if line.startswith('guid: '))
    yaml = ['%YAML 1.1','%TAG !u! tag:unity3d.com,2011:','--- !u!114 &11400000','MonoBehaviour:',
            '  m_ObjectHideFlags: 0','  m_CorrespondingSourceObject: {fileID: 0}','  m_PrefabInstance: {fileID: 0}',
            '  m_PrefabAsset: {fileID: 0}','  m_GameObject: {fileID: 0}','  m_Enabled: 1','  m_EditorHideFlags: 0',
            '  m_Script: {fileID: 11500000, guid: '+script_guid+', type: 3}',
            '  m_Name: OfficialBattleNpcPortraitCatalog','  m_EditorClassIdentifier:','  bindings:']
    for binding in bindings:
        yaml += ['  - npcClassId: '+str(binding['npcClassId']), '    icon: '+json.dumps(binding['icon'])]
        yaml += ['    sprite: '+('{fileID: 21300000, guid: '+guid(ROOT/'unity'/binding['sprite'])+', type: 3}' if binding['sprite'] else '{fileID: 0}')]
    write(ASSET, ('\n'.join(yaml)+'\n').encode())
    write(Path(str(ASSET)+'.meta'),('fileFormatVersion: 2\nguid: '+guid(ASSET)+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n').encode())
    for path in DEST.rglob('*'):
        if path.is_file() and path.suffix != '.meta': outputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
    code = ['client-source/GameRoundCharacterItemScene.cs','client-source/FightDynamicTexture.cs',
            'client-source/FightPlayableHandler.cs','client-source/CircleSpriteRenderer.cs',
            'common-source/Common/NpcInfo.cs','common-source/Common/TbNpcInfo.cs']
    receipt = dict(manifest=apk.version, inputs=list(inputs.values()), npcTableSha256=sha(raw),
        assetManifestSha256=sha(manifest_bytes), generator_sha256=sha(Path(__file__).read_bytes()),
        shared_meta_writer_sha256=sha((ROOT/'scripts/prepare_unity_battle_monster_portraits.py').read_bytes()),
        battleRulesSha256=sha(rules_path.read_bytes()), official_code={name:sha((ROOT/'.tools/source-packages/official-code'/name).read_bytes()) for name in code},
        runtime_sha256=sha(SCRIPT.read_bytes()), outputs=outputs, deferred=deferred,
        unresolvedBattleNpcIds=unresolved, missing_icon_checks=missing_checks,
        operation='APK-first original NpcInfo, full Texture2D PNG and exact full-rect Sprite metadata. Explicit NpcClassId bindings cover available icons. Configured missing paths, MapExploreBoss legacy and empty icons are not substituted.')
    write(RECEIPT,(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n').encode())
    print('NPC portrait bindings:',len(bindings),'converted textures:',len(sprites),'battle NPCs:',len(required),'deferred:',len(deferred))


if __name__ == '__main__':
    build()
