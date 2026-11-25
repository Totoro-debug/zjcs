// Original shared furniture-component lookup.
using UnityEngine;
using Zjcs.Official.HomeEntityRuntime;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeFurnitureProxy
{
    public class HomeFurnitureProxy : MonoBehaviour, IFurnitureProxy<FurnitureBase>
    {
        public MonoDictionary<EFurniture, FurnitureBase> FurnitureProxyDict;

        public FurnitureBase GetComponent(EFurniture furniture)
        {
            FurnitureProxyDict.TryGetValue(furniture, out FurnitureBase value);
            return value;
        }
    }
}
