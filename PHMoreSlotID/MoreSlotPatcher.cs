using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace PHMoreSlotID
{
    public static class MoreSlotPatcher
    {
        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

        public static void Patch(AssemblyDefinition ass)
        {
            var hardpatched = ass.MainModule.Resources.Any(r => string.Equals(r.Name, "ILRepack.List‎", StringComparison.InvariantCultureIgnoreCase));
            if (hardpatched)
            {
                Console.WriteLine("Could not patch PHMoreSlotID because the assembly is already hardpatched. Restore the original Assembly-CSharp and try again.");
                return;
            }

            foreach (var subdir in new[]
            {
                @"abdata\list\accessory",
                @"abdata\list\custommaterial",
                @"abdata\list\customtexture",
                @"abdata\list\hair",
                @"abdata\list\wear",
                @"abdata\thumnbnail_R",
            })
            {
                // Make sure the directories exist to mimic how the original was distributed, and to prevent crashes later on
                Directory.CreateDirectory(Path.Combine(Paths.GameRootPath, subdir));
            }

            var hookAss = AssemblyDefinition.ReadAssembly(Path.Combine(BepInEx.Paths.PatcherPluginPath, "PHMoreSlotIDPatchContainer.dll"));

            var customDataSetupLoader = ass.MainModule.GetType("CustomDataSetupLoader`1") ?? throw new EntryPointNotFoundException("CustomDataSetupLoader`1");
            var customDataSetupLoaderAction = customDataSetupLoader.Fields.FirstOrDefault(m => m.Name == "action") ?? throw new EntryPointNotFoundException("action");

            {
                var targetMethod = customDataSetupLoader.Methods.FirstOrDefault(m => m.Name == "Setup") ?? throw new EntryPointNotFoundException("Setup");

                var hookMethod = hookAss.MainModule.GetType("PHMoreSlotIDPatchContainer.CustomDataSetupLoader`1").Methods.FirstOrDefault(m => m.Name == "Setup");
                var hookRef = ass.MainModule.ImportReference(hookMethod);

                var il = targetMethod.Body.GetILProcessor();
                var ins = targetMethod.Body.Instructions.First();

                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_2));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ins, il.Create(OpCodes.Ldfld, customDataSetupLoaderAction));
                il.InsertBefore(ins, il.Create(OpCodes.Call, hookRef));
                il.InsertBefore(ins, il.Create(OpCodes.Ret));
            }

            {
                var targetMethod = customDataSetupLoader.Methods.FirstOrDefault(m => m.Name == "Setup_Search") ?? throw new EntryPointNotFoundException("Setup_Search");

                var hookMethod = hookAss.MainModule.GetType("PHMoreSlotIDPatchContainer.CustomDataSetupLoader`1").Methods.FirstOrDefault(m => m.Name == "Setup_Search");
                var hookRef = ass.MainModule.ImportReference(hookMethod);

                var il = targetMethod.Body.GetILProcessor();
                var ins = targetMethod.Body.Instructions.First();

                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_2));
                il.InsertBefore(ins, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ins, il.Create(OpCodes.Ldfld, customDataSetupLoaderAction));
                il.InsertBefore(ins, il.Create(OpCodes.Call, hookRef));
                il.InsertBefore(ins, il.Create(OpCodes.Ret));
            }

            {
                var targetType = ass.MainModule.GetType("AssetBundleController");
                var targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == "LoadAsset") ?? throw new EntryPointNotFoundException("LoadAsset");

                var hookMethod = hookAss.MainModule.GetType("PHMoreSlotIDPatchContainer.AssetBundleController").Methods.FirstOrDefault(m => m.Name == "LoadAsset");
                var hookRef = ass.MainModule.ImportReference(hookMethod);
                var hookGenericRef = new GenericInstanceMethod(hookRef);
                hookGenericRef.GenericArguments.Add(targetMethod.GenericParameters[0]);

                var assetBundle = targetType.Fields.FirstOrDefault(m => m.Name == "assetBundle") ?? throw new EntryPointNotFoundException("assetBundle");
                var directory = targetType.Properties.FirstOrDefault(m => m.Name == "directory")?.GetMethod ?? throw new EntryPointNotFoundException("directory");
                var assetBundleName = targetType.Properties.FirstOrDefault(m => m.Name == "assetBundleName")?.GetMethod ?? throw new EntryPointNotFoundException("assetBundleName");

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
                var targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == ".ctor" && m.Parameters.Count == 5) ?? throw new EntryPointNotFoundException("ItemDataBase.ctor");

                var hookMethod = hookAss.MainModule.GetType("PHMoreSlotIDPatchContainer.ItemDataBase").Methods.FirstOrDefault(m => m.Name == "CtorPostfix");
                var hookRef = ass.MainModule.ImportReference(hookMethod);

                var id = targetType.Fields.FirstOrDefault(m => m.Name == "id") ?? throw new EntryPointNotFoundException("id");

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
