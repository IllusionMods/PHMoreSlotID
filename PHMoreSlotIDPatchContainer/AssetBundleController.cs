using System;
using System.IO;
using UnityEngine;

namespace PHMoreSlotIDPatchContainer
{
    public static class AssetBundleController
    {
        public static T LoadAsset<T>(string assetName, AssetBundle assetBundle, string directory, string assetBundleName) where T : UnityEngine.Object
        {
            if (typeof(T) == typeof(Texture2D) && (assetBundleName.Contains("thumnbnail/thumbnail_") || assetBundleName.Contains("thumnbnail/thumnbs_")))
            {
                var path = directory + "/thumnbnail_R/" + assetName + ".png";
                if (File.Exists(path))
                    return LoadPNG(path) as T;
            }

            if (assetBundle != null)
                return assetBundle.LoadAsset<T>(assetName);

            return default;
        }

        public static Texture2D LoadPNG(string file)
        {
            try
            {
                using (var binaryReader = new BinaryReader(new FileStream(file, FileMode.Open, FileAccess.Read)))
                {
                    var pngBytes = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);
                    var texture2D = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                    texture2D.LoadImage(pngBytes);
                    return texture2D;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
                return null;
            }
        }
    }
}
