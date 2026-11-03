using GoldDigger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Hauptmenü: Auswahl „Tresor“ neben dem Spielstand-Platz (Bl4ck969, 26.10.2026: „5 Millionen als Auswahl bei Neues
    /// Spiel“). Bei „5 Millionen“ richten sich „Weiterspielen“ und „Neues Spiel“ des Spiels nach dem Mod-Spielstand.
    /// </summary>
    internal static class MenuPatches
    {
        internal static bool ModSelected => Plugin.Vault.Value == "5Mio";

        private static bool _logged;
        private static bool _inConfirmation;

        internal static void Apply(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(MainMenuUI), "OnGUI"), postfix: new HarmonyMethod(typeof(MenuPatches), nameof(DrawSelector)));
            harmony.Patch(AccessTools.Method(typeof(MainMenuUI), "Play"), prefix: new HarmonyMethod(typeof(MenuPatches), nameof(PlayPrefix)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(SaveManager), "HasSavedGame"), postfix: new HarmonyMethod(typeof(MenuPatches), nameof(HasSavedGamePostfix)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(SaveManager), "SavedRulesetCaption"), postfix: new HarmonyMethod(typeof(MenuPatches), nameof(CaptionPostfix)));
            harmony.Patch(AccessTools.Method(typeof(MainMenuUI), "DrawConfirmation"),
                prefix: new HarmonyMethod(typeof(MenuPatches), nameof(ConfirmationPrefix)), postfix: new HarmonyMethod(typeof(MenuPatches), nameof(ConfirmationPostfix)));
            harmony.Patch(AccessTools.Method(typeof(SaveSlots), "Label"), prefix: new HarmonyMethod(typeof(MenuPatches), nameof(SlotLabelPrefix)));
            // Prüfmodus „bilder“: Aufnahmen fürs README ohne die Steam-Karte (Name, Bild, Bestzeiten) rechts im Menü.
            if (PictureRun.Requested)
                harmony.Patch(AccessTools.Method(typeof(MainMenuUI), "DrawPlayerRecords"), prefix: new HarmonyMethod(typeof(MenuPatches), nameof(SkipPrefix)));
        }

        /// <summary>Nur im Hauptmenü und solange kein Mod-Lauf aktiv ist, sonst gilt überall das Spiel.</summary>
        private static bool InMenu => !ModState.Active && SceneManager.GetActiveScene().path == GameNavigation.MenuScene;

        /// <summary>
        /// Postfix auf MainMenuUI.OnGUI: Tresor-Auswahl rechts neben „Spielstand-Platz“, gleiche Skalierung und Knopfart
        /// wie das Menü (Maße aus MainMenuUI.Draw). Im Prüfmodus startet hier der automatische Lauf.
        /// </summary>
        private static void DrawSelector(MainMenuUI __instance)
        {
            if (GameNavigation.IsLoading || GameNavigation.SelectedLevel == null || GameNavigation.SelectedLevel.id != LevelCatalog.VaultId) return;
            var menu = Traverse.Create(__instance);
            if (menu.Field<bool>("_confirmNewGame").Value || menu.Field<bool>("_settingsOpen").Value || menu.Field<bool>("_creditsOpen").Value
                || menu.Field<bool>("_leaderboardsOpen").Value || menu.Field<bool>("_rulesetDropdownOpen").Value
                || menu.Field<bool>("_slotPickerOpen").Value || menu.Field<int>("_renameSlot").Value != 0)
            {
                return;
            }
            if (!_logged)
            {
                _logged = true;
                Plugin.Log.LogInfo($"Tresor-Auswahl im Hauptmenü aktiv ({(ModSelected ? "5 Millionen" : "Original")}).");
                if ((Diagnostics.Requested || LoadTest.Requested || SaveTest.Requested || SaveTest.LoadRequested || SkillTest.Requested || PictureRun.Requested)
                    && ModState.OfflineGuardsReady)
                {
                    Plugin.Log.LogInfo($"Prüfmodus \"{Plugin.CheckMode.Value}\": starte automatisch.");
                    // „bilder“ nimmt zuerst das Menü auf und startet den Lauf selbst, „spielstand-laden“ über „Weiter“.
                    if (PictureRun.Requested) PictureRun.Begin();
                    else if (SaveTest.LoadRequested) SaveTest.BeginLoad();
                    else Plugin.Launch();
                    return;
                }
            }

            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            float height = Screen.height / scale;
            float top = Mathf.Max(18f, (height - 720f) * 0.5f);
            float x = 86f + 356f + 16f;
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            LocalizedGUI.Label(new Rect(x, top + 244f, 330f, 18f), ModText.Of("VAULT", "TRESOR"), VaultUI.Theme.Caption);
            if (!ModState.OfflineGuardsReady)
            {
                LocalizedGUI.Label(new Rect(x, top + 265f, 330f, 32f),
                    ModText.Of("5 million: offline locks missing (see log)", "5 Mio.: Offline-Sperren fehlen (Log prüfen)"), VaultUI.Theme.Caption);
            }
            else if (VaultUI.ActionButton(new Rect(x, top + 265f, 330f, 32f),
                ModSelected ? ModText.Of("5 million · mod (offline)   ⇄", "5 Millionen · Mod (offline)   ⇄")
                    : ModText.Of("1 million · original   ⇄", "1 Million · Original   ⇄"), VaultUI.Theme.CompactPrimary))
            {
                Plugin.Vault.Value = ModSelected ? "Original" : "5Mio";
                Plugin.Log.LogInfo($"Tresor gewählt: {(ModSelected ? "5 Millionen" : "Original")}.");
            }
            if (ModSelected)
                LocalizedGUI.Label(new Rect(x, top + 304f, 330f, 40f),
                    ModText.Of("Separate save, nothing goes online.", "Eigener Spielstand, nichts geht online."), VaultUI.Theme.Caption);
            GUI.matrix = matrix;
        }

        /// <summary>„Weiterspielen“ und „Neues Spiel“ starten bei „5 Millionen“ den Mod-Lauf.</summary>
        private static bool PlayPrefix()
        {
            if (!ModSelected) return true;
            if (!ModState.OfflineGuardsReady)
            {
                Plugin.Log.LogWarning("5 Mio.: Offline-Sperren fehlen, Start abgelehnt.");
                return false;
            }
            ModState.Launching = true;
            return true;
        }

        private static void HasSavedGamePostfix(ref bool __result)
        {
            if (ModSelected && InMenu) __result = OfflinePatches.HasModSave;
        }

        private static void CaptionPostfix(ref string __result)
        {
            if (ModSelected && InMenu) __result = (OfflinePatches.Testing && !PictureRun.Requested ? " · Test" : "") + " · 5 Mio.";
        }

        private static bool SkipPrefix() => false;

        private static void ConfirmationPrefix() => _inConfirmation = true;

        private static void ConfirmationPostfix() => _inConfirmation = false;

        /// <summary>Im Bestätigungsdialog „Neues Spiel“ nennt das Spiel den Spielstand-Platz; bei 5 Millionen ist es der Mod-Spielstand.</summary>
        private static bool SlotLabelPrefix(ref string __result)
        {
            if (!_inConfirmation || !ModSelected || !InMenu) return true;
            __result = ModText.Of("5 million save", "5-Mio.-Spielstand");
            return false;
        }
    }
}
