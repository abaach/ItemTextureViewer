using RimWorld;
using UnityEngine;
using Verse;

namespace ItemTextureViewer
{
    public class PawnPreviewWindow : Window
    {
        public Pawn pawn;
        public float zoom = 1f;
        public Vector2 position;
        public RenderTexture cachedRenderTexture;
        public Vector2 texSize = new Vector2(512f, 512f);

        public override Vector2 InitialSize => texSize * zoom;

        public PawnPreviewWindow()
        {
            doCloseButton = false;
            closeOnClickedOutside = false;
            draggable = false;
            absorbInputAroundWindow = false;
            doWindowBackground = false;
            layer = WindowLayer.GameUI;
            drawShadow = false;
            preventCameraMotion = false;
            resizeable = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (pawn == null) return;
            windowRect.x = position.x;
            windowRect.y = position.y;
            windowRect.width = texSize.x * zoom;
            windowRect.height = texSize.y * zoom;

            if (cachedRenderTexture != null)
                GUI.DrawTexture(inRect, cachedRenderTexture, ScaleMode.ScaleToFit);

            if (ItemTextureViewerMod.Settings.showPreviewDebugArea)
                Widgets.DrawBoxSolid(inRect, new Color(1f, 0f, 0f, 0.3f));
        }
    }
}