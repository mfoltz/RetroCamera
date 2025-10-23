using BepInEx.Unity.IL2CPP.Hook;
using HarmonyLib;
using System.Reflection;
using System.Linq;

namespace RetroCamera.Utilities;
internal static class NativeDetour
{
    public static INativeDetour Create<T>(Type type, string innerTypeName, string methodName, T to, out T original) where T : Delegate
    {
        return Create(GetInnerType(type, innerTypeName, methodName), methodName, to, out original);
    }
    public static INativeDetour Create<T>(Type type, string methodName, T to, out T original) where T : Delegate
    {
        return Create(type.GetMethod(methodName, AccessTools.all), to, out original);
    }

    public static INativeDetour CreateBySignature<T>(
        Type type,
        Func<Type, bool> nestedTypePredicate,
        Func<MethodInfo, bool> methodPredicate,
        T to,
        out T original) where T : Delegate
    {
        var nestedType = type.GetNestedTypes()
            .FirstOrDefault(nestedTypePredicate)
            ?? throw new ArgumentException("Nested type not found", nameof(nestedTypePredicate));

        var method = nestedType
            .GetMethods(AccessTools.all)
            .FirstOrDefault(methodPredicate)
            ?? throw new ArgumentException("Method not found", nameof(methodPredicate));

        return Create(method, to, out original);
    }

    static INativeDetour Create<T>(MethodInfo method, T to, out T original) where T : Delegate
    {
        var address = MethodResolver.ResolveFromMethodInfo(method);
        return INativeDetour.CreateAndApply(address, to, out original);
    }

    static Type GetInnerType(Type type, string innerTypeName, string methodName)
    {
        var candidates = type
            .GetNestedTypes()
            .Where(x => x.Name.Contains(innerTypeName))
            .Where(x => x.GetMethod(methodName, AccessTools.all) != null)
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new ArgumentException(
                $"Nested type containing method '{methodName}' not found for substring '{innerTypeName}'",
                nameof(innerTypeName));
        }

        return candidates[0];
    }
}
