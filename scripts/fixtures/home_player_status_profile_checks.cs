// Inserted into the full original panel/portrait fixture. Status state and
// transitions below use the real profile; entry text/sprite detail services,
// ratings and active-map owner remain explicitly controlled by that fixture.
            Require(statusConfig.Statuses.Count==163 && statusConfig.Scopes.Count==11 && statusConfig.Entries.Count==891,"complete selected status/scope/entry configuration");
            Require(savedStatuses.Statuses.Count==0 && profile.Save.Player.StatusEffects.Count==0,"fresh player has no fabricated initial status");
            var buffGroup=avatar.Root.GetComponentInChildren<PlayerBuffContentUI>(true);
            Require(!buffGroup.gameObject.activeInHierarchy,"empty actual status records hide native group");
            int statusEvents=0;IHomePanelStatusEffect lastStatus=null;
            savedStatuses.StatusUpdated+=value=>{statusEvents++;lastStatus=value;};
            // ClassId 0 is a real selected row, not an invalid sentinel.
            Require(savedStatuses.GainStatusEffect(0,Zjcs.Official.HomePlayerStatusRules.EStatusSourceType.Battle,"status:zero").IsApplied,"official zero class id can be stored");
            yield return null;yield return null;
            Require(buffGroup.gameObject.activeInHierarchy && !avatar.Root.GetComponentsInChildren<BuffItemUI>().Any(item=>item.gameObject.activeInHierarchy),"original controller shows active group while provider filters battle-source records");
            Require(savedStatuses.GainStatusEffect(7522,Zjcs.Official.HomePlayerStatusRules.EStatusSourceType.Cooking,"status:food").IsApplied,"real profile gains selected two-use status");
            Require(savedStatuses.Statuses[7522].GetDisplayRemainTimes()==2 && statusEvents==2,"original remaining count reaches UI after successful commits");
            DateTime firstGain=savedStatuses.Statuses[7522].LastGainedTime;
            Require(firstGain.Kind==experience.Now.Kind,"persisted DateTime preserves clock kind without a timezone conversion");
            Require(!savedStatuses.GainStatusEffect(7522,Zjcs.Official.HomePlayerStatusRules.EStatusSourceType.Story,"status:food").IsApplied
                && savedStatuses.Statuses[7522].LastGainedTime==firstGain && statusEvents==2,"duplicate durable gain cannot reset or publish");
            Require(savedStatuses.UpdateStatusEffect(7522,"status:use",record=>{record.ActivedTimes=1;Require(record.TryOccupy(101) && !record.TryOccupy(101),"original occupancy is unique");}).IsApplied,"committed original record update");
            Require(savedStatuses.Statuses[7522].GetDisplayRemainTimes()==1,"original consumed count is reflected");
            experience.Now=experience.Now.AddSeconds(2);
            Require(savedStatuses.GainStatusEffect(7522,Zjcs.Official.HomePlayerStatusRules.EStatusSourceType.Story,"status:regain").IsApplied,"existing selected status resets");
            var resetData=profile.Save.Player.StatusEffects.Single(row=>row.ClassId==7522);
            Require(resetData.ActivedTimes==0 && resetData.OccupiedFightIds.Count==0 && resetData.LastGainedTime==experience.Now
                && savedStatuses.Statuses[7522].StatusSourceType==EStatusSourceType.Story,"original regain updates source, clears counts/occupancy and refreshes exact timestamp");
            int beforeRejected=statusEvents;
            Require(!savedStatuses.UpdateStatusEffect(7522,"status:invalid",record=>record.ActivedTimes=-1).IsApplied
                && statusEvents==beforeRejected && profile.Save.Player.StatusEffects.Single(row=>row.ClassId==7522).ActivedTimes==0,"invalid transaction rolls back without UI event");
            Require(savedStatuses.UpdateStatusEffect(7522,"status:throw",record=>{record.IsActive=false;throw new InvalidOperationException("controlled failed producer");}).Status==ProgressionResultStatus.InvalidState
                && savedStatuses.Statuses[7522].IsActive && statusEvents==beforeRejected,"failed producer cannot leak uncommitted state into native adapter");
            Require(savedStatuses.UpdateStatusEffect(7522,"status:nochange",record=>{}).Status==ProgressionResultStatus.Unchanged
                && statusEvents==beforeRejected,"unchanged transitions do not notify");
            Require(savedStatuses.UpdateStatusEffect(7522,"status:identity",record=>record.ClassId=31).Status==ProgressionResultStatus.InvalidState
                && statusEvents==beforeRejected && !savedStatuses.Statuses.ContainsKey(31),"producer cannot change record identity or install an uncommitted duplicate");
            Zjcs.Official.HomePlayerStatusRules.PlayerStatusEffectDataWrap escapedRecord=null;
            Require(savedStatuses.UpdateStatusEffect(7522,"status:detach",record=>{escapedRecord=record;record.TryOccupy(102);}).IsApplied,"detach producer argument after a committed transition");
            escapedRecord.ActivedTimes=1;escapedRecord.OccupiedFightIds.Clear();
            Require(profile.Save.Player.StatusEffects.Single(row=>row.ClassId==7522).ActivedTimes==0
                && profile.Save.Player.StatusEffects.Single(row=>row.ClassId==7522).OccupiedFightIds.SequenceEqual(new long[]{102}),"escaped producer reference cannot mutate installed profile state");
            Require(savedStatuses.GainStatusEffect(31,Zjcs.Official.HomePlayerStatusRules.EStatusSourceType.Map,"status:duration").IsApplied,"real selected duration status commits");
            yield return null;yield return null;
            var visibleBuffs=avatar.Root.GetComponentsInChildren<BuffItemUI>().Where(item=>item.gameObject.activeInHierarchy).ToArray();
            Require(visibleBuffs.Length==2,"original native provider displays only eligible committed sources");
            var durationItem=visibleBuffs.Single(item=>item.StatusEffectData.Info.ClassId==31);
            var durationDecorator=(BuffItemDurationDecorator)Field(durationItem,"mBuffItemDecorator");
            var countdown=(Image)Field(durationDecorator,"mTimeDownImg");
            Require(Math.Abs(countdown.fillAmount-1)<.01f && savedStatuses.Statuses[31].GetDisplayRemainTimes()==-1,"selected unlimited count and full original duration");
            experience.Now=experience.Now.AddSeconds(9);yield return null;yield return null;
            Require(Math.Abs(countdown.fillAmount-.75f)<.01f,"native countdown uses exact selected 36-second duration and saved gain time");
            Capture("home-player-status-saved.png");
            Click((Button)Field(buffGroup,"mClickBtn"));
            Require(experience.LastEntries.Count==2,"original group pointer receives committed displayed records");
            var actualEnd=(EntryDetailAdditionEndTimeInfo)experience.LastEntries.Single(entry=>entry.AdditionInfoDic!=null).AdditionInfoDic[EntryDetailAdditionInfoType.EndTime];
            Require(actualEnd.EndTime==savedStatuses.Statuses[31].LastGainedTime.AddSeconds(36),"original entry data keeps exact saved expiry timestamp");
            var reopenedStatusProfile=OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saveRoot),slot);
            using(var reopenedStatuses=new OfflineHomePlayerStatuses(reopenedStatusProfile,statusConfig,()=>experience.Now))
                Require(reopenedStatuses.Statuses.Count==3 && reopenedStatuses.Statuses[7522].GetDisplayRemainTimes()==2
                    && reopenedStatuses.Statuses[31].LastGainedTime==savedStatuses.Statuses[31].LastGainedTime
                    && reopenedStatuses.Statuses[31].LastGainedTime.Kind==savedStatuses.Statuses[31].LastGainedTime.Kind,"status records, counts, source, ticks and DateTime kind survive actual disk reopen");
            // Exercise all original count/occupancy branches against selected
            // table rows, without inventing a production battle-consumption rule.
            foreach(var info in statusConfig.Statuses)
            {
                var record=Zjcs.Official.HomePlayerStatusRules.PlayerStatusEffectDataWrap.CreateNew(actualPlayer.Id,info.ClassId,statusConfig.Get,()=>experience.Now);
                Require(record.GetKey()==info.ClassId && record.IsActive && record.GetRemainTimes()==info.MaxActivateTimes,"selected original initial count across all 163 rows");
                record.ActivedTimes=int.MaxValue;
                Require(record.GetRemainTimes()==(info.MaxActivateTimes<0?info.MaxActivateTimes:0),"selected original exhausted/unlimited branches");
                record.TryOccupy(11);record.TryOccupy(12);record.ReleaseFinishedOccupies(id=>id==12);
                Require(!record.IsOccupiedBy(11) && record.IsOccupiedBy(12),"original release retains only running fights");
                record.IsActive=false;
                Require(record.GetDisplayRemainTimes()==1 && !record.ShouldBeRemove() && record.IsActiveForFight(12)
                    && !record.IsActiveForFight(0) && !record.IsActiveForFight(11),"inactive occupied original record remains visible to its fight only");
                record.Deactivate();Require(record.GetDisplayRemainTimes()==0 && record.ShouldBeRemove() && record.OccupiedFightIds.Count==0,"original deactivate clears occupancy");
                record.LastIsReduceOncePerPlay=!info.IsReduceOncePerPlay;
                Require(!record.TryResetActivedTimesOnReduceOncePerPlayChanged() && record.LastIsReduceOncePerPlay==info.IsReduceOncePerPlay,"original removable record synchronizes config flag without reactivating");
                record.Reset();record.LastIsReduceOncePerPlay=!info.IsReduceOncePerPlay;
                Require(!record.TryResetActivedTimesOnReduceOncePerPlayChanged(),"unused active record only synchronizes flag");
                record.ActivedTimes=1;record.LastIsReduceOncePerPlay=!info.IsReduceOncePerPlay;
                Require(record.TryResetActivedTimesOnReduceOncePerPlayChanged() && record.ActivedTimes==0 && record.IsActive
                    && !record.TryResetActivedTimesOnReduceOncePerPlayChanged(),"changed reduce-once flag resets used active record once");
            }
            Require(savedStatuses.UpdateStatusEffect(31,"status:deactivate",record=>record.Deactivate()).IsApplied,"owner-controlled original deactivate commits");
            yield return null;yield return null;
            Require(avatar.Root.GetComponentsInChildren<BuffItemUI>().Count(item=>item.gameObject.activeInHierarchy)==1,"committed inactive duration returns its native icon to pool");
            float stoppedCountdown=countdown.fillAmount;experience.Now=experience.Now.AddSeconds(2);yield return null;yield return null;
            Require(countdown.fillAmount==stoppedCountdown,"returned original duration decorator cancels its coroutine");
            Require(profile.ApplyCharacterMutation(saved=>saved.StatusEffects.Clear()).IsApplied,"external committed status removal");
            savedStatuses.SynchronizeCommittedPlayer();yield return null;yield return null;
            Require(lastStatus==null && savedStatuses.Statuses.Count==0 && !buffGroup.gameObject.activeInHierarchy,"removal event is published after installing full committed dictionary");
            Capture("home-player-status-cleared.png");
