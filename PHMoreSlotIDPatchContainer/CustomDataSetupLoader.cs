using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PHMoreSlotIDPatchContainer
{
	public static class CustomDataSetupLoader<T_Data> where T_Data : class
	{
		public static void Setup(Dictionary<int, T_Data> datas, global::AssetBundleController abc, Action<Dictionary<int, T_Data>, global::AssetBundleController, CustomDataListLoader> action)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(abc.assetBundleName);
			string text = abc.directory + "/list/" + abc.assetBundleName + "_list.txt";
			if(File.Exists(text))
			{
				Console.WriteLine("Load list:" + text);
				CustomDataListLoader customDataListLoader = new CustomDataListLoader();
				customDataListLoader.Load(text);
				action(datas, abc, customDataListLoader);
				return;
			}
			TextAsset textAsset = abc.LoadAsset<TextAsset>(fileNameWithoutExtension + "_list");
			if(textAsset != null)
			{
				CustomDataListLoader customDataListLoader2 = new CustomDataListLoader();
				customDataListLoader2.Load(textAsset);
				action(datas, abc, customDataListLoader2);
			}
		}

		public static void Setup_Search(Dictionary<int, T_Data> datas, string search, Action<Dictionary<int, T_Data>, global::AssetBundleController, CustomDataListLoader> action)
		{
			string text = string.Empty;
			int num = search.LastIndexOf("/");
			if(num != -1)
			{
				text = search.Substring(0, num);
				search = search.Remove(0, num + 1);
			}
			string[] files = Directory.GetFiles(GlobalData.assetBundlePath + "/" + text, search, SearchOption.TopDirectoryOnly);
			Array.Sort(files);
			foreach(string path in files)
			{
				if(Path.GetExtension(path).Length == 0)
				{
					string text2 = Path.GetFileNameWithoutExtension(path);
					if(text.Length > 0)
					{
						text2 = text + "/" + text2;
					}
					global::AssetBundleController assetBundleController = new global::AssetBundleController();
					assetBundleController.OpenFromFile(GlobalData.assetBundlePath, text2);
					Setup(datas, assetBundleController, action);
					assetBundleController.Close(false);
				}
			}
			if(!Directory.Exists(GlobalData.assetBundlePath + "/list/" + text))
			{
				return;
			}
			foreach(string text3 in Directory.GetFiles(GlobalData.assetBundlePath + "/list/" + text, search + "_Mlist.txt"))
			{
				Console.WriteLine("Load Mlist:" + text3);
				StreamReader streamReader = new StreamReader(new FileStream(text3, FileMode.Open));
				string assetBundleName = streamReader.ReadLine();
				string contents = streamReader.ReadToEnd();
				string tempFileName = Path.GetTempFileName();
				File.WriteAllText(tempFileName, contents);
				CustomDataListLoader customDataListLoader = new CustomDataListLoader();
				customDataListLoader.Load(tempFileName);
				File.Delete(tempFileName);
				global::AssetBundleController assetBundleController2 = new global::AssetBundleController();
				assetBundleController2.OpenFromFile(GlobalData.assetBundlePath, assetBundleName);
				action(datas, assetBundleController2, customDataListLoader);
				assetBundleController2.Close(false);
			}
		}
	}
}
