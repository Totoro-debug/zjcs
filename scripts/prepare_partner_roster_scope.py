"""Restore the original exclusion-based roster scope and its complete card images."""
import argparse
import json
from pathlib import Path

from official_apk import ROOT
from prepare_partner_card_native import PartnerCardBinding, install_staged, sha
from prepare_partner_gift_configuration import method
from validation_workspace import validation_directory
from npc_travel_language import language


class RosterScopeBinding(PartnerCardBinding):
    def card_language(self, keys):
        texts, sources = language(keys)
        missing = keys - texts.keys()
        if missing != {'ui_npc_name_10609'}:
            raise ValueError('Review original missing roster language keys: ' + repr(missing))
        # Exact selected LocalizationBinaryTextService.GetText fallback, verified below.
        return dict(texts, **{key: key for key in missing}), sources


def verify_staged_scope(staged, npcs, version):
    dynamic = staged['dynamic_images']
    expected = {(row['Id'], field, row[field]) for row in npcs for field in ('FigureImage', 'SmallBgImage')}
    actual = [(consumer['npc_id'], consumer['field'], sprite['address'])
              for sprite in dynamic['sprites'] for consumer in sprite['consumers']]
    if dynamic['manifest'] != version or set(actual) != expected or len(actual) != len(expected):
        raise ValueError('Staged card image consumers differ from original roster scope')
    keys = {row['NameKey'] for row in npcs} | {'ui_new_mark_text'}
    texts = dynamic['texts']
    if {row['key'] for row in texts} != keys or len(texts) != len(keys):
        raise ValueError('Staged card text scope differs')
    if next(row['text'] for row in texts if row['key'] == 'ui_npc_name_10609') != 'ui_npc_name_10609':
        raise ValueError('Staged card must preserve original missing-key display')


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install', action='store_true')
    parser.add_argument('--install-staged', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    inputs, methods = {}, {}

    def selected(relative, signature=None):
        source = ROOT / '.tools/source-packages/official-code' / relative
        raw = source.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected roster source differs: ' + relative)
        inputs[source.relative_to(ROOT).as_posix()] = sha(raw)
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        if signature:
            text = method(text, signature)
            methods[relative + ':' + signature] = sha(text.encode())
        return text

    types = selected('common-source/Common/NpcType.cs')
    names = [line.strip().rstrip(',') for line in types.split('{', 1)[1].split('}', 1)[0].splitlines() if line.strip()]
    ignored = names.index('MapExploreBoss')
    if ignored != 2:
        raise ValueError('Review original exploration-boss NPC type')
    get_npcs = selected('common-source/Common/NpcInfoParser.cs', 'public IEnumerable<NpcInfo> GetNpcs(NpcType ignoreType)')
    if 'data.NpcType != ignoreType' not in get_npcs:
        raise ValueError('Review original NPC exclusion semantics')
    panel = selected('client-source/NpcPanelNpcList.cs', 'private void InitNpcList()')
    if 'GetNpcs(NpcType.MapExploreBoss)' not in panel or 'npc.IsStarGodNpc' not in panel:
        raise ValueError('Review original roster candidate consumption')
    selected('common-source/Common/HasManualMeetNpcCondition.cs', 'private bool CheckMeet(DataHub hub)')
    selected('common-source/NpcHelper.cs', 'public static NpcMeetStatus GetNpcMeetStatus(Player player, int npcClassId)')
    fallback = selected('framework-firstpass-source/Framework/LocalizationTextService.cs',
                        'public static string GetMissingKeyText(string key, bool isShowMissingKeyText)')
    if fallback.strip().split('{', 1)[1].rsplit('}', 1)[0].strip() != 'return key;':
        raise ValueError('Review original missing-key display')
    reader = selected('framework-firstpass-source/Framework/LocalizationBinaryTextService.cs',
                      'public bool TryGetOriginalText(string key, LanguageType language, out string result)')
    consumer = selected('framework-firstpass-source/Framework/LocalizationBinaryTextService.cs',
                        'public string GetText(string key, LanguageType language, params object[] paras)')
    if 'result = key;' not in reader or 'LocalizationTextService.GetMissingKeyText(result,' not in consumer:
        raise ValueError('Review original binary-language missing-key consumption')
    for relative in ('launch-source/GameLanguageBinaryTextDataProvider.cs', 'launch-source/LaunchLanguageBinaryTextDataProvider.cs'):
        selected(relative)
    roles = selected('common-source/Common/NpcRoleType.cs')
    if 'StarGod = 2' not in roles:
        raise ValueError('Review original star-god role')
    rules_path = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    rules = json.loads(rules_path.read_bytes())
    if rules['manifest'] != version:
        raise ValueError('Selected roster configuration differs')
    inputs[rules_path.relative_to(ROOT).as_posix()] = sha(rules_path.read_bytes())
    npcs = [row for row in rules['tables']['npc'] if row['NpcType'] != ignored and row['NpcRoleType'] != 2
            and not 11015 <= row['Id'] <= 11022]
    if len({row['Id'] for row in npcs}) != len(npcs):
        raise ValueError('Duplicate original roster identity')
    prior_path = ROOT / 'assets/reference/partner-card-binding-source-manifest.json'
    prior_raw = prior_path.read_bytes()
    prior = json.loads(prior_raw)
    if prior['manifest'] != version:
        raise ValueError('Installed card receipt differs from selected version')
    for relative, digest in prior['outputs'].items():
        path = ROOT / prior['installed_root'] / relative
        if sha(path.read_bytes()) != digest:
            raise ValueError('Preserve an installed card asset change: ' + relative)
    binding = RosterScopeBinding(work)
    if not args.install_staged:
        binding.build([row['Id'] for row in npcs])
    staged = json.loads((work / prior_path.name).read_bytes())
    verify_staged_scope(staged, npcs, version)
    if args.install_staged:
        generator = ROOT / 'scripts/prepare_partner_card_native.py'
        if staged['generator_sha256'] != sha(generator.read_bytes()):
            raise ValueError('Staged card generator changed')
        for relative, digest in staged['inputs'].items():
            if sha((ROOT / relative).read_bytes()) != digest:
                raise ValueError('Staged card input changed: ' + relative)
        for relative, digest in staged['outputs'].items():
            if sha((work / 'card-assets' / relative).read_bytes()) != digest:
                raise ValueError('Staged card output changed: ' + relative)
    if set(prior['outputs']) - staged['outputs'].keys():
        raise ValueError('Expanded roster must preserve every previously installed card payload')
    changed = {relative: dict(previous_sha256=digest, sha256=staged['outputs'][relative])
               for relative, digest in prior['outputs'].items() if staged['outputs'][relative] != digest}
    empty_key_repair = None
    if 'NpcPanelNpcItem.prefab' in changed:
        identity = 'cab-2b0348e1ff451d425e25e4ffbf1b133f:-4534563961035688561'
        original = binding.original(identity)
        fields = binding.recovery.fields(original)
        if fields['myKey'] != '':
            raise ValueError('Original card mark key is not an empty string')
        before = (ROOT / prior['installed_root'] / 'NpcPanelNpcItem.prefab').read_bytes()
        after = (work / 'card-assets/NpcPanelNpcItem.prefab').read_bytes()
        block = b'--- !u!114 &114354824845356902\n'
        head, tail = before.split(block)
        component, rest = tail.split(b'--- !u!', 1)
        if component.count(b'  myKey: null\n') != 1 or head + block + component.replace(b'  myKey: null\n', b"  myKey: ''\n") + b'--- !u!' + rest != after:
            raise ValueError('Review changes outside the original empty card mark key')
        empty_key_repair = dict(original=identity, original_sha256=sha(original.get_raw_data()),
                                native_file_id=114354824845356902, field='myKey', restored='')
    if set(changed) - {'partner-card-images.json', 'NpcPanelNpcItem.prefab'}:
        raise ValueError('Review unrelated card payload changes: ' + repr(sorted(changed)))
    for relative in ('unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.Partners.cs',
                     'scripts/fixtures/partner_roster_ui_validation.cs', 'scripts/prepare_partner_card_native.py',
                     'scripts/fixtures/partner_roster_native_validation.cs', 'scripts/fixtures/partner_roster_transaction_validation.cs',
                     'scripts/prepare_partner_gift_configuration.py'):
        inputs[relative] = sha((ROOT / relative).read_bytes())
    proof = dict(manifest=version, generator_sha256=sha(Path(__file__).read_bytes()), inputs=inputs, methods=methods,
        ignored_npc_type=ignored, excluded_role_type=2, excluded_collaboration_ids=list(range(11015, 11023)),
        npc_ids=[row['Id'] for row in npcs], additional_npc_ids=[row['Id'] for row in npcs if row['NpcType'] != 4],
        prior_card_receipt_sha256=sha(prior_raw), superseded_outputs=changed,
        original_empty_key_repair=empty_key_repair,
        missing_language_keys={'ui_npc_name_10609': dict(npc_id=10611, displayed_text='ui_npc_name_10609',
            behavior='Original binary language reader returns the key; GetMissingKeyText returns it unchanged.')},
        new_outputs={key: value for key, value in staged['outputs'].items() if key not in prior['outputs']},
        adaptations=['Original GetNpcs excludes its argument; retain traders and profession tutors under original meet/show conditions.',
            'Preserve the confirmed collaboration closure and original separate star-god page.',
            'Reuse the original card/root and installed dependencies; expand its explicit dynamic image/name catalog and restore the verified original empty card mark localization key.'])
    if args.install or args.install_staged:
        if prior_path.read_bytes() != prior_raw:
            raise ValueError('Installed card receipt changed during staging')
        install_staged(work)
        proof['card_receipt_sha256'] = sha(prior_path.read_bytes())
        proof['installed_outputs'] = {str(Path(prior['installed_root']) / key).replace('\\', '/'): value
                                      for key, value in staged['outputs'].items()}
        proof_path = ROOT / 'assets/reference/partner-roster-scope-source-manifest.json'
    else:
        proof_path = work / 'partner-roster-scope-source-manifest.json'
    proof_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Original roster candidates:', len(npcs), '; added traders/tutors:', len(proof['additional_npc_ids']))


if __name__ == '__main__':
    main()
