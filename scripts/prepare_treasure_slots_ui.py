"""Migrate complete original treasure slot and slot-panel controllers with local player/window/request ports."""
import argparse,hashlib,json,re
from pathlib import Path
from official_source import ROOT,OfficialSource
from prepare_pet_score_chest_preview import original_method
from official_apk import Apk
from official_tables import Constructors

DEST=ROOT/'unity/Assets/Game/Runtime/Official/TreasureSlotsUI'
RECEIPT=ROOT/'assets/reference/treasure-slots-ui-source-manifest.json'
NAMES=('TreasureSlotItem','TreasureSlotsPanelContent')
def sha(raw):return hashlib.sha256(raw).hexdigest()

def adapt(name,text):
    text=re.sub(r'^using (?:Common|Framework(?:\.DependencyInjection)?);\s*$','',text,flags=re.M)
    text=re.sub(r'^\s*//IL_.*$','',text,flags=re.M)
    text=re.sub(r'\n\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n','\n',text)
    at=text.index('\n{',text.index('public class'))+2
    text=text[:at]+'''
    private ITreasureSlotsPanelServices services;
    private ITreasureSlotsPanelServices Services => services ?? throw new System.InvalidOperationException("Bind original treasure slot services before activation.");
    public void Configure(ITreasureSlotsPanelServices value) => services=value??throw new System.ArgumentNullException(nameof(value));
'''+text[at:]
    text=text.replace('StateSwitcher<','WorldStateSwitcher<').replace('PlayerItemDataWrap','IProfessionItemData')
    if name=='TreasureSlotItem':
        text=text.replace('ReplaceListener','Services.Icons.ReplaceListener')
        text=text.replace('UIUtils.Instantiate(', 'Services.InstantiateIcon(')
        text=text.replace('TreasureDetailDialog component = go.GetComponent<TreasureDetailDialog>();',
            'ITreasureDetailOptions component = go.GetComponent<ITreasureDetailOptions>();')
        text=text.replace('(Object)(object)component != (Object)null','component != null')
        text=text.replace('TreasureHelper.CanSetupTreasureSlot(MeManager.MainPlayer, ', 'Services.Icons.CanSetupTreasureSlot(')
        text=text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerSetupTreasureSlot(', 'Services.Icons.SetupTreasureSlot(')
        text=text.replace('MeManager.MainPlayer.GetItemById(', 'Services.Icons.GetItemById(')
    else:
        text=text.replace('ReplaceEventListener','Services.Icons.ReplaceListener')
        text=text.replace('MultiLanguageText','WorldMultiLanguageText')
        text=text.replace('ServerInfoManager.Instance.OnFunctionDisabledUpdate','Services.OnFunctionDisabledUpdate')
        text=text.replace('ServerInfoManager.Instance != null','services != null')
        text=text.replace('OnFunctionDisabledUpdate(ServerFunctionType type, bool disabled, HashSet<FunctionsDisabledKeyBase> disabledKeys)',
            'OnFunctionDisabledUpdate(BattlePlanType type)')
        text=text.replace('ServerFunctionType.BattlePlanTreasure','BattlePlanType.Treasure')
        text=text.replace('MeManager.MainPlayer.OnSetupTreasureSlot','Services.OnSetupTreasureSlot')
        text=text.replace('MeManager.MainPlayer.OnTreasureRefresh','Services.Icons.OnTreasureRefresh')
        text=text.replace('MeManager.MainPlayer.OnAllTreasureBlessUpdate','Services.Icons.OnAllTreasureBlessUpdate')
        text=text.replace('MeManager.MainPlayer.GetItemById(', 'Services.Icons.GetItemById(')
        text=text.replace('MeManager.MainPlayer.GetTreasureElementSlotData(', 'Services.GetTreasureElementSlotData(')
        text=text.replace('MeManager.MainPlayer.GetActivePlanIndex(', 'Services.Plans.GetActivePlanIndex(')
        text=text.replace('MeManager.MainPlayer.GetPlanName(', 'Services.Plans.GetPlanName(')
        text=text.replace('MeManager.MainPlayer != null','services != null')
        text=text.replace('Parsers.GameSettings.TreasureSlotPropAddRate','Services.TreasureSlotPropAddRate')
        text=text.replace('BattlePlanUtils.IsPlanFunctionDisabled(MeManager.MainPlayer, ', 'Services.Plans.IsPlanFunctionDisabled(')
        text=text.replace('LanguageUtils.GetBattlePlanName(', 'Services.Plans.GetBattlePlanName(')
        text=re.sub(r'\n\t\tPlayerApplyBattlePlanRequest req = new PlayerApplyBattlePlanRequest\s*\{.*?\n\t\t\};','',text,flags=re.S)
        text=re.sub(r'\n\t\tPlayerClearBattlePlanRequest req = new PlayerClearBattlePlanRequest\s*\{.*?\n\t\t\};','',text,flags=re.S)
        text=text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerApplyBattlePlan(req).OnComplete += (object obj, int code) =>',
            'Services.Requests.Apply(BattlePlanType.Treasure, planIndex, (BattlePlanRequestOutcome result) =>')
        text=text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerClearBattlePlan(req).OnComplete += (object obj, int code) =>',
            'Services.Requests.Clear(BattlePlanType.Treasure, planIndex, (BattlePlanRequestOutcome result) =>')
        text=text.replace('if (code != 0)','if (!result.Success)').replace('if (code == 0)','if (result.Success)')
        text=text.replace('BroadcastManager.Instance.SystemMsg(BroadcastManager.Instance.GetErrText(code));','result.ReportFailure();')
        text=text.replace('BroadcastManager.Instance.SystemMsg(LanguageManager.Instance.GetTextFormated("ui_treasure_plan_switch_tip", battlePlanName));',
            'Services.Plans.SystemMessage("ui_treasure_plan_switch_tip", battlePlanName);')
        text=text.replace('BroadcastManager.Instance.SystemMsg(LanguageManager.Instance.GetText("ui_battle_plan_clear_success_tip"));',
            'Services.Plans.SystemMessage("ui_battle_plan_clear_success_tip");')
        old=original_method(text,'private void OnTreasurePlanClearClicked(')
        text=text.replace(old,'''private void OnTreasurePlanClearClicked(int planIndex)
    {
        Services.Plans.Confirm(this,"ui_battle_plan_clear_confirm_tip",()=>ConfirmClearTreasurePlan(planIndex));
    }''')
        # Both consumed proxy completions become ordinary callback arguments.
        for signature in ('private void OnTreasurePlanChanged(', 'private void ConfirmClearTreasurePlan('):
            old=original_method(text,signature);index=old.rfind('\n\t\t};')
            if index<0:raise ValueError('Original plan completion boundary changed: '+name)
            new=old[:index]+old[index:].replace('\n\t\t};','\n\t\t});',1)
            text=text.replace(old,new)
    if any(t in text for t in ('[Inject]','MeManager','Parsers.','UIUtils.','ServiceProxy','ServerInfoManager','BroadcastManager','LanguageUtils.','WindowManager')):
        raise ValueError('Bind original slot dependencies: '+name+'\n'+text)
    header='using Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\nusing Zjcs.Official.TreasureUI;\nusing Zjcs.Official.BattlePlanUI;\nusing BattlePlanType=Zjcs.Official.BattlePlanRules.BattlePlanType;\nusing Zjcs.Official.TreasureSlots;\n'
    return ('// Complete original slot controller; explicit local dependencies only.\n'+header+'namespace Zjcs.Official.TreasureSlotsUI {\nusing Object=UnityEngine.Object;\n'+
        '\n'.join(line.rstrip() for line in text.split('\n'))+'\n}\n').encode()

def prepare(check=False):
    source=OfficialSource();code=source.prepared_path/'official-code';proof=source.prepared_path/'code-manifest.json'
    inputs=[Path(__file__),proof];outputs={}
    for name in ('Assembly-CSharp.dll','Console.dll'):
        p=code/'managed/decoded'/name;assert sha(p.read_bytes())==json.loads(proof.read_bytes())['assemblies'][name]['decoded_sha256'];inputs.append(p)
    inputs.extend(code/'client-source'/name for name in ('UIUtils.cs','EUIInstantiateFlag.cs'))
    retained=ROOT/'unity/Assets/Game/Resources/Treasure/treasure-rules.json';inputs.append(retained)
    apk=Apk(source.version,reuse_current_cache=True)
    try:
        obj=apk.asset('defaultpackage_assets_config_luban.bundle','/game_settings.g.bytes');raw=obj.read().m_Script.encode('utf8','surrogateescape')
        settings=Constructors(code).table(raw,'TbGameSettings');prior_settings=json.loads(retained.read_bytes())['Settings']
        consumed=('TreasureMaxSlotCount','TreasurePlanMaxCount','TreasureSlotElementTypes','TreasureSlotPropAddRate')
        if any(settings[key]!=prior_settings[key] for key in consumed):raise ValueError('Current original slot settings differ from retained rules')
        settings_proof=dict(source=apk.provenance(obj),sha256=sha(raw),fields={key:settings[key] for key in consumed})
    finally:apk.reader.close()
    for name in NAMES:
        original=code/'client-source'/(name+'.cs');inputs.append(original);outputs[DEST/(name+'.cs')]=adapt(name,original.read_text('utf-8-sig'))
    original=code/'client-source/ItemListTypeMultiText.cs';inputs.append(original)
    text=original.read_text('utf-8-sig').replace('MultiLanguageText','Zjcs.Official.WorldUI.WorldMultiLanguageText')
    outputs[DEST/'ItemListTypeMultiText.cs']=('namespace Zjcs.Official.TreasureSlotsUI {\n'+text+'\n}\n').encode()
    for name,folder in [('ITreasureSlotsPanelServices',DEST),('IBattlePlanContentRequests',DEST.parent/'BattlePlanUI'),('ITreasureDetailOptions',DEST.parent/'TreasureIconUI')]:
        original=ROOT/'scripts/templates/treasure_ui'/(name+'.cs');inputs.append(original);outputs[folder/original.name]=original.read_bytes()
    assembly=DEST/'Zjcs.Official.TreasureSlotsUI.asmdef'
    outputs[assembly]=(json.dumps(dict(name='Zjcs.Official.TreasureSlotsUI',references=['Zjcs.Official.TreasureIconUI','Zjcs.Official.TreasureCultivation',
        'Zjcs.Official.ProfessionUI','Zjcs.Official.WorldUI','Zjcs.Official.BattlePlanUI','Zjcs.Official.BattlePlanRules','Unity.ugui']),indent=2)+'\n').encode()
    for path in list(outputs):
        token=hashlib.md5(('zjcs:treasure-slots-ui:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer='AssemblyDefinitionImporter' if path.suffix=='.asmdef' else 'MonoImporter'
        outputs[Path(str(path)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\n'+importer+':\n  externalObjects: {}\n').encode()
    outputs[Path(str(DEST)+'.meta')]=('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:treasure-slots-ui:folder').hexdigest()+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
    record=dict(manifest=source.version,inputs={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in inputs},outputs={p.relative_to(ROOT).as_posix():sha(raw) for p,raw in outputs.items()},
        retained_settings_compatibility=settings_proof,
        adaptations=['Retain the full original slot selection, dynamic item initialization, detail options, element states, replacement decisions, slot refresh, plan widget lifecycle, rollback and confirmation order.',
            'Replace global player/server/window/parser/network access with explicit consumed services and shared plan request completions.'])
    prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {};outputs[RECEIPT]=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode()
    for path,raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes()!=raw:raise ValueError('Original slot output differs: '+str(path))
        else:
            if path!=RECEIPT and path.is_file() and path.read_bytes()!=raw and sha(path.read_bytes())!=prior.get('outputs',{}).get(path.relative_to(ROOT).as_posix()):raise ValueError('Preserve another slot edit: '+str(path))
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
    print('Verified original treasure slot UI controllers',flush=True)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');prepare(p.parse_args().check)
