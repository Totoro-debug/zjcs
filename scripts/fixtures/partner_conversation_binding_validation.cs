using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;

[InitializeOnLoad]
public static class PartnerConversationBindingValidation
{
    const string Key = "PartnerConversationBindingValidation.Active";
    static PartnerConversationBindingValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original conversation configuration binding").AddComponent<PartnerConversationBindingRunner>();
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) ||
            !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "conversation-binding-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerConversationBindingRunner : MonoBehaviour
{
    sealed class ConditionBoundary : IPartnerConversationCondition
    {
        public bool IsMet(IPartnerConversationPlayer player) =>
            throw new InvalidOperationException("Configuration binding fixture does not execute player conditions.");
    }
    void Start()
    {
        try
        {
            int conditions = 0;
            var catalog = OfficialPartnerConversationAssetCatalog.LoadDefault().Load(value => {
                if (!value.ContainsKey("_type")) throw new Exception("Original condition identity is missing");
                conditions++; return new ConditionBoundary();
            });
            if (catalog.Nodes.Count() != 6707 || catalog.Nodes.Select(x => x.ClassId).Distinct().Count() != 349 ||
                conditions != 6912 || catalog.Get(2021, 1) == null || catalog.Get(-1, 1) != null)
                throw new Exception("Original conversation Resources payload is incomplete");
            Debug.Log("PARTNER_CONVERSATION_BINDING_PASSED: actual Resources and typed original node table in Play Mode.");
            PartnerConversationBindingValidation.Finish(null);
        }
        catch (Exception error) { PartnerConversationBindingValidation.Finish(error); }
    }
}
