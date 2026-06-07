using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ItemTextureViewer
{
    public class PawnPreviewManager : GameComponent
    {
        public static PawnPreviewManager Instance { get; private set; }
        private PawnPreviewWindow curWin;
        private float lastRenderTime;
        private RenderTexture cachedTex;
        private Pawn lastPawn;
        private bool needClose, needOpen;

        public PawnPreviewManager(Game game) => Instance = this;

        public override void GameComponentUpdate()
        {
            try
            {
                SafeUpdate();
            }
            catch (Exception ex) { Log.Error($"[ItemTextureViewer] PawnPreview error: {ex}"); }
        }

        private void SafeUpdate()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            var s = ItemTextureViewerMod.Settings;
            if (s == null || !s.autoOpenPawnPreview) return;
            if (Find.CurrentMap == null) return;

            Pawn sel = Find.Selector?.SingleSelectedThing as Pawn;
            if (sel?.Destroyed ?? true) sel = null;

            bool insp = Find.WindowStack?.WindowOfType<MainTabWindow_Inspect>() != null;
            bool show = sel != null && (s.autoFollowInspectPane ? insp : true);

            if (show && sel != null)
            {
                bool hum = sel.RaceProps?.Humanlike ?? false;
                bool ani = sel.RaceProps?.Animal ?? false;
                bool oth = !hum && !ani;
                if ((hum && !s.previewShowHumans) ||
                    (ani && !s.previewShowAnimals) ||
                    (oth && !s.previewShowOthers))
                    show = false;
            }

            if (show)
            {
                if (curWin == null || !curWin.IsOpen) { needOpen = true; needClose = false; }
                else if (curWin.pawn != sel) { curWin.pawn = sel; cachedTex = null; lastPawn = null; }

                if (curWin != null && curWin.IsOpen)
                {
                    curWin.zoom = s.previewZoom;
                    if (lastPawn != sel || cachedTex == null || Time.realtimeSinceStartup - lastRenderTime > s.previewRefreshInterval)
                    {
                        try
                        {
                            cachedTex = HarmonyPatches.GetPawnRenderTexture(sel, Rot4.South, new Vector2(512f, 512f));
                        }
                        catch (Exception) { cachedTex = null; }
                        lastRenderTime = Time.realtimeSinceStartup;
                        curWin.texSize = cachedTex != null ? new Vector2(cachedTex.width, cachedTex.height) : new Vector2(512f, 512f);
                        curWin.cachedRenderTexture = cachedTex;
                        lastPawn = sel;
                    }

                    if (s.autoFollowInspectPane)
                    {
                        var iw = Find.WindowStack.WindowOfType<MainTabWindow_Inspect>();
                        if (iw != null)
                        {
                            Rect ir = iw.windowRect;
                            float w = curWin.texSize.x * curWin.zoom, h = curWin.texSize.y * curWin.zoom;
                            float x = ir.x;
                            switch (s.previewAlignment)
                            {
                                case ItemTextureViewerSettings.PreviewAlignment.Left:
                                    x = ir.x + s.previewHorizontalOffset;
                                    break;
                                case ItemTextureViewerSettings.PreviewAlignment.Center:
                                    x = ir.center.x - w / 2f + s.previewHorizontalOffset;
                                    break;
                                case ItemTextureViewerSettings.PreviewAlignment.Right:
                                    x = ir.xMax - w + s.previewHorizontalOffset;
                                    break;
                            }
                            curWin.position = new Vector2(x, ir.y - h - s.previewVerticalGap);
                        }
                    }
                    else curWin.position = new Vector2(s.previewPosX, s.previewPosY);
                }
            }
            else { needClose = true; needOpen = false; }

            if (needOpen && !needClose && (curWin == null || !curWin.IsOpen))
            {
                curWin = new PawnPreviewWindow();
                Find.WindowStack.Add(curWin);
                needOpen = false;
            }
            if (needClose && curWin != null && curWin.IsOpen)
            {
                curWin.Close();
                curWin = null;
                needClose = false;
            }
        }
    }
}