using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        private static IEnumerator VerifyDialogueInputSteps()
        {
            Application.runInBackground = true;
            foreach (MonoBehaviour startup in Object.FindObjectsOfType<MonoBehaviour>()
                .Where(value => value.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            {
                startup.StopAllCoroutines();
                Object.Destroy(startup.gameObject);
            }
            SetUiGameViewSize(450, 800);
            yield return null;
            if (EventSystem.current == null) new GameObject("Map12DialogueInput", typeof(EventSystem));
            var parent = new GameObject("Map12DialogueFixture", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            parent.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            int mapId = UnityEditor.SessionState.GetInt(Key + ".uiMap", 12);
            var ui = new OfficialWorldUiRenderer(OfficialMap10UiCatalog.LoadDefault());
            ui.Mount(parent.transform, mapClassId: mapId);
            yield return new WaitForSecondsRealtime(.5f);
            ui.Dispose();
            if (parent.GetComponent<CanvasScaler>().uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize)
                throw new System.IO.InvalidDataException("Closing native dialogue did not restore its host scale mode.");
            yield return null;
            ui = new OfficialWorldUiRenderer(OfficialMap10UiCatalog.LoadDefault());
            ui.Mount(parent.transform, mapClassId: mapId);
            yield return new WaitForSecondsRealtime(.5f);
            ui.SetPanelActive("OptionTalkDialog", true);
            ui.SetOptionChoicesVisible(false);
            ui.SetNodeActive("OptionTalkDialog", "/OptionTalkDialog/NextBtn", true);
            System.Type portraitType = typeof(OfficialMainlineMapRuntime).Assembly.GetType(
                "Zjcs.Presentation.World.OfficialMap10DialoguePortraitView");
            object portraits = System.Activator.CreateInstance(portraitType, ui, "OptionTalkDialog", mapId);
            portraitType.GetMethod("SetSpeaker").Invoke(portraits, new object[]
            {
                new Map10RuntimeDialogRole { RoleId = 8402, PortraitPath = "UI/Portrait/Npc_Portrait_8402" },
                "", null
            });
            Button next = ui.Find("OptionTalkDialog", "/OptionTalkDialog/NextBtn").GetComponent<Button>();
            int clicks = 0;
            next.onClick.AddListener(() => clicks++);
            // CanvasRenderer receives its depth during the first actual render.
            // ForceUpdateCanvases alone cannot make an unrendered button clickable.
            yield return new WaitForEndOfFrame();
            CanvasScaler scaler = parent.GetComponent<CanvasScaler>();
            float expectedScale = Mathf.Min(Screen.width / scaler.referenceResolution.x,
                Screen.height / scaler.referenceResolution.y);
            if (Mathf.Abs(parent.GetComponent<Canvas>().scaleFactor - expectedScale) > .001f)
                throw new System.IO.InvalidDataException("Native dialogue inherited a different scale from the official CanvasScaler.");
            SimulateUiClick(next, null);
            if (clicks != 1) throw new System.IO.InvalidDataException("Original dialogue pointer callback did not run once.");
            ((System.IDisposable)portraits).Dispose();
            ui.Dispose();
            Object.Destroy(parent);
        }
    }
}
