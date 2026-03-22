using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using SharedConfig.Config;
using SharedConfig.Extensions;
using SharedConfig.Utils;

namespace SharedConfig.Config.UI;

public partial class NConfigDropdown : NDropdown
{
    private List<ConfigDropdownItem>? _items;
    private int _currentDisplayIndex = -1;
    private float _lastGlobalY;

    private static readonly FieldInfo DropdownContainerField =
        AccessTools.Field(typeof(NDropdown), "_dropdownContainer");

    public NConfigDropdown()
    {
        SetCustomMinimumSize(new Vector2(324, 64));
        SizeFlagsHorizontal = Godot.Control.SizeFlags.ShrinkEnd;
        SizeFlagsVertical = Godot.Control.SizeFlags.Fill;
        FocusMode = Control.FocusModeEnum.All;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (DropdownContainerField.GetValue(this) is Control { Visible: true } container)
        {
            container.GlobalPosition = GlobalPosition + new Vector2(0, Size.Y);
        }

        _lastGlobalY = GlobalPosition.Y;
    }

    public void SetItems(List<ConfigDropdownItem> items, int initialIndex)
    {
        _items = items;
        _currentDisplayIndex = initialIndex;
    }

    public override void _Ready()
    {
        ConnectSignals();
        ClearDropdownItems();

        if (_items == null) throw new Exception("Created config dropdown without setting items");

        for (var i = 0; i < _items.Count; i++)
        {
            NConfigDropdownItem child = NConfigDropdownItem.Create(_items[i]);
            _dropdownItems.AddChildSafely(child);
            child.Connect(NDropdownItem.SignalName_.Pressed,
                Callable.From<NConfigDropdownItem>(OnDropdownItemSelected));
            child.Init(i);

            if (i == _currentDisplayIndex)
                _currentOptionLabel.SetTextAutoSize(child.Data.Text);
        }

        _dropdownItems.GetParent().RefreshLayout();

        if (DropdownContainerField.GetValue(this) is Control container)
        {
            container.VisibilityChanged += () => {
                container.TopLevel = container.Visible;
                container.GlobalPosition = GlobalPosition + new Vector2(0, Size.Y);
            };
        }
    }

    private void OnDropdownItemSelected(NConfigDropdownItem nDropdownItem)
    {
        if (nDropdownItem == null) return;

        CloseDropdown();
        _currentOptionLabel.SetTextAutoSize(nDropdownItem.Data.Text);
        _currentDisplayIndex = nDropdownItem.DisplayIndex;
        nDropdownItem.Data.OnSet();
    }
}
