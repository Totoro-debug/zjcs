using System;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Zjcs.Editor.Combat;

public static class CurrentNativeImageEditorValidation
{
    [Serializable] sealed class Result { public bool passed; public string error; public int nativeSprites, aliases, rejections; }
    public static void Run()
    {
        var result = new Result();
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        try
        {
            var images = OfficialCurrentNativeImageValidation.ValidateAll();
            result.nativeSprites = images.ByPath.Count; result.aliases = images.ByAlias.Count;
            if (result.nativeSprites != 772 || result.aliases != 691) throw new InvalidDataException("Current native image scope changed.");
            OfficialBattleHudSpriteTools.Validate();
            OfficialBattleNpcPortraitTools.Validate();
            string source = File.ReadAllText(OfficialCurrentNativeImageValidation.HudReceipt);
            Reject(work, ReplaceOnce(source, "(\\\"outputs\\\"\\s*:\\s*\\[\\s*\\{[\\s\\S]*?\\\"sha256\\\"\\s*:\\s*\\\")[a-f0-9]{64}",
                match => match.Groups[1].Value + new string('0', 64), 1), "bytes changed", ref result.rejections);
            Reject(work, ReplaceOnce(source, "(\\\"expected\\\"\\s*:\\s*\\{[\\s\\S]*?\\\"rect\\\"\\s*:\\s*\\{[\\s\\S]*?\\\"width\\\"\\s*:\\s*)([-+eE0-9.]+)",
                match => match.Groups[1].Value + (float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture), 1),
                "rect width", ref result.rejections);
            Reject(work, ReplaceOnce(source, "(\\\"original_fields\\\"\\s*:\\s*\\{[\\s\\S]*?\\\"m_TextureSettings\\\"\\s*:\\s*\\{[\\s\\S]*?\\\"m_FilterMode\\\"\\s*:\\s*)-?[0-9]+",
                match => match.Groups[1].Value + "99", 1), "sampler changed", ref result.rejections);
            Reject(work, ReplaceOnce(source, "(\\\"pointer\\\"\\s*:\\s*\\{\\s*\\\"fileID\\\"\\s*:\\s*)21300000",
                match => match.Groups[1].Value + "0", 1), "HUD alias changed", ref result.rejections);
            result.passed = true;
            Debug.Log("Current native image editor validation passed: " + result.nativeSprites + " native Sprites, "
                + result.aliases + " real HUD aliases, both production validators and four corruption rejections.");
        }
        catch (Exception exception) { result.error = exception.ToString(); Debug.LogException(exception); }
        File.WriteAllText(Path.Combine(work, "native-image-editor-result.json"), JsonUtility.ToJson(result, true));
        EditorApplication.Exit(result.passed ? 0 : 1);
    }
    static string ReplaceOnce(string source, string pattern, MatchEvaluator replace, int count) => new Regex(pattern).Replace(source, replace, count);
    static void Reject(string work, string text, string expected, ref int count)
    {
        string path = Path.Combine(work, "invalid-image-proof.json"); File.WriteAllText(path, text);
        try
        {
            try { OfficialCurrentNativeImageValidation.Validate(path); }
            catch (InvalidDataException exception)
            {
                if (exception.Message.Contains(expected)) { count++; return; }
                throw new InvalidDataException("Wrong corruption rejection: " + expected, exception);
            }
            throw new InvalidDataException("A corrupted image proof was accepted: " + expected);
        }
        finally { File.Delete(path); }
    }
}
