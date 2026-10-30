namespace OneMillionCoins5Mio
{
    /// <summary>Ob der 5-Mio.-Modus in der laufenden Szene aktiv ist.</summary>
    internal static class ModState
    {
        /// <summary>True ab dem Start aus dem Mod-Knopf bis zur Rückkehr ins Hauptmenü.</summary>
        internal static bool Active;

        /// <summary>Gesetzt beim Klick auf den Mod-Knopf, verbraucht im nächsten GameNavigation.Play.</summary>
        internal static bool Launching;

        /// <summary>False, wenn eine Offline-Sperre nicht gesetzt werden konnte. Dann bleibt der Mod-Knopf aus.</summary>
        internal static bool OfflineGuardsReady = true;
    }
}
