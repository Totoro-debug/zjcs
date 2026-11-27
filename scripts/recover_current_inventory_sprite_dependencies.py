"""Retry unresolved original inventory objects while preserving verified conversions."""
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_source import OfficialSource
from prepare_current_creation_ui_sprites import CurrentUiSprites
from prepare_current_inventory_sprites import source_records, select_sprite
from refresh_current_character_meshes import preserve


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def recover():
    source = OfficialSource()
    cache = source.prepared_path/'inventory/native-sprites'
    target = cache/'source-manifest.json'
    previous_raw = target.read_bytes()
    previous = json.loads(previous_raw)
    if previous['manifest'] != source.version or previous['apk_sha256'] != source.apk_sha256:
        raise ValueError('Recover only the selected inventory source')
    if previous['completed']:
        return
    changed = 'scripts/prepare_unity_map12_team_panel_sprites.py'
    for row in previous['inputs']+previous['generators']:
        actual = sha((ROOT/row['path']).read_bytes())
        if actual == row['sha256']:
            continue
        preserved = ROOT/'.tools/source-packages/history/converted'/(row['sha256']+'.bin')
        if row['path'] != changed or not preserved.is_file() or sha(preserved.read_bytes()) != row['sha256']:
            raise ValueError('Inspect a changed conversion input before recovery: '+row['path'])
    for row in previous['sprites']+previous['textures']:
        for suffix,key in [('', 'sha256'),('.meta','meta_sha256')]:
            path = Path(str(ROOT/row['path'])+suffix).resolve()
            if not path.is_relative_to(cache.resolve()) or sha(path.read_bytes()) != row[key]:
                raise ValueError('Preserve an edited successful conversion: '+str(path))
    records,_ = source_records()
    aliases = {row['alias']:row for row in previous['aliases']}
    failures = []
    with Apk.current() as apk:
        converter = CurrentUiSprites(apk,cache)
        converter.sprites = {row['identity']:row for row in previous['sprites']}
        converter.textures = {row['identity']:row for row in previous['textures']}
        for alias in records.keys()-aliases.keys():
            row = records[alias]
            try:
                obj = select_sprite(list(apk.objects(row['bundle'])),row)
                aliases[alias] = dict(alias=alias,previous_source=row,native=converter.sprite(obj))
            except (ValueError,FileNotFoundError,KeyError) as error:
                failures.append(dict(alias=alias,bundle=row['bundle'],error=str(error)))
    result = dict(previous)
    result.update(aliases=list(aliases.values()),sprites=list(converter.sprites.values()),
                  textures=list(converter.textures.values()),failures=failures,
                  completed=not failures and set(aliases)==set(records))
    result['partial_conversion'] = preserve(target)
    result['generators'] = [dict(row,sha256=sha((ROOT/row['path']).read_bytes())) for row in previous['generators']]
    result['generators'].append(dict(path=Path(__file__).relative_to(ROOT).as_posix(),sha256=sha(Path(__file__).read_bytes())))
    result['recovery'] = 'Preserve successful native objects from the same selected inputs; retain original physics outlines on the two previously rejected built-in UI Sprites.'
    target.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n','utf8')
    print('Recovered inventory Sprites:',len(aliases),'textures:',len(converter.textures),'failures:',failures,flush=True)
    if not result['completed']:
        raise ValueError('Resolve the remaining native inventory dependencies')


if __name__ == '__main__':
    recover()
