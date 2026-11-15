#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeUI;
using Zjcs.Presentation.Home;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class HomeNativePlayerValidation
{
    private const string Active = "Zjcs.HomeNativePlayer.Active";
    static HomeNativePlayerValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HomeNativePlayerFixture").AddComponent<HomeNativePlayerRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Home facility host validation timed out."));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar)
            && !string.Equals(Path.GetFullPath(Application.dataPath), Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"), "unity/Assets")), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Owned validation project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 900);
        ConfigureNativeFrame();
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_NATIVE_PLAYER_PASSED: formal home native player panel, portrait pointer, XP animation, notification and saved restoration.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed partial class HomeNativePlayerRunner : MonoBehaviour
{
    private HomeFeatureRuntime home;
    private string root, slot;
    private int errors;
    private IEnumerator Start()
    {
        Application.runInBackground = true;
        AudioListener.volume = 0;
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++; };
        var steps = new Stack<IEnumerator>(); steps.Push(Check());
        while (true)
        {
            object current;
            try { if (!steps.Peek().MoveNext()) { steps.Pop(); if (steps.Count == 0) break; continue; } current = steps.Peek().Current; }
            catch (Exception error) { home?.Close(); HomeNativePlayerValidation.Finish(error); yield break; }
            if (current is IEnumerator nested) { steps.Push(nested); continue; }
            yield return current;
        }
        home.Close();
        HomeNativePlayerValidation.Finish(errors == 0 ? null : new Exception("Unexpected home host errors: " + errors));
    }
    private IEnumerator Check()
    {
        root = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "home-host-saves-" + Guid.NewGuid().ToString("N"));
        var creation = CharacterCreationOfflineService.OpenDefault(root);
        slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot); draft.name = "家园测试";
        Require(creation.TryCreateCharacter(slot, draft, out string error), error);
        Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out error), error);
        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        var character = OfficialCharacterAssetCatalog.LoadDefault().Load();
        Require(profile.ApplyCharacterMutation(player => { player.Level = 3; player.Experience = character.CumulativeLevelExperience[3]; player.SubRank = 1; }, "host-level").IsApplied, "Seed real level 3 profile");
        Open();
        yield return new WaitForSeconds(1);
        yield return CheckFormalPlayer();
    }
}

#endif
