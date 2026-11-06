"""Exercise automatic saved conversations at the actual original window's Open."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from official_apk import ROOT
from prepare_partner_npc_shared_validation import main as prepare_shared
from prepare_partner_npc_icon_validation import main as prepare_icons
from validation_workspace import validation_directory


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise ValueError('Review original window fixture anchor: ' + old)
    return text.replace(old, new)


def main():
    work = validation_directory()
    if not (work / 'npc-shared-inputs.json').exists():
        prepare_shared()
    prepare_icons()
    preparation_config = ROOT / 'unity/Assets/Game/Resources/Map16/world-alone-configuration.json'
    preparation_project_path = Path('Assets/Game/Resources/Map16')
    preparation_target = work / 'star-god-unity' / preparation_project_path
    preparation_target.mkdir(parents=True, exist_ok=True)
    for suffix in ('', '.meta'):
        source = Path(str(preparation_config) + suffix)
        shutil.copy2(source, preparation_target / source.name)
    gift_configuration_inputs = []
    gift_manifest_inputs = []
    gift_asset_records = {}
    for manifest_name, output_field in (
            ('partner-gift-configuration-source-manifest.json', 'outputs'),
            ('partner-gift-controller-source-manifest.json', 'installed_output_sha256'),
            ('partner-gift-presentation-assets-source-manifest.json', 'outputs')):
        manifest_path = ROOT / 'assets/reference' / manifest_name
        manifest = json.loads(manifest_path.read_bytes())
        gift_manifest_inputs.append(manifest_path)
        entries = dict(manifest[output_field])
        if manifest_name == 'partner-gift-controller-source-manifest.json':
            entries.update(manifest.get('reused_output_sha256', {}))
        if manifest_name == 'partner-gift-presentation-assets-source-manifest.json':
            entries.update(manifest.get('reused', {}))
        for relative, expected in entries.items():
            if relative.startswith('unity/Assets/Game/Runtime/'):
                continue
            gift_asset_records.setdefault(relative, set()).add(expected)
    tipdot_manifest_path = ROOT / 'assets/reference/partner-tipdot-catalog-source-manifest.json'
    tipdot_manifest = json.loads(tipdot_manifest_path.read_bytes())
    gift_manifest_inputs.append(tipdot_manifest_path)
    for relative, expected in tipdot_manifest['outputs'].items():
        if relative.startswith('unity/Assets/Game/Runtime/'):
            continue
        gift_asset_records.setdefault(relative, set()).add(expected)
    for relative, expected in tipdot_manifest['reused_original_graphs'].items():
        gift_asset_records.setdefault(relative, set()).add(expected)
    conversation_world_inputs = [
        ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerConversationWorldAssetCatalog.asset',
        ROOT / 'unity/Assets/Official/Partners/partner-conversation-world.json',
    ]
    for source in conversation_world_inputs:
        for path in (source, Path(str(source) + '.meta')):
            target = work / 'star-god-unity' / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)

    verified_gift_assets = {}
    discrepancies = []
    crlf_head_matches = []
    for relative, expected_hashes in sorted(gift_asset_records.items()):
        source = ROOT / relative
        current = source.read_bytes()
        current_hash = hashlib.sha256(current).hexdigest()
        expected_hashes = sorted(expected_hashes)
        if all(current_hash == expected for expected in expected_hashes):
            verified_gift_assets[relative] = current
            continue
        tracked = subprocess.run(['git', 'show', 'HEAD:' + relative], cwd=ROOT,
            check=True, capture_output=True).stdout
        tracked_hash = hashlib.sha256(tracked).hexdigest()
        if (all(tracked_hash == expected for expected in expected_hashes)
                and current.splitlines() == tracked.splitlines()):
            verified_gift_assets[relative] = tracked
            crlf_head_matches.append(dict(path=relative, working_sha256=current_hash,
                head_sha256=tracked_hash, expected_sha256=expected_hashes))
            continue
        discrepancies.append(dict(path=relative, working_sha256=current_hash,
            head_sha256=tracked_hash, expected_sha256=expected_hashes,
            line_equivalent=current.splitlines() == tracked.splitlines()))
    if discrepancies:
        details = json.dumps(dict(discrepancies=discrepancies,
            accepted_crlf_head_matches=crlf_head_matches), ensure_ascii=False, indent=2)
        raise ValueError('Partner gift source verification found mismatches:\n' + details)

    for relative, payload in verified_gift_assets.items():
        source = ROOT / relative
        target = work / 'star-god-unity' / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(payload)
        gift_configuration_inputs.append(source)
    print('Verified partner gift assets:', len(verified_gift_assets),
        'accepted HEAD byte substitutions for CRLF-only working-tree differences:', len(crlf_head_matches), flush=True)
    for match in crlf_head_matches:
        print('Accepted CRLF HEAD bytes:', json.dumps(match, ensure_ascii=False, sort_keys=True), flush=True)
    for relative in (
            'unity/Assets/Game/Resources/Partners/OfficialPartnerAssetCatalog.asset',
            'unity/Assets/Official/Partners/partner-rules.json',
            'unity/Assets/Official/Partners/partner-properties.json'):
        source = ROOT / relative
        for path in (source, Path(str(source) + '.meta')):
            target = work / 'star-god-unity' / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
            gift_configuration_inputs.append(path)
    editor = work / 'star-god-unity/Assets/Editor'
    definition = editor / 'Validation.asmdef'
    assembly = json.loads(definition.read_bytes())
    assembly['references'] = list(dict.fromkeys(assembly['references'] + ['Zjcs.Official.PartnerDialoguePlaybackUI']))
    definition.write_text(json.dumps(assembly, indent=2) + '\n', 'utf8', newline='\n')
    helper = editor / 'PartnerNpcIconValidation.cs'
    text = helper.read_text('utf8')
    start = text.index('        var player = new OfflinePartnerConversationPlayer(')
    end = text.index('\n    public static void CheckFolded', start)
    text = text[:start] + '''        Require(session.ApplyCharacterMutation(value =>
            value.CharacterProperties.NpcFriendships.Single(row => row.NpcClassId == 8402).Level = 40).IsApplied,
            "Commit the actual original friendship unlock condition");
        Require(session.Save.Player.CharacterProperties.NpcConversations.Records.Count == 0,
            "Creating/loading the actual character does not create conversations");
        presentation = new Presentation();
        opening = OfflinePartnerConversationUiSessionFactory.Create(session, character, catalog,
            () => new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc), () => false, 8,
            presentation, new PlaybackBoundary(), () => commits++);
        dialogues = opening.Source;
        playerId = session.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId;
        npcId = session.Save.Player.CharacterProperties.LocalActorIdentities.NpcId(8402);
        Require(dialogues.Dialogues.Count == 0 && commits == 0, "Constructing the UI host does not create conversations");
        return opening.Playback;
    }
    static OfflinePartnerConversationUiSession opening;
    static long playerId, npcId, dialogueId;
    static int commits, opens;
    public static void BindOpening(PartnerPanelContext context)
    {
        opening.Bind(context); opening.Bind(context);
        var prepare = context.PrepareOpen;
        context.PrepareOpen = () => { opens++; prepare(); };
    }
    public static void CheckOpeningLifecycle()
    {
        session.Reload();
        Require(opens == 3 && commits == 1 && session.Save.Player.CharacterProperties.NpcConversations.Records.Count == 1,
            "Repeated actual window opens retain one atomic creation without duplicate records or rewards");
    }
    sealed class PlaybackBoundary : IOfflinePartnerDialoguePlaybackPresentation
    {
        public string GetNpcNameKey(int id) => throw Outside();
        public void InitializeActorIcon(Component icon, long id, PartnerDialogueSpokesMan speaker) => throw Outside();
        public ItemIcon GetRewardIcon(int id, Transform parent) => throw Outside();
        public void RecycleRewardIcon(ItemIcon icon) => throw Outside();
        public void ShowAcknowledgeFailure(ProgressionResult result, bool byPlayer) => throw Outside();
    }
''' + text[end:]
    text = replace_once(text,
        '        var row = root.GetComponentsInChildren<NpcPanelDialogueListNpcItem>().Single();',
        '''        Require(dialogues.Dialogues.Count == 1 && dialogues.Npcs.Count == 1 && commits == 1,
            "Actual original window open creates the unlocked saved conversation before controls subscribe");
        var record = session.Save.Player.CharacterProperties.NpcConversations.Records.Single();
        Require(record.DialogueClassId == 2021 && record.PlayerId == playerId && record.NpcId == npcId,
            "Opening uses original NPC conversation order, unlock conditions and persisted local actor mappings");
        dialogueId = record.DialogueId;
        var row = root.GetComponentsInChildren<NpcPanelDialogueListNpcItem>().Single();''')
    for old, new in [('info.Id == 8001', 'info.Id == npcId'),
                     ('selected == 9001', 'selected == dialogueId'),
                     ('dialogues.Dialogues[9001]', 'dialogues.Dialogues[dialogueId]'),
                     ('CreateLiteInfo(8001,', 'CreateLiteInfo(npcId,'),
                     ('CreateLiteInfo(8002,', 'CreateLiteInfo(npcId + 1,'),
                     (', 7001,', ', playerId,'),
                     ('MainPlayerId => 7001', 'MainPlayerId => playerId'),
                     ('target.TargetId == 8001', 'target.TargetId == npcId')]:
        if old not in text:
            raise ValueError('Review original actor fixture anchor: ' + old)
        text = text.replace(old, new)
    helper.write_text(text, 'utf8', newline='\n')
    fixture = editor / 'PartnerPanelHostValidation.cs'
    text = fixture.read_text('utf8')
    text = replace_once(text, '        view.Open(OfficialPartnerPanelAssetCatalog.Load(), context);',
        '        PartnerNpcIconValidation.BindOpening(context);\n        view.Open(OfficialPartnerPanelAssetCatalog.Load(), context);')
    text = replace_once(text, '        Require(view.Window.State == WorldPopupWindow.EState.Show, "Original show clip completed");',
        '''        Require(view.Window.State == WorldPopupWindow.EState.Show, "Original show clip completed");
        bool duplicateOpenRejected = false;
        try { view.Open(OfficialPartnerPanelAssetCatalog.Load(), context); }
        catch (InvalidOperationException) { duplicateOpenRejected = true; }
        Require(duplicateOpenRejected, "An already open original window cannot run another opening transaction");''')
    text = replace_once(text, '        services.Dispose(); icons.Dispose();',
        '        PartnerNpcIconValidation.CheckOpeningLifecycle();\n        services.Dispose(); icons.Dispose();')
    fixture.write_text(text, 'utf8', newline='\n')
    files = [Path(__file__), ROOT / 'scripts/fixtures/partner_npc_icon_validation.cs',
        ROOT / 'scripts/fixtures/partner_panel_host_validation.cs', preparation_config,
        Path(str(preparation_config) + '.meta')]
    files.extend(gift_configuration_inputs + gift_manifest_inputs +
        [path for source in conversation_world_inputs for path in (source, Path(str(source) + '.meta'))])
    (work / 'conversation-window-inputs.json').write_text(json.dumps({
        str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in files},
        indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared original full window with production opening transactions and actual local actor identities', flush=True)


if __name__ == '__main__':
    main()
