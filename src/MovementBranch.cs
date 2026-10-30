using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using GoldDigger;
using HarmonyLib;
using UnityEngine;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Ast „Bewegung“ im 5-Mio.-Modus (Bl4ck969, 29.10.2026): schneller laufen, höher springen, Schlussstein Jetpack mit
    /// unendlich Treibstoff (Sprungtaste in der Luft halten), unten als Feld neben den Werkzeugen. Lauftempo und Sprunghöhe
    /// kennt das Spiel schon als Boni (SkillBonuses.Movement/Jump), der Baum v1.2 nutzt sie aber nirgends.
    /// Der Ast hat die Nummer 7 und steht zwischen „Zusatz“ und „Rutschen“. Das HUD nimmt an vielen Stellen 7 Äste an;
    /// diese Stellen werden hier erweitert. Im Original gibt es den Ast nicht: Katalog, Baum und Reihenfolgen wechseln
    /// mit dem Modus (<see cref="Sync"/>).
    /// </summary>
    internal static class MovementBranch
    {
        internal const int Branch = 7, Root = 1700, Jetpack = 1712;
        // Eine Spur wie die Rutschen, deshalb ebenso schmal.
        private const float Width = 280f;
        // Jetpack: steigt mit höchstens 8 m/s, Schub 30 m/s² über der Schwerkraft (24 m/s² wie im FirstPersonController).
        private const float Rise = 8f, Thrust = 30f, Gravity = 24f;
        // Icon der Turbine aus dem Rutschen-Ast.
        private const int JetpackIcon = 1608;

        private static readonly int[] Order = { 0, 4, 5, 1, 2, 3, Branch, 6 };

        /// <summary>Wahr, solange die Bewegungs-Skills im Katalog stehen (nur im 5-Mio.-Modus).</summary>
        internal static bool Injected;
        /// <summary>Wahr in dem Bild, in dem der Jetpack schiebt (für das Feld unten).</summary>
        private static bool _thrusting;
        /// <summary>Nur Prüfmodus „fertigkeiten“: Sprungtaste gilt als gehalten.</summary>
        internal static bool HoldJumpForTest;
        private static SkillDefinition[] _skills, _all, _route;
        private static Texture2D _jetpackIcon;
        private static bool _jetpackIconLoaded;

        private static AccessTools.FieldRef<GoldDiggerHUD, Rect[]> _treeColumns;
        private static AccessTools.FieldRef<GoldDiggerHUD, GameProgressManager> _hudProgress;
        private static AccessTools.FieldRef<FirstPersonController, CharacterController> _controller;
        private static AccessTools.FieldRef<FirstPersonController, float> _verticalVelocity;
        private static AccessTools.FieldRef<FirstPersonController, GoldDiggerHUD> _playerHud;

        private const string MoveText = "Movement speed +{0}% of base.", JumpText = "Jump height +{0}% of base.";
        private const string JetpackText = "Hold {Jump} in mid-air to fly upward. Release to fall. Unlimited fuel.";

        // Grundknoten und 12 Ränge: zusammen Lauftempo +100 % (6 -> 12 m/s) und Sprunghöhe +100 % (1,65 -> 3,3 m).
        // Kosten nach dem Muster der Rutschen-Spur, Icons aus dem Spiel.
        private static readonly (int Id, string Name, string German, int Move, int Jump, int Cost, int Icon)[] Steps =
        {
            (Root, "Light feet", "Leichtfüßig", 10, 0, 1, 1308),
            (1701, "Brisk pace", "Zügiger Schritt", 15, 0, 1, 13015),
            (1702, "Spring step", "Federschritt", 0, 20, 2, 13215),
            (1703, "Long stride", "Langer Schritt", 15, 0, 1, 1327),
            (1704, "Coiled legs", "Sprungkraft", 0, 20, 3, 30412),
            (1705, "Runner", "Läufer", 15, 0, 2, 1207),
            (1706, "High jump", "Hochsprung", 0, 20, 2, 1022),
            (1707, "Fleet foot", "Flinker Fuß", 15, 0, 3, 1622),
            (1708, "Leap", "Weiter Satz", 0, 20, 1, 1227),
            (1709, "Sprinter", "Sprinter", 15, 0, 3, 1128),
            (1710, "Moon jump", "Mondsprung", 0, 20, 1, 1311),
            (1711, "Tailwind", "Rückenwind", 15, 0, 2, 1624),
            (Jetpack, "Jetpack", "Jetpack", 0, 0, 3, JetpackIcon),
        };

        /// <summary>Deutsche Zeilen für den Übersetzungskatalog (Englisch wie im Mod, Deutsch im Stil der Spielübersetzung).</summary>
        internal static IEnumerable<(string English, string German)> German()
        {
            yield return ("Movement", "Bewegung");
            yield return (JetpackText, "Halte {Jump} in der Luft gedrückt, um aufzusteigen. Loslassen zum Fallen. Unbegrenzter Treibstoff.");
            foreach (var step in Steps)
            {
                yield return (step.Name, step.German);
                if (step.Move > 0) yield return (string.Format(MoveText, step.Move), $"Lauftempo +{step.Move}% des Grundwerts.");
                if (step.Jump > 0) yield return (string.Format(JumpText, step.Jump), $"Sprunghöhe +{step.Jump}% des Grundwerts.");
            }
        }

        internal static void Apply(Harmony harmony)
        {
            try
            {
                Build();
                var self = typeof(MovementBranch);
                var hud = typeof(GoldDiggerHUD);
                _treeColumns = AccessTools.FieldRefAccess<GoldDiggerHUD, Rect[]>("_treeColumns");
                _hudProgress = AccessTools.FieldRefAccess<GoldDiggerHUD, GameProgressManager>("_progress");
                _controller = AccessTools.FieldRefAccess<FirstPersonController, CharacterController>("_controller");
                _verticalVelocity = AccessTools.FieldRefAccess<FirstPersonController, float>("_verticalVelocity");
                _playerHud = AccessTools.FieldRefAccess<FirstPersonController, GoldDiggerHUD>("_hud");

                // Katalog und Baum.
                harmony.Patch(AccessTools.PropertyGetter(typeof(SkillTreeDefinition), nameof(SkillTreeDefinition.All)),
                    postfix: new HarmonyMethod(self, nameof(AllPostfix)));
                harmony.Patch(AccessTools.Method(typeof(SkillCatalog), nameof(SkillCatalog.DevelopmentRoute)),
                    postfix: new HarmonyMethod(self, nameof(RoutePostfix)));
                harmony.Patch(AccessTools.Method(typeof(SkillCatalog), nameof(SkillCatalog.PointsAt)),
                    postfix: new HarmonyMethod(self, nameof(PointsPostfix)));
                var map = typeof(SkillTreeMap);
                harmony.Patch(AccessTools.Constructor(map, new[] { typeof(SkillTreeDefinition) }),
                    transpiler: new HarmonyMethod(self, nameof(MapOrderTranspiler)));
                harmony.Patch(AccessTools.Method(map, nameof(SkillTreeMap.BranchWidth)), postfix: new HarmonyMethod(self, nameof(WidthPostfix)));
                harmony.Patch(AccessTools.PropertyGetter(map, nameof(SkillTreeMap.TotalWidth)), postfix: new HarmonyMethod(self, nameof(TotalWidthPostfix)));

                // HUD: Reihenfolge der Spalten, Spaltenpuffer, Titel und Farbe je Ast. Die Karten-Ansicht des HUD
                // (DrawProficiencyBrowser) ruft das Spiel in v1.2 nicht mehr auf; sie bleibt unverändert.
                ExtendHudArrays();
                // TreeColumnRect in einem Aufruf: Harmony wiederholt bei jedem weiteren Patch einer Methode ihre Transpiler.
                harmony.Patch(AccessTools.Method(hud, "TreeColumnRect"), prefix: new HarmonyMethod(self, nameof(ColumnsPrefix)),
                    transpiler: new HarmonyMethod(self, nameof(LayoutOrderTranspiler)));
                foreach (var name in new[] { "DrawTreeColumns", "DrawTreeColumnHeaders", "TreeHeaderAt", "DrawTreeRootConnections" })
                    harmony.Patch(AccessTools.Method(hud, name), transpiler: new HarmonyMethod(self, nameof(LayoutOrderTranspiler)));

                // Jetpack und sein Feld unten.
                harmony.Patch(AccessTools.Method(typeof(FirstPersonController), "Update"), prefix: new HarmonyMethod(self, nameof(JetpackPrefix)));
                harmony.Patch(AccessTools.Method(hud, "DrawOverdriveShortcut"), postfix: new HarmonyMethod(self, nameof(JetpackSlotPostfix)));

                Plugin.Log.LogInfo($"Ast Bewegung: {_skills.Length} Fertigkeiten; HUD-Stellen Reihenfolge {_layoutSites} (Soll 6), Baum {_mapSites} (Soll 1).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Ast Bewegung nicht eingebaut: " + e);
            }
        }

        private static void Build()
        {
            var list = new List<SkillDefinition>();
            for (int tier = 0; tier < Steps.Length; tier++)
            {
                var step = Steps[tier];
                var bonuses = new SkillBonuses { Movement = step.Move / 100f, Jump = step.Jump / 100f };
                string text = step.Id == Jetpack ? JetpackText
                    : step.Move > 0 ? string.Format(MoveText, step.Move) : string.Format(JumpText, step.Jump);
                SkillId? parent = tier == 0 ? (SkillId?)null : (SkillId)Steps[tier - 1].Id;
                list.Add(new SkillDefinition((SkillId)step.Id, step.Name, text, step.Cost, Branch, tier, 0, ItemId.Hands, parent,
                    bonuses: bonuses, keystone: step.Id == Jetpack, sourceId: (SkillId)step.Icon, plainDescription: true));
            }
            _skills = list.ToArray();
            _all = SkillCatalog.All.Concat(_skills).ToArray();
            _route = _skills.Where(s => s.Tier > 0).OrderBy(s => s.Tier).ToArray();
        }

        /// <summary>Bewegungs-Skills in den Katalog (Mod) bzw. heraus (Original); gecachte Baumkarten verwerfen.</summary>
        internal static void Sync(bool active)
        {
            if (_skills == null || Injected == active) return;
            var byId = Traverse.Create(typeof(SkillCatalog)).Field<Dictionary<SkillId, SkillDefinition>>("ById").Value;
            foreach (var skill in _skills)
            {
                if (active) byId[skill.Id] = skill;
                else byId.Remove(skill.Id);
            }
            Injected = active;
            Traverse.Create(typeof(SkillTreeLayout)).Field<Dictionary<SkillTreeDefinition, SkillTreeMap>>("Maps").Value?.Clear();
        }

        private static void AllPostfix(ref SkillDefinition[] __result)
        {
            SkillPatches.Sync();
            if (Injected && ReferenceEquals(__result, SkillCatalog.All)) __result = _all;
        }

        private static void RoutePostfix(int branch, int track, ref SkillDefinition[] __result)
        {
            if (Injected && branch == Branch && track == 1) __result = _route;
        }

        /// <summary>Punkte, die der volle Baum im 5-Mio.-Modus kostet: alle Skills des Spiels plus Ast Bewegung.</summary>
        internal static int FullTreeCost => SkillCatalog.TotalPointCost + Steps.Sum(s => s.Cost);

        /// <summary>
        /// Das Spiel vergibt ab 900.000 Münzen je 25.704 einen Punkt, gedeckelt bei den Kosten seiner eigenen Skills (409).
        /// Im 5-Mio.-Modus endet die Vergabe beim vollen Baum mit Ast Bewegung (Bl4ck969, 29.10.2026: so viele Punkte, wie
        /// man erlernen kann). Punkte bis 900.000 Münzen bleiben wie im Spiel.
        /// </summary>
        private static void PointsPostfix(long collected, ref int remainder, ref int required, ref int __result)
        {
            if (!Injected || collected < 900000) return;
            int total = FullTreeCost;
            long beyond = collected - 900000;
            __result = (int)Math.Min(total, 125 + beyond / 25704);
            remainder = __result < total ? (int)(beyond % 25704) : 0;
            required = 25704;
        }

        // ---- Baumkarte ----

        private static int _mapSites, _layoutSites;

        /// <summary>SkillTreeMap legt seine Reihenfolge im Konstruktor fest, bevor es die Lagen rechnet: dort einsetzen.</summary>
        private static IEnumerable<CodeInstruction> MapOrderTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var field = AccessTools.Field(typeof(SkillTreeMap), nameof(SkillTreeMap.BranchOrder));
            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(field))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MovementBranch), nameof(MapOrder)));
                    _mapSites++;
                }
                yield return instruction;
            }
        }

        private static int[] MapOrder(int[] order, SkillTreeDefinition tree)
        {
            return Injected && tree != null && Array.Exists(tree.All, s => s.Branch == Branch) ? Order : order;
        }

        private static void WidthPostfix(int branch, ref float __result)
        {
            if (branch == Branch) __result = Width;
        }

        /// <summary>Das Spiel rechnet 480 je Ast plus die schmalen Rutschen; mit einem zweiten schmalen Ast die echte Summe.</summary>
        private static void TotalWidthPostfix(SkillTreeMap __instance, ref float __result)
        {
            if (Array.IndexOf(__instance.BranchOrder, Branch) < 0) return;
            float sum = 0f;
            foreach (int branch in __instance.BranchOrder) sum += __instance.BranchWidth(branch);
            __result = sum;
        }

        // ---- HUD ----

        /// <summary>Die Spalten des HUD folgen der Reihenfolge der Baumkarte statt der festen des Spiels.</summary>
        private static IEnumerable<CodeInstruction> LayoutOrderTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var field = AccessTools.Field(typeof(SkillTreeLayout), nameof(SkillTreeLayout.BranchOrder));
            var call = AccessTools.Method(typeof(MovementBranch), nameof(LayoutOrder));
            foreach (var instruction in instructions)
            {
                if (instruction.LoadsField(field))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = call;
                    _layoutSites++;
                }
                yield return instruction;
            }
        }

        private static int[] LayoutOrder() => Injected ? SkillTreeLayout.For(LevelCatalog.Vault.skillTree).BranchOrder : SkillTreeLayout.BranchOrder;

        private static void ColumnsPrefix(GoldDiggerHUD __instance)
        {
            ref var columns = ref _treeColumns(__instance);
            if (columns.Length <= Branch) columns = new Rect[Branch + 1];
        }

        /// <summary>
        /// Farbe und Titel je Ast sind Felder mit 7 Einträgen. Sie bekommen beim Laden des Plugins einen 8. – vor dem
        /// ersten Übersetzen der HUD-Methoden, weil Unitys Mono „static readonly“ dabei als Konstante einsetzt.
        /// Im Original fragt niemand nach Ast 7.
        /// </summary>
        private static void ExtendHudArrays()
        {
            RuntimeHelpers.RunClassConstructor(typeof(GoldDiggerHUD).TypeHandle);
            Extend("BrowserAccents", new Color(0.93f, 0.6f, 0.74f));
            Extend("BrowserNames", "Movement");
        }

        private static void Extend<T>(string field, T value)
        {
            ref var array = ref AccessTools.StaticFieldRefAccess<GoldDiggerHUD, T[]>(field);
            if (array.Length > Branch) return;
            var wider = new T[Branch + 1];
            Array.Copy(array, wider, array.Length);
            wider[Branch] = value;
            array = wider;
        }

        // ---- Jetpack ----

        /// <summary>
        /// Vor FirstPersonController.Update: in der Luft mit gehaltener Sprungtaste nach oben schieben. Update zieht danach
        /// die Schwerkraft dieses Bildes ab; sie wird hier vorweg ausgeglichen. Wer schneller steigt als 8 m/s (frischer
        /// Sprung), bremst normal ab.
        /// </summary>
        private static void JetpackPrefix(FirstPersonController __instance)
        {
            _thrusting = false;
            if (!ModState.Active || !Injected || !PresentationSettings.WindowHasFocus) return;
            var progress = GameProgressManager.Instance;
            if (progress == null || !progress.HasSkill((SkillId)Jetpack)) return;
            var controller = _controller(__instance);
            if (controller == null || !controller.enabled || controller.isGrounded || __instance.ToolSlamActive) return;
            var hud = _playerHud(__instance);
            if (hud != null && hud.BlocksGameplay) return;
            if (!HoldJumpForTest && !GameInput.Held("Jump")) return;
            _thrusting = true;
            ref float velocity = ref _verticalVelocity(__instance);
            if (velocity >= Rise) return;
            float dt = Time.deltaTime;
            velocity = Mathf.Min(velocity + Thrust * dt, Rise) + Gravity * dt;
        }

        /// <summary>Feld „Jetpack + Sprungtaste“ unten rechts neben den Werkzeugen und dem Rutschen-Overdrive [R].</summary>
        private static void JetpackSlotPostfix(GoldDiggerHUD __instance, Rect dock, float y, int visibleSlots)
        {
            if (!ModState.Active || !Injected) return;
            var progress = _hudProgress(__instance);
            if (progress == null || !progress.HasSkill((SkillId)Jetpack)) return;
            var rect = new Rect(24f + visibleSlots * 52f, y, 84f, 36f);
            if (progress.ChuteOverdriveUnlocked)
            {
                // Lage des Overdrive-Felds wie im Spiel (bei Platzmangel eine Zeile höher, links).
                var overdrive = new Rect(rect.x, y, 52f, 36f);
                if (overdrive.xMax + 12f > dock.x) overdrive = new Rect(24f, y - 40f, 52f, 36f);
                rect.x = overdrive.xMax;
                rect.y = overdrive.y;
            }
            if (rect.xMax + 12f > dock.x) rect = new Rect(24f, y - 40f, 84f, 36f);
            VaultUI.Theme.HudPlate(rect, _thrusting);
            if (!_jetpackIconLoaded)
            {
                _jetpackIcon = Resources.Load<Texture2D>(VaultRulesets.IconRoot(progress.VaultRulesetId) + JetpackIcon);
                _jetpackIconLoaded = true;
            }
            if (_jetpackIcon != null) GUI.DrawTexture(new Rect(rect.x + 5f, rect.y + 5f, 25f, 25f), _jetpackIcon, ScaleMode.ScaleToFit, alphaBlend: true);
            InputPrompts.Draw(new Rect(rect.x + 33f, rect.y + 5f, 46f, 25f), "jump");
            if (_thrusting) VaultUI.Fill(new Rect(rect.x + 7f, rect.yMax - 3f, rect.width - 14f, 2f), VaultUI.Gold, 1f);
        }
    }
}
