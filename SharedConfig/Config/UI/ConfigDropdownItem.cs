namespace SharedConfig.Config.UI;

public class ConfigDropdownItem
{
    public readonly string Text;
    public readonly Action OnSet;

    public ConfigDropdownItem(string text, Action onSet)
    {
        Text = text;
        OnSet = onSet;
    }
}
