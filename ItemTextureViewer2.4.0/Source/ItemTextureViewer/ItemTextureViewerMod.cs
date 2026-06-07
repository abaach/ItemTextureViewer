using UnityEngine;
using Verse;

namespace ItemTextureViewer
{
    public class ItemTextureViewerMod : Mod
    {
        public static ItemTextureViewerSettings Settings;
        public static bool waitingForKey;

        public ItemTextureViewerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ItemTextureViewerSettings>();
            HarmonyPatches.Initialize();
            Current.Game?.GetComponent<PawnPreviewManager>();
        }

        public override void DoSettingsWindowContents(Rect inRect) => Settings.Draw(inRect);
        public override string SettingsCategory() => "ItemTextureViewer_SettingsCategory".Translate();
    }

    public class ItemTextureViewerSettings : ModSettings
    {
        // 信息卡T按钮偏移
        public float btnOffsetX = 19f, btnOffsetY = 3f;

        // 角色实时预览
        public bool autoOpenPawnPreview = true;
        public bool autoFollowInspectPane = true;
        public float previewPosX = 100f, previewPosY = 100f;
        public float previewZoom = 0.4f;
        public enum PreviewAlignment { Left, Center, Right }
        public PreviewAlignment previewAlignment = PreviewAlignment.Left;
        public float previewHorizontalOffset = 0f, previewVerticalGap = 25f;
        public float previewRefreshInterval = 0.3f;
        public bool showPreviewDebugArea = false;
        public bool previewShowHumans = true, previewShowAnimals = true, previewShowOthers = true;

        // 全局快捷键
        public KeyCode quickViewKey = KeyCode.Y;
        public bool quickViewCtrl = false, quickViewShift = false, quickViewAlt = false;

        // 全屏窗口记忆
        public float currentZoom = 1f;
        public float lastWindowPosX = -1f, lastWindowPosY = -1f;
        public float lastWindowZoom = 1f;
        public int lastSelectedSize = 1024;

        private Vector2 scrollPos;

        public void Draw(Rect canvas)
        {
            float vh = 1300f;
            Rect viewRect = new Rect(0, 0, canvas.width - 16f, vh);
            Widgets.BeginScrollView(canvas, ref scrollPos, viewRect);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(viewRect);

            ls.Label("ItemTextureViewer_SectionDisplay".Translate());
            ls.Gap(4);
            ls.Label(string.Format("ItemTextureViewer_ButtonHorizOffset".Translate(), btnOffsetX));
            btnOffsetX = ls.Slider(btnOffsetX, -200f, 200f);
            ls.Label(string.Format("ItemTextureViewer_ButtonVertOffset".Translate(), btnOffsetY));
            btnOffsetY = ls.Slider(btnOffsetY, -200f, 200f);
            ls.Gap();

            ls.Label("ItemTextureViewer_PreviewLayout".Translate());
            ls.CheckboxLabeled("ItemTextureViewer_AutoPawnPreview".Translate(), ref autoOpenPawnPreview);
            ls.CheckboxLabeled("ItemTextureViewer_FollowInspectPane".Translate(), ref autoFollowInspectPane);
            if (autoFollowInspectPane)
            {
                ls.Label("ItemTextureViewer_PreviewAlignment".Translate());
                string alignText;
                switch (previewAlignment)
                {
                    case PreviewAlignment.Left:
                        alignText = "ItemTextureViewer_PreviewAlignmentLeft".Translate(); break;
                    case PreviewAlignment.Center:
                        alignText = "ItemTextureViewer_PreviewAlignmentCenter".Translate(); break;
                    case PreviewAlignment.Right:
                        alignText = "ItemTextureViewer_PreviewAlignmentRight".Translate(); break;
                    default:
                        alignText = "ItemTextureViewer_PreviewAlignmentLeft".Translate(); break;
                }
                if (ls.ButtonText(alignText))
                    previewAlignment = (PreviewAlignment)(((int)previewAlignment + 1) % 3);
                ls.Label(string.Format("ItemTextureViewer_PreviewHorizontalOffset".Translate(), previewHorizontalOffset));
                previewHorizontalOffset = ls.Slider(previewHorizontalOffset, -500f, 500f);
                ls.Label(string.Format("ItemTextureViewer_PreviewVerticalGap".Translate(), previewVerticalGap));
                previewVerticalGap = ls.Slider(previewVerticalGap, -200f, 200f);
            }
            else
            {
                ls.Label(string.Format("ItemTextureViewer_PreviewPosX".Translate(), previewPosX));
                previewPosX = ls.Slider(previewPosX, -10000f, 10000f);
                ls.Label(string.Format("ItemTextureViewer_PreviewPosY".Translate(), previewPosY));
                previewPosY = ls.Slider(previewPosY, -10000f, 10000f);
            }
            ls.Label(string.Format("ItemTextureViewer_PreviewZoom".Translate(), previewZoom));
            previewZoom = ls.Slider(previewZoom, 0.2f, 10f);
            ls.Label(string.Format("ItemTextureViewer_PreviewRefresh".Translate(), previewRefreshInterval));
            previewRefreshInterval = ls.Slider(previewRefreshInterval, 0.1f, 5f);
            ls.Gap(20);

            ls.Label("ItemTextureViewer_SectionFeatures".Translate());
            ls.CheckboxLabeled("ItemTextureViewer_ShowPreviewDebug".Translate(), ref showPreviewDebugArea);
            ls.CheckboxLabeled("ItemTextureViewer_PreviewShowHumans".Translate(), ref previewShowHumans);
            ls.CheckboxLabeled("ItemTextureViewer_PreviewShowAnimals".Translate(), ref previewShowAnimals);
            ls.CheckboxLabeled("ItemTextureViewer_PreviewShowOthers".Translate(), ref previewShowOthers);
            ls.Gap();

            ls.Label("ItemTextureViewer_QuickViewLabel".Translate());
            string curKey = FormatKeyCombination(quickViewCtrl, quickViewShift, quickViewAlt, quickViewKey);
            if (ls.ButtonText(curKey))
                ItemTextureViewerMod.waitingForKey = true;
            if (ItemTextureViewerMod.waitingForKey)
            {
                ls.Label("ItemTextureViewer_PressKeys".Translate());
                if (Event.current.isKey && Event.current.type == EventType.KeyDown)
                {
                    KeyCode k = Event.current.keyCode;
                    if (k != KeyCode.None && k != KeyCode.Mouse0 && k != KeyCode.Mouse1)
                    {
                        quickViewKey = k;
                        quickViewCtrl = Event.current.control;
                        quickViewShift = Event.current.shift;
                        quickViewAlt = Event.current.alt;
                        ItemTextureViewerMod.waitingForKey = false;
                        Write();
                    }
                    Event.current.Use();
                }
            }
            ls.Gap(30);
            if (ls.ButtonText("ItemTextureViewer_Reset".Translate()))
            {
                ResetToDefault();
                Write();
            }
            ls.End();
            Widgets.EndScrollView();
        }

        private string FormatKeyCombination(bool ctrl, bool shift, bool alt, KeyCode key)
        {
            string r = "";
            if (ctrl) r += "Ctrl+";
            if (shift) r += "Shift+";
            if (alt) r += "Alt+";
            if (key == KeyCode.None)
                r += "ItemTextureViewer_NotSet".Translate();
            else
                r += key.ToString();
            return r;
        }

        private void ResetToDefault()
        {
            btnOffsetX = 19f; btnOffsetY = 3f;
            autoOpenPawnPreview = true; autoFollowInspectPane = true;
            previewPosX = 100f; previewPosY = 100f; previewZoom = 0.4f;
            previewAlignment = PreviewAlignment.Left;
            previewHorizontalOffset = 0f; previewVerticalGap = 25f;
            previewRefreshInterval = 0.3f;
            showPreviewDebugArea = false;
            previewShowHumans = true; previewShowAnimals = true; previewShowOthers = true;
            lastWindowPosX = -1f; lastWindowPosY = -1f; lastWindowZoom = 1f; lastSelectedSize = 1024;
            quickViewKey = KeyCode.Y; quickViewCtrl = false; quickViewShift = false; quickViewAlt = false;
            currentZoom = 1f;
            ItemTextureViewerMod.waitingForKey = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref btnOffsetX, "btnOffsetX", 19f);
            Scribe_Values.Look(ref btnOffsetY, "btnOffsetY", 3f);
            Scribe_Values.Look(ref autoOpenPawnPreview, "autoOpenPawnPreview", true);
            Scribe_Values.Look(ref autoFollowInspectPane, "autoFollowInspectPane", true);
            Scribe_Values.Look(ref previewPosX, "previewPosX", 100f);
            Scribe_Values.Look(ref previewPosY, "previewPosY", 100f);
            Scribe_Values.Look(ref previewZoom, "previewZoom", 0.4f);
            Scribe_Values.Look(ref previewAlignment, "previewAlignment", PreviewAlignment.Left);
            Scribe_Values.Look(ref previewHorizontalOffset, "previewHorizontalOffset", 0f);
            Scribe_Values.Look(ref previewVerticalGap, "previewVerticalGap", 25f);
            Scribe_Values.Look(ref previewRefreshInterval, "previewRefreshInterval", 0.3f);
            Scribe_Values.Look(ref showPreviewDebugArea, "showPreviewDebugArea", false);
            Scribe_Values.Look(ref previewShowHumans, "previewShowHumans", true);
            Scribe_Values.Look(ref previewShowAnimals, "previewShowAnimals", true);
            Scribe_Values.Look(ref previewShowOthers, "previewShowOthers", true);
            Scribe_Values.Look(ref lastWindowPosX, "lastWindowPosX", -1f);
            Scribe_Values.Look(ref lastWindowPosY, "lastWindowPosY", -1f);
            Scribe_Values.Look(ref lastWindowZoom, "lastWindowZoom", 1f);
            Scribe_Values.Look(ref lastSelectedSize, "lastSelectedSize", 1024);
            Scribe_Values.Look(ref quickViewKey, "quickViewKey", KeyCode.Y);
            Scribe_Values.Look(ref quickViewCtrl, "quickViewCtrl", false);
            Scribe_Values.Look(ref quickViewShift, "quickViewShift", false);
            Scribe_Values.Look(ref quickViewAlt, "quickViewAlt", false);
            Scribe_Values.Look(ref currentZoom, "currentZoom", 1f);
        }
    }
}