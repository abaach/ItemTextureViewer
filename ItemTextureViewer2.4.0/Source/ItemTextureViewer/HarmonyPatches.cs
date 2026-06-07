using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace ItemTextureViewer
{
    public static class HarmonyPatches
    {
        private static readonly Dictionary<string, Texture2D> itemCache = new Dictionary<string, Texture2D>();
        private const int MaxCacheItems = 200;
        private static bool patched;
        private static Harmony harmony;

        public static void Initialize()
        {
            if (patched) return;
            try
            {
                harmony = new Harmony("my.item.viewer");

                Type infoCard = AccessTools.TypeByName("Verse.Dialog_InfoCard");
                if (infoCard != null)
                {
                    MethodInfo dm = AccessTools.Method(infoCard, "DoWindowContents", new[] { typeof(Rect) });
                    if (dm != null) harmony.Patch(dm, postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(PostInfoCard)));
                    else Log.Warning("[ItemTextureViewer] 未找到 Dialog_InfoCard.DoWindowContents");
                }

                Type root = AccessTools.TypeByName("Verse.Root");
                if (root == null) root = AccessTools.TypeByName("Verse.Root_Entry");
                if (root != null)
                {
                    MethodInfo og = AccessTools.Method(root, "OnGUI");
                    if (og != null) harmony.Patch(og, postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(PostRootOnGUI)));
                }

                MethodInfo bt = AccessTools.Method(typeof(WindowStack), "BringToFront", new[] { typeof(Window) });
                if (bt != null) harmony.Patch(bt, prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(PreBringToFront)));

                patched = true;
            }
            catch (Exception ex) { Log.Error($"[ItemTextureViewer] Init error: {ex}"); }
        }

        private static bool PreBringToFront(WindowStack __instance, Window window) => !(window is PawnPreviewWindow);

        private static void PostRootOnGUI()
        {
            if (QuickViewController.PendingDrawObj != null)
            {
                var obj = QuickViewController.PendingDrawObj;
                QuickViewController.PendingDrawObj = null;
                Find.WindowStack.Add(new TextureFullscreenWindow(obj, ItemTextureViewerMod.Settings.currentZoom));
            }
        }

        private static void PostInfoCard(object __instance, Rect inRect)
        {
            Thing thing = Traverse.Create(__instance).Field("thing").GetValue<Thing>();
            ThingDef def = Traverse.Create(__instance).Field("def").GetValue() as ThingDef;
            object drawObj = thing ?? (object)def;
            if (drawObj == null) return;

            var s = ItemTextureViewerMod.Settings;
            Rect btn = new Rect(inRect.x + inRect.width - 44f + s.btnOffsetX,
                                inRect.y + 30f + s.btnOffsetY, 24f, 24f);
            if (Widgets.ButtonText(btn, "T", true, false, true))
                Find.WindowStack.Add(new TextureFullscreenWindow(drawObj, s.currentZoom));
        }

        // ========== 角色纹理 ==========
        public static RenderTexture GetPawnRenderTexture(Pawn pawn, Rot4 rot, Vector2 size)
        {
            if (pawn == null || pawn.Dead) return null;
            rot = rot.IsValid ? rot : Rot4.South;
            float zoom;
            int s = (int)size.x;
            if (s == 128) zoom = 1.2f;
            else if (s == 256) zoom = 0.9f;
            else if (s == 512) zoom = 0.7f;
            else if (s == 1024) zoom = 0.5f;
            else if (s == 2048) zoom = 0.3f;
            else zoom = 0.7f;
            return PortraitsCache.Get(pawn, size, rot, cameraZoom: zoom);
        }

        public static void RefreshPawnPortrait(Pawn pawn) => PortraitsCache.SetDirty(pawn);

        // ========== 物品纹理（基于UV提取，支持染色）==========
        // 从实际Thing实例提取，使用 MatSingleFor 的主纹理 + 材质颜色
        public static Texture2D GetThingHighResTexture(Thing thing)
        {
            if (thing?.def?.graphic == null) return null;
            string key = thing.def.defName + "_" + thing.thingIDNumber;
            if (itemCache.TryGetValue(key, out Texture2D cached) && cached != null)
                return cached;

            try
            {
                Material mat = thing.def.graphic.MatSingleFor(thing);
                if (mat == null) return null;

                Texture2D srcTex = mat.mainTexture as Texture2D;
                if (srcTex == null) return null;

                // 获取UV区域（与原方法相同）
                Rect uv = new Rect(mat.mainTextureOffset.x, mat.mainTextureOffset.y,
                                   mat.mainTextureScale.x, mat.mainTextureScale.y);
                int x = Mathf.RoundToInt(uv.x * srcTex.width);
                int y = Mathf.RoundToInt(uv.y * srcTex.height);
                int w = Mathf.RoundToInt(uv.width * srcTex.width);
                int h = Mathf.RoundToInt(uv.height * srcTex.height);
                x = Mathf.Clamp(x, 0, srcTex.width - 1);
                y = Mathf.Clamp(y, 0, srcTex.height - 1);
                w = Mathf.Clamp(w, 1, srcTex.width - x);
                h = Mathf.Clamp(h, 1, srcTex.height - y);

                Color[] pixels = srcTex.GetPixels(x, y, w, h);

                // 应用材质的颜色（如制作材料染色）
                if (mat.HasProperty("_Color"))
                {
                    Color tint = mat.color;
                    for (int i = 0; i < pixels.Length; i++)
                        pixels[i] *= tint;
                }

                Texture2D tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
                tex.SetPixels(pixels);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                if (itemCache.Count >= MaxCacheItems) ClearItemCache();
                itemCache[key] = tex;
                return tex;
            }
            catch (Exception ex)
            {
                Log.Warning($"[ItemTextureViewer] GetThingHighResTexture(Thing) failed for {thing.def.defName}: {ex.Message}");
                return null;
            }
        }

        // 无Thing实例时的回退方法（信息卡中的def，无染色信息）
        public static Texture2D GetThingHighResTexture(ThingDef def)
        {
            if (def?.graphic == null) return null;
            string key = def.defName;
            if (itemCache.TryGetValue(key, out Texture2D cached) && cached != null)
                return cached;

            try
            {
                Material mat = def.graphic.MatSingle;
                Texture2D atlas = mat.mainTexture as Texture2D;
                if (atlas == null || !atlas.isReadable)
                    return null;

                Rect uv = new Rect(mat.mainTextureOffset.x, mat.mainTextureOffset.y,
                                   mat.mainTextureScale.x, mat.mainTextureScale.y);
                int x = Mathf.RoundToInt(uv.x * atlas.width);
                int y = Mathf.RoundToInt(uv.y * atlas.height);
                int w = Mathf.RoundToInt(uv.width * atlas.width);
                int h = Mathf.RoundToInt(uv.height * atlas.height);
                x = Mathf.Clamp(x, 0, atlas.width - 1);
                y = Mathf.Clamp(y, 0, atlas.height - 1);
                w = Mathf.Clamp(w, 1, atlas.width - x);
                h = Mathf.Clamp(h, 1, atlas.height - y);

                Color[] pixels = atlas.GetPixels(x, y, w, h);
                Texture2D tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
                tex.SetPixels(pixels);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                if (itemCache.Count >= MaxCacheItems) ClearItemCache();
                itemCache[key] = tex;
                return tex;
            }
            catch { return null; }
        }

        public static void ClearItemCache()
        {
            foreach (var tex in itemCache.Values) UnityEngine.Object.Destroy(tex);
            itemCache.Clear();
        }

        // ========== 导出 ==========
        public static void ExportTextureToFile(Texture tex, string name)
        {
            if (tex == null) return;
            Texture2D outTex = null;
            if (tex is RenderTexture rt)
            {
                try
                {
                    RenderTexture.active = rt;
                    outTex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
                    outTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    outTex.Apply();
                }
                catch (Exception) { outTex = null; }
                finally { RenderTexture.active = null; }
            }
            else if (tex is Texture2D t2d) outTex = t2d;

            if (outTex == null) return;

            string dir = Path.Combine(Application.persistentDataPath, "ItemTextureViewer_Exports");
            Directory.CreateDirectory(dir);
            string safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            string path = Path.Combine(dir, $"{safe}_{DateTime.Now:yyyyMMddHHmmss}.png");
            File.WriteAllBytes(path, outTex.EncodeToPNG());
        }

        public static void OpenExportFolder()
        {
            string dir = Path.Combine(Application.persistentDataPath, "ItemTextureViewer_Exports");
            Directory.CreateDirectory(dir);
            Application.OpenURL("file://" + dir);
        }
    }
}