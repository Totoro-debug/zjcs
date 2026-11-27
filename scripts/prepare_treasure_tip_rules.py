"""Migrate the complete original player treasure-tip decision tree and current item classification."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_source import OfficialSource
from official_tables import Constructors
from prepare_pet_score_chest_preview import original_method

DEST=ROOT/'unity/Assets/Game/Runtime/Official/TreasureCultivation'
DATA=ROOT/'unity/Assets/Game/Resources/Treasure/treasure-tip-items.json'
RECEIPT=ROOT/'assets/reference/treasure-tips-source-manifest.json'
PORTS=ROOT/'scripts/templates/treasure_cultivation/TreasureTipPorts.cs'
SIGNATURES=('public bool TreasureRedDot(ElementType', 'public bool TreasureRedDot(PlayerItemDataWrap',
    'private bool TreasureCheckCanUpgrade(', 'private bool TreasureCheckCanAwake(',
    'public bool TreasureCanUpgrade(PlayerItemDataWrap itemData, bool autoMergeUpgrade, bool canUseCommonMaterial',
    'public bool TreasureCanUpgrade(PlayerItemDataWrap itemData, bool autoMergeUpgrade)', 'public bool TreasureCanWakeup(',
    'public bool TreasureSummaryRedDot(', 'public bool TreasurePieceRedDot(', 'public bool AnyTreasureRedDot(')

def sha(raw):return hashlib.sha256(raw).hexdigest()

def adapt(body):
    body=body.replace('Player mainPlayer = MeManager.MainPlayer;', '')
    body=re.sub(r'\bPlayerItemDataWrap\b','TreasureItem',body)
    body=body.replace('this.FindItemsByType(', 'context.FindItemsByType(').replace('this.HasItem(', 'context.HasItem(')
    body=body.replace('GetTreasureInSlotIndex(', 'context.GetTreasureInSlotIndex(')
    body=body.replace('ItemType.Treasure)', '(ItemType)305)')
    body=body.replace('item.Info.ElementType','context.GetElement(item.Info.ClassId)').replace('itemData.Info.ElementType','context.GetElement(itemData.Info.ClassId)')
    body=body.replace('Parsers.Instance.TreasureSuitInfoParser.GetSuitClassIdByTreasure(', 'context.GetSuitClassIdByTreasure(')
    body=body.replace('Formula.GetTreasureIdByPieceId(', 'context.GetTreasureIdByPieceId(')
    body=body.replace('ItemInfo itemInfo = Parsers.Instance.ItemInfoParser.Get(treasureIdByPieceId);', 'TreasureTipItemInfo itemInfo = context.GetItemInfo(treasureIdByPieceId);')
    body=body.replace('TreasureHelper.CanActivateTreasure(itemData.Info.ClassId, this)', 'context.CanActivateTreasure(itemData.Info.ClassId)')
    body=body.replace('TreasureHelper.CanMergeTreasure(treasureIdByPieceId, this)', 'context.CanMergeTreasure(treasureIdByPieceId)')
    body=body.replace('TreasureHelper.CanTreasureSlotLevelUp(this, ', 'context.CanTreasureSlotLevelUp(')
    body=body.replace('TreasureHelper.GetTreasureNextLevelUpgradeMaterial(itemData.Id, mainPlayer, ', 'context.GetTreasureNextLevelUpgradeMaterial(itemData.Id, ')
    body=body.replace('TreasureHelper.IsTreasureCanUpgradeUseCommonMaterial(mainPlayer, itemData.Id)', 'IsTreasureCanUpgradeUseCommonMaterial(itemData.Id)')
    body=body.replace('TreasureHelper.CanTreasureUpgrade(itemData.Info.ClassId, materials, this, ', 'context.CanTreasureUpgrade(itemData.Info.ClassId, materials, ')
    if any(s in body for s in ('MeManager','Parsers.','TreasureHelper.','Formula.','PlayerItemDataWrap','mainPlayer')):raise ValueError('Unbound original tip dependency')
    return '\n'.join(line.rstrip() for line in body.split('\n'))

def prepare(check=False,oracle=None):
    source=OfficialSource();code=source.prepared_path/'official-code';proof=source.prepared_path/'code-manifest.json'
    assemblies=[code/'managed/decoded'/name for name in ('Console.dll','Assembly-CSharp.dll')]
    for assembly in assemblies:
        assert sha(assembly.read_bytes())==json.loads(proof.read_bytes())['assemblies'][assembly.name]['decoded_sha256']
    player=code/'common-source/Common/Player.cs';helper=code/'common-source/TreasureHelper.cs';enum=code/'common-source/Common/TreasureRedDotEnum.cs'
    originals=[original_method(player.read_text('utf-8-sig'),sig) for sig in SIGNATURES]
    policy=original_method(helper.read_text('utf-8-sig'),'public static bool IsTreasureCanUpgradeUseCommonMaterial(')
    enum_text=enum.read_text('utf-8-sig').replace('namespace Common;','')
    if oracle:
        oracle.parent.mkdir(parents=True,exist_ok=True)
        oracle.write_text('using System;\nusing System.Collections.Generic;\nusing System.Linq;\nnamespace TreasureTipOracle {\n'+enum_text+
            '\npublic partial class Player {\n'+'\n'.join(originals)+'\n}\npublic static partial class TreasureHelper {\n'+policy+'\n}}\n','utf8',newline='\n')
        return
    policy=policy.replace('public static bool IsTreasureCanUpgradeUseCommonMaterial(Player player, int treasureId)', 'private bool IsTreasureCanUpgradeUseCommonMaterial(int treasureId)')
    policy=policy.replace('Parsers.Instance.TreasureInfoParser.Get(', 'context.GetTreasure(').replace('player.TreasureSlotData.UpgradeUseCommonMaterailEnable','context.UpgradeUseCommonMaterailEnable')
    header='// Original player treasure tip branches; explicit local queries.\nusing System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing ElementType=Zjcs.Official.TreasureSlots.ElementType;\nnamespace Zjcs.Official.TreasureCultivation {\n'
    body=header+enum_text+'\npublic sealed class OfficialTreasureTipRules {\nprivate readonly ITreasureTipContext context;\npublic OfficialTreasureTipRules(ITreasureTipContext context) { this.context=context??throw new ArgumentNullException(nameof(context)); }\n'+'\n'.join(adapt(b) for b in originals)+'\n'+policy+'\n}}\n'
    constructors=Constructors(code);apk=Apk(source.version,reuse_current_cache=True)
    try:
        obj=apk.asset('defaultpackage_assets_config_luban.bundle','/item.g.bytes');raw=obj.read().m_Script.encode('utf8','surrogateescape')
        rows=constructors.table(raw,'TbItemInfo')
        items=[{key:row[key] for key in ('ClassId','Quality','ItemType','ElementType')} for row in rows]
        metadata=dict(source=apk.provenance(obj),decoded_sha256=sha(raw))
        obj=apk.asset('defaultpackage_assets_config_luban.bundle','/treasure_suit.g.bytes');raw=obj.read().m_Script.encode('utf8','surrogateescape')
        suits=constructors.table(raw,'TbTreasureSuitInfo');suit_proof=dict(source=apk.provenance(obj),decoded_sha256=sha(raw))
    finally:apk.close()
    inherited=ROOT/'assets/reference/treasure-cultivation-checks-source-manifest.json'
    prior_checks=json.loads(inherited.read_bytes())
    common=[{key:row[key] for key in ('ClassId','Quality','ItemType')} for row in rows]
    assert sha((json.dumps(common,ensure_ascii=False,indent=2)+'\n').encode())==prior_checks['item_metadata']['metadata_sha256'], 'Review changed item classification before reusing cultivation checks'
    slots_data=ROOT/'unity/Assets/Game/Resources/Treasure/treasure-slots.json'
    current={row['ClassId']:row for row in items}
    assert all(current[row['ClassId']]['ElementType']==row['ElementType'] for row in json.loads(slots_data.read_bytes())['Items']), 'Review changed treasure elements before reusing canonical slots'
    treasure_data=ROOT/'unity/Assets/Game/Resources/Treasure/treasure-rules.json'
    suit_fields=lambda values:[{key:row[key] for key in ('ClassId','Treasures')} for row in values]
    assert suit_fields(suits)==suit_fields(json.loads(treasure_data.read_bytes())['Suits']), 'Review changed suit membership before reusing the treasure catalog'
    outputs={DEST/'OfficialTreasureTipRules.cs':body.encode(),DEST/'TreasureTipPorts.cs':PORTS.read_bytes(),
        DATA:(json.dumps(dict(Items=items),ensure_ascii=False,indent=2)+'\n').encode()}
    for path in list(outputs):
        token=hashlib.md5(('zjcs:treasure-tips:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind='MonoImporter' if path.suffix=='.cs' else 'TextScriptImporter'
        outputs[Path(str(path)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\n'+kind+':\n  externalObjects: {}\n').encode()
    client_setting=code/'client-source/TreasureClientSetting.cs'
    assert re.search(r'public bool AutoMergeWhenLevelUp\s*=\s*true;',client_setting.read_text('utf-8-sig')), 'Review original treasure client-setting default'
    inputs=[Path(__file__),PORTS,proof,*assemblies,player,helper,enum,ROOT/'scripts/official_tables.py',inherited,slots_data,client_setting,treasure_data,
        code/'common-source/Common/TreasureSuitInfoParser.cs']
    for name,text in constructors.sources.items():
        if text:
            path=constructors.source_root/(name+'.cs')
            if not path.is_file():path=constructors.source_root.parent/'Common.LubanGen'/(name+'.cs')
            inputs.append(path)
    runtime=[ROOT/name for name in ('unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureTipService.cs',
        'unity/Assets/Game/Runtime/Offline/HomeTipDots/OfflineHomeTreasureTipBinding.cs',
        'unity/Assets/Game/Runtime/Offline/HomeTipDots/OfflineHomeTipDotServices.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.HomeTipDots.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs')]
    for path in runtime[:2]:
        token=hashlib.md5(('zjcs:treasure-tips:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        outputs[Path(str(path)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\nMonoImporter:\n  externalObjects: {}\n').encode()
    record=dict(manifest=source.version,inputs={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in inputs},
        item_metadata=metadata,suit_membership=suit_proof,methods={sig:sha(b.encode()) for sig,b in zip(SIGNATURES,originals)},
        runtime_bindings={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in runtime},
        outputs={p.relative_to(ROOT).as_posix():sha(raw) for p,raw in outputs.items()},
        adaptations=['Original item/element/all/summary iteration, fixed Active/LevelUp/Wakeup/Upgrade priority, forced ordinary-star auto-merge and common-material policy retained.',
            'The explicit local query context delegates fees and eligibility to the existing original cultivation and slot modules; queries do not save or charge.'])
    prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    outputs[RECEIPT]=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode()
    for path,raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes()!=raw:raise ValueError('Treasure tip source differs: '+str(path))
        else:
            if path!=RECEIPT and path.is_file() and path.read_bytes()!=raw and sha(path.read_bytes())!=prior.get('outputs',{}).get(path.relative_to(ROOT).as_posix()):raise ValueError('Preserve another treasure tip edit: '+str(path))
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
    print('Verified original treasure tip branches and item classification:',len(items),flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');parser.add_argument('--oracle',type=Path)
    args=parser.parse_args();prepare(args.check,args.oracle)
