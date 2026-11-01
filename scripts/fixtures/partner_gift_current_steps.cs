    private IEnumerator Check()
    {
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        var pageAssets = OfficialPartnerGiftPageAssetCatalog.LoadDefault();
        var text = JsonUtility.FromJson<TextCatalog>(pageAssets.GiftTexts.text);
        var context = new Context { Texts = text.texts.ToDictionary(row => row.key, row => row.text) };
        var localAssets = OfficialPartnerFriendshipAssetCatalog.LoadDefault();
        foreach (var payload in new[] { localAssets.PropertyTexts, pageAssets.RelationshipTexts })
            foreach (var row in JsonUtility.FromJson<TextCatalog>(payload.text).texts) context.Texts[row.key] = row.text;
        context.Icons = new PartnerGiftPreferenceProbe(); context.Texts["ui_ItemIcon_count_key"] = context.Icons.CountFormat;
        var selected = OfficialPartnerAssetCatalog.LoadDefault(); var configuration = OfficialPartnerGiftAssetCatalog.LoadDefault();
        var slots = new ProfileSlotCatalog(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "gift-current-profiles"));
        string slot = slots.CreateSlot(); var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var player = new OfflinePartnerGiftPlayer(session, selected, configuration, context.Icons.GetItemInfo, () => context.AddressLimited);
        var bindings = new OfflineProfessionPropertyBindings(localAssets.PropertyConfiguration, context.Icons.ImageResources, context,
            (owner, path, name, desc, control) => { context.Popups++; }, (owner, rank, desc, effect, first, second) => { throw new InvalidOperationException(); });
        var properties = new OfflinePartnerFriendshipProperties(selected, bindings, () => context.Limited);
        var images = new OfflineProfessionImageResourceLoader(pageAssets.Images);
        var services = new OfflinePartnerGiftUiServices(session, player, configuration, properties, context, images, context);
        foreach (string background in selected.Rows("npc_friendship_tag").Select(row => (string)row["TagBg"]).Distinct())
            using (var operation = services.LoadSprite(background)) Require(operation.Status == 2 && operation.Result != null && operation.Result.texture != null, "all original dynamic relationship backgrounds load");
        Action<long, int, int> seed = (experience, rank, npcLevel) => {
            Require(session.ApplyCharacterMutation(value => {
                value.CharacterCreated = true; value.CharacterName = "原生赠礼验证"; value.SubRank = rank;
                value.CharacterProperties.NpcFriendships.Clear();
                value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8401, Level = npcLevel, FriendshipExp = experience });
                value.Inventory.Clear();
                foreach (int id in new[] { 1130, 1131, 1100, 1300 }) OfflineInventoryOperations.Grant(value, id, 8, 10, 0, true);
            }).IsApplied, "actual isolated seed"); player.Reload();
        };
        seed(0, 1, 1); context.Npc = configuration.GetNpcInfo(8401);
        PartnerGiftUiServices.Current = services; ProfessionPropertyBindings.Current = bindings; LanguageManager.Initialize(context); OfficialWwiseEvent.Host = context;
/*NATIVE_SETUP*/
        panel.Configure(services); level.Configure(services);
        int given = 0; var npc = player.GetNpcByClassId(8401);
        Action initialize = () => { level.Init(npc, context.Npc); panel.Init(npc, context.Npc, () => {
            var reopened = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(reopened.InventoryCount(1130) == player.GetItemAmount(1130), "disk commit before original success"); given++;
        }, level.ResetExpPreview, level.ShowExpPreview); };
        initialize(); root.SetActive(true); host.SetActive(true); yield return null; yield return null;
        Require(Get<Image>(panel, "mFriendshipTagBg").sprite != null, "original tag sprite is visibly bound");
        var icons = Get<Dictionary<int, ItemIcon>>(panel, "mGiftItemIconDict");
        Require(icons.Count == 4 && Get<NpcGiftBlueDotItem>(panel, "mNpcGiftBlueDotItem").IsShow, "actual inventory list and original preferred blue dot");
        Capture("gift-current-open.png");
        int increment = configuration.GetAllExpByDict(new Dictionary<int, long> { [1130] = 1 }, context.Npc.GiftPreferenceDict);
        Click(icons[1130].gameObject);
        Require(Selected(panel)[1130] == 1 && Get<Slider>(level, "mPreviewSlider").value == Math.Min(increment, configuration.Get(8401, 2).FriendshipExp), "real selected preview and current threshold");
        Capture("gift-current-selected.png");
        Click(icons[1130].GetAttachment(ItemIconAttachmentType.ReduceTip).gameObject); Require(Selected(panel).Count == 0, "original reduce input");
        Click(Get<Button>(panel, "mAutoSelectBtn").gameObject); Require(Selected(panel).Count > 0, "auto select current next threshold");
        Require(configuration.GetAllExpByDict(Selected(panel), context.Npc.GiftPreferenceDict) >= configuration.Get(8401, 2).FriendshipExp, "auto selection reaches original next level");
        Click(Get<Button>(panel, "mResetBtn").gameObject); Require(Selected(panel).Count == 0, "original reset input");
        icons = Get<Dictionary<int, ItemIcon>>(panel, "mGiftItemIconDict"); Click(icons[1130].gameObject); Click(Get<Button>(panel, "mGiveGiftBtn").gameObject);
        Require(given == 0 && player.GetItemAmount(1130) == 8, "local response is deferred");
        while (given == 0) yield return null;
        Require(player.GetItemAmount(1130) == 7 && npc.FriendShip == increment && npc.FriendShipLevel == configuration.GetFriendshipLevelByExp(8401, increment, 1) && Selected(panel).Count == 0, "native gifting commits current inventory/experience/level and refresh");
        Require(session.Save.Player.Inventory.Single(value => value.ItemId == 1130).UnitRecords.Count == 7, "native gift consumes tracked original units");
        Capture("gift-current-saved.png");
        // Reopen the same isolated file; the formal window route is tested later.
        player.Reload(); initialize(); yield return null; yield return null;
        Require(player.GetItemAmount(1130) == 7 && Get<WorldMultiLanguageText>(level, "mLevelText").Text.Contains(npc.FriendShipLevel.ToString()), "saved UI restores actual level");
        // A locked save must produce no success response and preserve selection.
        icons = Get<Dictionary<int, ItemIcon>>(panel, "mGiftItemIconDict"); Click(icons[1130].gameObject);
        byte[] before = File.ReadAllBytes(slots.GetProgressPath(slot)); int completed = given;
        using (var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Click(Get<Button>(panel, "mGiveGiftBtn").gameObject);
            while (context.Failures.Count == 0) yield return null;
            Require(given == completed && player.GetItemAmount(1130) == 7 && Selected(panel)[1130] == 1 && before.SequenceEqual(File.ReadAllBytes(slots.GetProgressPath(slot))), "write failure leaves native selection/state/save and no success");
        }
        Click(Get<Button>(panel, "mGiveGiftBtn").gameObject); while (given == completed) yield return null;
        Require(player.GetItemAmount(1130) == 6 && Selected(panel).Count == 0, "native retry commits once");
        int cap = configuration.GetMaxSubRankLevel(8401, 1);
        seed(configuration.Get(8401, cap).FriendshipExp, 1, cap); initialize(); yield return null; yield return null;
        icons = Get<Dictionary<int, ItemIcon>>(panel, "mGiftItemIconDict"); Click(icons[1130].gameObject);
        Require(Selected(panel).Count == 0 && context.Messages.Last() == context.Text("ui_npc_detail_select_gift_level_limit_tip"), "current rank cap uses original pointer rejection");
        Capture("gift-current-rank-limit.png");
        int topRank = selected.Rows("npc_friendship_level").Where(row => Convert.ToInt32(row["Id"]) == 8401).Max(row => Convert.ToInt32(row["SubRankLimit"]));
        long maximum = configuration.GetMaxLevelInfo(8401).FriendshipExp;
        seed(maximum - 1, topRank, 99); initialize(); yield return null; yield return null;
        icons = Get<Dictionary<int, ItemIcon>>(panel, "mGiftItemIconDict"); Click(icons[1130].gameObject); completed = given;
        Click(Get<Button>(panel, "mGiveGiftBtn").gameObject);
        Require(context.Confirm != null && given == completed && player.GetItemAmount(1130) == 8, "native overflow opens original confirmation before mutation");
        yield return null; Require(player.GetItemAmount(1130) == 8, "unconfirmed overflow is unchanged");
        context.Confirm.Accept(); while (given == completed) yield return null;
        Require(npc.FriendShip == maximum - 1 + increment && npc.FriendShipLevel == 100 && player.GetItemAmount(1130) == 7, "confirmed original overflow persists");
        Capture("gift-current-max-level.png");
        Click(Get<Button>(panel, "mCustomFriendshipTagBtn").gameObject); Require(context.Input.Masks == 1, "native title input endpoint");
        context.Input.Submit("\uD83D\uDE00"); Require(context.LastError == 1150 && npc.CustomFriendShipTag == "", "original whitelist rejects unsupported title");
        context.Input.Submit("伙伴"); Require(context.Closes == 0, "title waits for local response");
        while (context.Closes == 0) yield return null;
        Require(npc.CustomFriendShipTag == "伙伴" && OfflineMainlineProgressSession.OpenCharacter(slots, slot).Save.Player.CharacterProperties.NpcFriendships.Single().CustomFriendshipTag == "伙伴", "original title success follows actual save");
        host.SetActive(false); Object.Destroy(host); context.Icons.Dispose(); yield return null;
        Require(errors == 0, "actual current native gift render/input/lifecycle errors");
    }
