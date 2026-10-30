using System.Globalization;

namespace OneMillionCoins5Mio.Installer
{
    /// <summary>Deutsch bei deutscher Windows-Sprache, sonst Englisch.</summary>
    internal static class Texts
    {
        internal static bool German = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

        internal static string L(string german, string english) => German ? german : english;

        internal static string Title => L("One Million Coins – 5 Mio. Münzen (Mod)", "One Million Coins – 5 Million Coins (mod)");

        internal static string Disclaimer => German ? DisclaimerDe : DisclaimerEn;

        private const string DisclaimerDe =
@"Bitte vor der Installation lesen

• Inoffizielle Mod: „5 Mio. Münzen“ ist eine Fan-Mod für „One Million Coins“. Sie stammt nicht vom Entwickler des Spiels, wird von ihm nicht unterstützt und wurde von ihm nicht geprüft. Fragen und Fehler zur Mod bitte nicht an den Entwickler des Spiels richten.

• Mit KI geschrieben: Der Code dieser Mod und dieses Installers wurde mit Hilfe künstlicher Intelligenz (Claude von Anthropic) geschrieben. Er wurde ausprobiert und mit automatischen Prüfläufen getestet, kann aber Fehler enthalten.

• Eingriff in das Spiel: Der Installer legt den Mod-Lader BepInEx 5.4.23.6 und die Mod in den Spielordner. Die Mod verändert das Spiel bei jedem Start im Arbeitsspeicher – Gebäude, Münzzahl, Fertigkeitsbaum, Aufzug, Hauptmenü. Die Spieldateien selbst bleiben unverändert. Müsste der Installer eine vorhandene Datei überschreiben, sichert er sie vorher.

• Offline-Modus: Der Modus „5 Millionen“ sendet nichts an Steam – keine Bestenlisten, keine Erfolge, keine Steam Cloud. Er hat einen eigenen Spielstand unter %LOCALAPPDATA%\OneMillionCoins-5Mio. Der normale Spielstand wird nicht angefasst, das normale Spiel bleibt unverändert.

• Spielversion: Die Mod ist nur mit Version {0} geprüft. Aktualisiert Steam das Spiel, schaltet sie sich ab, bis es eine passende Fassung der Mod gibt; das Spiel läuft dann unverändert weiter.

• Nutzung auf eigene Gefahr: Soweit gesetzlich zulässig, wird keine Haftung für Schäden übernommen, etwa für verlorene Spielstände oder Folgen für dein Steam-Konto.

• Rückbau: „Deinstallieren“ in diesem Programm entfernt alles wieder und stellt den Originalzustand des Spielordners her. Danach fragt es, ob auch die Spielstände der Mod gelöscht werden sollen.";

        private const string DisclaimerEn =
@"Please read before installing

• Unofficial mod: ""5 Million Coins"" is a fan mod for ""One Million Coins"". It is not made, supported or reviewed by the game's developer. Please do not contact the game's developer about this mod.

• Written with AI: The code of this mod and of this installer was written with the help of artificial intelligence (Claude by Anthropic). It was played and tested with automated test runs, but it may contain bugs.

• Changes to the game: The installer puts the mod loader BepInEx 5.4.23.6 and the mod into the game folder. The mod changes the game in memory every time it starts – building, number of coins, skill tree, elevator, main menu. The game's files themselves stay unchanged. If the installer had to overwrite an existing file, it backs it up first.

• Offline mode: The ""5 million"" mode sends nothing to Steam – no leaderboards, no achievements, no Steam Cloud. It keeps its own save under %LOCALAPPDATA%\OneMillionCoins-5Mio. Your normal save is not touched, and the normal game stays unchanged.

• Game version: The mod is only tested with version {0}. When Steam updates the game, the mod switches itself off until a matching version of the mod is available; the game keeps running unmodified.

• Use at your own risk: To the extent permitted by law, no liability is accepted for any damage, such as lost saves or consequences for your Steam account.

• Removal: ""Uninstall"" in this program removes everything and restores the original state of the game folder. It then asks whether the mod's saves should be deleted as well.";
    }
}
