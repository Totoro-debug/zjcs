using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    // GameEndPanelBase's report action, with local result data and explicit
    // appearance adapters replacing the online result/provider dependencies.
    public sealed class OfficialBattleResultReport : MonoBehaviour
    {
        public sealed class Binding
        {
            public Button Report, PlaybackShare, SaveRecord, ThumbUp;
            public Transform PopupParent;
            public FightBattleCheckpoint Checkpoint;
            public FightBattleReport FinalReport;
            public Func<GameObject, OfficialBattleReportView.Presentation> Presentation;
            public bool FastVictory;
            // Native GameEndPanelBase owns its button subscription. Other
            // original result adapters can bind the same report action directly.
            public bool BindReportButton = true;
        }

        private Binding binding;
        private FightBattleCheckpoint checkpoint;
        private FightBattleReport finalReport;
        private OfficialBattleReportView report;
        private GameObject lifetime;
        private Action closed;
        private bool subscribed;

        public bool IsOpen => report != null;
        public FightBattleReport FinalReport => finalReport;

        public static OfficialBattleResultReport Attach(GameObject owner, Binding binding)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (binding == null || binding.Report == null || binding.PopupParent == null || binding.Presentation == null)
                throw new ArgumentException("Native result report controls and presentation are required.", nameof(binding));
            var view = owner.GetComponent<OfficialBattleResultReport>() ?? owner.AddComponent<OfficialBattleResultReport>();
            view.Subscribe(false);
            view.Close();
            view.binding = binding;
            bool available = binding.Checkpoint != null && binding.Checkpoint.Finished &&
                binding.Checkpoint.Outcome != null && !binding.Checkpoint.Outcome.Invalid && !binding.FastVictory;
            // A report survives the runner's disposal and must not observe later
            // mutations made while the host starts another battle.
            view.checkpoint = available ? FightBattleCheckpointSerializer.Load(
                FightBattleCheckpointSerializer.Save(binding.Checkpoint)) : null;
            view.finalReport = available
                ? UnityEngine.JsonUtility.FromJson<FightBattleReport>(UnityEngine.JsonUtility.ToJson(binding.FinalReport
                    ?? FightBattleReportBuilder.Build(binding.Checkpoint))) : null;
            binding.Report.gameObject.SetActive(available);
            // Existing offline choice: no online record/share/thumbs-up actions.
            foreach (Button button in new[] { binding.PlaybackShare, binding.SaveRecord, binding.ThumbUp })
                if (button != null) button.gameObject.SetActive(false);
            view.Subscribe(view.isActiveAndEnabled);
            return view;
        }

        private void OnEnable() { Subscribe(true); }
        private void OnDisable() { Subscribe(false); Close(); }
        private void OnDestroy() { Subscribe(false); Close(); }

        private void Subscribe(bool enabled)
        {
            if (binding == null || binding.Report == null) return;
            enabled &= binding.BindReportButton;
            if (subscribed == enabled) return;
            if (enabled) binding.Report.onClick.AddListener(Open);
            else binding.Report.onClick.RemoveListener(Open);
            subscribed = enabled;
        }

        public void Open()
        {
            if (report != null || checkpoint == null || !isActiveAndEnabled) return;
            lifetime = new GameObject("BattleResultReportLifetime");
            lifetime.transform.SetParent(transform, false);
            try
            {
                var presentation = binding.Presentation(lifetime)
                    ?? throw new InvalidOperationException("Battle report presentation is absent.");
                closed = presentation.Closed;
                presentation.Closed = Close;
                report = new OfficialBattleReportView(binding.PopupParent, checkpoint, finalReport, presentation);
            }
            catch { Close(); throw; }
        }

        public void Close()
        {
            var previous = report;
            var released = lifetime;
            Action callback = closed;
            report = null;
            lifetime = null;
            closed = null;
            previous?.Dispose();
            try { callback?.Invoke(); }
            finally { if (released != null) Destroy(released); }
        }
    }
}
