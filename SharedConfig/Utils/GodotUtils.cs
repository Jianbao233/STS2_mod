using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SharedConfig.Extensions;

namespace SharedConfig.Utils;

public static class GodotUtils
{
    /// <summary>
    /// Creates creature visuals from a scene path, converting to NCreatureVisuals if needed.
    /// </summary>
    public static NCreatureVisuals CreatureVisualsFromScene(string path)
    {
        Node n = PreloadManager.Cache.GetScene(path).Instantiate();
        if (n is NCreatureVisuals visuals)
        {
            return visuals;
        }

        var visualsNode = new NCreatureVisuals();
        TransferNodes(visualsNode, n, "Visuals", "Bounds", "IntentPos", "CenterPos", "OrbPos", "TalkPos");
        return visualsNode;
    }

    /// <summary>
    /// Transfers all child nodes from a source scene into the target object.
    /// </summary>
    public static T TransferAllNodes<T>(this T obj, string sourceScene, params string[] uniqueNames) where T : Node
    {
        TransferNodes(obj, PreloadManager.Cache.GetScene(sourceScene).Instantiate(), uniqueNames);
        return obj;
    }

    private static void TransferNodes(Node target, Node source, params string[] names)
    {
        TransferNodes(target, source, true, names);
    }

    private static void TransferNodes(Node target, Node source, bool uniqueNames, params string[] names)
    {
        target.Name = source.Name;

        List<string> requiredNames = [.. names];
        foreach (var child in source.GetChildren())
        {
            source.RemoveChild(child);
            if (requiredNames.Remove(child.Name) && uniqueNames) child.UniqueNameInOwner = true;
            target.AddChild(child);
            child.Owner = target;

            SetChildrenOwner(target, child);
        }

        if (requiredNames.Count > 0)
        {
            GD.PushWarning($"Created {target.GetType().FullName} missing required children {string.Join(" ", requiredNames)}");
        }

        source.QueueFree();
    }

    private static void SetChildrenOwner(Node target, Node child)
    {
        foreach (var grandchild in child.GetChildren())
        {
            grandchild.Owner = target;
            SetChildrenOwner(target, grandchild);
        }
    }
}
