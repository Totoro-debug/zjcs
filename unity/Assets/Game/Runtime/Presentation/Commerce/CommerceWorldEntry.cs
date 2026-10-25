using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zjcs.Presentation.Commerce
{
    public sealed partial class CommerceScreenController
    {
        private long fixedExchangeCount;
        private bool worldExchangeOnly;
        private Func<Transform, GameObject> worldExchangeFactory;

        // PayMoneyChangeDialog.RefreshMax(count > 0) keeps the requested shortage
        // even when the current balance cannot cover it, and hides its slider.
        public static CommerceScreenController OpenMoneyExchange(string slotId, Transform parent,
            long shortage, Action closed, string persistentRoot, Func<Transform, GameObject> exchangeFactory)
        {
            if (shortage <= 0) throw new ArgumentOutOfRangeException(nameof(shortage));
            if (!Zjcs.Offline.State.ProfileSlotCatalog.TryParseSlotId(slotId))
                throw new ArgumentException("An existing profile slot is required.", nameof(slotId));
            var host = new GameObject("CommerceWorldExchange");
            host.transform.SetParent(parent, false);
            var screen = host.AddComponent<CommerceScreenController>();
            screen.closedCallback = closed;
            try
            {
                screen.CreateCanvas();
                if (FindObjectOfType<EventSystem>() == null)
                    new GameObject("Commerce Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                screen.audioSource = host.AddComponent<AudioSource>();
                screen.audioSource.playOnAwake = false;
                screen.runtime = host.AddComponent<CommerceFeatureRuntime>();
                screen.runtime.PersistentRoot = persistentRoot;
                screen.runtime.OpenProfile(slotId);
                screen.fixedExchangeCount = shortage;
                screen.worldExchangeOnly = true;
                screen.worldExchangeFactory = exchangeFactory ?? throw new ArgumentNullException(nameof(exchangeFactory));
                screen.OpenExchange();
                screen.SetExchangeAmount(shortage);
                return screen;
            }
            catch { Destroy(host); throw; }
        }

        private void LateUpdate()
        {
            if (worldExchangeOnly && runtime != null && dialogStack.All(value => value == null)) Close();
        }

        private void OpenDirectPayFromExchange()
        {
            worldExchangeOnly = false;
            fixedExchangeCount = 0;
            if (mainWindow == null) BuildMainWindow();
            else mainWindow.gameObject.SetActive(true);
            CloseDialogKind(DialogKind.Exchange);
            SetMainTab(MainTab.DirectPay);
            SwitchPayTab(true);
        }

        private void RefreshFixedExchange(Transform dialog)
        {
            if (fixedExchangeCount <= 0 || dialog == null) return;
            Find(dialog, "BgDialog1/Content/ChangGo")?.gameObject.SetActive(false);
            Transform fixedText = Find(dialog, "BgDialog1/Content/TipExchangeTxt");
            if (fixedText == null) throw new InvalidOperationException("Official fixed exchange label is unbound.");
            fixedText.gameObject.SetActive(true);
            SetText(dialog, "BgDialog1/BgImg/BgTitle/TitleTxt", runtime.Catalog.Text("UI_Pay_ChangeTitle"));
            SetText(dialog, "BgDialog1/Content/TipExchangeTxt", string.Format(CultureInfo.InvariantCulture,
                runtime.Catalog.Text("UI_Pay_Exchange_Diamond_Confirm"), fixedExchangeCount));
        }
    }
}
