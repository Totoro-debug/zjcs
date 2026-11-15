using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.StoryUI
{
    public static class DialogueBackgroundAcceptance
    {
        private const string Key = "Zjcs.DialogueBackgroundAcceptance";
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            SessionState.SetBool(Key, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            int result = 1;
            try
            {
                DialogueBackgroundProbe.Verify();
                result = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(result);
        }

    }
}
