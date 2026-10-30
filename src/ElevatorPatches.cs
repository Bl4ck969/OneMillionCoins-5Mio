using System;
using System.Collections.Generic;
using System.Linq;
using GoldDigger;
using HarmonyLib;
using UnityEngine;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Aufzug im 5-Mio.-Modus. Das Spiel fährt jede Strecke als eine S-Kurve über die ganze Fahrzeit; bei 200 m dauern
    /// Anfahren und Bremsen so je fast eine halbe Minute. Hier: kurze Anfahr- und Bremsphase, dazwischen gleichmäßig.
    /// Die sechs Kabinentasten zeigen Etagen relativ zur aktuellen: 00 und Menü, zwei und eine darüber, zwei und eine darunter.
    /// </summary>
    internal static class ElevatorPatches
    {
        // Plätze der Kabinentasten in Leserichtung, wie von Bl4ck969 gewünscht (29.10.2026).
        private const int Top = 0, Menu = 1, Up2 = 2, Up1 = 3, Down2 = 4;
        private const int MenuTarget = -2, NoTarget = -1;
        private static readonly Color Available = new Color(0.4f, 0.85f, 0.64f), Locked = new Color(0.5f, 0.49f, 0.43f);

        private static AccessTools.FieldRef<CargoElevator, float> _startHeight, _elapsed, _duration;
        private static AccessTools.FieldRef<CargoElevator, bool> _riding;
        private static AccessTools.FieldRef<CargoElevator, Transform> _player;
        private static AccessTools.FieldRef<CargoElevator, Vector3> _riderOffset;
        private static AccessTools.FieldRef<CargoElevator, CoinFieldManager> _field;
        private static AccessTools.FieldRef<ElevatorControl, CargoElevator> _lift;
        private static AccessTools.FieldRef<ElevatorControl, int> _floor;
        private static AccessTools.FieldRef<ElevatorControl, bool> _landingCall;
        private static AccessTools.FieldRef<ElevatorControl, TextMesh> _label;
        private static AccessTools.FieldRef<ElevatorControl, Renderer> _lamp;

        private static CargoElevator _profiled;    // laufende Fahrt mit eigenem Profil; null = kurze Fahrt wie im Original
        private static float _distance, _speed, _ramp;
        private static readonly Dictionary<ElevatorControl, int> _slots = new Dictionary<ElevatorControl, int>();
        private static readonly HashSet<TextMesh> _shrunk = new HashSet<TextMesh>();
        private static MaterialPropertyBlock _block;
        private static readonly Dictionary<int, string> _reasons = new Dictionary<int, string>();
        private static float _reasonsUntil;

        internal static void Apply(Harmony harmony)
        {
            var lift = typeof(CargoElevator);
            try
            {
                _startHeight = AccessTools.FieldRefAccess<CargoElevator, float>("_startHeight");
                _elapsed = AccessTools.FieldRefAccess<CargoElevator, float>("_elapsed");
                _duration = AccessTools.FieldRefAccess<CargoElevator, float>("_duration");
                _riding = AccessTools.FieldRefAccess<CargoElevator, bool>("_riding");
                _player = AccessTools.FieldRefAccess<CargoElevator, Transform>("_player");
                _riderOffset = AccessTools.FieldRefAccess<CargoElevator, Vector3>("_riderOffset");
                _field = AccessTools.FieldRefAccess<CargoElevator, CoinFieldManager>("_field");
                harmony.Patch(AccessTools.Method(lift, "BeginTravel"), postfix: new HarmonyMethod(typeof(ElevatorPatches), nameof(BeginTravelPostfix)));
                harmony.Patch(AccessTools.Method(lift, nameof(CargoElevator.Tick)), postfix: new HarmonyMethod(typeof(ElevatorPatches), nameof(TickPostfix)));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Aufzug-Fahrprofil nicht angepasst (bleibt wie im Original): " + e.Message);
            }
            var control = typeof(ElevatorControl);
            try
            {
                _lift = AccessTools.FieldRefAccess<ElevatorControl, CargoElevator>("elevator");
                _floor = AccessTools.FieldRefAccess<ElevatorControl, int>("floor");
                _landingCall = AccessTools.FieldRefAccess<ElevatorControl, bool>("landingCall");
                _label = AccessTools.FieldRefAccess<ElevatorControl, TextMesh>("label");
                _lamp = AccessTools.FieldRefAccess<ElevatorControl, Renderer>("lamp");
                harmony.Patch(AccessTools.Method(control, nameof(ElevatorControl.Refresh)), prefix: new HarmonyMethod(typeof(ElevatorPatches), nameof(RefreshPrefix)));
                harmony.Patch(AccessTools.Method(control, nameof(ElevatorControl.Interact)), prefix: new HarmonyMethod(typeof(ElevatorPatches), nameof(InteractPrefix)));
                harmony.Patch(AccessTools.PropertyGetter(control, nameof(ElevatorControl.Prompt)), prefix: new HarmonyMethod(typeof(ElevatorPatches), nameof(PromptPrefix)));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Kabinentasten nicht angepasst (bleiben fest): " + e.Message);
            }
        }

        // ---------- Fahrprofil ----------

        /// <summary>Fahrzeit (ohne Tore) für eine Strecke, die länger ist als Anfahr- und Bremsweg zusammen.</summary>
        internal static float Duration(float distance, float speed, float ramp) => 2f * ramp + (distance - speed * ramp) / speed;

        /// <summary>Weg nach t Sekunden: in der Anfahrzeit weich auf Höchstgeschwindigkeit, gleichmäßig, ebenso weich bis zum Halt.</summary>
        internal static float Travelled(float t, float total, float distance, float speed, float ramp)
        {
            if (t <= 0f) return 0f;
            if (t >= total) return distance;
            if (t < ramp) return RampDistance(t / ramp, speed, ramp);
            if (total - t < ramp) return distance - RampDistance((total - t) / ramp, speed, ramp);
            return speed * ramp * 0.5f + speed * (t - ramp);
        }

        /// <summary>Weg in der Anfahrphase: Die Geschwindigkeit steigt wie smoothstep (3x² − 2x³), der Weg ist deren Integral.</summary>
        private static float RampDistance(float x, float speed, float ramp) => speed * ramp * (x * x * x - 0.5f * x * x * x * x);

        private static void BeginTravelPostfix(CargoElevator __instance, bool __result)
        {
            if (!__result) return;
            _profiled = null;
            if (!ModState.Active) return;
            _speed = Plugin.LiftSpeed.Value;
            _ramp = Plugin.LiftRamp.Value;
            _distance = Mathf.Abs(__instance.GetLayerHeight(__instance.TargetFloor) - _startHeight(__instance));
            // Kurze Fahrten (nicht länger als Anfahr- und Bremsweg) bleiben wie im Original: eine S-Kurve, mindestens 2,1 s.
            if (_distance <= _speed * _ramp) return;
            _duration(__instance) = Duration(_distance, _speed, _ramp);
            _profiled = __instance;
        }

        /// <summary>Das Spiel hat die Kabine schon nach seiner S-Kurve gesetzt; hier nach dem eigenen Profil, bei gleicher Fahrzeit.</summary>
        private static void TickPostfix(CargoElevator __instance)
        {
            if (__instance != _profiled || !__instance.IsMoving || __instance.Car == null) return;
            float t = _elapsed(__instance) - 0.5f, total = _duration(__instance);
            if (t <= 0f || t >= total) return;   // Tore schließen noch bzw. Ziel erreicht: Höhe hat das Spiel gesetzt
            float start = _startHeight(__instance), target = __instance.GetLayerHeight(__instance.TargetFloor);
            var car = __instance.Car;
            var position = car.localPosition;
            position.y = start + Mathf.Sign(target - start) * Travelled(t, total, _distance, _speed, _ramp);
            car.localPosition = position;
            var player = _player(__instance);
            if (_riding(__instance) && player != null) player.position = car.position + _riderOffset(__instance);
        }

        // ---------- Grund einer Sperre ----------

        /// <summary>
        /// Zusatz im Aufzugsmenü für eine gesperrte Etage. Das Spiel schreibt immer „Excavate landing“; gesperrt wird aber auch,
        /// wenn im Schacht zwischen Kabine und Ziel Münzen liegen. Dann wird die Etage genannt, an der die Kabine zuerst
        /// hängen bliebe. Alle 0,5 s neu (die Prüfung über den ganzen Schacht ist teuer, das Menü zeichnet jedes Bild).
        /// </summary>
        internal static string BlockReason(CargoElevator lift, int index)
        {
            if (Time.unscaledTime > _reasonsUntil)
            {
                _reasons.Clear();
                _reasonsUntil = Time.unscaledTime + 0.5f;
            }
            if (_reasons.TryGetValue(index, out var reason)) return reason;
            bool german = ModText.German;
            if (!lift.IsLandingClear(index))
            {
                reason = german ? " · Landeplatz freischaufeln" : " · Excavate landing";
            }
            else
            {
                int at = BlockedShaftFloor(lift, index);
                string name = at >= 0 ? lift.GetLayerName(at) : null;
                reason = german ? (name != null ? $" · Schacht bei {name} freischaufeln" : " · Schacht freischaufeln")
                    : (name != null ? $" · Excavate shaft at {name}" : " · Excavate shaft");
            }
            return _reasons[index] = reason;
        }

        /// <summary>
        /// Erste Etage von der Kabine aus Richtung Ziel, deren Schachtstück Münzen enthält (obere Etage des Stücks, von dort
        /// gräbt man nach unten); -1, wenn keins gefunden wird. Gleiches Volumen wie <c>CargoElevator.IsLayerAccessible</c>,
        /// nur Etage für Etage.
        /// </summary>
        internal static int BlockedShaftFloor(CargoElevator lift, int target)
        {
            var field = _field == null ? null : _field(lift);
            if (field == null || lift.Car == null) return -1;
            int from = lift.CurrentFloor, step = target > from ? 1 : -1;
            var car = lift.Car.position;
            for (int k = from; k != target; k += step)
            {
                float a = lift.FloorPosition(k).y, b = lift.FloorPosition(k + step).y;
                float low = Mathf.Max(Mathf.Min(a, b) - 0.24f, field.transform.position.y + 0.14f), high = Mathf.Max(a, b) + 3.4f;
                if (high <= low) continue;
                var volume = new Bounds(new Vector3(car.x, (low + high) * 0.5f, car.z), new Vector3(3.65f, high - low, 3.65f));
                if (!field.IsExcavationVolumeClear(volume)) return Mathf.Max(k, k + step);
            }
            return -1;
        }

        // ---------- Kabinentasten ----------

        /// <summary>Die sechs Kabinentasten in Leserichtung (Reihen von oben, in der Reihe in Schreibrichtung der Beschriftung).</summary>
        internal static List<ElevatorControl> CabinInReadingOrder(CargoElevator lift)
        {
            if (lift == null || lift.Car == null) return new List<ElevatorControl>();
            var cabin = lift.Car.GetComponentsInChildren<ElevatorControl>(true).Where(c => !_landingCall(c)).ToList();
            var label = cabin.Select(c => _label(c)).FirstOrDefault(l => l != null);
            var right = label != null ? label.transform.right : Vector3.right;
            return cabin.OrderByDescending(c => Mathf.RoundToInt(c.transform.position.y * 4f))
                .ThenBy(c => Vector3.Dot(c.transform.position, right)).ToList();
        }

        /// <summary>Platz einer Kabinentaste in Leserichtung (0 oben links … 5 unten rechts); -1 = Ruftaste oder unbekannte Kabine.</summary>
        private static int SlotOf(ElevatorControl control)
        {
            if (_slots.TryGetValue(control, out int slot)) return slot;
            var lift = _lift(control);
            if (_landingCall(control) || lift == null || lift.Car == null || !control.transform.IsChildOf(lift.Car)) return _slots[control] = -1;
            var cabin = CabinInReadingOrder(lift);
            if (cabin.Count != 6)
            {
                Plugin.Log.LogWarning($"Kabine hat {cabin.Count} statt 6 Tasten: Belegung bleibt wie im Spiel.");
                foreach (var c in cabin) _slots[c] = -1;
                return _slots[control] = -1;
            }
            Plugin.Log.LogInfo("Kabinentasten (Leserichtung) vorher: " + string.Join(" ", cabin.Select(c => lift.GetLayerName(_floor(c)))));
            for (int i = 0; i < cabin.Count; i++) _slots[cabin[i]] = i;
            return _slots.TryGetValue(control, out slot) ? slot : -1;
        }

        /// <summary>Ziel eines Platzes von der aktuellen Etage aus (Index von unten): 00, Menü, +2, +1, −2, −1.</summary>
        private static int TargetOf(int slot, CargoElevator lift)
        {
            int current = lift.CurrentFloor, top = lift.LayerCount - 1, target;
            switch (slot)
            {
                case Top: return top;
                case Menu: return MenuTarget;
                case Up2: target = current + 2; break;
                case Up1: target = current + 1; break;
                case Down2: target = current - 2; break;
                default: target = current - 1; break;
            }
            return target >= 0 && target <= top ? target : NoTarget;
        }

        /// <summary>Ziel der Taste bestimmen und bei einer Etage eintragen; -1/-2 = keine Etage bzw. Menü (dann ist der Mod zuständig).</summary>
        private static int Retarget(ElevatorControl control)
        {
            if (!ModState.Active || _lift(control) == null) return int.MinValue;
            int slot = SlotOf(control);
            if (slot < 0) return int.MinValue;
            int target = TargetOf(slot, _lift(control));
            if (target >= 0) _floor(control) = target;
            return target;
        }

        private static bool RefreshPrefix(ElevatorControl __instance)
        {
            int target = Retarget(__instance);
            if (target == int.MinValue || target >= 0) return true;   // Etage: Farbe und Nummer wie im Spiel
            var lift = _lift(__instance);
            bool usable = target == MenuTarget && lift.IsUnlocked && !lift.IsMoving;
            var label = _label(__instance);
            if (label != null)
            {
                label.text = target == MenuTarget ? "MENU" : "--";
                // Vier Buchstaben in Ziffergröße laufen über den Tastenrand; der Platz bleibt immer das Menü.
                if (target == MenuTarget && _shrunk.Add(label)) label.characterSize *= 0.5f;
            }
            var lamp = _lamp(__instance);
            if (lamp != null)
            {
                if (_block == null) _block = new MaterialPropertyBlock();
                var color = usable ? Available : Locked;
                _block.SetColor("_BaseColor", color);
                _block.SetColor("_EmissionColor", color * 0.5f);
                lamp.SetPropertyBlock(_block);
            }
            return false;
        }

        private static bool InteractPrefix(ElevatorControl __instance)
        {
            int target = Retarget(__instance);
            if (target == int.MinValue || target >= 0) return true;   // Etage: fährt wie im Spiel (mit dessen Sperren und Meldungen)
            var lift = _lift(__instance);
            if (target == MenuTarget && lift.IsUnlocked && !lift.IsMoving)
            {
                lift.Interact();   // öffnet das Aufzugsmenü des HUD mit allen Etagen
                MenuAudio.Click();
            }
            else
            {
                MenuAudio.Error();
            }
            return false;
        }

        private static bool PromptPrefix(ElevatorControl __instance, ref string __result)
        {
            int target = Retarget(__instance);
            if (target == int.MinValue || target >= 0 || !_lift(__instance).IsUnlocked) return true;
            __result = target == MenuTarget ? "E • MENU" : "--";
            return false;
        }
    }
}
