using GoldDigger;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Texte der Mod nach der Spielsprache: Deutsch, sonst Englisch (wie der Sperrgrund im Aufzugsmenü). Nicht für den
    /// Versionshinweis – der läuft auf ungeprüften Spielständen und ruft deshalb keinen Spielcode auf.
    /// </summary>
    internal static class ModText
    {
        /// <summary>Nur für Prüfläufe: Mod-Texte auf Englisch, ohne die Spracheinstellung des Spielers anzufassen.</summary>
        internal static bool ForceEnglish;

        internal static bool German => !ForceEnglish && GameLocalization.IsGerman;

        internal static string Of(string english, string german) => German ? german : english;
    }
}
