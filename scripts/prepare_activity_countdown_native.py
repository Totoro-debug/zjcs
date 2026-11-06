"""Bind the exact original activity preview countdown and its native dependencies."""
from pathlib import Path
import re

from official_apk import ROOT
from prepare_activity_battle_team_native import TeamBinding, build, install
from prepare_partner_card_native import guid, sha, script_id
from validation_workspace import validation_directory


class CountdownBinding(TeamBinding):
    def register_scripts(self):
        super().register_scripts()
        for name in ('CountdownText', 'CountdownView'):
            path = ROOT / 'unity/Assets/Game/Runtime/Official/ActivityBattleUI' / (name + '.cs')
            meta = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = pointer
            self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))


def main():
    binding = CountdownBinding(validation_directory())
    try:
        formatter = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldCultureTimeSpanArg.cs'
        keys = set(re.findall(r'"((?:TwoDayCountdown|DayCountdown|HourCountdown|SecondCountdown|MinutesCountdown|time_)[A-Za-z0-9_]*)"',
                              formatter.read_text('utf8')))
        if not keys: raise ValueError('Original countdown formatter language closure is empty')
        binding.inputs[formatter.relative_to(ROOT).as_posix()] = sha(formatter.read_bytes())
        result = build(binding, consumer_type='ActivityDomainBossPreviewContent', field='mCountdown',
                       prefab_name='ActivityDomainBossCountdown', group='Countdown', extra_text_keys=keys)
        install(binding, *result, group='Countdown', receipt_name='activity-countdown-native-source-manifest.json',
                extra_helpers=('prepare_activity_countdown_ui.py', 'prepare_activity_countdown_native.py'))
    finally:
        binding.recovery.apk.archive.close()


if __name__ == '__main__': main()
