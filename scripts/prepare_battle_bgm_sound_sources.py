"""Resolve original battle music banks and their complete packaged media."""
import json
import os
from pathlib import Path

from map14_audio_source import AudioSource
from prepare_battle_native_sound_sources import identity, values
from prepare_battle_native_sound_dependencies import media_receipts

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'assets/reference/battle-animation/bgm-sound-dependencies-source-manifest.json'


def main():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    assert work.parent == ROOT / 'output/validation' and (work / '.zjcs-validation.json').is_file()
    with AudioSource(expected_version='884_156692', work_directory=work) as audio:
        packages = {name for name in audio.inventory if '_battle_bgm_' in name and name.endswith('_pck.rawfile')}
        assert packages
        for name in sorted(packages):
            audio.objects(name)
        events = []
        for name in ('BattleBgm_Play', 'BattleBgm_Stop', 'BattleBgm_Pause', 'BattleBgm_Resume'):
            identifier, banks = audio.load_event(name)
            assert values(audio.by_id[identifier][1], 'eHircType') == [4]
            events.append(dict(name=name, id=identifier, banks=banks))
        media_ids = {value for _, fields in audio.by_id.values() for value in values(fields, 'sourceID') if value}
        assert media_ids
        # Streaming tracks can share their complete WEM with another package.
        # The source reader checks the APK first and retains every matching receipt.
        for identifier in sorted(media_ids):
            _, sources = audio.media(identifier)
            packages.update(source['package'] for source in sources)
        media = media_receipts(audio, media_ids)
        receipts = audio.receipts()
        selected = {name: receipts[name] for name in sorted(packages)}
        mapping = 'wwisebnk_assets_wwisedata_android_eventbankmapping_bytes.rawfile'
        selected[mapping] = receipts[mapping]
        proof = dict(manifest=audio.selected.version, packages=selected, events=events, media=media,
            bank_names={bank['name'].lower(): bank['name'] for bank in audio.mapping['banks']},
            objects=[dict(id=key, type=values(fields, 'eHircType')[0],
                          sources=[{k:v for k,v in source.items() if k != 'base64'}
                                   for source in audio.object_payloads[key]])
                     for key, (_, fields) in sorted(audio.by_id.items())],
            tools=[identity(Path(__file__)), identity(ROOT / 'scripts/map14_audio_source.py')],
            scope='Complete original Battle_BGM switch and track banks, ordered native music objects and full source media; no PCM replacements.')
        OUTPUT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
        print(json.dumps(dict(packages=len(packages), media=len(media), objects=len(proof['objects']))))


if __name__ == '__main__':
    main()
