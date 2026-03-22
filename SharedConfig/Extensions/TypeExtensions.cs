using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SharedConfig.Extensions;

public static class TypeExtensions
{
    private static Dictionary<Type, FieldInfo[]>? _declaredFields;

    /// <summary>
    /// Finds a field in a generated state machine class for an async method,
    /// given the state machine type and the name of the original field.
    /// </summary>
    public static FieldInfo FindStateMachineField(this Type type, string originalFieldName)
    {
        string stateMachineFieldName = $"<{originalFieldName}>";

        if (_declaredFields == null) _declaredFields = [];
        if (!_declaredFields.TryGetValue(type, out var declaredFields))
        {
            declaredFields = type.GetDeclaredFields().ToArray();
            _declaredFields[type] = declaredFields;
        }

        foreach (FieldInfo field in declaredFields)
        {
            if (field.Name.StartsWith(stateMachineFieldName))
            {
                return field;
            }
            else if (field.Name.Equals(originalFieldName))
            {
                return field;
            }
        }

        throw new ArgumentException($"No matching field found in type {type} for name {originalFieldName}");
    }
}
