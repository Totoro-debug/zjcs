using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Presentation.Character;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private SkillDamageTestBattleRunner skillDamageTest;
        internal bool SkillDamageTestRunning => skillDamageTest != null;
        private readonly List<GameObject> damageHiddenHomeUi = new List<GameObject>();

        private void SetDamageTestHomeUiVisible(bool visible)
        {
            if (!visible) {
                damageHiddenHomeUi.Clear();
                foreach (Transform child in CanvasTransform)
                    if (child.gameObject.activeSelf) {
                        damageHiddenHomeUi.Add(child.gameObject);
                        child.gameObject.SetActive(false);
                    }
            } else {
                foreach (GameObject root in damageHiddenHomeUi) if (root != null) root.SetActive(true);
                damageHiddenHomeUi.Clear();
            }
        }

        private void OnSkillDamageTestRequested(OfficialSkillDamageTestRequest request)
        {
            if (skillDamageTest != null) return;
            // Home reads the skill plan committed by the character session.
            // A damage test never grants awards or changes player combat HP.
            host.Profile.Reload();
            characterRoot.SetActive(false);
            SetDamageTestHomeUiVisible(false);
            skillDamageTest = host.gameObject.AddComponent<SkillDamageTestBattleRunner>();
            skillDamageTest.Left += () => {
                skillDamageTest = null;
                SetDamageTestHomeUiVisible(true);
                characterRoot.SetActive(true);
                characterRuntime.ReloadProfile();
            };
            skillDamageTest.Completed += checkpoint => {
                skillDamageTest = null;
                SetDamageTestHomeUiVisible(true);
                characterRoot.SetActive(true);
                characterRuntime.ShowDamageTestResult(request, checkpoint,
                    () => OnSkillDamageTestRequested(request), () => { }, () => { });
            };
            try { skillDamageTest.Begin(host, request); }
            catch (Exception error) {
                UnityEngine.Object.Destroy(skillDamageTest); skillDamageTest = null;
                SetDamageTestHomeUiVisible(true);
                characterRoot.SetActive(true);
                Debug.LogException(error);
                Notify("伤害测试打开失败：" + error.Message);
            }
        }
    }
}
