using Iced.Intel;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using System.Collections.Generic;
using System.Reflection;

namespace RetroCamera.Utilities;

/// Bloodstone IL2CPP Method Resolver (thank you Deca!)
internal static class MethodResolver
{
    static ulong ExtractTargetAddress(in Instruction instruction)
    {
        return instruction.Op0Kind switch
        {
            OpKind.FarBranch16 => instruction.FarBranch16,
            OpKind.FarBranch32 => instruction.FarBranch32,
            _ => instruction.NearBranchTarget,
        };
    }

    static unsafe IntPtr ResolveMethodPointer(IntPtr methodPointer)
    {
        var originalPointer = methodPointer;
        var patterns = new List<string>();

        for (var depth = 0; depth < 6; depth++)
        {
            var stream = new UnmanagedMemoryStream((byte*)methodPointer, 256, 256, FileAccess.Read);
            var decoder = Decoder.Create(IntPtr.Size == 8 ? 64 : 32, new StreamCodeReader(stream));
            decoder.IP = (ulong)methodPointer.ToInt64();

            ulong movRaxImm = 0;
            bool resolved = false;

            for (var i = 0; i < 16; i++)
            {
                decoder.Decode(out var instr);

                if (instr.Mnemonic == Mnemonic.Int3)
                    break;

                if (instr.Mnemonic == Mnemonic.Add)
                {
                    if (instr.Op0Register == Register.RCX && instr.Immediate32 == 0x10)
                        continue;

                    break;
                }

                if (instr.Mnemonic == Mnemonic.Mov &&
                    instr.Op0Register == Register.RAX &&
                    instr.Op1Kind == OpKind.Immediate64)
                {
                    movRaxImm = instr.Immediate64;
                    continue;
                }

                if (instr.Mnemonic == Mnemonic.Jmp)
                {
                    if (instr.Op0Kind == OpKind.Register && instr.Op0Register == Register.RAX && movRaxImm != 0)
                    {
                        methodPointer = (IntPtr)(long)movRaxImm;
                        patterns.Add("mov rax, imm64; jmp rax");
                        resolved = true;
                        break;
                    }

                    if (instr.Op0Kind == OpKind.NearBranch16 ||
                        instr.Op0Kind == OpKind.NearBranch32 ||
                        instr.Op0Kind == OpKind.NearBranch64 ||
                        instr.Op0Kind == OpKind.FarBranch16 ||
                        instr.Op0Kind == OpKind.FarBranch32)
                    {
                        methodPointer = new IntPtr((long)ExtractTargetAddress(instr));
                        patterns.Add("jmp imm");
                        resolved = true;
                        break;
                    }

                    if (instr.IsIPRelativeMemoryOperand)
                    {
                        methodPointer = (IntPtr)(long)(*(ulong*)instr.IPRelativeMemoryAddress);
                        patterns.Add("jmp [rip+disp32]");
                        resolved = true;
                        break;
                    }

                    break;
                }

                if (instr.Mnemonic == Mnemonic.Push &&
                    (instr.Op0Kind == OpKind.Immediate64 || instr.Op0Kind == OpKind.Immediate32))
                {
                    decoder.Decode(out var next);

                    if (next.Mnemonic == Mnemonic.Ret)
                    {
                        ulong imm = instr.Op0Kind == OpKind.Immediate64 ? instr.Immediate64 : instr.Immediate32;
                        methodPointer = (IntPtr)(long)imm;
                        patterns.Add("push imm64; ret");
                        resolved = true;
                    }

                    break;
                }

                break;
            }

            if (!resolved)
                break;
        }

        if (patterns.Count > 0)
            Core.Log.LogInfo($"[MethodResolver] Resolved {originalPointer} -> {methodPointer} via {string.Join(" -> ", patterns)}");
        else
            Core.Log.LogInfo($"[MethodResolver] No resolution for {methodPointer}");

        return methodPointer;
    }
    public static unsafe IntPtr ResolveFromMethodInfo(INativeMethodInfoStruct methodInfo)
    {
        return ResolveMethodPointer(methodInfo.MethodPointer);
    }
    public static unsafe IntPtr ResolveFromMethodInfo(MethodInfo method)
    {
        var methodInfoField = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method) ?? throw new Exception($"Couldn't obtain method info for {method}");
        var il2cppMethod = UnityVersionHandler.Wrap((Il2CppMethodInfo*)(IntPtr)(methodInfoField.GetValue(null) ?? IntPtr.Zero));
        return il2cppMethod == null ? throw new Exception($"Method info for {method} is invalid") : ResolveFromMethodInfo(il2cppMethod);
    }
}
