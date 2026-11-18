using System;
public static class BlockedActionHarness {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run() {
  var scope=new Zjcs.Offline.BattleNativePlayables.OfflineBattleNativePlayableScope();
  var views=new Zjcs.Presentation.Combat.OfficialBattleMoveActionPlayback(new UnityEngine.Transform(),null,scope,_=>null);
  var blocked=new Zjcs.Presentation.Combat.OfficialBattleMoveActionData();blocked.Movement.BlockedPos=4;
  views.Create(blocked); Check(Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Removed==0,"zero distance removed too soon");
  views.Advance(0); Check(Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Removed==0,"blocked graph was removed while playing");
  Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Last.Move.BlockedPlaybackPending=false; views.Advance(.01f);
  Check(Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Removed==1,"finished blocked graph not removed");
  var delayed=new Zjcs.Presentation.Combat.OfficialBattleMoveActionData();delayed.Movement.BlockedPos=4;delayed.Movement.Info.DelayDestroyTime=1;
  bool rejected=false;try{views.Create(delayed);}catch(NotSupportedException){rejected=true;} Check(rejected,"unverified server delay accepted");
  var normal=new Zjcs.Presentation.Combat.OfficialBattleMoveActionData();normal.Movement.TotalMoveDuration=.5f;normal.Movement.Info.DelayDestroyTime=.1f;
  views.Create(normal);views.Advance(.4f);Check(Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Removed==1,"ordinary action ended early");
  views.Advance(.21f);Check(Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Removed==2,"ordinary action did not end");views.Dispose();
  Console.WriteLine("Blocked and ordinary action lifecycle checks passed.");
 }
}
