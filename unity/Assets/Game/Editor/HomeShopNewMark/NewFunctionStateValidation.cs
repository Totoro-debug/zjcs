using System;
using UnityEngine;
using Zjcs.Official.HomeTipDots;

namespace Zjcs.Editor.HomeShopNewMark
{
    public static class NewFunctionStateValidation
    {
        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        public static void Run()
        {
            var state = new HomeNewFunctionState();
            int adds = 0, removes = 0;
            state.OnNewFunction += (type, arg) => { if (type == 18 && arg == 7) adds++; };
            state.OnRemoveNewFunction += (type, arg) => { if (type == 18) removes++; };
            Require(!state.IsNew(18) && state.GetNewFunctionArgsByType(18) == null, "An absent function must not be new.");
            state.AddNewFunction(18, 7); state.AddNewFunction(18, 7);
            Require(adds == 2 && state.IsNew(18, 7) && state.IsNew(18) && !state.IsNew(18, 6),
                "Repeated adds still publish; zero queries any argument for that type.");
            state.AddNewFunction(18, 6);
            state.RemoveNewFunction(18, 99);
            Require(removes == 1 && state.IsNew(18, 7), "Removal publishes when the type exists even if the argument did not.");
            state.RemoveNewFunction(18, 7);
            Require(removes == 2 && !state.IsNew(18, 7) && state.IsNew(18, 6), "Removing one argument must retain the other.");
            state.RemoveNewFunction(18, 6);
            state.RemoveNewFunction(18, 6);
            Require(removes == 3 && state.GetNewFunctionArgsByType(18) == null, "The final removal drops the type; absent removal is silent.");
            state.AddNewFunction(18); state.AddNewFunction(19, 1);
            state.Reset();
            Require(!state.IsNew(18) && !state.IsNew(19) && removes == 3, "Reset clears all state without publishing remove events.");
            Debug.Log("HOME_NEW_FUNCTION_STATE_VALIDATION_PASSED");
        }
    }
}
