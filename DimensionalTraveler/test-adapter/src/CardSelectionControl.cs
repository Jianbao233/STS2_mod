using System.Reflection;
using System.Text.Json.Nodes;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace DimensionalTraveler.TestAdapter;

internal static class CardSelectionControl
{
    public static TestToolSchema Schema { get; } = new(
        McpIntegration.SelectionToolName,
        "Inspect or complete an active vanilla choose-a-card selection after its input debounce.",
        """
        {
          "type": "object",
          "properties": {
            "action": { "type": "string", "enum": ["get", "select", "cancel"] },
            "candidate_index": { "type": "integer" },
            "card_id": { "type": "string" }
          },
          "required": ["action"]
        }
        """);

    private static readonly FieldInfo? OpenedTicksField =
        AccessTools.Field(typeof(NChooseACardSelectionScreen), "_openedTicks");

    private static readonly MethodInfo? SelectHolderMethod =
        AccessTools.Method(typeof(NChooseACardSelectionScreen), "SelectHolder", [typeof(NCardHolder)]);

    private static readonly MethodInfo? SkipMethod =
        AccessTools.Method(typeof(NChooseACardSelectionScreen), "OnSkipButtonReleased");

    private static readonly FieldInfo? CompletionSourceField =
        AccessTools.Field(typeof(NChooseACardSelectionScreen), "_completionSource");

    private static readonly FieldInfo? ScreenCompleteField =
        AccessTools.Field(typeof(NChooseACardSelectionScreen), "_screenComplete");

    private static readonly FieldInfo? CardSelectedField =
        AccessTools.Field(typeof(NChooseACardSelectionScreen), "_cardSelected");

    public static JsonObject Execute(JsonObject args)
    {
        var action = args["action"]?.GetValue<string>()?.Trim().ToLowerInvariant();
        var screen = TryGetScreen();
        if (action == "get")
            return TestToolResult.Ok(new JsonObject { ["selection"] = Capture(screen) });
        if (action == "cancel")
            return Cancel(screen);
        if (action != "select")
            return TestToolResult.Fail($"未知 action：{action ?? "<null>"}。", "invalid_action");
        if (screen is null)
            return TestToolResult.Fail("当前没有活动的 NChooseACardSelectionScreen。", "selection_inactive");

        var ageMs = GetAgeMs(screen);
        if (ageMs <= 350)
        {
            return TestToolResult.Fail(
                $"原生选择界面仍在输入防抖期，ageMs={ageMs}。",
                "selection_not_ready");
        }

        var candidates = GetCandidates(screen);
        NCardHolder? selected;
        if (args["candidate_index"] is JsonNode indexNode)
        {
            var index = indexNode.GetValue<int>();
            if (index < 0 || index >= candidates.Count)
            {
                return TestToolResult.Fail(
                    $"candidate_index {index} 超出范围，候选数为 {candidates.Count}。",
                    "candidate_out_of_range");
            }
            selected = candidates[index];
        }
        else
        {
            var cardId = args["card_id"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(cardId))
                return TestToolResult.Fail("select 需要 candidate_index 或 card_id。", "missing_candidate");
            selected = candidates.FirstOrDefault(holder =>
                string.Equals(holder.CardModel?.Id.Entry, cardId, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
                return TestToolResult.Fail($"候选中不存在 card_id {cardId}。", "candidate_not_found");
        }

        var completion = CompletionSourceField?.GetValue(screen);
        var taskBefore = GetCompletionTask(completion);
        var screenCompleteBefore = GetBoolean(ScreenCompleteField, screen);
        var cardSelectedBefore = GetBoolean(CardSelectedField, screen);
        var completionPath = "holder_pressed_signal";

        selected.EmitSignal(NCardHolder.SignalName.Pressed, selected);

        var taskAfterSignal = GetCompletionTask(completion);
        if (taskAfterSignal is { IsCompleted: false } && SelectHolderMethod is not null)
        {
            completionPath = "holder_pressed_signal_then_select_holder";
            SelectHolderMethod.Invoke(screen, [selected]);
        }

        var taskAfter = GetCompletionTask(completion);
        return TestToolResult.Ok(new JsonObject
        {
            ["cardId"] = selected.CardModel?.Id.Entry,
            ["ageMs"] = ageMs,
            ["activeScreenFound"] = true,
            ["holderCount"] = candidates.Count,
            ["holderIds"] = new JsonArray(candidates
                .Select(holder => JsonValue.Create(holder.CardModel?.Id.Entry))
                .ToArray()),
            ["completionSourceFieldFound"] = CompletionSourceField is not null,
            ["completionSourceType"] = completion?.GetType().FullName,
            ["completionTaskFound"] = taskBefore is not null,
            ["taskCompletedBefore"] = taskBefore?.IsCompleted,
            ["taskCompletedAfterSignal"] = taskAfterSignal?.IsCompleted,
            ["taskCompletedAfter"] = taskAfter?.IsCompleted,
            ["screenCompleteBefore"] = screenCompleteBefore,
            ["screenCompleteAfter"] = GetBoolean(ScreenCompleteField, screen),
            ["cardSelectedBefore"] = cardSelectedBefore,
            ["cardSelectedAfter"] = GetBoolean(CardSelectedField, screen),
            ["selectHolderMethodFound"] = SelectHolderMethod is not null,
            ["completionPath"] = completionPath,
        });
    }

    private static JsonObject Cancel(NChooseACardSelectionScreen? screen)
    {
        if (screen is null)
            return TestToolResult.Fail("当前没有活动的 NChooseACardSelectionScreen。", "selection_inactive");
        if (SkipMethod is null)
            return TestToolResult.Fail("当前游戏版本缺少原生选择跳过方法。", "selection_skip_incompatible");

        var completion = CompletionSourceField?.GetValue(screen);
        var taskBefore = GetCompletionTask(completion);
        SkipMethod.Invoke(screen, [null]);
        var taskAfter = GetCompletionTask(completion);
        return TestToolResult.Ok(new JsonObject
        {
            ["completionTaskFound"] = taskBefore is not null,
            ["taskCompletedBefore"] = taskBefore?.IsCompleted,
            ["taskCompletedAfter"] = taskAfter?.IsCompleted,
            ["completionPath"] = "native_skip",
        });
    }

    public static JsonObject Capture(NChooseACardSelectionScreen? screen = null)
    {
        screen ??= TryGetScreen();
        if (screen is null)
        {
            return new JsonObject
            {
                ["active"] = false,
                ["candidates"] = new JsonArray(),
                ["selectors"] = CaptureSelectorState(),
                ["overlayNodeTypes"] = CaptureOverlayTypes(),
            };
        }

        var candidates = new JsonArray();
        var holders = GetCandidates(screen);
        for (var index = 0; index < holders.Count; index++)
        {
            var card = holders[index].CardModel;
            candidates.Add(new JsonObject
            {
                ["index"] = index,
                ["cardId"] = card?.Id.Entry,
                ["name"] = card?.Title,
            });
        }

        return new JsonObject
        {
            ["active"] = true,
            ["ready"] = GetAgeMs(screen) > 350,
            ["ageMs"] = GetAgeMs(screen),
            ["candidates"] = candidates,
            ["selectors"] = CaptureSelectorState(),
            ["overlayNodeTypes"] = CaptureOverlayTypes(),
        };
    }

    private static NChooseACardSelectionScreen? TryGetScreen()
    {
        var overlays = NOverlayStack.Instance;
        if (overlays is null)
            return null;

        return Descendants(overlays)
            .OfType<NChooseACardSelectionScreen>()
            .FirstOrDefault(GodotObject.IsInstanceValid);
    }

    private static JsonArray CaptureOverlayTypes()
    {
        var overlays = NOverlayStack.Instance;
        if (overlays is null)
            return new JsonArray();

        return new JsonArray(Descendants(overlays)
            .Where(GodotObject.IsInstanceValid)
            .Select(node => node.GetType().FullName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static typeName => typeName, StringComparer.Ordinal)
            .Select(static typeName => (JsonNode?)typeName)
            .ToArray());
    }

    private static JsonObject CaptureSelectorState() => new()
    {
        ["selectorType"] = CardSelectCmd.Selector?.GetType().FullName,
        ["localSelectorType"] = CardSelectCmd.LocalSelector?.GetType().FullName,
    };

    private static ulong GetAgeMs(NChooseACardSelectionScreen screen)
    {
        var opened = OpenedTicksField?.GetValue(screen) as ulong? ?? 0UL;
        return Time.GetTicksMsec() - opened;
    }

    private static Task? GetCompletionTask(object? completionSource) =>
        completionSource?.GetType().GetProperty("Task", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(completionSource) as Task;

    private static bool? GetBoolean(FieldInfo? field, object instance) =>
        field?.GetValue(instance) as bool?;

    private static IReadOnlyList<NCardHolder> GetCandidates(Node root) =>
        Descendants(root)
            .OfType<NCardHolder>()
            .Where(holder => GodotObject.IsInstanceValid(holder)
                && holder.CardModel is not null
                && holder.IsVisibleInTree())
            .OrderBy(holder => holder.Position.X)
            .ToArray();

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}