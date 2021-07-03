using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;

namespace PHMoreSlotIDPatchContainer
{
    public static class CustomDataSetupLoader<T_Data> where T_Data : class
    {
        public static void Setup(Dictionary<int, T_Data> datas, global::AssetBundleController abc, Action<Dictionary<int, T_Data>, global::AssetBundleController, CustomDataListLoader> action)
        {
            var bundleName = Path.GetFileNameWithoutExtension(abc.assetBundleName);
            var listPath = abc.directory + "/list/" + abc.assetBundleName + "_list.txt";
            if(File.Exists(listPath))
            {
                var customDataListLoader = new CustomDataListLoader();
                customDataListLoader.Load(listPath);
                action(datas, abc, customDataListLoader);
                return;
            }

            var listAsset = abc.LoadAsset<TextAsset>(bundleName + "_list");
            if(listAsset)
            {
                var customDataListLoader2 = new CustomDataListLoader();
                customDataListLoader2.Load(listAsset);
                action(datas, abc, customDataListLoader2);
            }
        }

        private static MethodInfo CustomDataListLoader_Load = 
            typeof(CustomDataListLoader).GetMethod("Load", new Type[]{typeof(TextReader)});

        public static void Setup_Search(Dictionary<int, T_Data> datas, string search, Action<Dictionary<int, T_Data>, global::AssetBundleController, CustomDataListLoader> action)
        {
            var dir = "";
            var lastSlash = search.LastIndexOf("/", StringComparison.Ordinal);

            if(lastSlash != -1)
            {
                dir = search.Substring(0, lastSlash);
                search = search.Remove(0, lastSlash + 1);
            }

            var bundleDir = GlobalData.assetBundlePath + "/" + dir;
            var files = Directory.GetFiles(bundleDir, search, SearchOption.TopDirectoryOnly);
            Array.Sort(files);
            foreach(var bundlePath in files)
            {
                if(Path.GetExtension(bundlePath).Length == 0)
                {
                    var bundleName = Path.GetFileNameWithoutExtension(bundlePath);
                    if(dir.Length > 0)
                        bundleName = dir + "/" + bundleName;

                    var assetBundleController = new global::AssetBundleController();
                    assetBundleController.OpenFromFile(GlobalData.assetBundlePath, bundleName);
                    Setup(datas, assetBundleController, action);
                    assetBundleController.Close(false);
                }
            }

            var listDir = GlobalData.assetBundlePath + "/list/" + dir;
            if(!Directory.Exists(listDir))
                return;

            foreach(var text3 in Directory.GetFiles(listDir, search + "_Mlist.txt"))
            {
                using(var streamReader = new StreamReader(new FileStream(text3, FileMode.Open)))
                {
                    var assetBundleName = streamReader.ReadLine();
                    var customDataListLoader = new CustomDataListLoader();
                    CustomDataListLoader_Load.Invoke(customDataListLoader, new object[]{ streamReader });
                    var assetBundleController2 = new global::AssetBundleController();
                    assetBundleController2.OpenFromFile(GlobalData.assetBundlePath, assetBundleName);
                    action(datas, assetBundleController2, customDataListLoader);
                    assetBundleController2.Close(false);
                }
            }
        }
    }
}
