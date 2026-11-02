"""Port the selected partner player-info widget without relabelling legacy ports."""
import hashlib
import json
from pathlib import Path
import re

from prepare_unity_profession_player_info import CONTEXT, adapt_data
from prepare_unity_profession_ui_parts import common
from prepare_unity_profession_player_appearance import binding
from prepare_unity_profession_photographer import ROOT, clean, save


def main():
    selected = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if selected != '884_156692':
        raise ValueError('Review the selected player-info implementation before changing versions')
    code = ROOT / '.tools/source-packages/current' / selected / 'official-code'
    code_manifest_path = code.parent / 'code-manifest.json'
    code_manifest = json.loads(code_manifest_path.read_bytes())
    if code_manifest['manifest'] != selected:
        raise ValueError('Selected official code manifest differs')
    assemblies = [code / 'managed/decoded' / (name + '.dll') for name in ('Assembly-CSharp', 'Console')]
    for assembly in assemblies:
        if hashlib.sha256(assembly.read_bytes()).hexdigest() != code_manifest['assemblies'][assembly.name]['decoded_sha256']:
            raise ValueError('Selected official assembly differs: ' + assembly.name)
    source = code / 'client-source/PlayerInfoUI.cs'
    text = adapt_data(common(source.read_text('utf8'))).replace('IProfessionAppearancePartAsset', 'AssetOperationHandle<GameObject>')
    text = re.sub(r'^using (?:AppearanceSystem|R3);\n', '', text, flags=re.M)
    text = 'using Zjcs.Official.WorldUI;\n' + text
    text = text.replace('MultiLanguageText', 'WorldMultiLanguageText').replace('StateSwitcher<', 'WorldStateSwitcher<')
    for name in ('PlayerAnimationAppearanceScene', 'HeadCharacterPortrait', 'HonorTitleItemProvider', 'PlanesIconItem'):
        text = re.sub(r'\b' + name + r' (?=\w)', 'Component ', text)
    text = text.replace('private DisposableBag mDisposableBag;', 'private ProfessionResourceOperationBag mDisposableBag = new ProfessionResourceOperationBag();').replace('\t\tmDisposableBag = default;\n', '')
    text = binding(text, '\t[Inject]', '\tpublic PlayerInfoData PlayerInfoData', CONTEXT)
    replacements = {
        'new CultureTextArg(mPlayerInfoData.Name, mPlayerInfoData.TargetData.IsNpc)': 'InfoBindings.CreateNameArg(mPlayerInfoData.Name, mPlayerInfoData.TargetData.IsNpc)',
        'ProfessionSettings.Value.GetFullBodyImg(mPlayerInfoData.Profession, mPlayerInfoData.Sex).AssetGuid': 'InfoBindings.GetFullBodyGuid(mPlayerInfoData.Profession, mPlayerInfoData.Sex)',
        'ResourceLoader.LoadAssetByGuid<Sprite>': 'InfoBindings.Resources.LoadAssetByGuid<Sprite>',
        'ResourceLoader.LoadSprite(': 'InfoBindings.Resources.LoadAsset<Sprite>(',
        'ResourceLoader.LoadPrefab(': 'InfoBindings.Resources.LoadAsset<GameObject>(',
        'MeManager.MainPlayer': 'InfoBindings.MainPlayer',
        'PlayerAppearanceScene.RefreshAppearance(': '((IProfessionPlayerInfoScene)PlayerAppearanceScene).RefreshAppearance(',
        'HonorTitleItemProvider.ClearTitleItem()': '((IProfessionPlayerInfoTitle)HonorTitleItemProvider).ClearTitleItem()',
        'HonorTitleItemProvider.Init(': '((IProfessionPlayerInfoTitle)HonorTitleItemProvider).Init(',
        '!appearanceData.HonorItemDict.TryGetValue(HonorType.Title, out var _)': '!InfoBindings.HasHonorTitle(appearanceData)',
        'NpcInfo npcInfo = Parsers.Instance.NpcInfoParser.Get(npcClassId);': 'ProfessionInfoNpcDefinition npcInfo = InfoBindings.GetNpc(npcClassId);',
        'UIUtils.Instantiate(result, NpcAppearanceSlot.transform, EUIInstantiateFlag.KeepScale)': 'InfoBindings.InstantiateKeepScale(result, NpcAppearanceSlot.transform)',
        'NpcUtils.TryOverrideNpcFigureModelInitAnimationPlay(': 'InfoBindings.OverrideNpcInitialAnimation(',
        'PlayerHeadPortrait.GeneratePortrait(': '((IProfessionPlayerInfoHead)PlayerHeadPortrait).GeneratePortrait(',
        'LanguageUtils.GetEnumKey(mPlayerInfoData.SubRank)': 'InfoBindings.SubRankKey(mPlayerInfoData.SubRank)',
        'CultureNumberArg.Compact(': 'InfoBindings.CompactNumber(',
        'PlanesIconItem.Init(': '((IProfessionPlayerInfoPlanesIcon)PlanesIconItem).Init(',
        'LanguageUtils.TextKeyArgWrap playerNameWithPlanesNameText = PlanesNameProvider.GetPlayerNameWithPlanesNameText(': 'ProfessionInfoText playerNameWithPlanesNameText = InfoBindings.NameWithPlanes(',
        'ProfessionBaseInfoParser professionBaseInfoParser = Parsers.Instance.ProfessionBaseInfoParser;': 'IProfessionPlayerInfoBindings professionBaseInfoParser = InfoBindings;',
        'ProfessionBaseInfoParser parser': 'IProfessionPlayerInfoBindings parser',
        'parser.GetThirdIconPath(': 'parser.ThirdProfessionIcon(',
        'parser.GetFirstIconPath(': 'parser.FirstProfessionIcon(',
        'Profession.None': '0',
        'LanguageUtils.GetProfessionNameKey(': 'InfoBindings.ProfessionNameKey(',
        '((Component)this).GetComponentInParent<TeamInviteSubPanelBase>()?.TeamTarget?.TargetCatalog': 'InfoBindings.TeamCatalog(this)',
        'JointStrikeUtils.CanJointTogether(': 'InfoBindings.CanJointTogether(',
        'PlayerRobotInvite playerRobotInvite = ((Component)this).GetComponentInParent<TeamInviteRobotPanel>()?.RobotInviteCfg;': 'ProfessionInfoRobotInvite playerRobotInvite = InfoBindings.RobotInvite(this);',
        'Parsers.Instance.PlayerRobotInviteParser.Get(RobotInvitePlayType.All)': 'InfoBindings.DefaultRobotInvite()',
    }
    text = re.sub(r'Disposable.AddTo<AssetOperationHandle<(Sprite|GameObject)>>\((.*?), ref mDisposableBag\)', r'mDisposableBag.Add(\2)', text)
    for old, new in replacements.items():
        if old not in text:
            raise ValueError('Review current player-info adapter: ' + old)
        text = text.replace(old, new)
    start = text.index('\t\t\tGLoggerErrorInterpolatedStringHandler')
    end = text.index('\n\t\t}', start)
    text = text[:start] + '\t\t\tDebug.LogError("肖像生成失败. 玩家ID为" + mPlayerInfoData.Id + "\\n" + exception);' + text[end:]
    for token in ('Parsers.Instance', 'MeManager.', '[Inject]', 'GLogger', 'Disposable.AddTo', 'AssetOperationHandle<', 'CultureTextArg', 'LanguageUtils.', 'ProfessionSettings.Value'):
        if token in text:
            raise ValueError('Unbound current player-info endpoint: ' + token)
    target = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/PlayerInfoUI.cs'
    raw = ('namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;\n' + clean(text) + '\n}\n').encode('utf8')
    receipt = ROOT / 'assets/reference/partner-player-info-source-manifest.json'
    previous = json.loads(receipt.read_bytes()) if receipt.exists() else {}
    old = json.loads((ROOT / 'assets/reference/profession-ui/player-info-source-manifest.json').read_bytes())
    allowed = {row['runtime']['sha256'] for row in old['ports'] if row['runtime']['path'] == target.relative_to(ROOT).as_posix()}
    allowed.add(previous.get('binding_source_sha256', {}).get(target.relative_to(ROOT).as_posix()))
    if target.exists() and target.read_bytes() != raw and hashlib.sha256(target.read_bytes()).hexdigest() not in allowed:
        raise ValueError('Preserve modified player-info widget')
    save('PlayerInfoUI', source, text)

    def hashes(paths):
        return {path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                for path in sorted(paths)}

    runtime = ROOT / 'unity/Assets/Game/Runtime'
    outputs = [target, runtime / 'Offline/PartnersNpc/OfflinePartnerNpcRuntime.cs',
               runtime / 'Offline/PartnersUI/OfflinePartnerNpcIconBindings.cs']
    validation = [ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionPlayerInfoValidation.cs',
        ROOT / 'scripts/fixtures/partner_npc_icon_validation.cs', ROOT / 'scripts/prepare_partner_npc_icon_validation.py',
        ROOT / 'scripts/prepare_partner_player_info_validation.py', Path(__file__)]
    dependencies = [Path(str(path) + '.meta') for path in outputs + validation if path.suffix == '.cs' and '/unity/' in path.as_posix()]
    dependencies.extend(ROOT / 'scripts' / (name + '.py') for name in ('prepare_unity_profession_player_info',
        'prepare_unity_profession_ui_parts', 'prepare_unity_profession_player_appearance', 'prepare_unity_profession_photographer'))
    dependencies.append(ROOT / 'unity/Assets/Official/Partners/UI/Panel/NpcPanel.prefab')
    dependencies.extend((ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionValidationWorkspace.cs',
        ROOT / 'assets/reference/partner-npc-runtime-source-manifest.json',
        ROOT / 'assets/reference/profession-ui/player-info-source-manifest.json'))
    proof = dict(manifest=selected,
        official_source_sha256=hashes([ROOT / 'official-source.json', source, code / 'common-source/Common/ActorTargetData.cs',
            code / 'common-source/Common/NpcTargetData.cs', code_manifest_path] + assemblies),
        binding_source_sha256=hashes(outputs), validation_source_sha256=hashes(validation),
        dependency_source_sha256=hashes(dependencies),
        operation=[
            'PlayerInfoUI is copied from the selected 884 source; explicit host services replace injection and original opaque protocol/enum adapters remain.',
            'Collection visibility consumes the caller flag; pin mask checks collection reference, flag and target before original team compatibility/cooldown branches.',
            'NPC lite info preserves ServerId read from the actual selected NpcTargetData instance; icon target forwards it without assigning a server.',
            'Other profession widgets retain their separate historical source manifests; this receipt does not relabel them or claim full partner/Windows integration.'
        ])
    receipt.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Ported selected 884 PlayerInfoUI, retaining the separate legacy provenance of other profession widgets')


if __name__ == '__main__':
    main()
