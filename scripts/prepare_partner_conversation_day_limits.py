"""Port original Player daily conversation checks, counts and reset arithmetic."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_partner_gift_configuration import method, without_logging


def sha(raw): return hashlib.sha256(raw).hexdigest()


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review original day limits for selected version')
    inputs, methods = {}, {}

    def selected(name, signature, diagnostics=0):
        relative = 'common-source/Common/' + name + '.cs'
        path = ROOT / '.tools/source-packages/official-code' / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected original day-limit source differs: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        body = method(raw.decode('utf-8-sig').replace('\r\n', '\n'), signature)
        methods[name + '.' + signature] = sha(body.encode())
        return without_logging(body, diagnostics) if diagnostics else body

    player = '\n\n'.join(selected('Player', signature, 1) for signature in (
        'public bool CheckNpcDialogueHasDayLimit(long npcId, int dialogueId)',
        'public void RecordNpcDialogueDayLimit(long npcId, int dialogueId)'))
    player = player.replace('NpcDialogueInfo npcDialogueInfo', 'PartnerConversationNode npcDialogueInfo')
    player = player.replace('Parsers.Instance.NpcDialogueInfoParser.Get', 'configuration.Get')
    player = player.replace('NpcDialogueInfoParser.sDialogueStartNodeId', '1')
    player = player.replace('NpcDialogueDayLimitRecordDict', 'records')
    player = player.replace('new NpcDialogueDayLimitRecordData()', 'new PartnerConversationDayLimitRecord()')
    player = player.replace('TimeManager.Instance.GetDateTime()', 'now()')
    player = player.replace('TimeUtils.GetDailyResetTimeFromDateWithOffset', 'GetDailyResetTimeFromDateWithOffset')
    player = player.replace('Parsers.GameSettings.NpcDialogueDayLimit', 'limit')
    relative = 'common-source/Common/NpcDialogueDayLimitRecordData.cs'
    record_source = ROOT / '.tools/source-packages/official-code' / relative
    if record_source.read_bytes() != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
        raise ValueError('Selected original day-limit record differs')
    inputs[record_source.relative_to(ROOT).as_posix()] = sha(record_source.read_bytes())
    times = '\n\n'.join(selected('TimeUtils', signature) for signature in (
        'public static (int hour, int minute, int second) GetDailyResetTimeOfDay()',
        'public static DateTime GetDailyResetTimeFromDate(DateTime dateTime)',
        'public static DateTime GetDailyResetTimeFromDateWithOffset(DateTime dateTime, int days)'))
    times = times.replace('public static ', 'public ')
    times = times.replace('Parsers.Instance.GameSettingsParser.DailyResetTime', 'resetTime')
    times = times.replace('int timeZone = ServerInfoManager.Instance.TimeZone;', '')
    template = ROOT / 'scripts/templates/partner_dialogue/PartnerConversationDayLimitRules.cs'
    inputs[template.relative_to(ROOT).as_posix()] = sha(template.read_bytes())
    text = template.read_text('utf8').replace('/*PLAYER_METHODS*/', player).replace('/*TIME_METHODS*/', times)
    if any(t in text for t in ('Parsers.', 'TimeManager.', 'ServerInfoManager.', 'GLogger', '/*')):
        raise ValueError('Unadapted day-limit dependency')
    raw = ('\n'.join(l.rstrip().expandtabs(4) for l in text.splitlines()) + '\n').encode()
    destination = ROOT / 'unity/Assets/Game/Runtime/Official/Partners/PartnerConversationDayLimitRules.cs'
    meta = ('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs-partner-conversation-day-limits').hexdigest()
        + '\nMonoImporter:\n  externalObjects: {}\n').encode()
    outputs = {destination.relative_to(ROOT).as_posix(): raw, destination.relative_to(ROOT).as_posix() + '.meta': meta}
    receipt_path = ROOT / 'assets/reference/partner-conversation-day-limits-source-manifest.json'
    previous = json.loads(receipt_path.read_bytes()) if receipt_path.exists() else {}
    for path, content in outputs.items():
        target = ROOT / path
        if target.exists() and target.read_bytes() != content and sha(target.read_bytes()) != previous.get('installed_output_sha256', {}).get(path):
            raise ValueError('Preserve changed day-limit output: ' + path)
    for path, content in outputs.items(): (ROOT / path).write_bytes(content)
    local_outputs = ['unity/Assets/Game/Runtime/Offline/Progression/OfflineNpcConversationDayLimit.cs' + suffix
        for suffix in ('', '.meta')]
    local_outputs += ['unity/Assets/Game/Runtime/Offline/Progression/OfflineNpcConversationProgress.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs']
    validations = ['scripts/prepare_partner_conversation_day_limit_validation.py',
        'scripts/fixtures/partner_conversation_day_limit_validation.cs',
        'scripts/fixtures/partner_gift_transaction_validation.cs',
        'scripts/fixtures/partner_roster_transaction_validation.cs']
    receipt = dict(manifest=version, input_sha256=inputs, original_method_sha256=methods,
        generator_sha256=sha(Path(__file__).read_bytes()),
        installed_output_sha256={path: sha(content) for path, content in outputs.items()},
        local_output_sha256={path: sha((ROOT / path).read_bytes()) for path in local_outputs},
        validation_source_sha256={path: sha((ROOT / path).read_bytes()) for path in validations},
        adaptations=['Original check/record branches, clock call order and increment/reset sequence.',
            'Original reset-hour timezone modulo, Unix-epoch clamp and next-day offset.',
            'Explicit original configuration, record collection, UTC clock, limit and reset settings; diagnostic logs omitted.'])
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared two original Player day-limit methods and three TimeUtils methods', flush=True)


if __name__ == '__main__': main()
