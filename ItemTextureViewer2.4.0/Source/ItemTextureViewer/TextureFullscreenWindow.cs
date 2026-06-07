using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ItemTextureViewer
{
    public class TextureFullscreenWindow : Window
    {
        private object drawObj;
        private float zoom;
        private Vector2 offset;
        private Rot4 pawnRot = Rot4.South;
        private Texture2D cachedThingTexture;
        private RenderTexture pawnRenderTexture;

        private const float MinZoom = 0.1f, MaxZoom = 200f, ZoomSpeed = 0.05f;
        private bool dragging;
        private Vector2 lastMousePos;

        private static readonly int[] Sizes = { 128, 256, 512, 1024, 2048 };
        private int selectedSize = 1024;
        private float lastRefreshTime;

        public override Vector2 InitialSize => new Vector2(1024f, 1024f);

        public TextureFullscreenWindow(object drawObj, float initialZoom)
        {
            this.drawObj = drawObj;
            var s = ItemTextureViewerMod.Settings;
            zoom = s.lastWindowZoom > 0 ? s.lastWindowZoom : initialZoom;
            selectedSize = s.lastSelectedSize > 0 ? s.lastSelectedSize : 1024;

            if (s.lastWindowPosX >= 0 && s.lastWindowPosY >= 0)
            {
                windowRect.x = s.lastWindowPosX;
                windowRect.y = s.lastWindowPosY;
            }
            else
            {
                windowRect.x = (UI.screenWidth - 1024) / 2f;
                windowRect.y = (UI.screenHeight - 1024) / 2f;
            }

            doCloseButton = true;
            closeOnClickedOutside = false;
            draggable = true;
            absorbInputAroundWindow = false;
            layer = WindowLayer.Super;
            drawShadow = true;
            resizeable = true;

            if (!(drawObj is Pawn))
            {
                Thing thing = drawObj as Thing;
                if (thing != null)
                    cachedThingTexture = HarmonyPatches.GetThingHighResTexture(thing);
                else
                {
                    ThingDef def = drawObj as ThingDef;
                    if (def != null)
                        cachedThingTexture = HarmonyPatches.GetThingHighResTexture(def);
                }
            }
            else
            {
                Pawn pawn = drawObj as Pawn;
                if (pawn != null)
                {
                    try
                    {
                        pawnRenderTexture = HarmonyPatches.GetPawnRenderTexture(pawn, pawnRot, new Vector2(selectedSize, selectedSize));
                    }
                    catch (Exception) { }
                    lastRefreshTime = Time.realtimeSinceStartup;
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (drawObj == null) return;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { SaveState(); Close(); Event.current.Use(); return; }

            Rect sizeBtn = new Rect(inRect.xMax - 100f, inRect.y + 5f, 90f, 24f);
            if (Widgets.ButtonText(sizeBtn, $"{selectedSize}px"))
            {
                int idx = System.Array.IndexOf(Sizes, selectedSize);
                idx = (idx + 1) % Sizes.Length;
                selectedSize = Sizes[idx];
                if (drawObj is Pawn pawn)
                {
                    try
                    {
                        pawnRenderTexture = HarmonyPatches.GetPawnRenderTexture(pawn, pawnRot, new Vector2(selectedSize, selectedSize));
                    }
                    catch (Exception) { pawnRenderTexture = null; }
                    lastRefreshTime = Time.realtimeSinceStartup;
                }
            }

            if (drawObj is Pawn pawn2)
            {
                if (Time.realtimeSinceStartup - lastRefreshTime > ItemTextureViewerMod.Settings.previewRefreshInterval)
                {
                    try
                    {
                        pawnRenderTexture = HarmonyPatches.GetPawnRenderTexture(pawn2, pawnRot, new Vector2(selectedSize, selectedSize));
                    }
                    catch (Exception) { pawnRenderTexture = null; }
                    lastRefreshTime = Time.realtimeSinceStartup;
                }
            }

            if (drawObj is Pawn pawnRotate)
            {
                float y = inRect.yMax - 30f;
                if (Widgets.ButtonText(new Rect(inRect.x + 10f, y, 24f, 24f), "←"))
                {
                    pawnRot = pawnRot.Rotated(RotationDirection.Counterclockwise);
                    try
                    {
                        pawnRenderTexture = HarmonyPatches.GetPawnRenderTexture(pawnRotate, pawnRot, new Vector2(selectedSize, selectedSize));
                    }
                    catch (Exception) { pawnRenderTexture = null; }
                    lastRefreshTime = Time.realtimeSinceStartup;
                }
                if (Widgets.ButtonText(new Rect(inRect.x + 40f, y, 24f, 24f), "→"))
                {
                    pawnRot = pawnRot.Rotated(RotationDirection.Clockwise);
                    try
                    {
                        pawnRenderTexture = HarmonyPatches.GetPawnRenderTexture(pawnRotate, pawnRot, new Vector2(selectedSize, selectedSize));
                    }
                    catch (Exception) { pawnRenderTexture = null; }
                    lastRefreshTime = Time.realtimeSinceStartup;
                }
            }

            Rect export = new Rect(inRect.xMax - 80f, inRect.yMax - 30f, 70f, 24f);
            Widgets.DrawBoxSolid(export, new Color(0.2f, 0.2f, 0.2f, 0.7f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(export, "ItemTextureViewer_ExportButton".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            if (Mouse.IsOver(export))
            {
                if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                {
                    Texture tex;
                    if (drawObj is Pawn) tex = pawnRenderTexture;
                    else tex = cachedThingTexture;
                    if (tex != null)
                    {
                        HarmonyPatches.ExportTextureToFile(tex, GetExportName());
                        Messages.Message("ItemTextureViewer_ExportSuccess".Translate(), MessageTypeDefOf.TaskCompletion);
                    }
                    else Messages.Message("ItemTextureViewer_ExportFailed".Translate(), MessageTypeDefOf.RejectInput);
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseUp && Event.current.button == 1)
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption> { new FloatMenuOption("ItemTextureViewer_OpenExportFolder".Translate(), () => HarmonyPatches.OpenExportFolder()) };
                    Find.WindowStack.Add(new FloatMenu(opts));
                    Event.current.Use();
                }
            }

            if (Mouse.IsOver(inRect))
            {
                if (Event.current.type == EventType.ScrollWheel)
                {
                    float old = zoom;
                    zoom = Mathf.Clamp(zoom - Event.current.delta.y * ZoomSpeed, MinZoom, MaxZoom);
                    Vector2 centerScreen = inRect.center - inRect.position;
                    offset = (centerScreen - (centerScreen - offset * old) / old * zoom) / zoom;
                    Event.current.Use();
                }
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                { dragging = true; lastMousePos = Event.current.mousePosition; Event.current.Use(); }
                if (Event.current.type == EventType.MouseDrag && dragging)
                { offset += (Event.current.mousePosition - lastMousePos) / zoom; lastMousePos = Event.current.mousePosition; Event.current.Use(); }
                if (Event.current.type == EventType.MouseUp && dragging)
                { dragging = false; Event.current.Use(); }
            }

            Widgets.Label(new Rect(5, 5, 300, 30), string.Format("ItemTextureViewer_FullscreenHint".Translate(), zoom));

            GUI.BeginGroup(inRect);
            Vector2 drawCenter = new Vector2(inRect.width * 0.5f, inRect.height * 0.5f);
            if (drawObj is Pawn && pawnRenderTexture != null)
            {
                Rect r = new Rect(drawCenter.x + offset.x * zoom - pawnRenderTexture.width * zoom * 0.5f,
                                  drawCenter.y + offset.y * zoom - pawnRenderTexture.height * zoom * 0.5f,
                                  pawnRenderTexture.width * zoom, pawnRenderTexture.height * zoom);
                GUI.DrawTexture(r, pawnRenderTexture, ScaleMode.ScaleToFit);
            }
            else if (!(drawObj is Pawn) && cachedThingTexture != null)
            {
                Rect r = new Rect(drawCenter.x + offset.x * zoom - cachedThingTexture.width * zoom * 0.5f,
                                  drawCenter.y + offset.y * zoom - cachedThingTexture.height * zoom * 0.5f,
                                  cachedThingTexture.width * zoom, cachedThingTexture.height * zoom);
                GUI.DrawTexture(r, cachedThingTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                float size = 128f * zoom;
                Rect r = new Rect(drawCenter.x + offset.x * zoom - size * 0.5f, drawCenter.y + offset.y * zoom - size * 0.5f, size, size);
                if (drawObj is Thing t) Widgets.ThingIcon(r, t);
                else if (drawObj is ThingDef td) Widgets.ThingIcon(r, td);
                else Widgets.Label(r, "ItemTextureViewer_NoTexture".Translate());
            }
            GUI.EndGroup();
        }

        private void SaveState()
        {
            var s = ItemTextureViewerMod.Settings;
            s.lastWindowPosX = windowRect.x;
            s.lastWindowPosY = windowRect.y;
            s.lastWindowZoom = zoom;
            s.lastSelectedSize = selectedSize;
            s.Write();
        }

        public override void Close(bool doCloseSound = true) { SaveState(); base.Close(doCloseSound); }

        private string GetExportName()
        {
            if (drawObj is Pawn pawn) return pawn.Name?.ToStringShort ?? "pawn";
            if (drawObj is Thing t) return t.def.defName;
            if (drawObj is ThingDef td) return td.defName;
            return "texture";
        }
    }
}