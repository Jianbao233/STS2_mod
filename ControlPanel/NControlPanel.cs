using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ControlPanel;

/// <summary>
/// 控制面板：三栏布局（左大类 | 次左功能项 | 右功能区），含卡牌/遗物/药水/遭遇/事件/战斗控制。
/// F7 由 F7InputLayer 处理。命令按钮悬浮显示将执行的指令。
/// </summary>
public partial class NControlPanel : CanvasLayer
{
    private static readonly Color GoldColor = new Color(1f, 0.84f, 0f, 1f);
    private static readonly Color GoldBorder = new Color(1f, 0.84f, 0f, 0.3f);
    private const string ModVersion = "0.2.0";
    private const string ProjectName = "ControlPanel";
    private const string AuthorInfo = "煎包 / bili@我叫煎包 / Composer 1.5 | 生成敌人参考 ParasiteSpire";

    private Panel _mainPanel;
    private Button _titleBtn;
    private HSplitContainer _split;
    private VBoxContainer _leftColumn;
    private VBoxContainer _midColumn;
    private ScrollContainer _rightScroll;
    private Control _rightContent;
    private bool _isDragging;
    private Vector2 _dragOffset;
    private bool _titleDragPending;
    private Vector2 _titleDragStart;
    private Control _resizeGrip;
    private bool _resizing;
    private Vector2 _resizeStart;

    private string _currentCategory = "card";
    private string _currentSub = "add";

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 100;

        var viewportSize = GetViewport().GetVisibleRect().Size;
        var panelW = 960f;
        var panelH = 620f;
        var posX = (viewportSize.X - panelW) / 2f;
        var posY = (viewportSize.Y - panelH) / 2f;

        _mainPanel = new Panel
        {
            Position = new Vector2(posX, posY),
            CustomMinimumSize = new Vector2(700, 450),
            Size = new Vector2(panelW, panelH),
            ClipContents = true
        };
        _mainPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.14f, 0.95f),
            BorderColor = new Color(1f, 0.84f, 0f, 0.5f)
        };
        style.BorderWidthBottom = style.BorderWidthTop = style.BorderWidthLeft = style.BorderWidthRight = 2;
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 8;
        _mainPanel.AddThemeStyleboxOverride("panel", style);
        AddChild(_mainPanel);

        var margin = new MarginContainer { OffsetLeft = 12, OffsetTop = 12, OffsetRight = -12, OffsetBottom = -12 };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _mainPanel.AddChild(margin);

        var vbox = new VBoxContainer();
        margin.AddChild(vbox);

        var gameVer = GetGameVersion();
        _titleBtn = new Button
        {
            Text = $"{gameVer} | {ProjectName} v{ModVersion} | 作者：{AuthorInfo}  [F7 关闭]",
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _titleBtn.AddThemeColorOverride("font_color", GoldColor);
        _titleBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.95f, 0.5f, 1f));
        _titleBtn.AddThemeFontSizeOverride("font_size", 14);
        _titleBtn.GuiInput += OnTitleGuiInput;
        vbox.AddChild(_titleBtn);

        var sep = new HSeparator();
        sep.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color(1f, 0.84f, 0f, 0.25f) });
        vbox.AddChild(sep);

        _split = new HSplitContainer();
        _split.SplitOffset = 95;
        _split.CustomMinimumSize = new Vector2(0, 420);
        _split.DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Visible;
        vbox.AddChild(_split);

        _leftColumn = new VBoxContainer { CustomMinimumSize = new Vector2(75, 0), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        _split.AddChild(_leftColumn);

        var midSplit = new HSplitContainer();
        midSplit.SplitOffset = 125;
        midSplit.CustomMinimumSize = new Vector2(0, 0);
        midSplit.DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Visible;
        _split.AddChild(midSplit);

        _midColumn = new VBoxContainer { CustomMinimumSize = new Vector2(100, 0), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        midSplit.AddChild(_midColumn);

        _rightScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        midSplit.AddChild(_rightScroll);

        _rightContent = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(380, 0) };
        _rightScroll.AddChild(_rightContent);

        var gripPanel = new Panel
        {
            CustomMinimumSize = new Vector2(18, 18),
            Size = new Vector2(18, 18),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        gripPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        gripPanel.SetOffsetsPreset(Control.LayoutPreset.BottomRight, Control.LayoutPresetMode.KeepSize);
        var gripStyle = new StyleBoxFlat { BgColor = new Color(1f, 0.84f, 0f, 0.4f) };
        gripStyle.CornerRadiusBottomRight = 6;
        gripPanel.AddThemeStyleboxOverride("panel", gripStyle);
        gripPanel.GuiInput += OnResizeGripInput;
        _resizeGrip = gripPanel;
        _mainPanel.AddChild(_resizeGrip);

        BuildLeftColumn();
        BuildMidColumn();
        RefreshRightContent();
    }

    private void OnResizeGripInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed) _resizing = true;
            else _resizing = false;
        }
    }

    public override void _Process(double delta)
    {
        if (_resizing && _mainPanel != null)
        {
            var mp = GetViewport().GetMousePosition();
            var local = _mainPanel.GetGlobalTransformWithCanvas().AffineInverse() * mp;
            var newW = Mathf.Clamp((float)local.X, 600, 1600);
            var newH = Mathf.Clamp((float)local.Y, 400, 1200);
            _mainPanel.CustomMinimumSize = new Vector2(newW, newH);
            _mainPanel.Size = new Vector2(newW, newH);
        }
    }

    public override void _Input(InputEvent e)
    {
        if (_resizing && e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
            _resizing = false;
    }

    private static string GetGameVersion()
    {
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var mgrType = asm.GetType("MegaCrit.Sts2.Core.ReleaseInfoManager") ?? asm.GetType("ReleaseInfoManager");
                if (mgrType == null) continue;
                var inst = mgrType.GetProperty("Instance")?.GetValue(null);
                if (inst == null) continue;
                var info = mgrType.GetMethod("get_ReleaseInfo")?.Invoke(inst, null) ?? inst.GetType().GetProperty("ReleaseInfo")?.GetValue(inst);
                if (info == null) continue;
                var ver = info.GetType().GetProperty("Version")?.GetValue(info) as string;
                if (!string.IsNullOrEmpty(ver)) return ver;
            }
        }
        catch { }
        return ProjectName;
    }

    private void BuildLeftColumn()
    {
        foreach (var c in _leftColumn.GetChildren()) c.QueueFree();
        AddCatBtn("卡牌", "card");
        AddCatBtn("遗物", "relic");
        AddCatBtn("药水", "potion");
        AddCatBtn("遭遇战", "encounter");
        AddCatBtn("事件", "event");
        AddCatBtn("战斗控制", "combat");
    }

    private void AddCatBtn(string label, string key)
    {
        var btn = new Button { Text = label, Flat = true };
        btn.AddThemeFontSizeOverride("font_size", 13);
        if (_currentCategory == key)
            btn.AddThemeColorOverride("font_color", GoldColor);
        var k = key;
        btn.Pressed += () => { _currentCategory = k; _currentSub = k == "card" ? "add" : k == "relic" ? "owned" : k == "encounter" ? "normal" : k == "combat" ? "damage" : "add"; BuildMidColumn(); RefreshRightContent(); BuildLeftColumn(); };
        _leftColumn.AddChild(btn);
    }

    private void BuildMidColumn()
    {
        foreach (var c in _midColumn.GetChildren()) c.QueueFree();
        if (_currentCategory == "card")
        {
            AddSubBtn("添加卡牌", "add");
            AddSubBtn("删除卡牌", "remove");
        }
        else if (_currentCategory == "relic")
        {
            AddSubBtn("已拥有遗物", "owned");
            AddSubBtn("未拥有遗物", "unowned");
        }
        else if (_currentCategory == "potion")
        {
            if (_currentSub == "remove") _currentSub = "add";
            AddSubBtn("生成药水", "add");
        }
        else if (_currentCategory == "encounter")
        {
            AddSubBtn("普通", "normal");
            AddSubBtn("精英", "elite");
            AddSubBtn("Boss", "boss");
        }
        else if (_currentCategory == "event")
        {
            AddSubBtn("事件列表", "list");
        }
        else if (_currentCategory == "combat")
        {
            AddSubBtn("伤害/格挡/回血", "damage");
            AddSubBtn("能量/抽牌", "energy");
            AddSubBtn("能力/Buff", "power");
            AddSubBtn("击杀敌人", "kill");
            AddSubBtn("生成敌人", "spawn");
        }
    }

    private void AddSubBtn(string label, string key)
    {
        var btn = new Button { Text = label, Flat = true };
        btn.AddThemeFontSizeOverride("font_size", 12);
        if (_currentSub == key)
            btn.AddThemeColorOverride("font_color", GoldColor);
        var k = key;
        btn.Pressed += () => { _currentSub = k; RefreshRightContent(); BuildMidColumn(); };
        _midColumn.AddChild(btn);
    }

    private void RefreshRightContent()
    {
        foreach (var c in _rightContent.GetChildren()) c.QueueFree();
        CallDeferred("DeferredBuildContent");
    }

    private void DeferredBuildContent()
    {
        if (_currentCategory == "card") BuildCardContent();
        else if (_currentCategory == "relic") BuildRelicContent();
        else if (_currentCategory == "potion") BuildPotionContent();
        else if (_currentCategory == "encounter") BuildEncounterContent();
        else if (_currentCategory == "event") BuildEventContent();
        else if (_currentCategory == "combat") BuildCombatContent();
    }

    private void BuildCardContent()
    {
        if (_currentSub == "remove")
        {
            _rightContent.AddChild(new Label { Text = "删除卡牌：实时显示牌堆中的卡牌，点击删除。游戏 remove_card 仅支持 Hand/Deck（抽牌堆/弃牌堆需先抽到手上再删）" });
            var removePileRow = new HBoxContainer();
            removePileRow.AddChild(new Label { Text = "牌堆：" });
            var pileOpt = new OptionButton();
            pileOpt.AddItem("手牌"); pileOpt.AddItem("牌组");
            pileOpt.Selected = 0;
            removePileRow.AddChild(pileOpt);
            _rightContent.AddChild(removePileRow);

            var refreshBtn = new Button { Text = "刷新" };
            removePileRow.AddChild(refreshBtn);

            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            var list = new VBoxContainer();
            scroll.AddChild(list);
            _rightContent.AddChild(scroll);

            void Refresh()
            {
                foreach (var c in list.GetChildren()) c.QueueFree();
                var pileNames = new[] { "Hand", "Deck" };
                var pile = pileOpt.Selected < pileNames.Length ? pileNames[pileOpt.Selected] : "Hand";
                var cards = GameStateHelper.GetCardsInPile(pile);
                if (cards.Count == 0)
                {
                    var noRun = new Label { Text = GameStateHelper.IsInCombat() ? "该牌堆为空" : "需在战斗中才能查看牌堆" };
                    list.AddChild(noRun);
                    return;
                }
                var (allIds, allZhs) = FullDataLoader.GetFullCards();
                var allCards = new Dictionary<string, string>();
                for (int i = 0; i < allIds.Length; i++)
                    allCards[allIds[i]] = i < allZhs.Length ? allZhs[i] : "";
                foreach (var id in cards)
                {
                    var zh = allCards.TryGetValue(id, out var z) ? z : "";
                    var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    var icon = new TextureRect { CustomMinimumSize = new Vector2(32, 32), ExpandMode = TextureRect.ExpandModeEnum.FitHeight };
                    icon.Texture = IconLoader.GetCardIcon(id);
                    row.AddChild(icon);
                    var cmd = $"remove_card {id} {pile}";
                    var btn = new Button { Text = string.IsNullOrEmpty(zh) ? $"删除 {id}" : $"删除 {zh} ({id})", Flat = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    btn.TooltipText = $"将执行: {cmd}";
                    var idCopy = id;
                    var pileCopy = pile;
                    btn.Pressed += () => { RunCommand($"remove_card {idCopy} {pileCopy}"); Refresh(); };
                    row.AddChild(btn);
                    list.AddChild(row);
                }
            }
            pileOpt.ItemSelected += _ => Refresh();
            refreshBtn.Pressed += Refresh;
            Refresh();
            return;
        }

        // 按图2模板：角色/搜索 -> 卡牌列表(左) | 预览+属性+指令(右) -> 执行
        var topRow = new HBoxContainer();
        topRow.AddChild(new Label { Text = "角色：" });
        var charOpt = new OptionButton();
        charOpt.AddItem("全部");
        charOpt.AddItem("铁甲战士"); charOpt.AddItem("寂静猎手"); charOpt.AddItem("故障机器人");
        charOpt.AddItem("亡灵契约师"); charOpt.AddItem("储君");
        topRow.AddChild(charOpt);
        topRow.AddChild(new Label { Text = "  搜索：" });
        var addSearch = new LineEdit { PlaceholderText = "搜索卡牌...", ClearButtonEnabled = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        topRow.AddChild(addSearch);
        _rightContent.AddChild(topRow);

        var addPileLab = new Label { Text = "牌堆：" };
        var addPileOpt = new OptionButton();
        foreach (var (name, _) in PotionAndCardData.PileTypes)
            addPileOpt.AddItem(name);

        var mainSplit = new HSplitContainer();
        mainSplit.SplitOffset = 320;
        mainSplit.DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Visible;
        mainSplit.CustomMinimumSize = new Vector2(0, 280);

        var addScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var addList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        addScroll.AddChild(addList);
        mainSplit.AddChild(addScroll);

        var rightPanel = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(200, 0) };
        var previewRow = new HBoxContainer();
        var cardPreview = new TextureRect { CustomMinimumSize = new Vector2(110, 154), ExpandMode = TextureRect.ExpandModeEnum.FitHeight, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        previewRow.AddChild(cardPreview);
        var cardInfo = new VBoxContainer();
        cardInfo.AddChild(new Label { Text = "选中卡牌" });
        var selectedCardLabel = new Label { Text = "-", AutowrapMode = TextServer.AutowrapMode.Off };
        cardInfo.AddChild(selectedCardLabel);
        previewRow.AddChild(cardInfo);
        rightPanel.AddChild(previewRow);

        var pileRow = new HBoxContainer();
        pileRow.AddChild(addPileLab);
        pileRow.AddChild(addPileOpt);
        rightPanel.AddChild(pileRow);

        var cmdLabel = new Label { Text = "将执行: (选择卡牌)", AutowrapMode = TextServer.AutowrapMode.Off };
        rightPanel.AddChild(cmdLabel);

        var execBtn = new Button { Text = "执行指令", Flat = false };
        execBtn.AddThemeColorOverride("font_color", GoldColor);
        rightPanel.AddChild(execBtn);

        mainSplit.AddChild(rightPanel);
        mainSplit.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        addScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _rightContent.AddChild(mainSplit);

        string selectedId = "";
        string selectedZh = "";

        void UpdatePreview()
        {
            if (string.IsNullOrEmpty(selectedId)) { cardPreview.Texture = null; selectedCardLabel.Text = "-"; cmdLabel.Text = "将执行: (选择卡牌)"; return; }
            cardPreview.Texture = IconLoader.GetCardIcon(selectedId);
            selectedCardLabel.Text = string.IsNullOrEmpty(selectedZh) ? selectedId : $"{selectedZh}\n({selectedId})";
            var pile = addPileOpt.Selected < PotionAndCardData.PileTypes.Length ? PotionAndCardData.PileTypes[addPileOpt.Selected].cmd : "Hand";
            cmdLabel.Text = $"将执行: card {selectedId} {pile}";
        }

        execBtn.Pressed += () =>
        {
            if (string.IsNullOrEmpty(selectedId)) return;
            var pile = addPileOpt.Selected < PotionAndCardData.PileTypes.Length ? PotionAndCardData.PileTypes[addPileOpt.Selected].cmd : "Hand";
            RunCommand($"card {selectedId} {pile}");
        };
        execBtn.TooltipText = "先选择卡牌，再选择牌堆";
        addPileOpt.ItemSelected += _ => UpdatePreview();

        addSearch.TextChanged += _ => BuildCardList(addSearch.Text, charOpt.Selected, addPileOpt.Selected);

        void BuildCardList(string q, int charIdx, int pileIdx)
        {
            foreach (var c in addList.GetChildren()) c.QueueFree();
            q = (q ?? "").Trim().ToLowerInvariant();
            if (charIdx <= 0 && string.IsNullOrEmpty(q))
            {
                addList.AddChild(new Label { Text = "请选择角色或输入搜索以加载卡牌列表\n（避免一次性加载500+卡牌造成卡顿）" });
                return;
            }
            var (cardIds, cardZhs) = FullDataLoader.GetFullCards();
            int count = 0;
            const int maxShow = 200;
            for (int i = 0; i < cardIds.Length && count < maxShow; i++)
            {
                var id = cardIds[i];
                var zh = i < cardZhs.Length ? cardZhs[i] : "";
                if (!CardPoolHelper.MatchesCharacter(id, charIdx)) continue;
                if (!string.IsNullOrEmpty(q) && !id.ToLowerInvariant().Contains(q) && !(zh?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                    continue;
                count++;
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                var icon = new TextureRect { CustomMinimumSize = new Vector2(28, 28), ExpandMode = TextureRect.ExpandModeEnum.FitHeight };
                icon.Texture = IconLoader.GetCardIcon(id);
                row.AddChild(icon);
                var btn = new Button { Text = string.IsNullOrEmpty(zh) ? id : $"{zh} ({id})", Flat = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                var idCopy = id;
                var zhCopy = zh;
                btn.Pressed += () => { selectedId = idCopy; selectedZh = zhCopy; UpdatePreview(); };
                btn.TooltipText = "点击选择";
                row.AddChild(btn);
                addList.AddChild(row);
            }
            if (count >= maxShow)
                addList.AddChild(new Label { Text = $"已显示前 {maxShow} 张，请用搜索缩小范围" });
        }
        charOpt.ItemSelected += _ => BuildCardList(addSearch.Text, charOpt.Selected, addPileOpt.Selected);
        _ = CardPoolHelper.GetCharacter("STRIKE_IRONCLAD"); // 预热缓存，避免首次选择角色时卡顿
        BuildCardList("", 0, 0);
    }

    private void BuildRelicContent()
    {
        // 遗物：按稀有度分类，图标网格 + ID，左键直接添加/删除
        var topRow = new HBoxContainer();
        topRow.AddChild(new Label { Text = "稀有度：" });
        var rarityOpt = new OptionButton();
        foreach (var r in PotionAndCardData.RelicRarities)
            rarityOpt.AddItem(r);
        topRow.AddChild(rarityOpt);
        var refreshBtn = new Button { Text = "刷新" };
        topRow.AddChild(refreshBtn);
        _rightContent.AddChild(topRow);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var grid = new GridContainer { Columns = 12, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(grid);
        _rightContent.AddChild(scroll);

        var rarityMap = new Dictionary<int, string> { { 1, "Common" }, { 2, "Uncommon" }, { 3, "Rare" }, { 4, "Ancient" }, { 5, "Starter" }, { 6, "Event" }, { 7, "Shop" } };
        var fullRelics = FullDataLoader.GetFullRelics();
        var allRelics = new List<(string id, string zh, string rarity)>();
        if (_currentSub == "owned")
        {
            var owned = GameStateHelper.GetOwnedRelics();
            var known = new Dictionary<string, string>();
            foreach (var (id, zh, _) in fullRelics) known[id] = zh;
            foreach (var id in owned)
                allRelics.Add((id, known.TryGetValue(id, out var zh) ? zh : "", GameStateHelper.GetRelicRarityFromGame(id)));
        }
        else
        {
            var ownedSet = new HashSet<string>(GameStateHelper.GetOwnedRelics());
            foreach (var (id, zh, _) in fullRelics)
                if (!ownedSet.Contains(id)) allRelics.Add((id, zh, GameStateHelper.GetRelicRarityFromGame(id)));
        }

        void Refresh()
        {
            foreach (var c in grid.GetChildren()) c.QueueFree();
            if (_currentSub == "owned")
            {
                allRelics.Clear();
                var owned = GameStateHelper.GetOwnedRelics();
                var known = new Dictionary<string, string>();
                foreach (var (id, zh, _) in fullRelics) known[id] = zh;
                foreach (var id in owned)
                    allRelics.Add((id, known.TryGetValue(id, out var zh) ? zh : "", GameStateHelper.GetRelicRarityFromGame(id)));
            }
            else
            {
                allRelics.Clear();
                var ownedSet = new HashSet<string>(GameStateHelper.GetOwnedRelics());
                foreach (var (id, zh, _) in fullRelics)
                    if (!ownedSet.Contains(id)) allRelics.Add((id, zh, GameStateHelper.GetRelicRarityFromGame(id)));
            }
            var sel = rarityOpt.Selected <= 0 ? "" : (rarityMap.TryGetValue(rarityOpt.Selected, out var m) ? m : "");
            foreach (var (id, zh, rarity) in allRelics)
            {
                if (!string.IsNullOrEmpty(sel) && rarity != sel) continue;
                var cell = new Control { CustomMinimumSize = new Vector2(56, 72) };
                var vbox = new VBoxContainer();
                var iconRect = new TextureRect { CustomMinimumSize = new Vector2(40, 40), ExpandMode = TextureRect.ExpandModeEnum.FitHeight, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
                iconRect.Texture = IconLoader.GetRelicIcon(id);
                vbox.AddChild(iconRect);
                var lbl = new Label { Text = string.IsNullOrEmpty(zh) ? id : zh, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.Arbitrary };
                lbl.AddThemeFontSizeOverride("font_size", 9);
                vbox.AddChild(lbl);
                cell.AddChild(vbox);
                var btn = new Button { Flat = true };
                btn.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                btn.SetOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize);
                var idCopy = id;
                btn.Pressed += () =>
                {
                    RunCommand(_currentSub == "owned" ? $"relic remove {idCopy}" : $"relic add {idCopy}");
                    if (_currentSub == "owned") Refresh();
                };
                btn.TooltipText = string.IsNullOrEmpty(zh) ? id : $"{zh} ({id})\n左键{(_currentSub == "owned" ? "删除" : "获得")}";
                cell.AddChild(btn);
                grid.AddChild(cell);
            }
        }
        rarityOpt.ItemSelected += _ => Refresh();
        refreshBtn.Pressed += Refresh;
        Refresh();
    }

    private void BuildPotionContent()
    {
        // 模板：分类+搜索 -> 药水列表(左) | 预览+指令(右) -> 执行
        var topRow = new HBoxContainer();
        topRow.AddChild(new Label { Text = "分类：" });
        var catOpt = new OptionButton();
        foreach (var c in PotionAndCardData.PotionCategories)
            catOpt.AddItem(c);
        topRow.AddChild(catOpt);
        topRow.AddChild(new Label { Text = "  搜索：" });
        var search = new LineEdit { PlaceholderText = "搜索...", ClearButtonEnabled = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        topRow.AddChild(search);
        _rightContent.AddChild(topRow);

        var mainSplit = new HSplitContainer();
        mainSplit.SplitOffset = 300;
        mainSplit.DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Visible;
        mainSplit.CustomMinimumSize = new Vector2(0, 280);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(list);
        mainSplit.AddChild(scroll);

        var rightPanel = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(180, 0) };
        var preview = new TextureRect { CustomMinimumSize = new Vector2(40, 40), ExpandMode = TextureRect.ExpandModeEnum.FitHeight, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        rightPanel.AddChild(preview);
        var cmdLabel = new Label { Text = "将执行: potion <id>", AutowrapMode = TextServer.AutowrapMode.Off };
        rightPanel.AddChild(cmdLabel);
        var descLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.Off, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        descLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f, 1f));
        descLabel.AddThemeFontSizeOverride("font_size", 11);
        rightPanel.AddChild(descLabel);
        var execBtn = new Button { Text = "执行指令", Flat = false };
        execBtn.AddThemeColorOverride("font_color", GoldColor);
        rightPanel.AddChild(execBtn);
        mainSplit.AddChild(rightPanel);
        mainSplit.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _rightContent.AddChild(mainSplit);

        string selectedId = "";
        execBtn.Pressed += () => { if (!string.IsNullOrEmpty(selectedId)) RunCommand($"potion {selectedId}"); };
        execBtn.TooltipText = "先在列表中点击选择药水";

        void OnPotionSelect(string id)
        {
            selectedId = id;
            preview.Texture = IconLoader.GetPotionIcon(id);
            cmdLabel.Text = $"将执行: potion {id}";
            descLabel.Text = LocalizationHelper.GetPotionDescription(id);
        }

        void Refresh()
        {
            foreach (var c in list.GetChildren()) c.QueueFree();
            var cat = catOpt.Selected <= 0 ? "" : PotionAndCardData.PotionCategories[catOpt.Selected];
            var q = search.Text?.Trim().ToLowerInvariant() ?? "";
            foreach (var (id, zh, c) in PotionAndCardData.PotionData)
            {
                if (!string.IsNullOrEmpty(cat) && c != cat) continue;
                if (!string.IsNullOrEmpty(q) && !id.ToLowerInvariant().Contains(q) && !(zh?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                    continue;
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                var icon = new TextureRect { CustomMinimumSize = new Vector2(32, 32), ExpandMode = TextureRect.ExpandModeEnum.FitHeight };
                icon.Texture = IconLoader.GetPotionIcon(id);
                row.AddChild(icon);
                var btn = new Button { Text = string.IsNullOrEmpty(zh) ? id : $"{zh} ({id})", Flat = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                var idCopy = id;
                btn.Pressed += () => OnPotionSelect(idCopy);
                btn.TooltipText = "点击选择";
                row.AddChild(btn);
                list.AddChild(row);
            }
        }
        catOpt.ItemSelected += _ => Refresh();
        search.TextChanged += _ => Refresh();
        Refresh();
    }

    private void BuildEncounterContent()
    {
        // 模板：遭遇列表(左) | 预览+指令(右) -> 执行
        var targetType = _currentSub switch { "elite" => EncounterType.Elite, "boss" => EncounterType.Boss, _ => EncounterType.Normal };
        var mainSplit = new HSplitContainer();
        mainSplit.SplitOffset = 280;
        mainSplit.DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Visible;
        mainSplit.CustomMinimumSize = new Vector2(0, 300);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(list);
        mainSplit.AddChild(scroll);

        var rightPanel = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(180, 0) };
        var cmdLabel = new Label { Text = "将执行: fight <id>", AutowrapMode = TextServer.AutowrapMode.Off };
        rightPanel.AddChild(cmdLabel);
        var execBtn = new Button { Text = "执行指令", Flat = false };
        execBtn.AddThemeColorOverride("font_color", GoldColor);
        rightPanel.AddChild(execBtn);
        mainSplit.AddChild(rightPanel);
        _rightContent.AddChild(mainSplit);

        string selectedId = "";
        execBtn.Pressed += () => { if (!string.IsNullOrEmpty(selectedId)) RunCommand($"fight {selectedId}"); };
        execBtn.TooltipText = "先在列表中点击选择遭遇";

        foreach (var (id, zh) in PotionAndCardData.FightData)
        {
            if (PotionAndCardData.GetEncounterType(id) != targetType) continue;
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var btn = new Button { Text = string.IsNullOrEmpty(zh) ? id : zh, Flat = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var idCopy = id;
            btn.Pressed += () => { selectedId = idCopy; cmdLabel.Text = $"将执行: fight {idCopy}"; };
            btn.TooltipText = "点击选择";
            row.AddChild(btn);
            list.AddChild(row);
        }
    }

    private void BuildEventContent()
    {
        // 模板：事件列表(左) | 指令(右) -> 执行
        var mainSplit = new HSplitContainer();
        mainSplit.SplitOffset = 280;
        mainSplit.DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Visible;
        mainSplit.CustomMinimumSize = new Vector2(0, 300);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(list);
        mainSplit.AddChild(scroll);

        var rightPanel = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(240, 0) };
        var cmdLabel = new Label { Text = "将执行: event <id>", AutowrapMode = TextServer.AutowrapMode.Off };
        cmdLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.8f, 1f));
        cmdLabel.AddThemeFontSizeOverride("font_size", 12);
        rightPanel.AddChild(cmdLabel);
        var eventTextPanel = new PanelContainer();
        var eventTextStyle = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.16f, 0.9f), BorderColor = new Color(1f, 0.84f, 0f, 0.2f) };
        eventTextStyle.BorderWidthTop = eventTextStyle.BorderWidthBottom = eventTextStyle.BorderWidthLeft = eventTextStyle.BorderWidthRight = 1;
        eventTextStyle.CornerRadiusTopLeft = eventTextStyle.CornerRadiusTopRight = eventTextStyle.CornerRadiusBottomLeft = eventTextStyle.CornerRadiusBottomRight = 4;
        eventTextStyle.ContentMarginLeft = eventTextStyle.ContentMarginRight = 10;
        eventTextStyle.ContentMarginTop = eventTextStyle.ContentMarginBottom = 8;
        eventTextPanel.AddThemeStyleboxOverride("panel", eventTextStyle);
        var eventTextLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.Arbitrary, SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        eventTextLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.95f, 1f));
        eventTextLabel.AddThemeFontSizeOverride("font_size", 12);
        eventTextPanel.AddChild(eventTextLabel);
        eventTextPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        rightPanel.AddChild(eventTextPanel);
        var execBtn = new Button { Text = "执行指令", Flat = false };
        execBtn.AddThemeColorOverride("font_color", GoldColor);
        rightPanel.AddChild(execBtn);
        mainSplit.AddChild(rightPanel);
        mainSplit.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _rightContent.AddChild(mainSplit);

        string selectedId = "";
        execBtn.Pressed += () => { if (!string.IsNullOrEmpty(selectedId)) RunCommand($"event {selectedId}"); };
        execBtn.TooltipText = "先在列表中点击选择事件";

        foreach (var (id, zh) in PotionAndCardData.EventData)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var btn = new Button { Text = string.IsNullOrEmpty(zh) ? id : $"{zh} ({id})", Flat = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var idCopy = id;
            btn.Pressed += () => { selectedId = idCopy; cmdLabel.Text = $"将执行: event {idCopy}"; eventTextLabel.Text = LocalizationHelper.GetEventText(idCopy); };
            btn.TooltipText = "点击选择";
            row.AddChild(btn);
            list.AddChild(row);
        }
    }

    private void BuildCombatContent()
    {
        if (_currentSub == "damage")
        {
            _rightContent.AddChild(new Label { Text = "伤害/格挡/回血（需战斗中）" });
            var grid = new GridContainer { Columns = 2 };
            grid.AddChild(new Label { Text = "数值：" });
            var amountEdit = new SpinBox { Value = 10, MinValue = 0, MaxValue = 999 };
            grid.AddChild(amountEdit);
            grid.AddChild(new Label { Text = "目标(0=玩家,空=全体敌人)：" });
            var targetEdit = new LineEdit { PlaceholderText = "留空=全体" };
            grid.AddChild(targetEdit);
            _rightContent.AddChild(grid);
            AddCmdBtnWithCtrl("造成伤害", () => $"damage {(int)amountEdit.Value}" + (string.IsNullOrWhiteSpace(targetEdit.Text) ? "" : " " + targetEdit.Text.Trim()));
            AddCmdBtnWithCtrl("给予格挡", () => $"block {(int)amountEdit.Value}" + (string.IsNullOrWhiteSpace(targetEdit.Text) ? "" : " " + targetEdit.Text.Trim()));
            AddCmdBtnWithCtrl("治疗", () => $"heal {(int)amountEdit.Value}" + (string.IsNullOrWhiteSpace(targetEdit.Text) ? "" : " " + targetEdit.Text.Trim()));
        }
        else if (_currentSub == "energy")
        {
            _rightContent.AddChild(new Label { Text = "能量/抽牌" });
            var spin = new SpinBox { Value = 3, MinValue = 0, MaxValue = 99 };
            _rightContent.AddChild(spin);
            AddCmdBtnWithCtrl("增加能量", () => $"energy {(int)spin.Value}");
            AddCmdBtnWithCtrl("抽牌", () => $"draw {(int)spin.Value}");
        }
        else if (_currentSub == "power")
        {
            _rightContent.AddChild(new Label { Text = "施加能力（target 0=玩家，1+=敌人）" });
            var amountEdit = new SpinBox { Value = 2, MinValue = -99, MaxValue = 99 };
            var targetEdit = new SpinBox { Value = 0, MinValue = 0, MaxValue = 99 };
            _rightContent.AddChild(amountEdit);
            _rightContent.AddChild(targetEdit);
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 120), SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var grid = new GridContainer { Columns = 8, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            scroll.AddChild(grid);
            _rightContent.AddChild(scroll);
            foreach (var (id, zh) in FullDataLoader.GetFullPowers())
            {
                var btn = new Button { Flat = true, Text = string.IsNullOrEmpty(zh) ? id : $"{zh} ({id})", Icon = IconLoader.GetPowerIcon(id) };
                btn.AddThemeFontSizeOverride("font_size", 9);
                var idCopy = id;
                btn.Pressed += () => RunCommand($"power {idCopy} {(int)amountEdit.Value} {(int)targetEdit.Value}");
                btn.TooltipText = $"将执行: power {idCopy} {(int)amountEdit.Value} {(int)targetEdit.Value}";
                grid.AddChild(btn);
            }
        }
        else if (_currentSub == "kill")
        {
            _rightContent.AddChild(new Label { Text = "击杀敌人（index=敌人序号，all=全部）" });
            var idxEdit = new LineEdit { PlaceholderText = "0 或 all" };
            _rightContent.AddChild(idxEdit);
            AddCmdBtnWithCtrl("击杀", () => $"kill {(string.IsNullOrWhiteSpace(idxEdit.Text) ? "0" : idxEdit.Text.Trim())}");
        }
        else if (_currentSub == "spawn")
        {
            _rightContent.AddChild(new Label { Text = "本次战斗中生成敌人：点击下方怪物加入当前战斗。若无效请查看 godot.log 中 [SpawnEnemy] 错误" });
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 120), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            scroll.AddChild(list);
            _rightContent.AddChild(scroll);
            var monsterList = SpawnEnemyHelper.GetSpawnableMonsterIds();
            foreach (var (id, zh) in monsterList)
            {
                var row = new HBoxContainer();
                var btn = new Button { Text = string.IsNullOrEmpty(zh) ? id : $"{zh} ({id})", Flat = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                var idCopy = id;
                btn.Pressed += () => SpawnEnemyHelper.SpawnInCombat(idCopy);
                btn.TooltipText = $"在当前战斗中生成 {idCopy}";
                row.AddChild(btn);
                list.AddChild(row);
            }
            if (monsterList.Length == 0)
                list.AddChild(new Label { Text = "无法获取怪物列表（需游戏中）" });
        }
    }

    private void AddCmdBtn(string label, Func<string> cmdFn)
    {
        var btn = new Button { Text = label, Flat = true };
        btn.Pressed += () => { var c = cmdFn(); RunCommand(c); };
        btn.MouseEntered += () => btn.TooltipText = $"将执行: {cmdFn()}";
        btn.MouseExited += () => btn.TooltipText = "";
        _rightContent.AddChild(btn);
    }

    private void AddCmdBtnWithCtrl(string label, Func<string> cmdFn)
    {
        var btn = new Button { Text = label, Flat = true };
        btn.Pressed += () => RunCommand(cmdFn());
        btn.MouseEntered += () => btn.TooltipText = $"将执行: {cmdFn()}";
        btn.MouseExited += () => btn.TooltipText = "";
        _rightContent.AddChild(btn);
    }

    private void RunCommand(string cmd)
    {
        try
        {
            var devConsole = GetDevConsole();
            if (devConsole == null)
            {
                GD.PrintErr($"[ControlPanel] DevConsole 未找到，无法执行: {cmd}");
                return;
            }
            var t = devConsole.GetType();
            var method = t.GetMethod("ProcessCommand", new[] { typeof(string) })
                ?? t.GetMethod("ProcessCommand", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
            if (method == null) return;
            var result = method.Invoke(devConsole, new object[] { cmd });
            if (result != null)
            {
                var success = result.GetType().GetProperty("Success")?.GetValue(result) as bool?
                    ?? result.GetType().GetProperty("success")?.GetValue(result) as bool?;
                var msg = result.GetType().GetProperty("Msg")?.GetValue(result) as string
                    ?? result.GetType().GetProperty("msg")?.GetValue(result) as string;
                GD.Print($"[ControlPanel] {cmd} => {(success == true ? "OK" : "FAIL")} {msg ?? ""}");
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ControlPanel] RunCommand failed: {e.Message}");
        }
    }

    private static readonly string[] NDevConsoleTypeNames = {
        "MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole", "Sts2.Core.Nodes.Debug.NDevConsole", "NDevConsole"
    };
    private static readonly string[] DevConsoleTypeNames = {
        "MegaCrit.Sts2.Core.DevConsole.DevConsole", "Sts2.Core.DevConsole.DevConsole", "DevConsole"
    };

    private static object GetDevConsole()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var typeName in NDevConsoleTypeNames)
                {
                    var ndevType = asm.GetType(typeName);
                    if (ndevType == null) continue;
                    var instance = ndevType.GetProperty("Instance")?.GetValue(null);
                    if (instance == null) continue;
                    var devField = ndevType.GetField("_devConsole", BindingFlags.NonPublic | BindingFlags.Instance);
                    var dev = devField?.GetValue(instance);
                    if (dev != null) return dev;
                }
            }
            catch (InvalidOperationException) { }
            catch { }
        }
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type devType = null;
                foreach (var name in DevConsoleTypeNames)
                {
                    devType = asm.GetType(name);
                    if (devType != null) break;
                }
                if (devType != null)
                {
                    var dev = Activator.CreateInstance(devType, true);
                    if (dev != null) return dev;
                }
            }
            catch { }
        }
        return null;
    }

    private void OnTitleGuiInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed) { _titleDragPending = true; _titleDragStart = mb.GlobalPosition; }
                else { _isDragging = false; _titleDragPending = false; }
            }
            return;
        }
        if (ev is InputEventMouseMotion mm && _titleDragPending && _mainPanel != null)
        {
            if (!_isDragging && _titleDragStart.DistanceTo(mm.GlobalPosition) > 4f)
            {
                _isDragging = true;
                _dragOffset = _mainPanel.Position - mm.GlobalPosition;
                _titleBtn.ReleaseFocus();
            }
            if (_isDragging)
            {
                _mainPanel.Position = mm.GlobalPosition + _dragOffset;
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
