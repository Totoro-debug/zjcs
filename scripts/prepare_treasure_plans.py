"""Migrate the original treasure plan application/clearing and activation auto-slot contracts."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_source import OfficialSource, ROOT
from prepare_pet_score_chest_preview import original_method
from prepare_treasure_slots import adapted

TEMPLATE=ROOT/'scripts/templates/treasure_cultivation/TreasurePlanPorts.cs'
DEST=ROOT/'unity/Assets/Game/Runtime/Official/TreasureCultivation/OfficialTreasurePlans.cs'
RECEIPT=ROOT/'assets/reference/treasure-plans-source-manifest.json'
SIGNATURES=('public static bool TryGetTreasurePlanSlots(', 'public static void ParseTreasurePlanSlots(',
    'public static bool HasTreasurePlanSlot(', 'public static Dictionary<int, int> ReadWornTreasureSlots(',
    'public static void ApplyWornTreasureTargetState(', 'public static int RemoveTreasureFromAllPlans(')

def sha(data):return hashlib.sha256(data).hexdigest()

def prepare(check=False,oracle=None):
    source=OfficialSource();code=source.prepared_path/'official-code';common=code/'common-source/Common'
    proof=source.prepared_path/'code-manifest.json';dll=code/'managed/decoded/Console.dll'
    if sha(dll.read_bytes())!=json.loads(proof.read_bytes())['assemblies']['Console.dll']['decoded_sha256']:raise ValueError('Original plan assembly differs')
    inputs=[proof,dll,TEMPLATE,Path(__file__),ROOT/'scripts/prepare_treasure_slots.py']
    framework=code/'managed/decoded/Framework.FirstPass.Runtime.dll'
    if sha(framework.read_bytes())!=json.loads(proof.read_bytes())['assemblies'][framework.name]['decoded_sha256']:raise ValueError('Original plan signal assembly differs')
    inputs.extend([framework,common/'ItemType.cs'])
    if not re.search(r'\bTreasure\s*=\s*305\s*,',(common/'ItemType.cs').read_text('utf-8-sig')):raise ValueError('Original treasure item type differs')
    methods={};contracts={}
    def body(path,signature,contract=False):
        if path not in inputs:inputs.append(path)
        text=original_method(path.read_text('utf-8-sig'),signature)
        (contracts if contract else methods)[path.relative_to(code).as_posix()+':'+signature]=sha(text.encode())
        return text
    ext=[body(common/'PlayerTreasurePlanExt.cs',s) for s in SIGNATURES]
    ext.append(body(common/'BattlePlanAccessor.cs','public static bool ClearPlanContent('))
    notify=body(common/'Player.cs','public void FireOnBattlePlanContentCleared(')
    callbacks=[]
    for signature in ('protected override void OperPlayerApplyBattlePlanCallback(',
        'protected override void OperPlayerClearBattlePlanCallback(', 'private static void ClearWornWhenActivePlanCleared('):
        value=body(code/'common-source/PlayerServiceProxyImpl.cs',signature)
        if 'case BattlePlanType.Equip:' in value:
            begin=value.index('case BattlePlanType.Equip:');end=value.index('case BattlePlanType.Treasure:',begin)
            value=value[:begin]+value[end:]
        value=value.replace('protected override void','public static void')
        name=signature.split('void ')[1].split('(')[0]
        value=value.replace(name+'(',name+'(Player player, ',1)
        value=value.replace('MeManager.MainPlayer','player')
        if name=='OperPlayerClearBattlePlanCallback':value=value.replace('ClearWornWhenActivePlanCleared(req.', 'ClearWornWhenActivePlanCleared(player, req.')
        callbacks.append(value)
    signal=body(code/'framework-firstpass-source/Framework/GAction.cs','public sealed class GAction<T1, T2>')
    # Only the original framework logging boundary changes; its linked list/version/exception behavior stays verbatim.
    signal=re.sub(r'GLoggerErrorInterpolatedStringHandler message = .*?Log.Error\(exception, ref message,.*?\);',
        'TreasurePlanDiagnostics.ReportObserverError?.Invoke(exception);',signal,flags=re.S)
    if 'GLogger' in signal:raise ValueError('Unbound original signal logging')
    for relative,signatures in (
        ('common-source/Common/Player.cs',('private void Player_OnTreasureActive(', 'private async void Player_OnTreasureListChanged(',
            'private void Player_OnItemCountUpdate1(', 'private UniTask AutoSetupTreasure(', 'public int GetCanSetupTreasureSlotIndex(')),
        ('common-source/Common/BattlePlanAccessor.cs',('public static bool EnsurePlanEntries(', 'public static bool SetActivePlanIndex(', 'public static bool TryGetPlanContent(')),
        ('common-source/Common/PlayerBattlePlanTreasurePlanContentWrap.cs',('public override void Clear(',)),
        ('client-source/TreasureSlotsPanelContent.cs',('private void OnTreasurePlanChanged(', 'private void ConfirmClearTreasurePlan(')),
    ):
        for signature in signatures:body(code/relative,signature,True)
    # The async entry point's state machine contains the actual next-frame/request ordering.
    player_text=(common/'Player.cs').read_text('utf-8-sig');begin=player_text.index('private struct _003CAutoSetupTreasure_');end=player_text.index('\n\t[CompilerGenerated]',begin)
    contracts['AutoSetupTreasure:state-machine']=sha(player_text[begin:end].encode())
    text=TEMPLATE.read_text('utf8').replace('/*SIGNAL*/',signal).replace('/*NOTIFY*/',notify)
    text=text.replace('/*METHODS*/','\n\n'.join(ext if oracle else [adapted(v,'PlayerTreasurePlanExt') for v in ext]))
    text=text.replace('/*CALLBACKS*/','\n\n'.join(callbacks if oracle else [adapted(v,'PlayerTreasurePlanExt') for v in callbacks]))
    text='\n'.join(line.rstrip() for line in text.splitlines())+'\n'
    if oracle:
        oracle.parent.mkdir(parents=True,exist_ok=True);oracle.write_text(text.replace('Zjcs.Official.TreasureSlots','TreasureSlotOracle'),'utf8',newline='\n');return
    guid=hashlib.md5(('zjcs:treasure-plans:'+DEST.relative_to(ROOT).as_posix()).encode()).hexdigest()
    outputs={DEST:text.encode(),Path(str(DEST)+'.meta'):('fileFormatVersion: 2\nguid: '+guid+'\nMonoImporter:\n  externalObjects: {}\n').encode()}
    bindings=[ROOT/name for name in (
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotService.Plans.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotService.Plans.cs.meta',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotService.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureAutoSetup.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureAutoSetup.cs.meta',
        'scripts/fixtures/treasure_plans_validation.cs','scripts/test_treasure_plans.ps1')]
    receipt=dict(manifest=source.version,inputs={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in inputs},methods=methods,contracts=contracts,
        runtime_bindings={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in bindings},outputs={p.relative_to(ROOT).as_posix():sha(v) for p,v in outputs.items()},
        scope='Original treasure plan projection/application/removal/clear transitions and Treasure-only client callback branches; activation-driven next-frame auto-setup uses original free-index and manual setup paths. Names, battle-use plan groups and native widgets are separate owners.')
    outputs[RECEIPT]=(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n').encode()
    prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    for p,v in outputs.items():
        if check:
            if not p.is_file() or p.read_bytes()!=v:raise ValueError('Treasure plan source differs: '+str(p))
        else:
            if p!=RECEIPT and p.is_file() and p.read_bytes()!=v and sha(p.read_bytes())!=prior.get('outputs',{}).get(p.relative_to(ROOT).as_posix()):raise ValueError('Preserve another plan edit: '+str(p))
            p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(v)
    print('Verified original treasure plan methods:',len(methods))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');parser.add_argument('--oracle',type=Path)
    args=parser.parse_args();prepare(args.check,args.oracle)
