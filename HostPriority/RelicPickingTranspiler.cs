using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;

namespace HostPriority;

/// <summary>
/// Transpiler 改写 TreasureRoomRelicSynchronizer.AwardRelics()：
/// 把 GenerateRelicFight 的第3个参数（随机出拳 lambda）替换为作弊版本——
/// 房主（slot 0）永远出克制手势，100% 获胜。
/// </summary>
[HarmonyPatch]
internal static class RelicPickingTranspiler
{
    // ── 1. 定位目标 ────────────────────────────────────────────────────────────
    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Multiplayer.Game.TreasureRoomRelicSynchronizer")
            ?? AccessTools.TypeByName("TreasureRoomRelicSynchronizer");
        if (t == null)
        {
            GD.PushError("[HostPriority-RPS] TreasureRoomRelicSynchronizer type not found!");
            return null;
        }

        var method = AccessTools.Method(t, "AwardRelics");
        if (method != null)
            GD.Print($"[HostPriority-RPS] Target: TreasureRoomRelicSynchronizer.AwardRelics");
        else
            GD.PushError("[HostPriority-RPS] AwardRelics method not found!");

        return method;
    }

    // ── 2. Transpiler ─────────────────────────────────────────────────────────
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        var codes = instructions.ToList();
        GD.Print($"[HostPriority-RPS] Transpiler START, {codes.Count} instructions.");

        if (!HostPriorityMod.Enabled)
        {
            GD.Print("[HostPriority-RPS] Mod disabled, returning original IL.");
            return codes;
        }

        // ── 收集游戏类型 ───────────────────────────────────────────────────────
        var moveType = Type.GetType("MegaCrit.Sts2.Core.Entities.TreasureRelicPicking.RelicPickingFightMove, sts2")
            ?? AccessTools.TypeByName("RelicPickingFightMove");

        if (moveType == null)
        {
            GD.PushError("[HostPriority-RPS] RelicPickingFightMove type NOT found!");
            return codes;
        }

        var rngType = Type.GetType("MegaCrit.Sts2.Core.Utils.IRng, sts2")
            ?? Type.GetType("MegaCrit.Sts2.Core.Utils.Rng, sts2")
            ?? AccessTools.TypeByName("Rng")
            ?? typeof(object);

        var moveListType = typeof(List<>).MakeGenericType(moveType);

        // 找 _rng 字段（TreasureRoomRelicSynchronizer._rng）
        var syncType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Multiplayer.Game.TreasureRoomRelicSynchronizer")
            ?? AccessTools.TypeByName("TreasureRoomRelicSynchronizer");
        var rngField = syncType?.GetField("_rng", BindingFlags.NonPublic | BindingFlags.Instance);

        if (rngField == null)
        {
            GD.PushError("[HostPriority-RPS] _rng field not found!");
            return codes;
        }

        GD.Print($"[HostPriority-RPS] moveType={moveType}, rngField={rngField}");

        // ── 找 GenerateRelicFight 调用 ─────────────────────────────────────────
        var genFightType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.TreasureRelicPicking.RelicPickingResult")
            ?? AccessTools.TypeByName("RelicPickingResult");

        var playerType = Type.GetType("MegaCrit.Sts2.Core.Entities.Players.Player, sts2")
            ?? AccessTools.TypeByName("Player");

        MethodInfo generateRelicFight = genFightType?.GetMethod("GenerateRelicFight",
            BindingFlags.Public | BindingFlags.Static, null,
            new[]
            {
                typeof(List<>).MakeGenericType(playerType ?? typeof(object)),
                typeof(object),
                typeof(Func<>).MakeGenericType(moveType)
            }, null)
            ?? AccessTools.Method(genFightType, "GenerateRelicFight");

        if (generateRelicFight == null)
        {
            GD.PushError("[HostPriority-RPS] GenerateRelicFight method NOT found!");
            return codes;
        }

        GD.Print($"[HostPriority-RPS] Found GenerateRelicFight");

        // 找 call GenerateRelicFight 的索引
        int callIdx = -1;
        for (int i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].Calls(generateRelicFight))
            {
                callIdx = i;
                break;
            }
        }

        if (callIdx < 0)
        {
            GD.PushError("[HostPriority-RPS] GenerateRelicFight call NOT found in IL!");
            return codes;
        }

        GD.Print($"[HostPriority-RPS] GenerateRelicFight call at index {callIdx}");

        // ── 分析 generateMove lambda 的 IL 范围 ───────────────────────────────
        // 典型 IL 结构（简化）：
        //   ldc.i4.3                    // array length = 3
        //   call Enum.GetValues<Move>   // RelicPickingFightMove[]
        //   ldloc X                     // _rng (IRng)
        //   ldloc Y                     // possibleMoves array
        //   callvirt IRng.NextItem<Move>
        //   ret
        //
        // 作弊：强制让 host 选择第一个 Move（REWARD）
        // [HostPriority-RPS] GenerateRelicFight call at index {callIdx}
        // 找 lambda 开始的 ldarg/ldloc
        int lambdaStart = -1;
        for (int i = callIdx - 1; i >= 0 && i > callIdx - 30; i--)
        {
            var op = codes[i].opcode;
            if (op == OpCodes.Ldarg_S || op == OpCodes.Ldarg
                || op == OpCodes.Ldarga || op == OpCodes.Ldarga_S
                || op == OpCodes.Ldloc || op == OpCodes.Ldloc_S
                || op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1
                || op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3
                || op == OpCodes.Ldftn)
            {
                lambdaStart = i + 1;
                break;
            }
            if (op == OpCodes.Newobj) { lambdaStart = i + 1; break; }
        }

        if (lambdaStart < 0) lambdaStart = callIdx - 25;

        // 找到 ret 结尾
        int lambdaEnd = callIdx - 1;
        for (int i = lambdaStart; i < callIdx; i++)
        {
            if (codes[i].opcode == OpCodes.Ret)
            {
                lambdaEnd = i;
                break;
            }
        }

        GD.Print($"[HostPriority-RPS] Lambda IL range: [{lambdaStart}, {lambdaEnd}]");

        // ── 构建作弊 lambda IL ─────────────────────────────────────────────────
        //作弊版本逻辑:
        //1. 调用原 lambda（对手出拳）
        //2. 读返回值，转 int
        //3. host = (opp + 1) % 3（永远克制）
        //4. 返回 (RelicPickingFightMove)host

        var cheatingIL = new List<CodeInstruction>();

        // 第1步：找 generateMove 的 ldloc 指令，在它之前把 lambda 执行了
        // 找 lambda ldloc 在哪里——应该在 lambdaStart 到 lambdaEnd 之间有个 ldloc 引用 _rng
        int rngLdlocIdx = -1;
        int rngLocalSlot = -1;
        for (int i = lambdaStart; i <= lambdaEnd; i++)
        {
            if (codes[i].opcode == OpCodes.Ldloc_S || codes[i].opcode == OpCodes.Ldloc
                || codes[i].opcode == OpCodes.Ldloc_0 || codes[i].opcode == OpCodes.Ldloc_1
                || codes[i].opcode == OpCodes.Ldloc_2 || codes[i].opcode == OpCodes.Ldloc_3)
            {
                var operand = codes[i].operand;
                if (operand is LocalBuilder lb && rngField != null)
                {
                    // 检查这个局部变量是不是 rng 类型（通过找 match）
                    rngLdlocIdx = i;
                    rngLocalSlot = lb.LocalIndex;
                    break;
                }
            }
        }

        if (rngLdlocIdx < 0)
        {
            GD.PushError($"[HostPriority-RPS] Cannot find rng ldloc in lambda IL.");
            return codes;
        }

        // 找 lambda 内 ldc.i4.3 (array creation)
        int arrayLenIdx = -1;
        for (int i = lambdaStart; i < rngLdlocIdx; i++)
        {
            if (codes[i].opcode == OpCodes.Ldc_I4_M1
                && codes[i].operand is int v && v == 3)
            {
                arrayLenIdx = i; break;
            }
            // 也可能是 ldc.i4.3 (shortcut)
            if ((int)codes[i].opcode.Value == 0x17) // ldc.i4.3
            {
                arrayLenIdx = i; break;
            }
        }

        if (arrayLenIdx < 0)
        {
            // 找 ldc.i4 或 ldc.i4.s
            for (int i = lambdaStart; i < rngLdlocIdx && i < lambdaStart + 5; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4
                    || codes[i].opcode == OpCodes.Ldc_I4_S
                    || codes[i].opcode == OpCodes.Ldc_I4_0
                    || codes[i].opcode == OpCodes.Ldc_I4_1
                    || codes[i].opcode == OpCodes.Ldc_I4_2
                    || codes[i].opcode == OpCodes.Ldc_I4_3
                    || codes[i].opcode == OpCodes.Ldc_I4_4
                    || codes[i].opcode == OpCodes.Ldc_I4_5
                    || codes[i].opcode == OpCodes.Ldc_I4_6
                    || codes[i].opcode == OpCodes.Ldc_I4_7
                    || codes[i].opcode == OpCodes.Ldc_I4_8)
                {
                    arrayLenIdx = i; break;
                }
            }
        }

        GD.Print($"[HostPriority-RPS] RNG ldloc at idx={rngLdlocIdx}(slot={rngLocalSlot}), arrayLen candidate at idx={arrayLenIdx}");

        // ── 实际替换策略 ──────────────────────────────────────────────────────
        // 找到 rngLdlocIdx 之前的 "ldc.i4.3 / call Enum.GetValues" 序列
        // 把整个 lambda 替换为作弊版本

        int genMoveLocalIdx = -1;
        LocalBuilder genMoveLocal = null;
        for (int i = lambdaStart - 1; i >= 0 && i > lambdaStart - 5; i--)
        {
            if (codes[i].opcode == OpCodes.Stloc_S || codes[i].opcode == OpCodes.Stloc
                || codes[i].opcode == OpCodes.Stloc_0 || codes[i].opcode == OpCodes.Stloc_1
                || codes[i].opcode == OpCodes.Stloc_2 || codes[i].opcode == OpCodes.Stloc_3)
            {
                genMoveLocalIdx = (codes[i].operand as LocalBuilder)?.LocalIndex ?? -1;
                if (genMoveLocalIdx >= 0)
                {
                    genMoveLocal = codes[i].operand as LocalBuilder;
                    break;
                }
            }
        }

        if (genMoveLocalIdx < 0)
        {
            GD.PushError($"[HostPriority-RPS] Cannot find generateMove stloc.");
            return codes;
        }

        GD.Print($"[HostPriority-RPS] generateMove stored at slot={genMoveLocalIdx}");

        // ── 替换 IL：把 lambda 的 stloc+ldloc 替换为作弊版本 ─────────────────
        // 替换点：从 lambdaStart 到 lambdaEnd（含 stloc 和 ldloc）
        var newCodes = new List<CodeInstruction>();

        for (int i = 0; i < lambdaStart; i++)
        {
            newCodes.Add(codes[i]);
        }

        // 跳过原 lambda IL（lambdaStart 到 lambdaEnd，含 stloc at genMoveLocalIdx 的前一个）
        // 找 stloc 位置
        int stlocIdx = -1;
        for (int i = lambdaStart; i <= lambdaEnd; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_S || codes[i].opcode == OpCodes.Stloc
                || codes[i].opcode == OpCodes.Stloc_0 || codes[i].opcode == OpCodes.Stloc_1
                || codes[i].opcode == OpCodes.Stloc_2 || codes[i].opcode == OpCodes.Stloc_3)
            {
                stlocIdx = i; break;
            }
        }

        // 在 stlocIdx 之前插入作弊 lambda 的 IL
        if (stlocIdx > lambdaStart)
        {
            for (int i = lambdaStart; i < stlocIdx; i++)
                newCodes.Add(codes[i]);
        }

        // 跳过 stloc 到 ret（含）
        // 找 ret
        int retIdx = lambdaEnd;
        for (int i = stlocIdx; i <= lambdaEnd; i++)
        {
            if (codes[i].opcode == OpCodes.Ret) { retIdx = i; break; }
        }

        // 插入作弊 IL：调用作弊工厂方法
        //作弊工厂 = static Func<Move> CheatMoveFactory(Func<Move> originalMoveGen, IRng rng, Move[] possibleMoves)
        var cheatMethod = typeof(RelicPickingTranspiler)
            .GetMethod("CheatMoveFactory", BindingFlags.NonPublic | BindingFlags.Static);

        if (cheatMethod != null)
        {
            // 构造作弊 lambda：call CheatMoveFactory(ldloc genMove, ldloc rng, ldloc array)
            // 但我们需要 ldloc rng 和 ldloc array 的索引
            int rngIdx = -1, arrayIdx = -1;

            // 找 lambda 内的 rng ldloc 和 array 相关的 ldloc
            for (int i = lambdaStart; i < stlocIdx; i++)
            {
                var op = codes[i].opcode;
                object operand = codes[i].operand;
                if (op == OpCodes.Ldloc_S || op == OpCodes.Ldloc
                    || op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1
                    || op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3)
                {
                    var lb = operand as LocalBuilder;
                    if (lb != null && rngIdx < 0)
                        rngIdx = lb.LocalIndex;
                }
            }

            // 找 array 相关（Enum.GetValues 后存到局部）
            for (int i = lambdaStart; i < stlocIdx; i++)
            {
                if (codes[i].opcode == OpCodes.Stloc_S || codes[i].opcode == OpCodes.Stloc
                    || codes[i].opcode == OpCodes.Stloc_0 || codes[i].opcode == OpCodes.Stloc_1
                    || codes[i].opcode == OpCodes.Stloc_2 || codes[i].opcode == OpCodes.Stloc_3)
                {
                    var lb = codes[i].operand as LocalBuilder;
                    if (lb != null && lb.LocalType == moveListType && arrayIdx < 0)
                        arrayIdx = lb.LocalIndex;
                }
            }

            GD.Print($"[HostPriority-RPS] Will call CheatMoveFactory, rngSlot={rngIdx}, arraySlot={arrayIdx}");

            // 跳过整个原 lambda（从 lambdaStart 到 retIdx）
            // newCodes 已包含 lambdaStart 之前的
            // 使用整数索引让 Harmony 自动创建 LocalBuilder 引用
            int rngSlot = rngIdx >= 0 ? rngIdx : 0;
            int arraySlot = arrayIdx >= 0 ? arrayIdx : 1;

            // 添加作弊 lambda 的 IL：call CheatMoveFactory(ldloc genMove, ldloc rng, ldloc array)
            newCodes.Add(new CodeInstruction(OpCodes.Ldloc_S, genMoveLocalIdx));
            newCodes.Add(new CodeInstruction(OpCodes.Ldloc_S, rngSlot));
            newCodes.Add(new CodeInstruction(OpCodes.Ldloc_S, arraySlot));
            newCodes.Add(new CodeInstruction(OpCodes.Call, cheatMethod));
            newCodes.Add(new CodeInstruction(OpCodes.Stloc_S, genMoveLocal ?? il.DeclareLocal(cheatMethod.ReturnType)));
            newCodes.Add(new CodeInstruction(OpCodes.Ret));

            GD.Print($"[HostPriority-RPS] Inserted cheat factory IL: call CheatMoveFactory");

            // 继续添加剩余 IL（retIdx 之后）
            for (int i = retIdx + 1; i < codes.Count; i++)
            {
                newCodes.Add(codes[i]);
            }

            return newCodes.AsEnumerable();
        }
        else
        {
            GD.PushError("[HostPriority-RPS] CheatMoveFactory method not found!");
            return codes;
        }
    }

    // ── 3. 作弊工厂（静态方法，由 Transpiler 的 IL 调用）─────────────────────
    /// <summary>
    /// 由 Transpiler 注入 IL 调用。
    /// 传入原 lambda（对手出拳）和 RNG，返回作弊 lambda（克制拳）。
    /// </summary>
    private static object CheatMoveFactory(object originalFunc, object rng, object possibleMovesArray)
    {
        try
        {
            GD.Print($"[HostPriority-RPS] CheatMoveFactory called. Enabled={HostPriorityMod.Enabled}");
            if (!HostPriorityMod.Enabled || originalFunc == null)
                return originalFunc;

            // 读取原 lambda 的类型
            var funcType = originalFunc.GetType();
            var invokeMethod = funcType.GetMethod("Invoke");
            if (invokeMethod == null)
            {
                GD.PushError("[HostPriority-RPS] Cannot find Invoke on originalFunc!");
                return originalFunc;
            }

            var moveType = Type.GetType("MegaCrit.Sts2.Core.Entities.TreasureRelicPicking.RelicPickingFightMove, sts2")
                ?? AccessTools.TypeByName("RelicPickingFightMove")
                ?? typeof(Enum);

            // 动态构建作弊 Func<Move>
            var cheatLambda = BuildCheatDelegate(funcType, invokeMethod, moveType);
            if (cheatLambda != null)
            {
                GD.Print("[HostPriority-RPS] CheatMoveFactory: cheating lambda created successfully!");
                return cheatLambda;
            }
            else
            {
                GD.PushError("[HostPriority-RPS] CheatMoveFactory: failed to create cheating lambda.");
                return originalFunc;
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"[HostPriority-RPS] CheatMoveFactory EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            return originalFunc;
        }
    }

    private static object BuildCheatDelegate(Type funcType, MethodInfo invokeMethod, Type moveType)
    {
        try
        {
            // 动态构造作弊 lambda：
            // () => {
            //     var opponentMove = ((Func<Move>)originalFunc)();
            //     int opp = Convert.ToInt32(opponentMove);
            //     int host = (opp + 1) % 3;
            //     return (Move)Enum.ToObject(moveType, host);
            // }
            var returnType = invokeMethod.ReturnType;
            var dm = new DynamicMethod(
                "CheatMove_" + Guid.NewGuid().ToString("N"),
                returnType,
                Type.EmptyTypes,
                typeof(RelicPickingTranspiler).Module,
                true);

            var il = dm.GetILGenerator();

            // 调用原 lambda
            il.Emit(OpCodes.Ldnull); // 静态 lambda 没有 target
            il.Emit(OpCodes.Ldftn, invokeMethod);
            il.Emit(OpCodes.Newobj, funcType.GetConstructors()[0]);

            // 调用 invoke
            il.Emit(OpCodes.Callvirt, invokeMethod);

            // 结果装箱（如果是值类型）
            if (moveType.IsValueType)
                il.Emit(OpCodes.Box, moveType);

            // 拆箱转 int
            il.Emit(OpCodes.Unbox_Any, typeof(int));
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldc_I4_3);
            il.Emit(OpCodes.Rem);

            // 转回 enum
            var toObject = typeof(Enum).GetMethod("ToObject", new[] { typeof(Type), typeof(int) });
            il.Emit(OpCodes.Ldtoken, moveType);
            il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
            il.Emit(OpCodes.Call, toObject);
            il.Emit(OpCodes.Unbox_Any, moveType);
            il.Emit(OpCodes.Ret);

            return dm.CreateDelegate(funcType);
        }
        catch (Exception ex)
        {
            GD.PushError($"[HostPriority-RPS] BuildCheatDelegate failed: {ex.Message}");
            return null;
        }
    }
}
