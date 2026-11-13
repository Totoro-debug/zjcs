"""Record original chain components, client presentation and local execution bindings."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CURRENT = ROOT / '.tools/source-packages/current/884_156692'


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw),
                sha256=hashlib.sha256(raw).hexdigest())


def main():
    code = json.loads((CURRENT / 'code-manifest.json').read_bytes())
    assemblies = [CURRENT / ('official-code/managed/decoded/' + name)
                  for name in ('Console.dll', 'Assembly-CSharp.dll')]
    assert code['manifest'] == '884_156692'
    for assembly in assemblies:
        assert identity(assembly)['sha256'] == code['assemblies'][assembly.name]['decoded_sha256']
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    proof = dict(
        code_manifest='884_156692', configuration_manifest='854_156383',
        code_receipt=identity(CURRENT / 'code-manifest.json'),
        client_receipt=identity(CURRENT / 'client-decompilation-manifest.json'),
        assemblies=[identity(assembly) for assembly in assemblies],
        original=[identity(CURRENT / ('official-code/' + path)) for path in (
            'common-source/Common/FightHitChainedComponent.cs',
            'common-source/Common/FightHitChainedComponentInfo.cs',
            'common-source/Common/FightHitBaseComponentInfo.cs',
            'client-source/MyFightHitChained.cs')],
        input=[identity(ROOT / ('unity/Assets/Official/Combat/' + name)) for name in (
            'skill-rules.json', 'skill-rules-source-manifest.json',
            'battle-rules.json', 'battle-rules-source-manifest.json')],
        migration=[identity(runtime / path) for path in (
            'Offline/Combat/FightHitChainExecution.cs',
            'Offline/Combat/FightBattleHitSelectionSnapshot.cs',
            'Official/Combat/FightBattleSkillCastPresentation.cs',
            'Presentation/Combat/OfficialBattleHitReceiptPlayback.cs')],
        validation=[identity(ROOT / ('scripts/' + path)) for path in (
            'test_battle_chained_hits.ps1', 'fixtures/battle_chained_hits_harness.cs',
            'test_chained_hit_player.ps1')],
        native_probe=identity(runtime / 'Presentation/Combat/OfficialBattleGraphCapabilityValidation.ChainedHits.cs'),
        generator=identity(Path(__file__).resolve()),
        bindings=dict(collector='FightBattleHitCollector.CollectChildren/BindChainParent',
            executor='FightBattleExecutor.ExecuteSkillCore',
            native='OfficialBattleHitReceiptPlayback.Enqueue -> MyFightHitChained.OnStart'),
        operation='Preserve original graph references, flags, scopes, hit/flight delays and client start-grid presentation. '
            'Server next-hop selection is unavailable. The confirmed offline decision uses the previous actual hit cell '
            'plus original skill Range, stops without legal candidates, obeys Repeat/Back/PreferUnvisited, then chooses '
            'nearest squared grid distance with unit ID and original body-cell order ties. '
            'Per-cast shared branch histories and independent root histories feed the existing settlement path; '
            'receipts retain each actual hop origin without persisting traversal references. '
            'Old receipts without origin evidence do not invent native chain flights.')
    destination = ROOT / 'assets/reference/battle-rules/chained-hits-source-manifest.json'
    destination.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Recorded original chained-hit components, client view and explicit offline bindings.')


if __name__ == '__main__':
    main()
