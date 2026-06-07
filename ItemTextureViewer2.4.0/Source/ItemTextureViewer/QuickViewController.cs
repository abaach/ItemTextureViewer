using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ItemTextureViewer
{
    public class QuickViewController : GameComponent
    {
        public static object PendingDrawObj;

        public QuickViewController(Game game) { }

        public override void GameComponentUpdate()
        {
            try
            {
                if (Current.ProgramState != ProgramState.Playing) return;
                var s = ItemTextureViewerMod.Settings;
                if (s == null || s.quickViewKey == KeyCode.None) return;

                bool ctrl = !s.quickViewCtrl || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool shift = !s.quickViewShift || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool alt = !s.quickViewAlt || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                if (ctrl && shift && alt && Input.GetKeyDown(s.quickViewKey))
                {
                    Thing t = Find.Selector?.SingleSelectedThing;
                    if (t == null)
                    {
                        var windows = Find.WindowStack?.Windows;
                        if (windows != null)
                        {
                            foreach (var w in windows)
                            {
                                if (w.GetType().Name == "Dialog_InfoCard")
                                {
                                    t = Traverse.Create(w).Field("thing").GetValue<Thing>();
                                    break;
                                }
                            }
                        }
                    }
                    if (t != null) PendingDrawObj = t;
                }
            }
            catch (Exception ex) { Log.Error($"[ItemTextureViewer] QuickView: {ex}"); }
        }
    }
}