"""Bind current official monster head Sprites for every Map12 battle roster."""
import io
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_unity_battle_monster_portraits import BUNDLES, asset_yaml, sprite_meta, md5
from prepare_unity_map12_sources import encode, meta, sha, source_path, write


def build():
    apk = Apk.current()
    base = ROOT / 'unity/Assets/Official/World/Map/12'
    battle = base / 'Resources/Map12/battle-runtime.json'
    table = base / 'Source/tables.json'
    rosters = json.loads(battle.read_bytes())['monsterRosters']
    monsters = {v['Id']: v for v in json.loads(table.read_bytes())['monster']}
    wanted = {monsters[u['MonsterId']]['IconId'] for r in rosters for w in r['Waves'] for u in w['Units']}
    wanted.discard(0)
    found, sources, outputs = {}, [], []
    for bundle in BUNDLES:
        for path, pointer in apk.container(bundle):
            if pointer.type.name != 'Sprite':
                continue
            match = re.search(r'/G_(\d+)\.png$', path)
            if match is None or int(match[1]) not in wanted:
                continue
            identity = int(match[1])
            if identity in found:
                raise ValueError('Ambiguous official monster portrait: ' + str(identity))
            obj = pointer.deref()
            sprite = obj.read()
            texture = sprite.m_RD.texture.read()
            if (sprite.m_Pivot.x, sprite.m_Pivot.y, sprite.m_PixelsToUnits) != (.5, .5, 100):
                raise ValueError('Map12 portrait needs a different native Sprite binding')
            if any(getattr(sprite.m_Border, v) != 0 for v in ('x', 'y', 'z', 'w')) or texture.m_MipCount != 1:
                raise ValueError('Map12 portrait border/mips require a different converter')
            target = base / f'Resources/Map12/MonsterPortraits/G_{identity}.png'
            stream = io.BytesIO()
            sprite.image.save(stream, format='PNG')
            write(target, stream.getvalue())
            guid = md5('zjcs-official:' + source_path(target))
            settings = texture.m_TextureSettings
            raw_meta = sprite_meta(target, guid, md5('zjcs-sprite:' + source_path(target))).decode('utf8')
            raw_meta = raw_meta.replace('  sRGBTexture: 1',
                                        '  sRGBTexture: ' + ('0' if texture.m_ColorSpace == 1 else '1'))
            for key, value in [('filterMode', settings.m_FilterMode), ('aniso', settings.m_Aniso),
                               ('mipBias', settings.m_MipBias), ('wrapU', settings.m_WrapU),
                               ('wrapV', settings.m_WrapV), ('wrapW', settings.m_WrapW)]:
                raw_meta = re.sub(r'(?m)^(    ' + key + r': ).*$', lambda m: m[1] + str(value), raw_meta)
            # Preserve an existing Unity GUID while regenerating source settings.
            target_meta = Path(str(target) + '.meta')
            if target_meta.exists():
                existing = re.search(r'(?m)^guid: (\w+)', target_meta.read_text('utf8'))[1]
                if existing != guid:
                    raise ValueError('Portrait GUID changed: ' + source_path(target))
            write(target_meta, raw_meta.encode('utf8'))
            found[identity] = dict(iconId=identity, guid=guid)
            sources.append(dict(iconId=identity, source=apk.provenance(obj),
                                spriteObjectSha256=sha(obj.get_raw_data()),
                                textureSource=apk.provenance(sprite.m_RD.texture.deref())))
            outputs.extend([target, target_meta])
        if wanted == set(found):
            break
    if wanted != set(found):
        raise ValueError('Missing original Map12 portrait ids: ' + str(sorted(wanted - set(found))))
    script = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMonsterPortraitCatalog.cs'
    script_guid = re.search(r'(?m)^guid: (\w+)', Path(str(script) + '.meta').read_text('utf8'))[1]
    catalog = base / 'Resources/Map12/monster-portraits.asset'
    write(catalog, asset_yaml(script_guid, [found[v] for v in sorted(found)]))
    meta(catalog)
    outputs.extend([catalog, Path(str(catalog) + '.meta')])
    record = lambda p: dict(path=source_path(p), sha256=sha(p.read_bytes()), bytes=p.stat().st_size)
    receipt = dict(manifest=apk.version, operation='APK-first current source Sprite extraction for all Map12 monster IconIds; explicit per-map native catalog.',
                   inputs=[record(p) for p in (Path(__file__).resolve(), battle, table, script,
                           ROOT / 'scripts/prepare_unity_battle_monster_portraits.py')],
                   sprites=sources, outputs=[record(p) for p in outputs])
    write(ROOT / 'assets/reference/official-map/map12-battle-portraits-source-manifest.json', encode(receipt))
    print('Map12 current official monster portraits:', len(found))


if __name__ == '__main__':
    build()
