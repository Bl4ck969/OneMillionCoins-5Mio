using System;
using GoldDigger;
using HarmonyLib;
using UnityEngine;

namespace OneMillionCoins5Mio
{
    /// <summary>HUD-Anpassungen für mehr Etagen: Aufzugsmenü in Spalten, sobald es nicht mehr auf den Bildschirm passt, mit Sperrgrund.</summary>
    internal static class HudPatches
    {
        // Maße wie in GoldDiggerHUD.DrawCargoElevator: Kopf 72, Zeile 54 (Knopf 48), Fuß 76.
        private const float Head = 72f, Row = 54f, Foot = 76f, Column = 350f, Gap = 20f, Margin = 40f;

        private static AccessTools.FieldRef<GoldDiggerHUD, CargoElevator> _elevator;
        private static AccessTools.FieldRef<GoldDiggerHUD, bool> _open;
        private static AccessTools.FieldRef<GoldDiggerHUD, GUIStyle> _button;
        private static AccessTools.FieldRef<GoldDiggerHUD, GUIStyle> _heading;
        private static Func<float> _uiWidth, _uiHeight;
        private static Action<Rect, Color> _drawPanel;
        private static Action<GoldDiggerHUD, bool> _setCursorLocked;

        internal static void Apply(Harmony harmony)
        {
            var hud = typeof(GoldDiggerHUD);
            try
            {
                _elevator = AccessTools.FieldRefAccess<GoldDiggerHUD, CargoElevator>("_cargoElevator");
                _open = AccessTools.FieldRefAccess<GoldDiggerHUD, bool>("_elevatorOpen");
                _button = AccessTools.FieldRefAccess<GoldDiggerHUD, GUIStyle>("_buttonStyle");
                _heading = AccessTools.FieldRefAccess<GoldDiggerHUD, GUIStyle>("_notificationStyle");
                var width = AccessTools.Field(hud, "_uiWidth");
                var height = AccessTools.Field(hud, "_uiHeight");
                _uiWidth = () => (float)width.GetValue(null);
                _uiHeight = () => (float)height.GetValue(null);
                _drawPanel = AccessTools.MethodDelegate<Action<Rect, Color>>(AccessTools.Method(hud, "DrawPanel"));
                _setCursorLocked = AccessTools.MethodDelegate<Action<GoldDiggerHUD, bool>>(AccessTools.Method(hud, "SetCursorLocked"));
                harmony.Patch(AccessTools.Method(hud, "DrawCargoElevator"), prefix: new HarmonyMethod(typeof(HudPatches), nameof(ElevatorMenuPrefix)));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Aufzugsmenü nicht angepasst (bleibt einspaltig): " + e.Message);
            }
        }

        /// <summary>
        /// Gleiche Knöpfe und Regeln wie im Spiel, bei Bedarf auf mehrere Spalten verteilt, von oben nach unten und dann
        /// nach rechts (oberste Etage links oben). Gesperrte Etagen nennen den Grund (Landeplatz oder Schacht bei Etage NN).
        /// </summary>
        private static bool ElevatorMenuPrefix(GoldDiggerHUD __instance)
        {
            if (!ModState.Active) return true;
            var elevator = _elevator(__instance);
            int count = elevator != null ? elevator.LayerCount : 0;
            float uiWidth = _uiWidth(), uiHeight = _uiHeight();
            int rows = Mathf.Max(1, Mathf.FloorToInt((uiHeight - 24f - Head - Foot) / Row));
            if (count == 0) return true;

            // Auch einspaltig zeichnet der Mod selbst, damit gesperrte Etagen ihren Grund nennen (Landeplatz oder Schacht).
            int columns = (count + rows - 1) / rows;
            rows = (count + columns - 1) / columns;   // gleichmäßig verteilen
            float width = Margin * 2f + columns * Column + (columns - 1) * Gap;
            float height = Head + rows * Row + Foot;
            var rect = new Rect((uiWidth - width) * 0.5f, (uiHeight - height) * 0.5f, width, height);
            _drawPanel(rect, new Color(0.025f, 0.04f, 0.05f, 0.99f));
            LocalizedGUI.Label(new Rect(rect.x + 20f, rect.y + 22f, rect.width - 40f, 44f), "Atlas cargo lift", _heading(__instance));
            var buttonStyle = _button(__instance);
            for (int n = 0; n < count; n++)
            {
                int index = count - 1 - n;   // n = 0 ist die oberste Etage „00“
                int column = n / rows, row = n % rows;
                var button = new Rect(rect.x + Margin + column * (Column + Gap), rect.y + Head + row * Row, Column, 48f);
                bool enabled = elevator.IsAuthorized(index) && elevator.IsLayerAccessible(index) && index != elevator.CurrentFloor;
                string text;
                if (index == elevator.CurrentFloor) text = " · You are here";
                else if (elevator.IsAuthorized(index)) text = !elevator.IsLayerAccessible(index) ? ElevatorPatches.BlockReason(elevator, index) : "";
                else text = " · Not authorized";
                GUI.enabled = enabled;
                if (VaultUI.ActionButton(button, elevator.GetLayerName(index) + text, buttonStyle))
                {
                    _open(__instance) = false;
                    _setCursorLocked(__instance, true);
                    elevator.TravelTo(index);
                }
                GUI.enabled = true;
            }
            if (VaultUI.ActionButton(new Rect(rect.center.x - 100f, rect.yMax - 54f, 200f, 40f), "Close  [Esc]", buttonStyle))
            {
                _open(__instance) = false;
                _setCursorLocked(__instance, true);
            }
            return false;
        }
    }
}
