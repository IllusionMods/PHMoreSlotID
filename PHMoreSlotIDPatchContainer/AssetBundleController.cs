using System;
using System.IO;
using UnityEngine;

namespace PHMoreSlotIDPatchContainer
{
	public static class AssetBundleController
	{
		public static T LoadAsset<T>(string assetName, AssetBundle assetBundle, string directory, string assetBundleName) where T : UnityEngine.Object
		{
			if(typeof(T) == typeof(Texture2D) && (assetBundleName.Contains("thumnbnail/thumbnail_") || assetBundleName.Contains("thumnbnail/thumnbs_")))
			{
				string text = directory + "/thumnbnail_R/" + assetName + ".png";
				if(File.Exists(text))
				{
					return (T)((object)LoadPNG(text));
				}
			}

			if(assetBundle != null)
			{
				return assetBundle.LoadAsset<T>(assetName);
			}

			return default;
		}

		public static Texture2D LoadPNG(string file)
		{
			byte[] array;
			using(BinaryReader binaryReader = new BinaryReader(new FileStream(file, FileMode.Open, FileAccess.Read)))
			{
				try
				{
					array = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);
				}
				catch(Exception message)
				{
					Debug.LogWarning(message);
					array = null;
				}
				binaryReader.Close();
			}
			if(array == null)
			{
				return null;
			}
			Texture2D texture2D = new Texture2D(1, 1, TextureFormat.ARGB32, false);
			texture2D.LoadImage(array);
			return texture2D;
		}
	}
}
