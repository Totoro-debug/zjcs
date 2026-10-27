using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionWindowService
    {
        void CloseLast();
        void Close(GameObject window);
        void ObserveDestroyed(WorldPopupWindow window, Action callback);
        void StopObservingDestroyed(WorldPopupWindow window, Action callback);
    }
    public sealed class CloseWindowBtn : MonoBehaviour
    {
        public enum CloseWindowType { Parent, Specified, Last }
        public Button myButton;
        public CloseWindowType CloseType;
        public WorldPopupWindow mySpecifiedWindow;
        private WorldPopupWindow myCloseWindow;
        private IProfessionWindowService windows;
        private void Reset() => myButton = GetComponent<Button>();
        private void Awake()
        {
            myButton.onClick.AddListener(HandleOnButtonClick);
            if (myCloseWindow == null)
            {
                switch (CloseType)
                {
                    case CloseWindowType.Parent: myCloseWindow = GetComponentInParent<WorldPopupWindow>(); break;
                    case CloseWindowType.Specified: myCloseWindow = mySpecifiedWindow; break;
                }
            }
        }
        public void Init(IProfessionWindowService value)
        {
            windows = value ?? throw new ArgumentNullException(nameof(value));
            if (myCloseWindow != null) windows.ObserveDestroyed(myCloseWindow, PopupWindow_OnWindowDestory);
        }
        private void HandleOnButtonClick()
        {
            switch (CloseType)
            {
                case CloseWindowType.Last: windows.CloseLast(); break;
                case CloseWindowType.Parent:
                case CloseWindowType.Specified: windows.Close(myCloseWindow.gameObject); break;
            }
        }
        public void SetSpecifiedWindow(WorldPopupWindow window)
        {
            if (myCloseWindow != null) windows.StopObservingDestroyed(myCloseWindow, PopupWindow_OnWindowDestory);
            myCloseWindow = window;
            windows.ObserveDestroyed(myCloseWindow, PopupWindow_OnWindowDestory);
            CloseType = CloseWindowType.Specified;
        }
        private void PopupWindow_OnWindowDestory()
        {
            if (myCloseWindow != null) windows.StopObservingDestroyed(myCloseWindow, PopupWindow_OnWindowDestory);
            myCloseWindow = null;
        }
        private void OnDestroy()
        {
            if (windows != null && myCloseWindow != null)
                windows.StopObservingDestroyed(myCloseWindow, PopupWindow_OnWindowDestory);
        }
    }
}
