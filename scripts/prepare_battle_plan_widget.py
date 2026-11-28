"""Migrate the original shared plan widget and name accessors with explicit local player/window ports."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_source import OfficialSource, ROOT
from prepare_pet_score_chest_preview import original_method

UI=ROOT/'unity/Assets/Game/Runtime/Official/BattlePlanUI'
RULES=ROOT/'unity/Assets/Game/Runtime/Official/BattlePlanRules'
RECEIPT=ROOT/'assets/reference/battle-plan-widget-source-manifest.json'

def sha(raw):return hashlib.sha256(raw).hexdigest()

def prepare(check=False,oracle=None):
    source=OfficialSource();code=source.prepared_path/'official-code';proof=source.prepared_path/'code-manifest.json'
    inputs=[proof,Path(__file__),ROOT/'scripts/prepare_pet_score_chest_preview.py']
    for name in ('Assembly-CSharp.dll','Console.dll','Framework.FirstPass.Runtime.dll'):
        path=code/'managed/decoded'/name
        if sha(path.read_bytes())!=json.loads(proof.read_bytes())['assemblies'][name]['decoded_sha256']:raise ValueError('Original battle plan assembly differs')
        inputs.append(path)
    original=code/'client-source/PlanSwitchWidget.cs';inputs.append(original)
    text=original.read_text('utf-8-sig')
    text=text.replace('using Common;','using Zjcs.Official.BattlePlanRules;\nusing Zjcs.Official.ProfessionUI;\nusing MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\nusing ContentModerationClickMask = Zjcs.Official.WorldUI.WorldContentModerationClickMask;\nusing Object = UnityEngine.Object;').replace('using Framework;\n','')
    text=re.sub(r'(?m)^\s*//IL_[^\n]*\n','\n',text)
    text=text.replace('public class PlanSwitchWidget : UIBase\n{','''public class PlanSwitchWidget : UIBase
{
    private IBattlePlanWidgetServices services;
    private IBattlePlanWidgetServices Services => services ?? throw new InvalidOperationException("Bind original plan widget services before initialization.");
    public void Configure(IBattlePlanWidgetServices value) { services = value ?? throw new ArgumentNullException(nameof(value)); }
''')
    text=text.replace('MeManager.MainPlayer != null','services != null').replace('MeManager.MainPlayer.','Services.')
    text=text.replace('LanguageUtils.GetBattlePlanName(', 'Services.GetBattlePlanName(')
    text=text.replace('Services.IsFunctionLimit(FunctionType.BattleConfigPlan)', 'Services.IsPlanLocked()')
    text=text.replace('BattlePlanUtils.IsPlanFunctionDisabled(MeManager.MainPlayer, mPlanType)', 'Services.IsPlanFunctionDisabled(mPlanType)')
    max_method=original_method(text,'private static int GetPlanMaxCount(')
    text=text.replace(max_method,'private int GetPlanMaxCount(BattlePlanType planType) { return Services.GetPlanMaxCount(planType); }')
    text=text.replace('Formula.Instance.', 'Services.')
    text=text.replace('BroadcastManager.ShowErrText(702, null, false, characterMaxCount)', 'Services.ShowError(702, characterMaxCount)')
    text=text.replace('BroadcastManager.ShowErrText(num, null, false)', 'Services.ShowError(num)')
    text=text.replace('BroadcastManager.Instance.SystemMsg(LanguageManager.Instance.GetText("ChangeSkillPlanNameSuc"))', 'Services.SystemMessage("ChangeSkillPlanNameSuc")')
    old='''WindowManager.Instance.Popup(this, (ConfirmDialog go) =>
		{
			go.Init("ui_battle_plan_contentmoderation_confirm", () =>
			{
				RequestChangePlanName(string.Empty, custom: false);
			});
		});'''
    if old not in text:raise ValueError('Review changed original plan moderation confirmation')
    text=text.replace(old,'''Services.Confirm(this, "ui_battle_plan_contentmoderation_confirm", () =>
        {
            RequestChangePlanName(string.Empty, custom: false);
        });''')
    text=text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerChangeBattlePlanName(req).OnComplete += (object obj, int code) =>',
        'Services.ChangePlanName(req, (bool success) =>')
    # This branch's service completion is the only network result in the widget.
    text=text.replace('if (code != 0)', 'if (!success)')
    marker='\n\t}\n\n\tprivate void RestorePlanNameInput()'
    if marker not in text:raise ValueError('Review original name completion boundary')
    before,after=text.split(marker,1)
    index=before.rfind('\n\t\t};')
    if index<0:raise ValueError('Original name callback closure missing')
    before=before[:index]+before[index:].replace('\n\t\t};','\n\t\t});',1)
    text=before+marker+after
    if any(token in text for token in ('MeManager','Parsers.','ServiceProxy','WindowManager','BroadcastManager','Formula.Instance','LanguageUtils.')):
        raise ValueError('Unbound original plan widget infrastructure')
    ui='// Complete original shared plan widget; see source receipt.\nnamespace Zjcs.Official.BattlePlanUI\n{\n'+text+'\n}\n'
    ports=(ROOT/'scripts/templates/battle_plan_widget/IBattlePlanWidgetServices.cs').read_bytes()
    inputs.append(ROOT/'scripts/templates/battle_plan_widget/IBattlePlanWidgetServices.cs')
    accessor=code/'common-source/Common/BattlePlanAccessor.cs';inputs.append(accessor)
    methods=[]
    for signature in ('public static bool IsValidPlanType(', 'public static string GetPlanName(', 'public static bool SetPlanName('):
        methods.append(original_method(accessor.read_text('utf-8-sig'),signature).replace('this Player player','this IBattlePlanNamePlayer player'))
    enum=code/'common-source/Common/BattlePlanType.cs';inputs.append(enum)
    enum_text=enum.read_text('utf-8-sig').replace('namespace Common;','')
    rule_ports=(ROOT/'scripts/templates/battle_plan_widget/BattlePlanNamePorts.cs').read_text('utf8');inputs.append(ROOT/'scripts/templates/battle_plan_widget/BattlePlanNamePorts.cs')
    rules=rule_ports.replace('/*ENUM*/',enum_text).replace('/*METHODS*/','\n'.join(methods))
    labels_source=code/'common-source/Common/LanguageUtils.cs';inputs.append(labels_source)
    labels=[]
    for signature in ('public static string GetBattlePlanName(', 'private static string GetDefaultBattlePlanName('):
        body=original_method(labels_source.read_text('utf-8-sig'),signature)
        body=body.replace('int planIndex, string name)', 'int planIndex, string name, Func<string,string> text)')
        body=body.replace('int planIndex)', 'int planIndex, Func<string,string> text)')
        body=body.replace('GetDefaultBattlePlanName(planType, planIndex)', 'GetDefaultBattlePlanName(planType, planIndex, text)')
        body=body.replace('LanguageManager.Instance.GetText(', 'text(')
        labels.append(body)
    rules=rules.replace('/*LABELS*/','\n'.join(labels))
    signal_source=code/'framework-firstpass-source/Framework/GAction.cs';inputs.append(signal_source)
    signal=original_method(signal_source.read_text('utf-8-sig'),'public sealed class GAction<T1, T2, T3, T4>')
    signal=re.sub(r'GLoggerErrorInterpolatedStringHandler message = .*?Log.Error\(exception, ref message,.*?\);',
        'BattlePlanNameDiagnostics.ReportObserverError?.Invoke(exception);',signal,flags=re.S)
    if 'GLogger' in signal:raise ValueError('Unbound original name signal logging')
    rules=rules.replace('/*SIGNAL*/',signal)
    rules='\n'.join(line.rstrip() for line in rules.split('\n'))
    formula=code/'common-source/Common/FormulaHandler.cs';inputs.extend([formula,code/'common-source/Common/Formula.cs',code/'common-source/Common/CJK.cs'])
    # Both current original name validators have the same emptiness, CJK count and regex checks;
    # the shared skill owner supplies those checks, and this owner preserves battle-config error 62503.
    for signature in ('public int CheckBattleConfigPlanNameLegal(', 'public int CheckSkillPlanNameLegal(', 'public int GetCharacterCount('):
        original_method(formula.read_text('utf-8-sig'),signature)
    if oracle is not None:
        regex=re.search(r'public static Regex IllegalCharacterRegex \{ get; \} = new Regex\((.*)\);',(code/'common-source/Common/Formula.cs').read_text('utf-8-sig'))[1]
        bodies=[original_method(formula.read_text('utf-8-sig'),sig) for sig in (
            'public int CheckBattleConfigPlanNameLegal(', 'public int CheckSkillPlanNameLegal(', 'public int GetCharacterCount(')]
        oracle_text='''namespace PlanNameOracle {
using System;
using System.Text;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
public static class Formula { public static Regex IllegalCharacterRegex = new Regex('''+regex+'''); }
public class Validator { public int Maximum; public int GetCharacterMaxCount()=>Maximum;
'''+ '\n'.join(bodies)+'\n}\n'+original_method((code/'common-source/Common/CJK.cs').read_text('utf-8-sig'),'public static class CJK')+'\n}\n'
        oracle.parent.mkdir(parents=True,exist_ok=True);oracle.write_text(oracle_text,'utf8',newline='\n');return
    inputs.append(ROOT/'unity/Assets/Game/Runtime/Official/SkillRules/OfficialSkillPlanName.cs')
    outputs={UI/'PlanSwitchWidget.cs':ui.encode(),UI/'IBattlePlanWidgetServices.cs':ports,RULES/'BattlePlanNames.cs':rules.encode()}
    for folder,refs in ((UI,['Zjcs.Official.BattlePlanRules','Zjcs.Official.ProfessionUI','Zjcs.Official.WorldUI','Unity.TextMeshPro','Unity.ugui']),
        (RULES,['Zjcs.Official.SkillRules'])):
        body=dict(name='Zjcs.Official.'+folder.name,rootNamespace='Zjcs.Official.'+folder.name,references=refs)
        if folder==RULES:body['noEngineReferences']=True
        outputs[folder/(body['name']+'.asmdef')]=(json.dumps(body,indent=2)+'\n').encode()
        token=hashlib.md5(('zjcs:battle-plan:'+folder.relative_to(ROOT).as_posix()).encode()).hexdigest()
        outputs[Path(str(folder)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
    local=ROOT/'unity/Assets/Game/Runtime/Offline/TreasureUI'
    outputs[local/'Zjcs.Offline.TreasureUI.asmdef']=(json.dumps(dict(name='Zjcs.Offline.TreasureUI',references=[
        'Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Offline.CharacterCreation','Zjcs.Official.TreasureCultivation',
        'Zjcs.Official.TreasureIconUI','Zjcs.Official.TreasureSlotsUI','Zjcs.Official.HomeMainUiControls','Zjcs.Official.HomeHudUI',
        'Zjcs.Official.BattlePlanRules','Zjcs.Official.BattlePlanUI','Zjcs.Official.ProfessionUI','Zjcs.Official.SkillRules',
        'Zjcs.Official.WorldUI','Zjcs.Official.World.NativeLabelUI','Zjcs.Official.TreasureDetailUI','Zjcs.Official.TreasurePromoteUI',
        'Zjcs.Official.ItemDetailUI','Zjcs.Offline.ProfessionUI','Zjcs.Offline.PartnersUI','Zjcs.Offline.HomeCurrencyUI','Zjcs.Offline.HomeUI','Unity.ugui','Unity.TextMeshPro']),indent=2)+'\n').encode()
    folder_token=hashlib.md5(('zjcs:battle-plan:'+local.relative_to(ROOT).as_posix()).encode()).hexdigest()
    outputs[Path(str(local)+'.meta')]=('fileFormatVersion: 2\nguid: '+folder_token+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
    for path in list(outputs):
        if path.suffix=='.meta':continue
        token=hashlib.md5(('zjcs:battle-plan:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer='MonoImporter' if path.suffix=='.cs' else 'AssemblyDefinitionImporter'
        outputs[Path(str(path)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\n'+importer+':\n  externalObjects: {}\n').encode()
    runtime=[ROOT/'unity/Assets/Game/Runtime'/path for path in (
        'Offline/TreasureUI/OfflineTreasurePlanWidgetServices.cs','Offline/Progression/OfflineTreasurePlanNameService.cs',
        'Offline/TreasureUI/OfflineTreasurePlanWidget.cs',
        'Offline/TreasureUI/OfflineTreasurePlanActions.cs',
        'Offline/TreasureUI/OfflineTreasurePlanRequests.cs',
        'Offline/Progression/OfflineCharacterPropertyProgress.cs','Offline/Progression/Zjcs.Offline.Progression.asmdef')]
    for path in runtime[:4]:
        token=hashlib.md5(('zjcs:battle-plan:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        outputs[Path(str(path)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\nMonoImporter:\n  externalObjects: {}\n').encode()
    record=dict(manifest=source.version,inputs={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in inputs},
        outputs={p.relative_to(ROOT).as_posix():sha(raw) for p,raw in outputs.items()},
        runtime_bindings={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in runtime},
        adaptations=['Original widget lifecycle, navigation, empty-plan skipping, selection bar, rollback, rename capture and confirmation branches retained.',
            'Explicit player/config/localization/window/name-request ports replace global services. A bool completion projects local durable-save success.',
            'Name accessors retain original transitions; local plan names are a separate persisted metadata facet, shared UI does not own slots or training.'])
    prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    outputs[RECEIPT]=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode()
    for path,raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes()!=raw:raise ValueError('Original plan widget source differs: '+str(path))
        else:
            if path!=RECEIPT and path.is_file() and path.read_bytes()!=raw and sha(path.read_bytes())!=prior.get('outputs',{}).get(path.relative_to(ROOT).as_posix()):raise ValueError('Preserve another plan widget edit: '+str(path))
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
    print('Verified original shared plan widget and name accessors',flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');parser.add_argument('--oracle',type=Path)
    args=parser.parse_args();prepare(args.check,args.oracle)
