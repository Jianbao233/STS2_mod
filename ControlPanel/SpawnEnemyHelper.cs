using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace ControlPanel;

/// <summary>
/// 战斗内生成敌人：通过反射调用 CreatureCmd.Add 和 ModelDb。
/// </summary>
public static class SpawnEnemyHelper
{
    private static string _monsterCategoryCache;

    /// <summary>获取可生成的怪物列表 (id, zh)。参考 ParasiteSpire 使用 CreatureCmd.Add</summary>
    public static (string id, string zh)[] GetSpawnableMonsterIds()
    {
        var result = new List<(string, string)>();
        try
        {
            var modelDb = GetModelDbType();
            if (modelDb == null) { GD.PrintErr("[SpawnEnemy] ModelDb 未找到"); return Array.Empty<(string, string)>(); }
            var monstersProp = modelDb.GetProperty("Monsters") ?? modelDb.GetProperty("AllMonsters") ?? modelDb.GetProperty("Creatures");
            if (monstersProp == null) { GD.PrintErr("[SpawnEnemy] ModelDb 无 Monsters 属性"); return Array.Empty<(string, string)>(); }
            var monsters = monstersProp.GetValue(null) as System.Collections.IEnumerable;
            if (monsters == null) return Array.Empty<(string, string)>();
            foreach (var m in monsters)
            {
                if (m == null) continue;
                var idProp = m.GetType().GetProperty("Id");
                if (idProp == null) continue;
                var idObj = idProp.GetValue(m);
                if (idObj == null) continue;
                var catProp = idObj.GetType().GetProperty("Category");
                var entryProp = idObj.GetType().GetProperty("Entry");
                var cat = catProp?.GetValue(idObj) as string;
                var entry = entryProp?.GetValue(idObj) as string ?? idObj.ToString();
                if (string.IsNullOrEmpty(entry)) continue;
                if (!string.IsNullOrEmpty(cat)) _monsterCategoryCache = cat;
                var titleProp = m.GetType().GetProperty("Title");
                var zh = "";
                if (titleProp != null)
                {
                    var title = titleProp.GetValue(m);
                    var fmt = title?.GetType().GetMethod("GetFormattedText");
                    if (fmt != null) zh = fmt.Invoke(title, null) as string ?? "";
                }
                result.Add((entry, zh ?? ""));
            }
        }
        catch (Exception e) { GD.PrintErr($"[SpawnEnemy] GetList: {e.Message}"); }
        return result.OrderBy(x => x.Item1).ToArray();
    }

    /// <summary>在当前战斗中生成指定 ID 的敌人</summary>
    public static void SpawnInCombat(string monsterId)
    {
        try
        {
            var combatMgr = GetCombatManager();
            if (combatMgr == null) { GD.PrintErr("[SpawnEnemy] CombatManager 未找到"); return; }
            var combatType = combatMgr.GetType();
            var inProgress = combatType.GetProperty("IsInProgress")?.GetValue(combatMgr) as bool?;
            if (inProgress != true) { GD.PrintErr("[SpawnEnemy] 需在战斗中"); return; }
            var state = combatType.GetMethod("DebugOnlyGetState")?.Invoke(combatMgr, null);
            if (state == null) { GD.PrintErr("[SpawnEnemy] CombatState 未找到"); return; }

            var modelDb = GetModelDbType();
            if (modelDb == null) { GD.PrintErr("[SpawnEnemy] ModelDb 未找到"); return; }
            var modelIdType = GetModelIdType();
            var getById = modelDb.GetMethod("GetById", BindingFlags.Public | BindingFlags.Static, null, new[] { modelIdType }, null);
            if (getById == null && modelIdType != null)
                getById = modelDb.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "GetById" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            if (getById == null) { GD.PrintErr("[SpawnEnemy] GetById 未找到"); return; }

            var modelId = CreateModelId(monsterId);
            if (modelId == null) { GD.PrintErr($"[SpawnEnemy] 无效 ID: {monsterId}"); return; }

            var monster = getById.MakeGenericMethod(GetMonsterModelType()).Invoke(null, new[] { modelId });
            if (monster == null) { GD.PrintErr($"[SpawnEnemy] 怪物未找到: {monsterId}"); return; }

            var toMutable = monster.GetType().GetMethod("ToMutable");
            var mutable = toMutable?.Invoke(monster, null);
            if (mutable == null) { GD.PrintErr("[SpawnEnemy] ToMutable 失败"); return; }

            var creatureCmd = GetCreatureCmdType();
            if (creatureCmd == null) { GD.PrintErr("[SpawnEnemy] CreatureCmd 未找到"); return; }
            var combatStateType = GetType("MegaCrit.Sts2.Core.Combat.CombatState") ?? GetType("CombatState");
            var addMethod = creatureCmd.GetMethod("Add", new[] { GetMonsterModelType(), combatStateType ?? state.GetType(), GetCombatSideType(), typeof(string) });
            if (addMethod == null) { GD.PrintErr("[SpawnEnemy] CreatureCmd.Add 未找到"); return; }

            var combatSideEnemy = Enum.Parse(GetCombatSideType(), "Enemy");
            var task = addMethod.Invoke(null, new[] { mutable, state, combatSideEnemy, (string)null });
            if (task != null)
            {
                var getAwaiter = task.GetType().GetMethod("GetAwaiter");
                var awaiter = getAwaiter?.Invoke(task, null);
                if (awaiter != null)
                {
                    var getResult = awaiter.GetType().GetMethod("GetResult");
                    getResult?.Invoke(awaiter, null);
                }
            }
            GD.Print($"[SpawnEnemy] 已生成 {monsterId}");
        }
        catch (Exception e) { GD.PrintErr($"[SpawnEnemy] SpawnInCombat: {e}"); }
    }

    private static Type GetModelDbType()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("MegaCrit.Sts2.Core.Models.ModelDb") ?? asm.GetType("ModelDb");
            if (t != null) return t;
        }
        return null;
    }

    private static Type GetModelIdType()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("MegaCrit.Sts2.Core.Models.ModelId") ?? asm.GetType("ModelId");
            if (t != null) return t;
        }
        return null;
    }

    private static Type GetMonsterModelType()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("MegaCrit.Sts2.Core.Models.Monsters.MonsterModel") ?? asm.GetType("MonsterModel");
            if (t != null) return t;
        }
        return null;
    }

    private static Type GetCombatSideType()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("MegaCrit.Sts2.Core.Combat.CombatSide") ?? asm.GetType("CombatSide");
            if (t != null) return t;
        }
        return null;
    }

    private static object GetCombatManager()
    {
        var t = GetType("MegaCrit.Sts2.Core.Combat.CombatManager");
        return t?.GetProperty("Instance")?.GetValue(null);
    }

    private static Type GetCreatureCmdType()
    {
        return GetType("MegaCrit.Sts2.Core.Commands.CreatureCmd");
    }

    private static Type GetType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    private static object CreateModelId(string entry)
    {
        try
        {
            var modelIdType = GetModelIdType();
            if (modelIdType == null) return null;
            var trimmed = entry?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmed)) return null;
            string category, ent;
            if (trimmed.Contains("."))
            {
                var parts = trimmed.Split(new[] { '.' }, 2);
                category = parts[0]; ent = parts[1];
            }
            else
            {
                category = _monsterCategoryCache ?? "MONSTER";
                ent = trimmed;
            }
            var ctor = modelIdType.GetConstructor(new[] { typeof(string), typeof(string) });
            if (ctor != null) return ctor.Invoke(new object[] { category, ent });
        }
        catch (Exception e) { GD.PrintErr($"[SpawnEnemy] CreateModelId: {e.Message}"); }
        return null;
    }
}
