using System;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace PHMoreSlotIDPatchContainer
{
    public static class AssetBundleController
    {
        private static FieldInfo assetBundleFieldInfo = 
            typeof(global::AssetBundleController).GetField("assetBundle", BindingFlags.NonPublic | BindingFlags.Instance);

        public static T LoadAsset<T>(string assetName, global::AssetBundleController abc) where T : UnityEngine.Object
        {
            AssetBundle assetBundle = assetBundleFieldInfo.GetValue(abc) as AssetBundle;
            if (assetBundle == null)
            {
                string text = abc.directory + "/" + abc.assetBundleName;
                if (File.Exists(text))
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    assetBundleFieldInfo.SetValue(abc, AssetBundle.LoadFromFile(text));
                    if (stopwatch.ElapsedMilliseconds > 100L)
                    {
                        Console.WriteLine("LoadFromFile is slow for: {0}, {1}ms\n", abc.assetBundleName, stopwatch.ElapsedMilliseconds.ToString());
                    }
                    stopwatch.Stop();
                    assetBundle = (assetBundleFieldInfo.GetValue(abc) as AssetBundle);
                }
                else
                {
                    UnityEngine.Debug.Log("アセットバンドルない：" + text);
                }
            }
            if (assetBundle != null)
            {
                T t = assetBundle.LoadAsset<T>(assetName);
                if (typeof(T) == typeof(Texture2D) && t == null && (abc.assetBundleName.Contains("thumnbnail/thumbnail_") || abc.assetBundleName.Contains("thumnbnail/thumnbs_")))
                {
                    string pngPath = abc.directory + "/thumnbnail_R/" + assetName + ".png";
                    if (File.Exists(pngPath))
                    {
                        return LoadPNG(pngPath) as T;
                    }
                }
                return t;
            }
            return default(T);
        }

        public static Texture2D LoadPNG(string file)
        {
            try
            {
                var pngBytes = File.ReadAllBytes(file);
                var texture2D = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                texture2D.LoadImage(pngBytes);
                return texture2D;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(ex);
                return null;
            }
        }
    }
}
