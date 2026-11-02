"""Exercise automatic saved conversations at the actual original window's Open."""
import hashlib
import json
from pathlib import Path

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
        session.EnsureLocalActorIdentities();
        playerId = session.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId;
        npcId = session.Save.Player.CharacterProperties.LocalActorIdentities.NpcId(8402);
        Require(session.Save.Player.CharacterProperties.NpcConversations.Records.Count == 0,
            "Creating/loading the actual character does not create conversations");
        presentation = new Presentation();
        opening = new OfflinePartnerConversationUiSession(session, character, configuration, new WorldBoundary(),
            () => new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc), () => false, 8,
            presentation, new PlaybackBoundary(), () => commits++);
        dialogues = opening.Source;
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
        ROOT / 'scripts/fixtures/partner_panel_host_validation.cs']
    (work / 'conversation-window-inputs.json').write_text(json.dumps({
        str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in files},
        indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared original full window with production opening transactions and actual local actor identities', flush=True)


if __name__ == '__main__':
    main()
