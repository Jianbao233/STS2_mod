using Godot;

namespace MultiplayerTools.Core
{
    internal static class MainMenuGuard
    {
        internal static bool IsMainMenuHomeActive()
        {
            var root = (Engine.GetMainLoop() as SceneTree)?.Root;
            if (root == null)
                return false;

            var mainMenu = root.GetNodeOrNull<Control>("MainMenu")
                ?? root.FindChild("MainMenu", true, false) as Control;
            if (mainMenu == null || !mainMenu.IsVisibleInTree())
                return false;

            var mainMenuTextButtons = mainMenu.GetNodeOrNull<Control>("MainMenuTextButtons");
            if (mainMenuTextButtons == null || !mainMenuTextButtons.IsVisibleInTree())
                return false;

            var submenus = mainMenu.GetNodeOrNull<Control>("Submenus");
            if (submenus == null)
                return true;

            foreach (Node child in submenus.GetChildren())
            {
                if (child is CanvasItem canvasItem && canvasItem.IsVisibleInTree())
                    return false;
            }

            return true;
        }
    }
}
