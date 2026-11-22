using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.HomeOutputDetailUI;

namespace Zjcs.Editor
{
    public static class HomeOutputDetailValidation
    {
        public static void Validate()
        {
            var assets = OfficialHomeOutputDetailAssets.LoadDefault();
            var popup = assets.popup.GetComponent<PopTipOutputAddition>();
            if (!popup) throw new InvalidOperationException("Original output-detail controller did not import.");
            var fields = new SerializedObject(popup);
            foreach (string field in new[] { "mScroll", "mRect", "mBgImage" })
                if (!fields.FindProperty(field).objectReferenceValue)
                    throw new InvalidOperationException("Original output-detail reference is unbound: " + field);
            var scroll = (Component)fields.FindProperty("mScroll").objectReferenceValue;
            var scrollFields = new SerializedObject(scroll);
            var template = scrollFields.FindProperty("DefaultPrefabSource.Prefab").objectReferenceValue as GameObject;
            if (!template || !template.GetComponent<OutputAdditionItem>())
                throw new InvalidOperationException("The original scroll row template is unbound.");
            var row = new SerializedObject(template.GetComponent<OutputAdditionItem>());
            var property = row.FindProperty("mPropItemPrefab").objectReferenceValue as OutputAdditionPropItem;
            if (!property) throw new InvalidOperationException("The original property row template is unbound.");
            foreach (var root in new[] { assets.popup, template, property.gameObject })
                if (root.GetComponentsInChildren<Component>(true).Any(value => !value))
                    throw new InvalidOperationException("A native output-detail component is missing: " + root.name);
            if (assets.Text("time_hour") == "time_hour") throw new InvalidOperationException("Original countdown text is unbound.");
            Debug.Log("HOME_OUTPUT_DETAIL_IMPORT_PASSED: original popup, pooled rows, property template and countdown text.");
        }
    }
}
