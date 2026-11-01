"""Migrate the original reward-preview comparer used by NpcFriendshipDialog."""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_partner_gift_configuration import method, normalize, sha, without_logging
from prepare_partner_ui_helpers import meta


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692':
        raise ValueError('Review reward sorting after changing the selected version')
    inputs, methods = {}, {}

    def source(name):
        path = ROOT / '.tools/source-packages/official-code/common-source' / name
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code/common-source' / name).read_bytes():
            raise ValueError('Selected reward source differs: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def selected(name, signatures):
        text = source(name)
        bodies = [method(text, signature) for signature in signatures]
        methods.setdefault(name, {}).update({signature: sha(body.encode()) for signature, body in zip(signatures, bodies)})
        return '\n\n'.join(bodies)

    base = selected('ItemComparerBase.cs', [
        'public virtual int RewardItemComparer(ItemInfo x, ItemInfo y)',
        'public static int ComparerResult(long x, long y, ComparerOrderType type)',
        'protected virtual long GetScore(ItemInfo itemInfo, ItemComparerType comparerType)',
        'protected int Comparer(ItemInfo x, ItemInfo y, List<(ItemComparerType comparerType, ComparerOrderType orderType)> priorityList)',
        'public static int ConvertLong2IntForSort(long longValue)'])
    base = without_logging(base, 1)
    reward = selected('RewardPreviewItemComparer.cs', [
        'protected override long GetScore(ItemInfo itemInfo, ItemComparerType comparerType)'])
    priority, = re.findall(r'\tprotected virtual List<[^\n]+ RewardItemComparerPriorityList.*?\n\t\};', source('ItemComparerBase.cs'), re.S)
    if 'sRewardItemComparer = new RewardPreviewItemComparer();' not in source('Common/PlayerTaskDataUtils.cs'):
        raise ValueError('Review original reward-preview comparer selection')
    enums = source('ItemComparerType.cs') + '\n' + source('ComparerOrderType.cs')
    if not re.search(r'Money = 1,', source('Common/ItemType.cs')):
        raise ValueError('Review money item identity')
    # The complete comparer methods retain their original priority, branching,
    # subtraction/sign semantics and fallback behavior; only data ports change.
    body = '''using System.Collections.Generic;
namespace Zjcs.Official.PartnersUI
{
    public sealed class PartnerRewardItemInfo
    {
        public int ClassId, ItemType, Quality, Rank;
    }
    public sealed class PartnerRewardPreviewItemComparer : PartnerRewardItemComparerBase
    {
        public PartnerRewardPreviewItemComparer(int playerExpItemId) : base(playerExpItemId) { }
        /*REWARD*/
    }
    public class PartnerRewardItemComparerBase
    {
        protected readonly int playerExpItemId;
        protected PartnerRewardItemComparerBase(int playerExpItemId) { this.playerExpItemId = playerExpItemId; }
        /*PRIORITY*/
        /*BASE*/
    }
    /*ENUMS*/
}
'''.replace('/*PRIORITY*/', priority).replace('/*REWARD*/', reward).replace('/*BASE*/', base).replace('/*ENUMS*/', enums)
    body = body.replace('Parsers.GameSettings.PlayerExpItemId', 'playerExpItemId').replace('ItemType.Money', '1')
    body = re.sub(r'\bItemInfo\b', 'PartnerRewardItemInfo', body)
    body = re.sub(r'\bItemComparerType\b', 'PartnerRewardItemComparerType', body)
    body = re.sub(r'\bComparerOrderType\b', 'PartnerRewardComparerOrderType', body)
    if 'Parsers.' in body or 'GLogger' in body or '/*' in body:
        raise ValueError('Unadapted reward comparer dependency')
    target = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/FriendshipDialog/PartnerRewardPreviewItemComparer.cs'
    receipt = ROOT / 'assets/reference/partner-reward-comparer-source-manifest.json'
    raw = normalize('// Original reward-preview sorting; see partner-reward-comparer-source-manifest.json.\n' + body).encode()
    if target.exists() and (not receipt.exists() or sha(target.read_bytes()) != json.loads(receipt.read_bytes())['outputs'][target.relative_to(ROOT).as_posix()]):
        raise ValueError('Preserve edited reward comparer')
    target.write_bytes(raw)
    meta(target)
    metadata = Path(str(target) + '.meta')
    metadata.write_bytes(b'\n'.join(line.rstrip() for line in metadata.read_bytes().splitlines()) + b'\n')
    proof = dict(manifest=version, generator_sha256=sha(Path(__file__).read_bytes()), source_sha256=inputs,
        methods=methods, outputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (target, metadata)},
        adaptations=['Typed item projection and explicit player-experience item binding.',
            'Original unused comparer entry points omitted; complete consumed methods and enum identities retained.',
            'Original unsupported-order diagnostic logging omitted; fallback return value retained.'])
    receipt.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Migrated selected reward-preview comparer methods.')


if __name__ == '__main__':
    main()
