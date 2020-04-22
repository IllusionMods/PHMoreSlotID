using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PHMoreSlotID
{
    public static class MoreSlut
    {
        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

        public static void Patch(AssemblyDefinition ass)
        {
            var hookAss = AssemblyDefinition.ReadAssembly(Path.Combine(BepInEx.Paths.PatcherPluginPath, "PHMoreSlotIDPatchContainer.dll"));

            {
                var targetType = ass.MainModule.GetType("CustomDataSetupLoader`1");
                var targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == "Setup");

                var hookMethod = hookAss.MainModule.GetType("PHMoreSlotIDPatchContainer.CustomDataSetupLoader`1").Methods.FirstOrDefault(m => m.Name == "Setup");
                var hookRef = ass.MainModule.ImportReference(hookMethod);

                var action = targetType.Fields.FirstOrDefault(m => m.Name == "action");

                var il = targetMethod.Body.GetILProcessor();
                var ins = targetMethod.Body.Instructions.First();

                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_2));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ins, il.Create(OpCodes.Ldfld, action));
                il.InsertBefore(ins, il.Create(OpCodes.Call, hookRef));
                il.InsertBefore(ins, il.Create(OpCodes.Ret));
            }

            {
                var targetType = ass.MainModule.GetType("CustomDataSetupLoader`1");
                var targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == "Setup_Search");

                var hookMethod = hookAss.MainModule.GetType("PHMoreSlotIDPatchContainer.CustomDataSetupLoader`1").Methods.FirstOrDefault(m => m.Name == "Setup_Search");
                var hookRef = ass.MainModule.ImportReference(hookMethod);

                var action = targetType.Fields.FirstOrDefault(m => m.Name == "action");

                var il = targetMethod.Body.GetILProcessor();
                var ins = targetMethod.Body.Instructions.First();

                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_2));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ins, il.Create(OpCodes.Ldfld, action));
                il.InsertBefore(ins, il.Create(OpCodes.Call, hookRef));
                il.InsertBefore(ins, il.Create(OpCodes.Ret));
            }

            {
                var targetType = ass.MainModule.GetType("AssetBundleController");
                var targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == "LoadAsset");

                var hookMethod = hookAss.MainModule.GetType("PHMoreSlotIDPatchContainer.AssetBundleController").Methods.FirstOrDefault(m => m.Name == "LoadAsset");
                var hookRef = ass.MainModule.ImportReference(hookMethod);
                var hookGenericRef = new GenericInstanceMethod(hookRef);
                hookGenericRef.GenericArguments.Add(targetMethod.GenericParameters[0]);

                var assetBundle = targetType.Fields.FirstOrDefault(m => m.Name == "assetBundle");
                var directory = targetType.Properties.FirstOrDefault(m => m.Name == "directory").GetMethod;
                var assetBundleName = targetType.Properties.FirstOrDefault(m => m.Name == "assetBundleName").GetMethod;

                var il = targetMethod.Body.GetILProcessor();
                var ins = targetMethod.Body.Instructions.First();

                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ins, il.Create(OpCodes.Ldfld, assetBundle));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ins, il.Create(OpCodes.Callvirt, directory));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ins, il.Create(OpCodes.Callvirt, assetBundleName));
                il.InsertBefore(ins, il.Create(OpCodes.Call, hookGenericRef));
                il.InsertBefore(ins, il.Create(OpCodes.Ret));
            }

            {
                var targetType = ass.MainModule.GetType("ItemDataBase");
                var targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == ".ctor" && m.Parameters.Count == 5);

                var hookMethod = hookAss.MainModule.GetType("PHMoreSlotIDPatchContainer.ItemDataBase").Methods.FirstOrDefault(m => m.Name == "CtorPostfix");
                var hookRef = ass.MainModule.ImportReference(hookMethod);

                var id = targetType.Fields.FirstOrDefault(m => m.Name == "id");

                var il = targetMethod.Body.GetILProcessor();
                var ins = targetMethod.Body.Instructions.Last();

                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ins, il.Create(OpCodes.Ldflda, id));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(ins, il.Create(OpCodes.Call, hookRef));
            }
        }
    }
}
