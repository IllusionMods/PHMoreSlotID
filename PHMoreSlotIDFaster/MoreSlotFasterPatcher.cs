using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading;

namespace PHMoreSlotIDFaster
{
    [BepInPlugin("org.bepinex.plugins.phmoreslotidfaster", "PHMoreSlotIDFaster Plugin", "1.0.0.0")]
    public class MoreSlotFasterPatcher : BaseUnityPlugin
    {
        void Awake()
        {
            var harmony = new Harmony("org.bepinex.plugins.phmoreslotidfaster");
            GameObject obj = new GameObject("EditMode_Patch_Data");
            obj.AddComponent<EditMode_Data>();
            DontDestroyOnLoad(obj);
            harmony.PatchAll();
        }
    }

    //Note: This is the key to speed up the whole game startup preloading process.
    //
    //      AssetBundleController.OpenFromFile is called **just to extract** a TextAsset (list) from ALL asset bundles
    //      at startup. PHMoreSlotID.dll did provided the _Mlist addition so that the "later" PH mods don't have to 
    //      combine the list inside the asset bundles, thus lower the time needed when you add more and more mods. 
    //      However, the vanilla mods and early PH mods are still in question. 
    //      They waste tens of seconds on a PH cold startup -- 
    //
    //      The solution comes in 2 folds -- AssetBundleController.LoadAsset<T> was patched by PHMoreSlotID,
    //      (Because of the thumbnail_R patch) 
    //      and I further patched it so that it tries to call LoadFromFile within LoadAsset<T>, if said assetbundle
    //      is not yet loaded. Which means OpenFromFile no longer has the responsibility of calling LoadFromFile, 
    //      only remembering the assetBundleName so that it can be used later by LoadAsset<T>.
    // 
    //      This must be used with my version of PHMoreSlotID.dll and PHMoreSlotIDPatchContainer.dll preload patchers.
    [HarmonyPatch(typeof(AssetBundleController), "OpenFromFile")]
    class AssetBundleController_OpenFromFile_Patch
    {
        static PropertyInfo directoryProp = typeof(AssetBundleController).GetProperty("directory");
        static MethodInfo directorySetter = directoryProp.GetSetMethod(true);
        static PropertyInfo assetBundleNameProp = typeof(AssetBundleController).GetProperty("assetBundleName");
        static MethodInfo assetBundleNameSetter = assetBundleNameProp.GetSetMethod(true);

        static bool Prefix(AssetBundleController __instance, string directory, string assetBundleName, ref bool __result)
        {
            directorySetter.Invoke(__instance, new object[] { directory });
            assetBundleNameSetter.Invoke(__instance, new object[] { assetBundleName });
            if (File.Exists(directory + "/" + assetBundleName)) __result = true;
            else __result = false;
            return false;
        }
    }

    //Note: Now it comes to the EditMode's thumbnail loading performance issue. 
    //      The culprit is not hard to understand: 
    //      1) Because of the PHMoreSlotID thumbnail_R patch, it tries to access harddrive THOUSANDS of times, 
    //         instead of just dozens of times. Significantly slowing the thumbnail loading process when files are cold.
    //      2) Because of the fact that PNG decoding is actually slow. It's not as slow as accessing harddrive
    //         when the files are cold, but it is still very noticable when there are thousands of individual 
    //         PNGs to be decoded. This is why we have dxt, pvrtc, or even some times use uncompressed images.
    //       
    //      Firstly, I am not trying to solve issue 2 at all. It's pretty impossible and it means all thumbnail_R
    //      we have so far has to be dumped. But for the 1st issue. After tracing deeper into the UI code of  
    //      EditMode, I realized that the thumbnail textures themselves are entirely not needed within the 
    //      lifetime of EditMode.Setup()! 
    //     
    //      So the solution is this: We still generate CustomSelectSet(s) from EditMode.CreateData(), however, 
    //      we just passed 2 nulls to thumbnail_S and thumbnail_L parameters, as if they don't have thumbnails.
    //      We then push a "Task" object which has all the records needed to generate a thumbnail Texture2D.
    //      And then, we added a prefix to EditMode.Update() to consume the task queue frame-by-frame, 
    //      effectively making this reading process non-blocking at initialization, and sort of have this "async"
    //      effect when polling the EditMode.Update(). I can also have more fine control over using coroutine this way.
    //
    //      The caveat is the additional persistent data we need inside the scope of EditMode, 
    //      which would need to be manually cleared and reset if we consider going in and out of EditMode many times. 
    //      A "pretend" reference count of asset bundle opened in the process needs to be kept track of as well, 
    //      Otherwise we would open a bunch of thumbnail asset bundles outside of the control of the main game code flow, 
    //      and no one would try to close them. 
    //public struct ThumbLoadTask
    //{
    //    public CustomSelectSet select;
    //    public string prefab_name;
    //    public Rect smallRect;
    //    public AssetBundleController abc;
    //    public ThumbLoadTask(CustomSelectSet a, string b, Rect c, AssetBundleController d)
    //    {
    //        select = a; prefab_name = b; smallRect = c; abc = d;
    //    }
    //}

    //Note: After benchmarking, while ThumbThreadTask has no advantage over ThumbLoadTask when the files are cached
    //      (because the only thing that multithreading can help us is the part when loading bytes from disk to buffer)
    //      (and that is veeeeeery minor in the scheme of IO when cached, about ~0.1~0.2ms) 
    //      multithreading actually helps tremendously on the IO of cold files, with a whooping 50% time off (for me).
    //      I think that's totally worth it. 
    //
    //      I am keeping the singlethreaded queue code commented-out for reference.
    class ThumbThreadTask
    {
        public CustomSelectSet select;
        public string prefab_name;
        public Rect smallRect;
        public AssetBundleController abc;
        public enum STATE { NO_TEX, LOADING, LOAD_END, HAS_TEX, TASK_END }

        private byte[] buf;
        private string filename;
        private Texture2D tex;
        private Thread thread;
        public STATE state { get; private set; }
        private static Vector2 vector = new Vector2(256f, 256f);

        public ThumbThreadTask(CustomSelectSet a, string b, Rect c, AssetBundleController d)
        {
            select = a; prefab_name = b; smallRect = c; abc = d; state = STATE.NO_TEX;
        }

        private void ThreadedLoadPNG()
        {
            buf = File.ReadAllBytes(filename);
            state = STATE.LOAD_END;
        }

        static FieldInfo assetBundleFieldInfo = typeof(AssetBundleController).GetField("assetBundle", BindingFlags.NonPublic | BindingFlags.Instance);
        private void LoadedTex()
        {
            AssetBundle ab = assetBundleFieldInfo.GetValue(abc) as AssetBundle;
            if (ab == null)
            {
                string text = abc.directory + "/" + abc.assetBundleName;
                if (File.Exists(text))
                {
                    ab = AssetBundle.LoadFromFile(text);
                    assetBundleFieldInfo.SetValue(abc, ab);
                }
                else
                {
                    UnityEngine.Debug.Log("アセットバンドルない：" + text);
                }
            }

            if (ab != null)
            {
                tex = ab.LoadAsset<Texture2D>(prefab_name);
                if (tex != null)
                {
                    state = STATE.HAS_TEX;
                    return;
                }
            }

            filename = abc.directory + "/thumnbnail_R/" + prefab_name + ".png";
            if (File.Exists(filename))
            {
                state = STATE.LOADING;
                thread = new Thread(new ThreadStart(ThreadedLoadPNG));
                thread.Start();
            }
            else state = STATE.TASK_END;
        }

        private void CreateTex()
        {
            tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            tex.LoadImage(buf);
            buf = null;
            state = STATE.HAS_TEX;
        }

        public void UpdateSprite()
        {
            if (state == STATE.NO_TEX && tex == null && (thread == null || !thread.IsAlive))
                LoadedTex();
            if (state == STATE.LOAD_END)
                CreateTex();
            if (state == STATE.HAS_TEX)
            {
                select.thumbnail_L = Sprite.Create(tex, new Rect(Vector2.zero, vector), vector * 0.5f, 100f, 0U, SpriteMeshType.FullRect);
                select.thumbnail_S = Sprite.Create(tex, smallRect, smallRect.size * 0.5f, 100f, 0U, SpriteMeshType.FullRect);
                state = STATE.TASK_END;
            }
        }
    }

    [HarmonyPatch(typeof(EditMode), "Setup", new Type[]{
        typeof(Human),
        typeof(EditScene)
    })]
    class EditMode_Data : MonoBehaviour
    {
        static FieldInfo itemSelectUI_Field = typeof(EditMode).GetField("itemSelectUI", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selectUI_Field = typeof(MoveableThumbnailSelectUI).GetField("select", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo cells_Field = typeof(ThumbnailSelectUI).GetField("cells", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo datas_Field = typeof(ThumbnailSelectUI).GetField("datas", BindingFlags.NonPublic | BindingFlags.Instance);

        //public static Queue<ThumbLoadTask> async_thumb_load_task;
        public static List<ThumbThreadTask> async_thumb_load_task;
        public static Dictionary<string, int> thumb_ab_fake_refcount;
        public static ThumbnailSelectUI thumb_select;

        private static EditMode_Data self;
        private static EditMode editMode;
        static FieldInfo face_Field = typeof(EditMode).GetField("face", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo body_Field = typeof(EditMode).GetField("body", BindingFlags.NonPublic | BindingFlags.Instance);
        static FaceCustomEdit editFace;
        static BodyCustomEdit editBody;

        private void Awake()
        {
            self = this;
        }

        static void Prefix(EditMode __instance)
        {
            Console.WriteLine("PHMoreSlotIDFaster initialized central data it needs at the start of EditMode.");
            
            //Note: Additional clean up for these may be needed if EditMode ends prematurely.
            self.CancelInvoke();
            if (async_thumb_load_task != null && async_thumb_load_task.Count != 0)
                async_thumb_load_task.Clear();
            if (thumb_ab_fake_refcount != null && thumb_ab_fake_refcount.Count != 0)
                thumb_ab_fake_refcount.Clear();

            editMode = __instance;
            editFace = face_Field.GetValue(editMode) as FaceCustomEdit;
            editBody = body_Field.GetValue(editMode) as BodyCustomEdit;
            EditMode_Update_Patch.accumulated_time = 0;
            //async_thumb_load_task = new Queue<ThumbLoadTask>();
            async_thumb_load_task = new List<ThumbThreadTask>();
            thumb_ab_fake_refcount = new Dictionary<string, int>();
            thumb_select = selectUI_Field.GetValue(itemSelectUI_Field.GetValue(__instance)) as ThumbnailSelectUI;

            self.InvokeRepeating("Update_Thumbnail", 4f, 2f);
        }

        private void Update_Thumbnail()
        {
            //Note: This is to periodically update the ThumbnailSelectCell[] -- just a small UX improvement.
            //      if load task is 0 it's either no thumbnail has been initialized, or all thumbnail has been loaded.
            //      if the thumbselectUI is active, then the player can actually see the effects.
            string scene_name = SceneManager.GetActiveScene().name;

            if (scene_name == "EditScene" || scene_name == "H")
            {
                if (thumb_select.isActiveAndEnabled)
                {
                    Console.WriteLine("PHMoreSlotIDFaster: Try to update active ThumbnailSelectCells periodically.");
                    Array thumb_select_cells = cells_Field.GetValue(thumb_select) as Array;
                    List<CustomSelectSet> thumb_select_datas = datas_Field.GetValue(thumb_select) as List<CustomSelectSet>;
                    for (int i = 0; i < thumb_select_cells.Length; i++)
                    {
                        ThumbnailSelectCell cell = thumb_select_cells.GetValue(i) as ThumbnailSelectCell;
                        if (!cell.gameObject.activeSelf) break;

                        cell.Setup(thumb_select, i, thumb_select_datas[i].name, thumb_select_datas[i].thumbnail_S, thumb_select_datas[i].isNew);
                    }
                }
                deal_with_individual_sel_toggle();
            }

            if (async_thumb_load_task.Count == 0 || (scene_name != "EditScene" && scene_name != "H") )
            {
                Console.WriteLine("PHMoreSlotIDFaster: No more thumbnail tasks remain, stopping the timer, accumulated time: " + EditMode_Update_Patch.accumulated_time);
                CancelInvoke("Update_Thumbnail");
            }
        }

        //Note: this is rather stupid, but I don't see another way... some thumbnail on the toggle don't update.
        static FieldInfo selSets_FaceType = typeof(FaceCustomEdit).GetField("selSets_FaceType", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_SkinType = typeof(FaceCustomEdit).GetField("selSets_SkinType", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_BumpType = typeof(FaceCustomEdit).GetField("selSets_BumpType", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_EyeL = typeof(FaceCustomEdit).GetField("selSets_EyeL", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_EyeR = typeof(FaceCustomEdit).GetField("selSets_EyeR", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_EyeHighlight = typeof(FaceCustomEdit).GetField("selSets_EyeHighlight", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_Eyebrow = typeof(FaceCustomEdit).GetField("selSets_Eyebrow", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_Eyelash = typeof(FaceCustomEdit).GetField("selSets_Eyelash", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_Mole = typeof(FaceCustomEdit).GetField("selSets_Mole", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_EyeShadow = typeof(FaceCustomEdit).GetField("selSets_EyeShadow", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_Cheek = typeof(FaceCustomEdit).GetField("selSets_Cheek", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_Lip = typeof(FaceCustomEdit).GetField("selSets_Lip", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_FaceTattoo = typeof(FaceCustomEdit).GetField("selSets_Tattoo", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_Beard = typeof(FaceCustomEdit).GetField("selSets_Beard", BindingFlags.NonPublic | BindingFlags.Instance);
        
        static FieldInfo selSets_Skin = typeof(BodyCustomEdit).GetField("selSets_Skin", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_Nip = typeof(BodyCustomEdit).GetField("selSets_Nip", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_UnderHair = typeof(BodyCustomEdit).GetField("selSets_UnderHair", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_Sunburn = typeof(BodyCustomEdit).GetField("selSets_Sunburn", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo selSets_BodyTattoo = typeof(BodyCustomEdit).GetField("selSets_Tattoo", BindingFlags.NonPublic | BindingFlags.Instance);

        private void deal_with_individual_sel_toggle()
        {
            ItemSelectUISets itemSelectUI;
            itemSelectUI = selSets_FaceType.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_SkinType.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_BumpType.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_EyeL.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_EyeR.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_EyeHighlight.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_Eyebrow.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_Eyelash.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_Mole.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_EyeShadow.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_Cheek.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_Lip.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_FaceTattoo.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_Beard.GetValue(editFace) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_Skin.GetValue(editBody) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_Nip.GetValue(editBody) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_UnderHair.GetValue(editBody) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_Sunburn.GetValue(editBody) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
            itemSelectUI = selSets_BodyTattoo.GetValue(editBody) as ItemSelectUISets; itemSelectUI.ApplyFromSelectedData();
        }
    }

    //Note: CreateData overloading 1 of 4
    [HarmonyPatch(typeof(EditMode), "CreateData", new Type[] {
        typeof(CombineTextureData),
        typeof(Rect),
        typeof(AssetBundleController)
    })]
    class EditMode_CreateData_Patch1
    {
        static bool Prefix(CombineTextureData item, Rect smallRect, AssetBundleController abc, ref CustomSelectSet __result)
        {
            __result = new CustomSelectSet(item.id, item.name_LineFeed, null, null, item.isNew);

            if (EditMode_Data.thumb_ab_fake_refcount.ContainsKey(abc.assetBundleName))
                EditMode_Data.thumb_ab_fake_refcount[abc.assetBundleName]++;
            else EditMode_Data.thumb_ab_fake_refcount.Add(abc.assetBundleName, 1);

            //EditMode_Data.async_thumb_load_task.Enqueue(new ThumbLoadTask(__result, item.textureName, smallRect, abc));
            EditMode_Data.async_thumb_load_task.Add(new ThumbThreadTask(__result, item.textureName, smallRect, abc));

            return false;
        }
    }

    //Note: CreateData overloading 2 of 4
    [HarmonyPatch(typeof(EditMode), "CreateData", new Type[] {
        typeof(HeadData),
        typeof(Rect),
        typeof(AssetBundleController)
    })]
    class EditMode_CreateData_Patch2
    {
        static bool Prefix(HeadData item, Rect smallRect, AssetBundleController abc, ref CustomSelectSet __result)
        {
            __result = new CustomSelectSet(item.id, item.name_LineFeed, null, null, item.isNew);

            if (EditMode_Data.thumb_ab_fake_refcount.ContainsKey(abc.assetBundleName))
                EditMode_Data.thumb_ab_fake_refcount[abc.assetBundleName]++;
            else EditMode_Data.thumb_ab_fake_refcount.Add(abc.assetBundleName, 1);

            //EditMode_Data.async_thumb_load_task.Enqueue(new ThumbLoadTask(__result, item.path, smallRect, abc));
            EditMode_Data.async_thumb_load_task.Add(new ThumbThreadTask(__result, item.path, smallRect, abc));

            return false;
        }
    }

    //Note: CreateData overloading 3 of 4
    [HarmonyPatch(typeof(EditMode), "CreateData", new Type[] {
        typeof(AccessoryData),
        typeof(Rect),
        typeof(AssetBundleController)
    })]
    class EditMode_CreateData_Patch3
    {
        static bool Prefix(AccessoryData item, Rect smallRect, AssetBundleController abc, ref CustomSelectSet __result)
        {
            __result = new CustomSelectSet(item.id, item.name_LineFeed, null, null, item.isNew);

            if (EditMode_Data.thumb_ab_fake_refcount.ContainsKey(abc.assetBundleName))
                EditMode_Data.thumb_ab_fake_refcount[abc.assetBundleName]++;
            else EditMode_Data.thumb_ab_fake_refcount.Add(abc.assetBundleName, 1);

            //EditMode_Data.async_thumb_load_task.Enqueue(new ThumbLoadTask(__result, item.prefab_F, smallRect, abc));
            EditMode_Data.async_thumb_load_task.Add(new ThumbThreadTask(__result, item.prefab_F, smallRect, abc));

            return false;
        }
    }

    //Note: CreateData overloading 4 of 4
    [HarmonyPatch(typeof(EditMode), "CreateData", new Type[] {
        typeof(PrefabData),
        typeof(Rect),
        typeof(AssetBundleController)
    })]
    class EditMode_CreateData_Patch4
    {
        static bool Prefix(PrefabData item, Rect smallRect, AssetBundleController abc, ref CustomSelectSet __result)
        {
            __result = new CustomSelectSet(item.id, item.name_LineFeed, null, null, item.isNew);

            if (EditMode_Data.thumb_ab_fake_refcount.ContainsKey(abc.assetBundleName))
                EditMode_Data.thumb_ab_fake_refcount[abc.assetBundleName]++;
            else EditMode_Data.thumb_ab_fake_refcount.Add(abc.assetBundleName, 1);

            //EditMode_Data.async_thumb_load_task.Enqueue(new ThumbLoadTask(__result, item.prefab, smallRect, abc));
            EditMode_Data.async_thumb_load_task.Add(new ThumbThreadTask(__result, item.prefab, smallRect, abc));

            return false;
        }
    }

    [HarmonyPatch(typeof(EditMode), "Update")]
    class EditMode_Update_Patch
    {
        static public long accumulated_time = 0;
        static Vector2 vector = new Vector2(256f, 256f);
        //static void Prefix(EditMode __instance)
        //{
        //    Stopwatch t = new Stopwatch();
        //    t.Start();
        //    while (EditMode_Data.async_thumb_load_task.Count > 0 && t.ElapsedMilliseconds < 12)
        //    {
        //        ThumbLoadTask task = EditMode_Data.async_thumb_load_task.Dequeue();
        //        EditMode_Data.thumb_ab_fake_refcount[task.abc.assetBundleName]--;
        //        Texture2D texture = task.abc.LoadAsset<Texture2D>(task.prefab_name);

        //        if (EditMode_Data.thumb_ab_fake_refcount[task.abc.assetBundleName] <= 0)
        //            task.abc.Close(false);

        //        if (texture == null) continue;
        //        task.select.thumbnail_L = Sprite.Create(texture, new Rect(Vector2.zero, vector), vector * 0.5f, 100f, 0U, SpriteMeshType.FullRect);
        //        task.select.thumbnail_S = Sprite.Create(texture, task.smallRect, task.smallRect.size * 0.5f, 100f, 0U, SpriteMeshType.FullRect);
        //    }
        //    t.Stop();
        //    accumulated_time += t.ElapsedMilliseconds;
        //}

        static void Prefix(EditMode __instance)
        {
            Stopwatch t = new Stopwatch();
            t.Start();
            for (int i = EditMode_Data.async_thumb_load_task.Count - 1; i >= 0 && t.ElapsedMilliseconds < 12; i--)
            {
                ThumbThreadTask task = EditMode_Data.async_thumb_load_task[i];
                task.UpdateSprite();
                if (task.state == ThumbThreadTask.STATE.TASK_END)
                {
                    EditMode_Data.thumb_ab_fake_refcount[task.abc.assetBundleName]--;
                    if (EditMode_Data.thumb_ab_fake_refcount[task.abc.assetBundleName] <= 0)
                        task.abc.Close(false);

                    EditMode_Data.async_thumb_load_task.RemoveAt(i);
                }
            }
            t.Stop();
            accumulated_time += t.ElapsedMilliseconds;
        }
    }
}
