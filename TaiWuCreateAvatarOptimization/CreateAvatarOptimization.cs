using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UICommon.Character.Avatar;
using System.Collections.Generic;
using TaiwuModdingLib.Core.Plugin;
using GameData.Domains.Character.AvatarSystem;
using System.Linq;
using GameData.Domains.Mod;
using XLua;
using FrameWork.ModSystem;

namespace TaiWuCreateAvatarOptimization
{
    [PluginConfig("ModCreateAvatarOptimization", "观察者", "20221015")]
    public class CreateAvatarOptimization : TaiwuRemakePlugin
    {
        private static List<ulong> ModOrderIds = new List<ulong>();
        private static Dictionary<ulong, ModId> ModIdDic = new Dictionary<ulong, ModId>();

        private Harmony harmony;
        //路径缓存
        private static Dictionary<string, string> SmallCachePath = new Dictionary<string, string>();
        private static Dictionary<string, string> NormalCachePath = new Dictionary<string, string>();
        private static Dictionary<string, string> BigCachePath = new Dictionary<string, string>();
        // 缓存
        public static Dictionary<string, Sprite> SmallCache = new Dictionary<string, Sprite>();
        public static Dictionary<string, Sprite> NormalCache = new Dictionary<string, Sprite>();
        public static Dictionary<string, Sprite> BigCache = new Dictionary<string, Sprite>();

        public string ModOrder { set; get; }
        /// <summary>
        /// Mod被关闭
        /// </summary>
        public override void Dispose()
        {
            // Mod被关闭时取消Patch
            if (harmony != null)
            {
                harmony.UnpatchSelf();
                harmony = null;
            }
        }
        /// <summary>
        /// Mod初始化
        /// </summary>
        public override void Initialize()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(CreateAvatarOptimization));
        }

        public static string ModOrderStr = "";
        public override void OnModSettingUpdate()
        {
            ModManager.GetSetting(this.ModIdStr, "ModOrder", ref ModOrderStr);
            WLog($"用户设置变更[{ModOrderStr}]");
            IsLoad = false;
            LoadPath();
            //GetUserSetting();
            //GetEnableModSettings();
        }
        //获取除用户设置外的设置
        private static void GetEnableModSettings()
        {
            try
            {
                var modIds= ModManager.EnabledMods;
                foreach (var item in modIds)
                {
                    try
                    {
                        var modInfo = ModManager.GetModInfo(item);
                        if (modInfo==null)
                        {
                            continue;
                        }
                        var path = modInfo.DirectoryName;
                        DirectoryInfo modDir = new DirectoryInfo(path);
                        if (modDir.Exists)
                        {
                            // 在Mod文件夹中找CreateAvatarOptimizationPackage
                            var paks = modDir.GetDirectories("CreateAvatarOptimizationPackage");
                            if (paks != null && paks.Length > 0)
                            {
                                WLog($"[{item.FileId}]({item.FileId.ToString()}):[{modInfo.DirectoryName}]");
                                ModIdDic[item.FileId] = modInfo.ModId;
                                ModOrderIds.Insert(0,item.FileId);
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }

                }
            }
            catch (Exception)
            {

            }
        }
        //获取用户界面的设置
        private static void GetUserSetting()
        {
            try
            {
                WLog($"用户设置[{ModOrderStr}]");
                if (string.IsNullOrWhiteSpace(ModOrderStr))
                {
                    return;
                }
                ModOrderIds.Clear();
                ModIdDic.Clear();
                var orderStr = ModOrderStr;
                var orderStrs = orderStr.Split(';');
                var ids = orderStrs.Select(_ => _.Split(':').FirstOrDefault().Trim()).Where(_ => !string.IsNullOrWhiteSpace(_)).ToList();
                WLog($"用户设置ID[{string.Join(" ", ids)}]");
                var modIds = ModManager.EnabledMods.Where(_=>ids.Contains(_.FileId.ToString())).ToDictionary(_=>_.FileId,_=>_);
                WLog($"获取ModID[{string.Join(" ", modIds.Keys)}]");
                foreach (var item in ids)
                {
                    if (!ulong.TryParse(item, out var uId) ||ModOrderIds.Contains(uId)|| !modIds.ContainsKey(uId))
                    {
                        continue;
                    }
                    WLog($"[{item}]成功");
                    var modId = modIds[uId];
                    ModIdDic[uId] = modId;
                    ModOrderIds.Add(uId);
                } 
            }
            catch (Exception)
            {

            }
        }

        #region 替换原图
        [HarmonyPostfix, HarmonyPatch(typeof(AvatarAtlasAssets), nameof(AvatarAtlasAssets.GetSpriteArray))]
        public static void AvatarAtlasAssets_Post_GetSpriteArray(AvatarAtlasAssets __instance, byte avatarId, string spriteName, ref Sprite[] __result)
        {
            if (__result != null && __result.Length == 3)
            {
                LoadPath();
                if (__result[0] != null)
                {
                    string name = __result[0].name.Replace("(Clone)", "");
                    var isGet = TryGetRepaceSprite(name, 0, out var sprit);
                    if (isGet)
                    {
                        __result[0] = sprit;
                    }
                }
                if (__result[1] != null)
                {
                    string name = __result[1].name.Replace("(Clone)", "");
                    var isGet = TryGetRepaceSprite(name, 1, out var sprit);
                    if (isGet)
                    {
                        __result[1] = sprit;
                    }
                }
                if (__result[2] != null)
                {
                    string name = __result[2].name.Replace("(Clone)", "");
                    var isGet = TryGetRepaceSprite(name, 2, out var sprit);
                    if (isGet)
                    {
                        __result[2] = sprit;
                    }
                }
            }
        }


        [HarmonyPostfix, HarmonyPatch(typeof(AvatarManager),nameof(AvatarManager.GetSprite))]
        public static void AvatarManager_Post_GetSprite(AvatarManager __instance, int size, ref Sprite __result)
        {
            if (__result == null) return;
            LoadPath();
            string name = __result.name.Replace("(Clone)", "");
            var isGet=TryGetRepaceSprite(name,size,out var sprit);
            if (isGet)
            {
                __result = sprit;
            }
        }
        #endregion


        /// <summary>
        /// 尝试获取替换的图片
        /// </summary>
        /// <param name="name"></param>
        /// <param name="size"></param>
        public static bool TryGetRepaceSprite(string fileName, int sizeName, out Sprite sprite)
        {
            sprite = default;
            bool isGet = false;
            string path = null;

            switch (sizeName)
            {
                case 2:
                    isGet = TryGetRepaceSprite(ref SmallCachePath, ref SmallCache, fileName, out sprite);
                    break;
                case 1:
                    isGet = TryGetRepaceSprite(ref NormalCachePath, ref NormalCache, fileName, out sprite);
                    break;
                case 0:
                    isGet = TryGetRepaceSprite(ref BigCachePath, ref BigCache, fileName, out sprite);
                    break;
                default:
                    break;
            }
            return isGet;
        }
        private static bool TryGetRepaceSprite(ref Dictionary<string, string> pathDic, ref Dictionary<string, Sprite> cacheDic, string fileName, out Sprite sprite)
        {
            bool isGet = false;
            sprite = default;
            isGet = cacheDic.TryGetValue(fileName, out sprite);
            if (!isGet && pathDic.TryGetValue(fileName, out var path))
            {
                sprite = LoadTextureToSprite(path);
                if (sprite != null)
                {
                    cacheDic[fileName] = sprite;
                    isGet = true;
                }
                else
                {
                    pathDic.Remove(fileName);
                }
            }
            return isGet;
        }

        /// <summary>
        /// 是否已加载资源路径
        /// </summary>
        private static bool IsLoad = false;

        private static void LoadPath()
        {
            if (IsLoad)
            {
                return;
            }
            try
            {
                //路径缓存
                SmallCachePath.Clear();
                NormalCachePath.Clear();
                BigCachePath.Clear();
                // 缓存
                SmallCache.Clear();
                NormalCache.Clear();
                BigCache.Clear();
                GetUserSetting();
                GetEnableModSettings();
                foreach (var modOrderId in ModOrderIds)
                {
                    if (!ModIdDic.TryGetValue(modOrderId, out var modId))
                    {
                        continue;
                    }
                    var info = ModManager.GetModInfo(modId);
                    if (info==null)
                    {
                        continue;
                    }
                    var path = info.DirectoryName;
                    DirectoryInfo modDir = new DirectoryInfo(path);
                    if (!modDir.Exists)
                    {
                        continue;
                    }
                    var paks = modDir.GetDirectories("CreateAvatarOptimizationPackage");
                    if (paks == null || paks.Length == 0)
                    {
                        continue;
                    }
                    var pakDir = paks.FirstOrDefault();
                    var smallDir = new DirectoryInfo(pakDir + "/Small");
                    var normalDir = new DirectoryInfo(pakDir + "/Normal");
                    var bigDir = new DirectoryInfo(pakDir + "/Big");

                    LoadPicPath(smallDir, ref SmallCachePath);
                    LoadPicPath(normalDir, ref NormalCachePath);
                    LoadPicPath(bigDir, ref BigCachePath);

                }
                Debug.Log("加载完毕");
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
            IsLoad = true;
        }
        private static void LoadPicPath(DirectoryInfo dicInfo,ref Dictionary<string, string> cachePath)
        {
            if (!dicInfo.Exists)
            {
                Debug.LogWarning($"找不到文件夹[{dicInfo.FullName}]");
                return;
            }
            var pics = dicInfo.GetFiles("*.png");
            if (pics == null || pics.Length == 0)
            {
                return;
            }
            foreach (var file in pics)
            {
                string name = file.Name.Replace(".png", "");
                cachePath[name] = file.FullName;
            }

        }

        /// <summary>
        /// 加载图片
        /// </summary>
        private static Sprite LoadTextureToSprite(string texPath)
        {
            if (File.Exists(texPath))
            {
                try
                {
                    using (FileStream fileStream = new FileStream(texPath, FileMode.Open))
                    {
                        byte[] array = new byte[fileStream.Length];
                        fileStream.Read(array, 0, array.Length);
                        fileStream.Close();
                        Texture2D texture2D = new Texture2D(1024, 1024, TextureFormat.ARGB32, false);
                        texture2D.LoadImage(array);
                        return Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), Vector2.zero);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[捏人优化]加载图片失败，图片:{texPath}，异常信息:{ex.Message}");
                }
            }
            return null;
        }


        static string logPath = @"Mod\ModCAO\log.txt";
        public static void WLog(string msg)
        {
            return;
            FileInfo fi = new FileInfo(logPath);
            using (var ws = fi.AppendText())
            {
                ws.WriteLine($"{DateTime.Now} {msg}");
            }
        }
    }
}