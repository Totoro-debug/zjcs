using System;
using System.IO;
using UnityEngine;
using Zjcs.Presentation.Home;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
public static class PetShopOwnerValidation {
    static void Require(bool ok,string message) { if(!ok) throw new Exception(message); }
    public static void Run() {
        string root=Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "owner-saves-"+Guid.NewGuid().ToString("N"));
        var slots=new ProfileSlotCatalog(root); string slot=slots.CreateSlot();
        var writer=OfflineMainlineProgressSession.OpenCharacter(slots,slot);
        Require(writer.ApplyCharacterMutationIfChanged(p=>{p.Level=49;return true;}).IsApplied,"seed");
        HomePetShopSession.Begin(root,slot);
        var owner=HomePetShopSession.Current;
        HomePetShopSession.Begin(root,slot); Require(ReferenceEquals(owner,HomePetShopSession.Current),"same session across navigation");
        Require(writer.ApplyCharacterMutationIfChanged(p=>{p.Level=50;return true;}).IsApplied,"level");
        Require(new OfflineFruitTreeService(writer).TryRepair(OfflineFruitTreeService.PetFurnitureType,50,false,1700000000,"owner:repair").IsApplied,"repair");
        Require(owner.State.IsNew(18,7),"real owner receives unlock");
        HomePetShopSession.Begin(root,slot); Require(HomePetShopSession.Current.State.IsNew(18,7),"mark survives reopen");
        owner.OnTabChanged(true,true); Require(!owner.State.IsNew(18,7),"shop clears");
        OfflineMainlineProgressSession.EndPlayerSession(); Require(HomePetShopSession.Current==null,"logout clears owner");
        HomePetShopSession.Begin(root,slot); Require(!HomePetShopSession.Current.IsLimited && !HomePetShopSession.Current.State.IsNew(18,7),"reopen does not restore mark");
        OfflineMainlineProgressSession.EndPlayerSession(); Debug.Log("PET_SHOP_OWNER_VALIDATION_PASSED");
    }
}
