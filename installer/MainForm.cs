using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using static OneMillionCoins5Mio.Installer.Texts;

namespace OneMillionCoins5Mio.Installer
{
    internal sealed class MainForm : Form
    {
        private static readonly Color Green = Color.FromArgb(0x1B, 0x7F, 0x3A);
        private static readonly Color Amber = Color.FromArgb(0x8A, 0x5A, 0x00);
        private static readonly Color Red = Color.FromArgb(0xC4, 0x2B, 0x1C);

        private readonly string saveFolder;
        private readonly float scale;
        private readonly TextBox folderBox = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, TabStop = false };
        private readonly TableLayoutPanel findings = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, BackColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle,
        };
        private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Maximum = 1000, Visible = false };
        private readonly Button browseButton, licenseButton, verifyButton, installButton, uninstallButton, closeButton;
        private SteamGame game;
        private Setup.Check check;
        private bool busy;

        internal MainForm(string folder, string saveFolder)
        {
            this.saveFolder = saveFolder;
            scale = DeviceDpi / 96f;
            Text = Title;
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = !Program.Snapshot;
            ClientSize = Scaled(780, 560);
            Padding = new Padding(Px(16));

            var title = new Label { Text = Title, AutoSize = true, Font = new Font(Font.FontFamily, Font.Size * 1.5f, FontStyle.Bold), Margin = new Padding(0) };
            var subtitle = new Label
            {
                Text = L($"Version {ReleaseInfo.ModVersion} · inoffizielle Fan-Mod · geprüft mit Spielversion {ReleaseInfo.TestedVersions}",
                    $"Version {ReleaseInfo.ModVersion} · unofficial fan mod · tested with game version {ReleaseInfo.TestedVersions}"),
                AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, Px(2), 0, Px(12)),
            };

            browseButton = MakeButton(L("Ändern …", "Change …"), Browse);
            var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, Px(8)) };
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            folderRow.Controls.Add(new Label { Text = L("Spielordner:", "Game folder:"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, Px(6), 0) }, 0, 0);
            folderBox.Margin = new Padding(0, Px(4), Px(6), 0);
            folderRow.Controls.Add(folderBox, 1, 0);
            folderRow.Controls.Add(browseButton, 2, 0);

            findings.Padding = new Padding(Px(8));
            findings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            findings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            progress.Height = Px(10);
            progress.Margin = new Padding(0, Px(6), 0, 0);

            licenseButton = MakeButton(L("Lizenzen", "Licenses"), ShowLicenses);
            verifyButton = MakeButton(L("Spieldateien prüfen", "Verify game files"), VerifyGameFiles);
            installButton = MakeButton(L("Installieren …", "Install …"), Install);
            uninstallButton = MakeButton(L("Deinstallieren …", "Uninstall …"), Uninstall);
            closeButton = MakeButton(L("Schließen", "Close"), (s, e) => Close());
            var left = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Left, WrapContents = false, Margin = Padding.Empty };
            left.Controls.AddRange(new Control[] { licenseButton, verifyButton });
            var right = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty };
            right.Controls.AddRange(new Control[] { closeButton, uninstallButton, installButton });
            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, Px(10), 0, 0) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttons.Controls.Add(left, 0, 0);
            buttons.Controls.Add(right, 1, 0);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
            foreach (var size in new[] { SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.Percent, SizeType.AutoSize, SizeType.AutoSize })
                layout.RowStyles.Add(size == SizeType.Percent ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(subtitle, 0, 1);
            layout.Controls.Add(folderRow, 0, 2);
            layout.Controls.Add(findings, 0, 3);
            layout.Controls.Add(progress, 0, 4);
            layout.Controls.Add(buttons, 0, 5);
            Controls.Add(layout);
            CancelButton = closeButton;

            game = folder != null ? SteamGame.ForFolder(folder) : SteamGame.Locate();
        }

        protected override bool ShowWithoutActivation => Program.Snapshot;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            PerformLayout();
            RefreshState(clear: true);
        }

        private int Px(int value) => (int)Math.Round(value * scale);

        private Size Scaled(int width, int height) => new Size(Px(width), Px(height));

        private Button MakeButton(string text, EventHandler click)
        {
            var button = new Button { Text = text, AutoSize = true, MinimumSize = Scaled(96, 28), Padding = new Padding(Px(6), 0, Px(6), 0), Margin = new Padding(Px(3)) };
            button.Click += click;
            return button;
        }

        // ---------------------------------------------------------------- Anzeige

        private void RefreshState(bool clear)
        {
            folderBox.Text = game?.Folder ?? "";
            check = Setup.Preflight(game, saveFolder);
            if (clear)
            {
                ClearFindings();
                foreach (var finding in check.Findings) AddFinding(finding.Level, finding.Text);
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            installButton.Text = check.Installed ? L("Neu installieren …", "Reinstall …") : L("Installieren …", "Install …");
            installButton.Enabled = !busy && check.CanInstall;
            uninstallButton.Enabled = !busy && check.CanUninstall;
            verifyButton.Enabled = !busy && game != null && SteamGame.IsGameFolder(game.Folder);
            browseButton.Enabled = licenseButton.Enabled = closeButton.Enabled = !busy;
        }

        private void ClearFindings()
        {
            findings.SuspendLayout();
            while (findings.Controls.Count > 0) findings.Controls[0].Dispose();
            findings.RowStyles.Clear();
            findings.RowCount = 0;
            findings.ResumeLayout();
        }

        private void AddFinding(Level level, string text) =>
            AddRow(level == Level.Ok ? "OK" : level == Level.Note ? L("Hinweis", "Note") : L("Fehler", "Error"),
                level == Level.Ok ? Green : level == Level.Note ? Amber : Red, text);

        private void AddLog(string text) => AddRow("·", SystemColors.GrayText, text);

        private void AddRow(string status, Color color, string text)
        {
            var bold = new Font(Font, FontStyle.Bold);
            int statusWidth = TextRenderer.MeasureText(L("Hinweis", "Note"), bold).Width + Px(4);
            int textWidth = findings.ClientSize.Width - statusWidth - findings.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - Px(16);
            var statusLabel = new Label
            {
                Text = status, ForeColor = color, Font = bold, AutoSize = true, MinimumSize = new Size(statusWidth, 0),
                Margin = new Padding(0, Px(2), Px(8), Px(6)),
            };
            var textLabel = new Label { Text = text, AutoSize = true, MaximumSize = new Size(Math.Max(Px(200), textWidth), 0), Margin = new Padding(0, Px(2), 0, Px(6)) };
            int row = findings.RowCount++;
            findings.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            findings.Controls.Add(statusLabel, 0, row);
            findings.Controls.Add(textLabel, 1, row);
            findings.ScrollControlIntoView(textLabel);
        }

        // ---------------------------------------------------------------- Aktionen

        private void Browse(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = L("Ordner von „One Million Coins“ wählen (dort liegt One Million Coins.exe).",
                    "Choose the \"One Million Coins\" folder (it contains One Million Coins.exe)."),
                ShowNewFolderButton = false,
                SelectedPath = game?.Folder ?? "",
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                game = SteamGame.ForFolder(dialog.SelectedPath);
                RefreshState(clear: true);
            }
        }

        private void Install(object sender, EventArgs e)
        {
            using (var dialog = DisclaimerDialog())
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
            // Nicht geprüfter Spielstand (meist: Steam hat das Spiel aktualisiert): Der Spieler entscheidet, ob die Mod darauf läuft.
            bool release = false;
            if (check.Untested)
            {
                var answer = MessageBox.Show(this, L(
                        $"Diese Spielversion ist nicht geprüft – geprüft ist die Mod mit {ReleaseInfo.TestedVersions}.\n\n" +
                        "Die Mod greift an Stellen im Spielcode ein, die sich mit einem Update geändert haben können. Möglich sind Fehler im Gebäude, " +
                        "ein Absturz oder ein unbrauchbarer Spielstand im 5-Mio.-Modus, im schlimmsten Fall auch Fehler im normalen Spiel.\n\n" +
                        "Soll die Mod auf dieser Version trotzdem laufen?\n\n" +
                        "Ja – installieren und einschalten (auf eigene Gefahr)\n" +
                        "Nein – installieren, aber ausgeschaltet lassen; einschalten geht später im Hauptmenü des Spiels unten links\n" +
                        "Abbrechen – nichts installieren",
                        $"This game version is not tested – the mod is tested with {ReleaseInfo.TestedVersions}.\n\n" +
                        "The mod hooks into places in the game's code that an update may have changed. Possible results are glitches in the building, " +
                        "a crash or an unusable save in 5 million mode, in the worst case also glitches in the normal game.\n\n" +
                        "Should the mod run on this version anyway?\n\n" +
                        "Yes – install and switch on (at your own risk)\n" +
                        "No – install, but leave it switched off; you can switch it on later in the game's main menu, bottom left\n" +
                        "Cancel – install nothing"),
                    Title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (answer == DialogResult.Cancel) return;
                release = answer == DialogResult.Yes;
            }
            string howTo = L("Im Hauptmenü des Spiels rechts neben „Speicherplatz“ die Auswahl „Tresor“ auf „5 Millionen · Mod (offline)“ stellen, dann „Neues Spiel“.",
                "In the game's main menu, switch \"Vault\" (next to the save slot) to \"5 million · mod (offline)\", then start a new game.");
            string done = !check.Untested ? L("Die Mod ist installiert. ", "The mod is installed. ") + howTo
                : release ? L("Die Mod ist installiert und für diese Spielversion eingeschaltet. ", "The mod is installed and switched on for this game version. ") + howTo
                : L("Die Mod ist installiert, aber ausgeschaltet, weil die Spielversion nicht geprüft ist. Einschalten: im Hauptmenü des Spiels unten links „Trotzdem einschalten …“, danach das Spiel neu starten.",
                    "The mod is installed but switched off, because the game version is not tested. To switch it on: \"Switch on anyway …\" at the bottom left of the game's main menu, then restart the game.");
            Run(L("Installation …", "Installing …"), setup =>
            {
                setup.Install(release);
                return new List<string>();
            }, verifyAfter: false, done: done);
        }

        private void Uninstall(object sender, EventArgs e)
        {
            bool deleteSaves = false;
            string intro = L("Die Mod wird entfernt und der Originalzustand des Spielordners wiederhergestellt.",
                "The mod will be removed and the original state of the game folder restored.");
            if (Directory.Exists(saveFolder))
            {
                var answer = MessageBox.Show(this, intro + "\n\n" + L(
                        "Sollen auch die Spielstände der Mod gelöscht werden?\n" + saveFolder +
                        "\n\nJa – Spielstände und Bestenliste der Mod löschen\nNein – behalten (für eine spätere Neuinstallation)\n\nDer normale Spielstand des Spiels ist davon nicht betroffen.",
                        "Should the mod's saves be deleted as well?\n" + saveFolder +
                        "\n\nYes – delete the mod's saves and leaderboard\nNo – keep them (for a later reinstall)\n\nThe game's normal save is not affected."),
                    Title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (answer == DialogResult.Cancel) return;
                deleteSaves = answer == DialogResult.Yes;
            }
            else if (MessageBox.Show(this, intro, Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            Run(L("Deinstallation …", "Uninstalling …"), setup => setup.Uninstall(deleteSaves), verifyAfter: true,
                done: L("Die Mod ist entfernt.", "The mod has been removed."));
        }

        private void VerifyGameFiles(object sender, EventArgs e) =>
            Run(L("Spielordner wird mit Steams Dateiliste verglichen …", "Comparing the game folder with Steam's file list …"),
                _ => new List<string>(), verifyAfter: true, done: null);

        private void ShowLicenses(object sender, EventArgs e)
        {
            using (var dialog = new TextDialog(L("Lizenzen", "Licenses"), Setup.ResourceText("Drittanbieter.txt"), null, L("Schließen", "Close")))
                dialog.ShowDialog(this);
        }

        private static TextDialog DisclaimerDialog() =>
            new TextDialog(L("Hinweis zur Mod", "About this mod"), string.Format(Disclaimer, ReleaseInfo.TestedVersions),
                L("Ich habe den Hinweis gelesen und bin einverstanden.", "I have read this notice and agree."), L("Installieren", "Install"));

        /// <summary>Arbeit im Hintergrund; Zeilen und Fortschritt kommen über BeginInvoke ins Fenster.</summary>
        private void Run(string title, Func<Setup, List<string>> work, bool verifyAfter, string done)
        {
            busy = true;
            UpdateButtons();
            ClearFindings();
            AddLog(title);
            progress.Value = 0;
            progress.Visible = verifyAfter;
            var target = game;
            Task.Run(() =>
            {
                Exception error = null;
                List<string> problems = null;
                List<Finding> verified = null;
                try
                {
                    var setup = new Setup(target.Folder, saveFolder, line => BeginInvoke((Action)(() => AddLog(line))));
                    problems = work(setup);
                    if (verifyAfter)
                        verified = Setup.VerifyFiles(target, (part, total) =>
                            BeginInvoke((Action)(() => progress.Value = (int)(1000 * part / Math.Max(1, total)))));
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                BeginInvoke((Action)(() => Finish(error, problems, verified, done)));
            });
        }

        private void Finish(Exception error, List<string> problems, List<Finding> verified, string done)
        {
            busy = false;
            progress.Visible = false;
            foreach (string problem in problems ?? new List<string>()) AddFinding(Level.Error, problem);
            if (verified != null)
                foreach (var finding in verified) AddFinding(finding.Level, finding.Text);
            bool ok = error == null && (problems == null || problems.Count == 0);
            if (error is UnauthorizedAccessException)
                AddFinding(Level.Error, error.Message + " " + L("Keine Schreibrechte im Spielordner: den Installer mit Rechtsklick → „Als Administrator ausführen“ starten.",
                    "No write access to the game folder: start the installer with right-click → \"Run as administrator\"."));
            else if (error != null)
                AddFinding(Level.Error, error is SetupException || error is IOException ? error.Message : error.ToString());
            else if (ok && done != null)
                AddFinding(Level.Ok, done);
            RefreshState(clear: false);
            if (done == null) return;
            MessageBox.Show(this, ok ? done : L("Das hat nicht geklappt. Die Einzelheiten stehen im Fenster.", "That did not work. The details are shown in the window."),
                Title, MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        // ---------------------------------------------------------------- Bildschirmfotos zum Prüfen des Layouts

        internal static void SaveSnapshots(string outFolder, string folder, string saves)
        {
            Directory.CreateDirectory(outFolder);
            string language = German ? "de" : "en";
            using (var form = new MainForm(folder, saves))
                CaptureForm(form, Path.Combine(outFolder, "hauptfenster_" + language + ".png"));
            using (var dialog = DisclaimerDialog())
                CaptureForm(dialog, Path.Combine(outFolder, "hinweis_" + language + ".png"));
        }

        private static void CaptureForm(Form form, string file)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            form.Show();
            Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(file, ImageFormat.Png);
            }
            form.Close();
        }
    }

    /// <summary>Langer Text zum Lesen; mit Zustimmungs-Haken (Hinweis) oder ohne (Lizenzen).</summary>
    internal sealed class TextDialog : Form
    {
        internal TextDialog(string title, string text, string agreement, string accept)
        {
            float scale = DeviceDpi / 96f;
            int Px(int value) => (int)Math.Round(value * scale);
            Text = title;
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(Px(680), Px(640));
            Padding = new Padding(Px(12));

            var box = new TextBox
            {
                Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, TabStop = false,
                BackColor = SystemColors.Window, Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n"),
            };
            var acceptButton = new Button { Text = accept, DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(Px(110), Px(28)), Enabled = agreement == null };
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, Px(8), 0, 0) };
            CheckBox agree = null;
            if (agreement != null)
            {
                agree = new CheckBox { Text = agreement, AutoSize = true, Margin = new Padding(0, Px(10), 0, 0) };
                agree.CheckedChanged += (s, e) => acceptButton.Enabled = agree.Checked;
                var cancel = new Button { Text = L("Abbrechen", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(Px(110), Px(28)) };
                buttons.Controls.Add(cancel);
                CancelButton = cancel;
            }
            else
            {
                CancelButton = acceptButton;
            }
            buttons.Controls.Add(acceptButton);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(box, 0, 0);
            if (agree != null) layout.Controls.Add(agree, 0, 1);
            layout.Controls.Add(buttons, 0, 2);
            Controls.Add(layout);
        }

        protected override bool ShowWithoutActivation => Program.Snapshot;
    }
}
