using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor
{
    public static class ValidationGameView
    {
        // Only changes the owned Editor view; does not focus it or resize a display.
        public static void Portrait(int width = 675, int height = 1463)
        {
            if (Application.isBatchMode)
                throw new InvalidOperationException("Native frame-end UI verification requires a rendered Editor view.");
            var assembly = typeof(EditorWindow).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes,
                new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType", true), "Standalone") });
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            int selected = -1;
            for (int index = 0; index < count; index++)
            {
                var size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { index });
                var type = size.GetType();
                if ((int)type.GetProperty("width").GetValue(size) == width
                    && (int)type.GetProperty("height").GetValue(size) == height)
                { selected = index; break; }
            }
            if (selected < 0)
            {
                var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize", true),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new object[] { Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType", true), 1),
                        width, height, "Building verification" }, null);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                selected = count;
            }
            var viewType = assembly.GetType("UnityEditor.GameView", true);
            var view = EditorWindow.GetWindow(viewType, false, "Game", false);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(view, selected);
            view.Repaint();
        }
    }
}
