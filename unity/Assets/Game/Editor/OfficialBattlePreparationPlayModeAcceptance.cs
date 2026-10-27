namespace Zjcs.Editor
{
    public static class OfficialBattlePreparationPlayModeAcceptance
    {
        public static void ImportAndRunSummonInput()
        {
            Combat.OfficialSummonNativeModelTools.ImportAndValidate();
            ProjectTools.Validate();
            OfficialMap10BattlePlayModeAcceptance.RunRealInput();
        }

        public static void ImportAndRunInput()
        {
            Combat.OfficialBattlePreparationAssetTools.ImportAndValidate();
            ProjectTools.Validate();
            OfficialMap10BattlePlayModeAcceptance.RunRealInput();
        }
    }
}
