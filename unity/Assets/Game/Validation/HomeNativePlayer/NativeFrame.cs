#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
public static partial class HomeNativePlayerValidation
{
    private static void ConfigureNativeFrame()
    {
        if (Application.isBatchMode)
            throw new InvalidOperationException("Original frame-end text alignment and blur require an editor frame outside batch mode.");
        var editor = typeof(UnityEditor.EditorWindow).Assembly;
        var sizesType = editor.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes,
            new[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
        var size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1), 608, 1080, "Home income fixture" }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
        var viewType = editor.GetType("UnityEditor.GameView");
        // Only the owned editor's internal view changes; never focus a window.
        var view = UnityEditor.EditorWindow.GetWindow(viewType, false, "Game", false);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
        view.Repaint();
    }
}


#endif
