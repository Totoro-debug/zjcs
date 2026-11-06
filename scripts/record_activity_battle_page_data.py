"""Record original reward-preview inputs and the local saved-page projection."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_partner_input_dialog import metadata


def main():
    sources, outputs = {}, {}
    common = ROOT / '.tools/source-packages/current/884_156692/official-code/common-source/Common'
    for name in ('RewardRuleInfoParser.cs', 'ItemDropInfo.cs'):
        path = common / name
        sources[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    config = ROOT / 'unity/Assets/Game/Resources/DailyChallenges/daily-challenges.json'
    data = json.loads(config.read_text('utf8'))
    assert data['manifest'] == '871_156598'
    for row in data['reward_rule']:
        if any(row.get(key) for key in ('Condition', 'LevelCountId', 'SubRankCountId')):
            raise ValueError('Selected reward rows need additional original parser inputs: ' + str(row['Id']))
    sources[config.relative_to(ROOT).as_posix()] = hashlib.sha256(config.read_bytes()).hexdigest()
    for name in ('Runtime/Offline/Progression/OfficialDailyChallengeCatalog.cs',
                 'Runtime/Offline/Progression/OfficialDailyChallengeCatalog.RewardPreview.cs',
                 'Runtime/Offline/Progression/OfflineDailyChallengeService.DomainBossPage.cs',
                 'Editor/ActivityDomainBossPageDataAcceptance.cs'):
        path = ROOT / 'unity/Assets/Game' / name
        for output in (path, metadata(path)):
            outputs[output.relative_to(ROOT).as_posix()] = hashlib.sha256(output.read_bytes()).hexdigest()
    receipt = dict(rule_manifest=data['manifest'], client_source_manifest='884_156692',
        sources=sources, outputs=outputs,
        generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        adaptation='Original fixed/probability item preview without rolling rewards. Selected rule rows have no '
            'conditions or level/subrank multipliers. The local page projection uses existing participation, '
            'period/day refresh and monotonic clock rules; it returns detached tier claims and actual counters. '
            'Full activity page production binding and rendered interaction validation remain pending.')
    (ROOT / 'assets/reference/daily-challenges/activity-page-data-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__': main()
