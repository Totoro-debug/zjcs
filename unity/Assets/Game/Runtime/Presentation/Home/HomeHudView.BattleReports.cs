using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        [Serializable] private sealed class ResultReportBindings
        { public ResultReportWindow[] windows; public ReportProfessionIcon[] professionIcons; }
        [Serializable] private sealed class ResultReportWindow
        { public string resource, report, share, save, thumbsUp; }
        [Serializable] private sealed class ReportProfessionIcon
        { public int profession; public string address; }

        private void BindDailyChallengeBattleReport(GameObject result, string resource, FightBattleCheckpoint checkpoint)
        {
            var data = Resources.Load<TextAsset>("Combat/BattleReport/result-report-bindings");
            if (data == null) throw new InvalidOperationException("Original result report bindings are absent.");
            var bindings = JsonUtility.FromJson<ResultReportBindings>(data.text);
            var window = bindings.windows.Single(value => value.resource == resource);
            Button Control(string path)
            {
                if (path == null) return null;
                Transform node = result.transform;
                if (path.Length != 0) foreach (string index in path.Split('/')) node = node.GetChild(int.Parse(index));
                return node.GetComponent<Button>();
            }
            // The legacy activity export still has omitted uGUI scripts. Keep
            // its existing result usable until the original native UI is restored.
            Button reportButton = Control(window.report);
            if (reportButton == null) return;
            OfficialBattleResultReport.Attach(result, new OfficialBattleResultReport.Binding {
                Report = reportButton, PlaybackShare = Control(window.share),
                SaveRecord = Control(window.save), ThumbUp = Control(window.thumbsUp),
                PopupParent = canvas.transform, Checkpoint = checkpoint,
                Presentation = lifetime => {
                    // No skill page is opened. Its existing original item/detail
                    // adapter supplies only the art and read-only skill snapshot.
                    var adapter = lifetime.AddComponent<CharacterSkillPanel>();
                    adapter.Initialize(OfficialCharacterAssetCatalog.LoadDefault(), host.DailyDungeonProgression,
                        host.ActiveProfileSlotId, host.SavesRoot);
                    var presentation = adapter.CreateBattleReportPresentation(lifetime, checkpoint.MaxRound, canvas.transform);
                    var partners = OfficialPartnerAssetCatalog.LoadDefault();
                    var images = Resources.Load<OfflineProfessionImageCatalog>("Partners/OfficialPartnerNpcIconImages");
                    if (images == null) throw new InvalidOperationException("Original NPC report icons are absent.");
                    Sprite Image(string address) => images.Entries.Single(value => value.Path == address).Asset as Sprite
                        ?? throw new InvalidOperationException("Original report sprite is unbound: " + address);
                    System.Collections.Generic.Dictionary<string, object> Npc(int id) {
                        var unit = checkpoint.Units.Single(value => value.Id == id);
                        return partners.Rows("npc").Single(value => Convert.ToInt32(value["Id"], CultureInfo.InvariantCulture) == unit.NpcClassId);
                    }
                    string Name(int id) {
                        if (id == 1) return host.Profile.Save.Player.CharacterName;
                        string key = Convert.ToString(Npc(id)["NameKey"], CultureInfo.InvariantCulture);
                        return host.Catalog.LocalizedText(key, null)
                            ?? throw new InvalidOperationException("Original report NPC name is unbound: " + key);
                    }
                    presentation.PlayerName = Name;
                    presentation.PlayerIcon = (icon, id) => {
                        if (id == 1) { adapter.BindReportPlayerIcon(icon); return; }
                        var unit = checkpoint.Units.Single(value => value.Id == id);
                        var npc = Npc(id);
                        OfficialBattleReportIdentityView.Bind(icon, new OfficialBattleReportIdentityView.Identity {
                            Name = Name(id), IsNpc = true, Profession = unit.Profession,
                            NpcIcon = Image(Convert.ToString(npc["Icon"], CultureInfo.InvariantCulture)),
                            ProfessionIcon = unit.Profession == 0 ? null : Image(bindings.professionIcons
                                .Single(value => value.profession == unit.Profession).address)
                        });
                    };
                    return presentation;
                }
            });
        }
    }
}
