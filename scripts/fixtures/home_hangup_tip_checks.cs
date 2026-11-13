        // Rates and camera positions are controlled owner inputs; the ledger,
        // thresholds, settings, timer and 105-object native UI are real.
        root.SetActive(false);
        var tipConfig=OfflineHomeHangUpTipConfiguration.LoadDefault();
        Require(tipConfig.records.Count==9&&tipConfig.records[0].id==1&&tipConfig.records[8].id==9,"selected recorded income IDs preserve parser order");
        host.FocusDuration=tipConfig.directionGuideFocusDuration;
        string tipSaveDirectory=Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"hangup-tip-saves-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tipSaveDirectory);string tipPath=Path.Combine(tipSaveDirectory,"home.json");
        long tipNow=1700000000,levelUpForTip=1000;float clockStart=0;bool liveClock=false,quickReceive=true,ownHome=true,canGetExp=true;
        var definitions=new[]{
            new OfflineHomeIncomeDefinition{HangUpId=1,OutputItemId=50,Unlocked=true,Rule=new HangUpIncomeRule{HourlyOutput=1000,LevelNum=1,MaxStoreHours=36}},
            new OfflineHomeIncomeDefinition{HangUpId=2,OutputItemId=1,Unlocked=true,Rule=new HangUpIncomeRule{HourlyOutput=1000,LevelNum=1,MaxStoreHours=36,IsCart=true}},
            new OfflineHomeIncomeDefinition{HangUpId=3,OutputItemId=2,Unlocked=true,Rule=new HangUpIncomeRule{HourlyOutput=100,LevelNum=1,MaxStoreHours=36,IsCart=true}}};
        var tipProperties=new Dictionary<int,long>();var tipFloatIds=new HashSet<int>();
        var income=OfflineHomeIncomeSession.Open(tipPath,definitions,tipProperties,tipFloatIds,tipNow);
        var tipOrder=new List<string>();var switched=new Dictionary<EFurniture,int>();
        var incomeSource=new OfflineHomeHangUpTipSource(income,tipConfig,
            ()=>tipNow+(liveClock?(long)(Time.time-clockStart):0),()=>ownHome,()=>1,()=>canGetExp,_=>levelUpForTip,
            _=>quickReceive,(f,t)=>{switched[f]=t;tipOrder.Add("state:"+(int)f+":"+t);},
            data=>{tipOrder.Add("publish:"+(int)data.FurnitureType+":"+data.TipLevel);host.EmitTip(data.FurnitureType,data.TipLevel);});
        var tipPorts=new TipPorts(incomeSource);
        using var bedTips=new HomeHangUpTipController(EFurniture.Bed,tipPorts);
        using var cartTips=new HomeHangUpTipController(EFurniture.Cart,tipPorts);
        host.Bed.Tip=()=>bedTips.TipLevel;host.Cart.Tip=()=>cartTips.TipLevel;
        bedTips.Enable();cartTips.Enable();bedTips.Start();cartTips.Start();root.SetActive(true);yield return null;
        var incomeGuides=root.GetComponentsInChildren<HomeHangUpDirectionGuider>(true);
        var incomeBed=incomeGuides.Single(x=>Field<EFurniture>(x,"mFurniture")==EFurniture.Bed);
        var incomeCart=incomeGuides.Single(x=>x!=incomeBed);
        var incomeBedButton=Field<Button>(incomeBed,"mGuiderBtn");var incomeCartButton=Field<Button>(incomeCart,"mGuiderBtn");
        Require(bedTips.TipLevel==-1&&cartTips.TipLevel==-1&&tipPorts.Scheduled==0,"unowned facilities reset without scheduling or publishing");
        Require(!incomeBedButton.gameObject.activeInHierarchy&&!incomeCartButton.gameObject.activeInHierarchy,"original guide reads current negative tip on enable");
        Require(income.Mutate("tip-fixture-repair",save=>{
            save.Furniture.Add(new OfflineHomeFurnitureEntry{Type=0,Owned=true});save.Furniture.Add(new OfflineHomeFurnitureEntry{Type=10,Owned=true});
            save.HangUps.Add(new OfflineHomeHangUpEntry{HangUpId=1,Unlocked=true});save.HangUps.Add(new OfflineHomeHangUpEntry{HangUpId=2,Unlocked=true});
        }),"durable repair and initial income unlock");
        tipOrder.Clear();incomeSource.NotifyRepaired(EFurniture.Bed);incomeSource.NotifyRepaired(EFurniture.Cart);
        Require(tipOrder.Take(3).SequenceEqual(new[]{"state:0:-1","publish:0:-1","state:0:-1"}),"original reset, publish, switch order is retained even when unchanged");
        Require(tipPorts.LastSeconds==648,"selected 0.5 percent first threshold waits 648 seconds at 1000 per hour over 36 hours");
        void SetIncome(int id,long count)
        {
            Require(income.Mutate("tip-count-"+Guid.NewGuid().ToString("N"),save=>{
                var entry=save.Income.SingleOrDefault(x=>x.HangUpId==id);
                if(entry==null){entry=new OfflineHomeIncomeEntry{HangUpId=id};save.Income.Add(entry);}
                entry.StoredSeconds=0;entry.SettledCount=count;
            }),"persist controlled income boundary");
            incomeSource.NotifyDataChanged(id);
        }
        SetIncome(1,99);Require(bedTips.TipLevel==-1,"bed below ten percent of level-up XP and below first capacity threshold");
        levelUpForTip=100;incomeSource.NotifyLevelChanged(2);Require(bedTips.TipLevel==-1,"official bed level event does not refresh while CanGetExp is true");
        canGetExp=false;incomeSource.NotifyLevelChanged(2);Require(bedTips.TipLevel==0,"official bed level event refreshes when CanGetExp is false");
        levelUpForTip=1000;canGetExp=true;incomeSource.NotifyDataChanged(1);
        SetIncome(1,100);Require(bedTips.TipLevel==0&&tipPorts.LastSeconds==288,"bed custom XP tip keeps wait computed from capacity before custom override");
        SetIncome(1,1799);Require(bedTips.TipLevel==0,"bed below five percent capacity");
        SetIncome(1,1800);Require(bedTips.TipLevel==1&&tipPorts.LastSeconds==0,"bed at five percent has highest tip and no further wait");
        SetIncome(2,178);Require(cartTips.TipLevel==-1,"cart below original float-to-long first threshold");
        SetIncome(2,179);Require(cartTips.TipLevel==0,"original Windows Mono expression truncates first threshold to 179 without added rounding");
        SetIncome(2,180);Require(cartTips.TipLevel==0,"cart stays in first tier after crossing fractional threshold");
        SetIncome(2,1799);Require(cartTips.TipLevel==0,"cart just below second threshold");
        SetIncome(2,1800);Require(cartTips.TipLevel==1,"cart second threshold is inclusive");
        SetIncome(2,10799);Require(cartTips.TipLevel==1,"cart just below thirty percent");
        SetIncome(2,10800);Require(cartTips.TipLevel==2,"cart thirty percent reaches final tip");
        SetIncome(2,2000);SetIncome(3,1080);
        Require(cartTips.TipLevel==1,"unrecorded income is not included by data-change notification alone");
        Require(income.Mutate("tip-unlock-cart3",save=>save.HangUps.Add(new OfflineHomeHangUpEntry{HangUpId=3,Unlocked=true})),"durable new cart unlock");
        incomeSource.NotifyUnlocked(3);Require(cartTips.TipLevel==2,"newly unlocked income with fewer rewards but greater fill ratio controls cart tip");
        yield return null;
        Require(incomeBedButton.gameObject.activeInHierarchy&&incomeCartButton.gameObject.activeInHierarchy,"actual stored income enables both original native direction buttons");
        Capture("home-hangup-tip-income.png");
        quickReceive=false;Require(!bedTips.CanReceiveAward()&&!cartTips.CanReceiveAward(),"quick-receive preference blocks direct award predicate");
        quickReceive=true;Require(bedTips.CanReceiveAward()&&cartTips.CanReceiveAward(),"quick-receive still requires a nonnegative official tip");
        long received=income.Collect(1,"tip-collect-bed");Require(received==1800&&income.Collect(1,"tip-collect-bed")==0,"real ledger awards once per receipt");
        incomeSource.NotifyDataChanged(1);yield return null;
        Require(bedTips.TipLevel==-1&&!incomeBedButton.gameObject.activeInHierarchy&&incomeCartButton.gameObject.activeInHierarchy,"real collection clears only bed direction prompt");
        Require(income.Save.Inventory.Any(x=>x.ItemId==50&&x.Count==1800),"collected XP remains in durable output ledger");
        Capture("home-hangup-tip-collected.png");
        int before=tipPorts.Scheduled;ownHome=false;SetIncome(2,0);SetIncome(3,0);
        Require(tipPorts.Scheduled==before,"visiting mode computes state but does not start timers");ownHome=true;
        // A one-second wait, paused through scaled time, exercises the actual
        // selected UniTask scheduling flags rather than calling its callback.
        definitions[1].Rule.HourlyOutput=7200;
        income.Reconfigure(definitions,tipProperties,tipFloatIds,Array.Empty<OfflineHomeRandomRewardDefinition>());
        SetIncome(2,1294);Require(cartTips.TipLevel==-1,"controlled cart is below first threshold");
        Require((int)Cysharp.Threading.Tasks.PlayerLoopTiming.Update==8,"ported Update timing has original numeric value eight");
        tipPorts.RealWait=true;liveClock=true;clockStart=Time.time;
        incomeSource.NotifyDataChanged(2);Require(tipPorts.LastSeconds==1,"original next-threshold ceiling produces one second");
        int fired=tipPorts.Fired;Time.timeScale=0;
        yield return new WaitForSecondsRealtime(1.1f);
        Require(tipPorts.Fired==fired&&cartTips.TipLevel==-1,"official wait is paused by timeScale zero");
        Time.timeScale=1;float timerDeadline=Time.realtimeSinceStartup+4;
        while(tipPorts.Fired==fired&&Time.realtimeSinceStartup<timerDeadline)yield return null;
        Require(tipPorts.Fired==fired+1&&cartTips.TipLevel==0,"real scheduled wake advances local income and reaches threshold");
        yield return null;Require(incomeCartButton.gameObject.activeInHierarchy,"scheduled threshold publishes to native direction UI");
        Capture("home-hangup-tip-timer.png");
        int canceled=tipPorts.Canceled;cartTips.Disable();Require(tipPorts.Canceled==canceled+1,"disable cancels replacement wait");
        fired=tipPorts.Fired;yield return new WaitForSeconds(1.1f);Require(tipPorts.Fired==fired,"disabled controller cannot refresh from its canceled wait");
        tipNow+=(long)(Time.time-clockStart);liveClock=false;tipPorts.RealWait=false;cartTips.Enable();
        SetIncome(1,1800);
        var restored=OfflineHomeIncomeSession.Open(tipPath,definitions,tipProperties,tipFloatIds,tipNow);
        var restoredSource=new OfflineHomeHangUpTipSource(restored,tipConfig,()=>tipNow,()=>true,()=>1,()=>true,_=>1000,_=>true,(_,__)=>{},_=>{});
        using(var restoredBed=new HomeHangUpTipController(EFurniture.Bed,new TipPorts(restoredSource)))
        using(var restoredCart=new HomeHangUpTipController(EFurniture.Cart,new TipPorts(restoredSource)))
        {
            restoredBed.Enable();restoredCart.Enable();restoredBed.Start();restoredCart.Start();Require(restoredBed.TipLevel==bedTips.TipLevel&&restoredCart.TipLevel==cartTips.TipLevel,"disk reopen reconstructs tip state from real saved income and unlocks");
        }
        bedTips.Dispose();cartTips.Dispose();
        foreach(string evt in new[]{"OnHangUpUnlock","OnHangUpDataChanged","OnRepairFurniture","OnDebugTimeOffset","OnLevelUpdate"})
            Require(Field<Delegate>(incomeSource,evt)==null,"tip disposal releases owner event "+evt);
        yield return null;
        Debug.Log("HOME_HANGUP_TIP_LEDGER_PASSED: selected thresholds, bed custom XP, maximum fill ratio, event order, actual saved collection, disk reopen, scaled timer, pause and cancellation.");
