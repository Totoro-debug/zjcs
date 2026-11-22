using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeOutputAddition;

namespace Zjcs.Official.HomeOutputDetailUI
{
    public interface IOutputAdditionDetailServices
    {
        Vector2 PointerPosition { get; }
        List<(List<OutputAdditionInfo> propList, DateTime endTime)> Groups(OutputGroupType type);
        void HookGlobalPointerDown(Action<Vector2> callback);
        void UnhookGlobalPointerDown(Action<Vector2> callback);
        GameObject Instantiate(GameObject prefab, Transform parent);
        string Text(string key);
        void Close(GameObject window);
    }

    // Original LanguageUtils.GetEnumKey joins the type name and enum value.
    public static class OutputDetailLanguage
    {
        public static string EnumKey<T>(T value) where T : Enum => typeof(T).Name + "." + value;
    }
}
