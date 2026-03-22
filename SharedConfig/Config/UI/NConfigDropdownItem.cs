using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using SharedConfig.Config;
using SharedConfig.Extensions;
using SharedConfig.Utils;

namespace SharedConfig.Config.UI;

public partial class NConfigDropdownItem : NDropdownItem
{
    private static readonly string BaseScenePath = SceneHelper.GetScenePath("ui/dropdown_item");

    public static NConfigDropdownItem Create(ConfigDropdownItem data)
    {
        var dropdownItem = new NConfigDropdownItem { Data = data };
        dropdownItem.SetCustomMinimumSize(new Vector2(288, 44));
        dropdownItem.MouseFilter = MouseFilterEnum.Pass;
        dropdownItem.TransferAllNodes(BaseScenePath);
        return dropdownItem;
    }

    public required ConfigDropdownItem Data;
    public int DisplayIndex;

    private NConfigDropdownItem() { }

    public void Init(int setIndex)
    {
        DisplayIndex = setIndex;
        _label.SetTextAutoSize(Data.Text);
    }
}
