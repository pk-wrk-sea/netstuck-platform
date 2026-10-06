using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NetStuck
{
    internal sealed class ReleaseAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public long size { get; set; }
    }
    internal sealed class ReleaseInfo
    {
        public string tag_name { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public string body { get; set; }
        public List<ReleaseAsset> assets { get; set; }
    }
    internal sealed class ReleasePackage
    {
        public ReleaseInfo Release { get; set; }
        public Version Version { get; set; }
        public override string ToString()
        {
            return "v" + Version.ToString(3) + (Release.tag_name == "v" + Version.ToString(3) ? "" : " (archive on " + Release.tag_name + ")");
        }
    }
    internal sealed class UpdateJob
    {
        public int ProcessId { get; set; }
        public long ProcessStartedUtcTicks { get; set; }
        public string Target { get; set; }
        public string Payload { get; set; }
        public string Version { get; set; }
        public bool AllowDowngrade { get; set; }
        public string ExpectedInstalledVersion { get; set; }
    }
    internal sealed class UpdateRecovery
    {
        public string Target { get; set; }
        public List<string> Existed { get; set; }
    }

    internal static class UpdateEngine
    {
        public const string Repository = "pk-wrk-sea/netstuck-platform";
        public const string LatestUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";
        public const string VersionsUrl = "https://api.github.com/repos/" + Repository + "/releases?per_page=100";
        public static readonly string[] PackageFiles = { "CHANGELOG.md", "NetStuck-Icon.png", "NetStuck.exe", "README-TH.md", "README.md", "TEST-REPORT.txt", "tools/plink.exe", "tools/PuTTY-LICENCE.txt", "SHA256SUMS.txt" };

        public static Version ParseVersion(string text)
        {
            if (text == null || !Regex.IsMatch(text, @"^v?\d+\.\d+\.\d+$")) throw new InvalidDataException("Invalid stable release version.");
            return new Version(text.TrimStart('v') + ".0");
        }
        public static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var file = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }
        public static ReleaseAsset Asset(ReleaseInfo release, string name)
        {
            var matches = (release.assets ?? new List<ReleaseAsset>()).Where(a => a.name == name).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Release is missing a unique " + name + ".");
            var asset = matches[0];
            string expected = "https://github.com/" + Repository + "/releases/download/" + release.tag_name + "/" + name;
            if (asset.browser_download_url != expected || asset.size <= 0 || asset.size > 100 * 1024 * 1024)
                throw new InvalidDataException("Unexpected release asset URL or size.");
            return asset;
        }
        public static ReleaseInfo ParseRelease(string json)
        {
            var release = new JavaScriptSerializer().Deserialize<ReleaseInfo>(json);
            if (release == null || release.draft || release.prerelease) throw new InvalidDataException("No stable release available.");
            Version version = ParseVersion(release.tag_name);
            string zip = "NetStuck-v." + version.ToString(3) + ".zip";
            Asset(release, zip); Asset(release, zip + ".sha256.txt");
            return release;
        }

        public static List<ReleasePackage> PreviousPackages(string json, Version installed)
        {
            if (json == null || !json.TrimStart().StartsWith("[", StringComparison.Ordinal)) throw new InvalidDataException("Invalid release list.");
            var releases = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Deserialize<List<ReleaseInfo>>(json);
            if (releases == null) throw new InvalidDataException("Invalid release list.");
            var packages = new Dictionary<Version, ReleasePackage>();
            foreach (var release in releases)
            {
                if (release == null || release.draft || release.prerelease) continue;
                Version releaseVersion;
                try { releaseVersion = ParseVersion(release.tag_name); } catch (InvalidDataException) { continue; }
                foreach (var asset in release.assets ?? new List<ReleaseAsset>())
                {
                    if (asset == null || asset.name == null) continue;
                    var match = Regex.Match(asset.name, @"^NetStuck-v\.(\d+\.\d+\.\d+)\.zip$");
                    if (!match.Success) continue;
                    Version version = ParseVersion(match.Groups[1].Value);
                    // Earlier releases used a different portable inventory. Never relax
                    // package validation to silently install an incompatible legacy ZIP.
                    if (version < new Version(1, 3, 1, 0) || version >= installed || version > releaseVersion) continue;
                    try { Asset(release, asset.name); Asset(release, asset.name + ".sha256.txt"); }
                    catch (InvalidDataException) { continue; }
                    ReleasePackage existing;
                    bool canonical = releaseVersion == version;
                    if (!packages.TryGetValue(version, out existing) || (canonical && ParseVersion(existing.Release.tag_name) != version))
                        packages[version] = new ReleasePackage { Release = release, Version = version };
                }
            }
            return packages.Values.OrderByDescending(p => p.Version).ToList();
        }

        public static void ValidateTransition(Version installed, Version requested, bool allowDowngrade, string expectedInstalled)
        {
            if (installed == null || requested == null) throw new InvalidDataException("Missing application version.");
            if (!String.IsNullOrEmpty(expectedInstalled) && installed.ToString(4) != expectedInstalled)
                throw new IOException("The installed version changed; version replacement cancelled.");
            if (allowDowngrade ? requested >= installed : requested <= installed)
                throw new IOException(allowDowngrade ? "Select an earlier version for downgrade." : "The installed version is already equal or newer; update cancelled.");
        }

        // Streams are bounded, cancellation aborts the actual request (including reads).
        public static async Task Download(string url, string destination, long limit, CancellationToken token, Action<long> progress)
        {
            Uri uri = new Uri(url);
            if (uri.Scheme != "https") throw new InvalidDataException("HTTPS is required.");
            for (int redirect = 0; redirect < 6; redirect++)
            {
                if (!(uri.Host == "api.github.com" || uri.Host == "github.com" || uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com"))
                    throw new InvalidDataException("Untrusted download host.");
                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.AllowAutoRedirect = false; request.UserAgent = "NetStuck/2.0.0";
                request.Accept = "application/vnd.github+json";
                request.Timeout = 15000; request.ReadWriteTimeout = 15000;
                using (token.Register(request.Abort))
                {
                    token.ThrowIfCancellationRequested();
                    using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    {
                        int status = (int)response.StatusCode;
                        if (status >= 300 && status <= 399)
                        {
                            uri = new Uri(uri, response.Headers["Location"]);
                            if (uri.Scheme != "https") throw new InvalidDataException("Unsafe redirect.");
                            continue;
                        }
                        if (response.ContentLength > limit) throw new InvalidDataException("Download exceeds size limit.");
                        using (var input = response.GetResponseStream())
                        using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[65536]; long total = 0; int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                            {
                                total += count;
                                if (total > limit) throw new InvalidDataException("Download exceeds size limit.");
                                await output.WriteAsync(buffer, 0, count, token).ConfigureAwait(false);
                                if (progress != null) progress(total);
                            }
                            output.Flush(true);
                        }
                        return;
                    }
                }
            }
            throw new InvalidDataException("Too many redirects.");
        }

        public static void ExtractPackage(string zip, string payload, Version version)
        {
            if (Directory.Exists(payload)) throw new IOException("Staging directory already exists.");
            Directory.CreateDirectory(payload);
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string prefix = "NetStuck-v." + version.ToString(3) + "/";
            using (var archive = ZipFile.OpenRead(zip))
            {
                long total = 0;
                foreach (var entry in archive.Entries)
                {
                    string path = entry.FullName.Replace('\\', '/');
                    if (!path.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Unexpected ZIP root.");
                    string relative = path.Substring(prefix.Length);
                    if (relative == "" || relative == "tools/") continue;
                    if (!PackageFiles.Contains(relative, StringComparer.Ordinal) || !found.Add(relative)) throw new InvalidDataException("Unexpected or duplicate ZIP entry.");
                    total += entry.Length;
                    if (entry.Length < 0 || total > 200 * 1024 * 1024) throw new InvalidDataException("Expanded package is too large.");
                    string target = Path.Combine(payload, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var input = entry.Open()) using (var output = File.Create(target))
                    {
                        byte[] bytes = new byte[65536]; long written = 0; int count;
                        while ((count = input.Read(bytes, 0, bytes.Length)) > 0)
                        { written += count; if (written > entry.Length) throw new InvalidDataException("Invalid ZIP length."); output.Write(bytes, 0, count); }
                        if (written != entry.Length) throw new InvalidDataException("Truncated ZIP entry.");
                    }
                }
            }
            if (found.Count != PackageFiles.Length) throw new InvalidDataException("Incomplete package.");
            VerifyPayload(payload, version);
        }

        public static void VerifyPayload(string payload, Version version)
        {
            var actual = Directory.GetFiles(payload, "*", SearchOption.AllDirectories).Select(p => p.Substring(payload.TrimEnd('\\').Length + 1).Replace('\\', '/')).ToArray();
            if (actual.Length != PackageFiles.Length || actual.Any(p => !PackageFiles.Contains(p, StringComparer.Ordinal))) throw new InvalidDataException("Package inventory mismatch.");
            string[] lines = File.ReadAllLines(Path.Combine(payload, "SHA256SUMS.txt")).Where(l => l.Length > 0).ToArray();
            if (lines.Length != PackageFiles.Length - 1) throw new InvalidDataException("Invalid manifest.");
            foreach (string relative in PackageFiles.Where(p => p != "SHA256SUMS.txt"))
            {
                string file = Path.Combine(payload, relative.Replace('/', '\\'));
                string expected = Hash(file) + "  " + relative;
                if (lines.Count(l => l == expected) != 1) throw new InvalidDataException("Package checksum mismatch.");
            }
            if (FileVersionInfo.GetVersionInfo(Path.Combine(payload, "NetStuck.exe")).FileVersion != version.ToString(4)) throw new InvalidDataException("Executable version mismatch.");
        }

        static void NoReparsePoints(string path)
        {
            for (string current = Path.GetFullPath(path); !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Update paths cannot contain links or junctions.");
        }

        // Only the nine declared package paths are changed; user files are never enumerated for deletion.
        public static void Install(string payload, string target, string backup, Version version, Action<int> fault)
        {
            NoReparsePoints(payload); NoReparsePoints(target); NoReparsePoints(backup);
            VerifyPayload(payload, version);
            if (!File.Exists(Path.Combine(target, "NetStuck.exe"))) throw new IOException("Existing NetStuck installation is missing.");
            if (Directory.Exists(backup)) throw new IOException("Backup already exists.");
            Directory.CreateDirectory(backup);
            var existed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = new List<string>();
            foreach (string relative in PackageFiles)
            {
                string destination = Path.Combine(target, relative.Replace('/', '\\'));
                NoReparsePoints(destination);
                if (!File.Exists(destination)) continue;
                // Fail before modifying any file if another instance or an ACL blocks replacement.
                using (new FileStream(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                string copy = Path.Combine(backup, relative.Replace('/', '\\'));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)); File.Copy(destination, copy);
                if (Hash(destination) != Hash(copy)) throw new IOException("Backup verification failed.");
                existed.Add(relative);
            }
            // Durable recovery instructions are written before touching the installation.
            AtomicJson.Write(Path.Combine(backup, "recovery.json"), new UpdateRecovery { Target = Path.GetFullPath(target), Existed = existed.ToList() });
            try
            {
                foreach (string relative in PackageFiles)
                {
                    string destination = Path.Combine(target, relative.Replace('/', '\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    changed.Add(relative);
                    File.Copy(Path.Combine(payload, relative.Replace('/', '\\')), destination, true);
                    if (fault != null) fault(changed.Count);
                }
                VerifyPayloadFilesAtTarget(payload, target);
            }
            catch (Exception original)
            {
                Exception recovery = null;
                foreach (string relative in changed.AsEnumerable().Reverse())
                {
                    string destination = Path.Combine(target, relative.Replace('/', '\\'));
                    try
                    {
                        if (existed.Contains(relative))
                        {
                            string copy = Path.Combine(backup, relative.Replace('/', '\\'));
                            File.Copy(copy, destination, true);
                            if (Hash(copy) != Hash(destination)) throw new IOException("Restore verification failed.");
                        }
                        else if (File.Exists(destination)) File.Delete(destination);
                    }
                    catch (Exception ex) { recovery = ex; }
                }
                if (recovery != null) throw new IOException("Update and rollback failed. Recover from: " + backup, recovery);
                throw new IOException("Update failed; the original files were restored.", original);
            }
        }
        static void VerifyPayloadFilesAtTarget(string payload, string target)
        {
            foreach (string relative in PackageFiles)
                if (Hash(Path.Combine(payload, relative)) != Hash(Path.Combine(target, relative))) throw new IOException("Installed file verification failed.");
        }

        public static void Recover(string backup)
        {
            NoReparsePoints(backup);
            var recovery = AtomicJson.Read<UpdateRecovery>(Path.Combine(backup, "recovery.json"));
            if (recovery == null || recovery.Existed == null || recovery.Existed.Any(p => !PackageFiles.Contains(p))) throw new InvalidDataException("Invalid recovery record.");
            NoReparsePoints(recovery.Target);
            foreach (string relative in PackageFiles)
            {
                string destination = Path.Combine(recovery.Target, relative.Replace('/', '\\'));
                NoReparsePoints(destination);
                if (recovery.Existed.Contains(relative))
                {
                    string original = Path.Combine(backup, relative.Replace('/', '\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(original, destination, true);
                    if (Hash(original) != Hash(destination)) throw new IOException("Recovered file verification failed.");
                }
                else if (File.Exists(destination)) File.Delete(destination);
            }
        }

        public static int ApplyJob(string jobPath, bool recover)
        {
            try
            {
                string root = Path.GetDirectoryName(Path.GetFullPath(jobPath));
                var job = AtomicJson.Read<UpdateJob>(jobPath);
                if (job == null || Path.GetFullPath(job.Payload) != Path.Combine(root, "payload")) throw new InvalidDataException("Invalid update job.");
                if (recover)
                {
                    Recover(Path.Combine(root, "backup"));
                    Process.Start(new ProcessStartInfo(Path.Combine(job.Target, "NetStuck.exe")) { WorkingDirectory = job.Target, UseShellExecute = true });
                    return 0;
                }
                Version version = ParseVersion(job.Version);
                // The helper executes a copy of the already installed binary, never a downloaded script.
                try
                {
                    using (var parent = Process.GetProcessById(job.ProcessId))
                    {
                        if (parent.StartTime.ToUniversalTime().Ticks != job.ProcessStartedUtcTicks) throw new IOException("Process identity changed.");
                        if (!parent.WaitForExit(60000)) throw new IOException("NetStuck did not close; update cancelled.");
                    }
                }
                catch (ArgumentException) { } // Parent has already exited.
                string backup = Path.Combine(root, "backup");
                var installed = new Version(FileVersionInfo.GetVersionInfo(Path.Combine(job.Target, "NetStuck.exe")).FileVersion);
                ValidateTransition(installed, version, job.AllowDowngrade, job.ExpectedInstalledVersion);
                Install(job.Payload, job.Target, backup, version, null);
                try
                {
                    using (var restarted = Process.Start(new ProcessStartInfo(Path.Combine(job.Target, "NetStuck.exe")) { WorkingDirectory = job.Target, UseShellExecute = true }))
                    {
                        if (restarted == null || !restarted.WaitForInputIdle(15000) || restarted.HasExited) throw new IOException("Updated application did not become ready.");
                    }
                }
                catch (Exception ex)
                {
                    // A possibly running new process may still hold the EXE. Preserve backup;
                    // do not overwrite under it or report a successful restart.
                    throw new IOException("Installed files verified, but restart was not confirmed. Backup: " + backup, ex);
                }
                // Keep verified backup and job as local recovery evidence. No operator state is copied.
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update could not complete.\r\n" + ex.Message + "\r\nYou can reopen NetStuck from its original folder.", "NetStuck update", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    public sealed partial class MainForm
    {
        Button checkUpdatesButton, updateNowButton;
        Label updateStatus, lastUpdateCheck;
        ProgressBar updateProgress;
        ReleaseInfo availableRelease;
        bool updateBusy;
        bool collectorPingBusy;
        DateTime lastCheckedUtc;
        CheckBox autoCheckUpdates;
        Button browseVersionsButton, downgradeButton;
        ComboBox previousVersionCombo;
        Label previousVersionStatus;
        string UpdateCachePath { get { return Path.Combine(Path.GetDirectoryName(statePath), "update-check.json"); } }

        void BuildUpdateControls(Control card)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 170, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            updateStatus = new Label { Dock = DockStyle.Fill, Text = "Current version: " + AppVersion, AutoEllipsis = true, AccessibleName = "Update status" };
            lastUpdateCheck = new Label { Dock = DockStyle.Fill, Text = "Last checked: Never", AccessibleName = "Last update check" };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            checkUpdatesButton = ActionButton("Check for updates", false, 170); checkUpdatesButton.Name = "checkForUpdates";
            updateNowButton = ActionButton("Update now", true, 140); updateNowButton.Name = "updateNow"; updateNowButton.Enabled = false;
            autoCheckUpdates = new CheckBox { Text = "Check automatically (daily)", Checked = true, AutoSize = true, Padding = new Padding(8) };
            updateProgress = new ProgressBar { Dock = DockStyle.Fill, Visible = false };
            checkUpdatesButton.Click += async delegate { await CheckForUpdatesAsync(); };
            updateNowButton.Click += async delegate { await UpdateNowAsync(); };
            actions.Controls.AddRange(new Control[] { checkUpdatesButton, updateNowButton, autoCheckUpdates });
            panel.Controls.Add(updateStatus, 0, 0); panel.Controls.Add(lastUpdateCheck, 0, 1); panel.Controls.Add(actions, 0, 2); panel.Controls.Add(updateProgress, 0, 3);
            card.Controls.Add(panel);
            BuildVersionRecoveryControls(card);
            try
            {
                var stamp = AtomicJson.Read<Dictionary<string, string>>(UpdateCachePath);
                DateTime parsed;
                if (stamp != null && stamp.ContainsKey("checkedUtc") && DateTime.TryParse(stamp["checkedUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind, out parsed))
                { lastCheckedUtc = parsed.ToUniversalTime(); lastUpdateCheck.Text = "Last checked: " + lastCheckedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"); }
                if (stamp != null && stamp.ContainsKey("release"))
                {
                    var cached = UpdateEngine.ParseRelease(stamp["release"]);
                    if (UpdateEngine.ParseVersion(cached.tag_name) > GetType().Assembly.GetName().Version)
                    { availableRelease = cached; updateNowButton.Enabled = true; updateStatus.Text = "New version available: " + cached.tag_name + " (last check)"; pagesByName["Updates"].Text = "Updates (new)"; }
                }
            }
            catch { }
        }

        async Task CheckForUpdatesAsync()
        {
            if (updateBusy || appClosing) return;
            updateBusy = true; availableRelease = null; RefreshUpdateActions();
            updateStatus.Text = "Checking GitHub for a stable release...";
            string file = Path.Combine(Path.GetTempPath(), "NetStuck-release-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(maintenanceCancellation.Token))
                {
                    timeout.CancelAfter(15000);
                    await UpdateEngine.Download(UpdateEngine.LatestUrl, file, 1024 * 1024, timeout.Token, null);
                }
                if (appClosing) return;
                var release = UpdateEngine.ParseRelease(File.ReadAllText(file));
                lastCheckedUtc = DateTime.UtcNow;
                lastUpdateCheck.Text = "Last checked: " + lastCheckedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                try { AtomicJson.Write(UpdateCachePath, new Dictionary<string, string> { { "checkedUtc", lastCheckedUtc.ToString("o") }, { "release", File.ReadAllText(file) } }); } catch { }
                if (UpdateEngine.ParseVersion(release.tag_name) > GetType().Assembly.GetName().Version)
                { availableRelease = release; updateStatus.Text = "New version available: " + release.tag_name; pagesByName["Updates"].Text = "Updates (new)"; }
                else { updateStatus.Text = "You are up to date (" + AppVersion + ")."; pagesByName["Updates"].Text = "Updates"; }
            }
            catch (Exception)
            {
                if (!appClosing) updateStatus.Text = "Could not check for updates. Check connectivity/proxy or try again later (GitHub may rate-limit requests).";
            }
            finally
            {
                try { if (File.Exists(file)) File.Delete(file); } catch { }
                updateBusy = false;
                if (!appClosing) RefreshUpdateActions();
            }
        }

        bool ActiveNetworkWork()
        {
            return pingCancellation != null || dnsCancellation != null || collectorCancellation != null || collectorPingBusy
                || traceSessionsV103.Any(s => s.Cancellation != null || s.ActiveRun != null)
                || !macLookupButton.Enabled || !wanLookupButton.Enabled;
        }

        async Task UpdateNowAsync()
        {
            if (updateBusy || availableRelease == null || appClosing) return;
            await InstallReleaseAsync(new ReleasePackage { Release = availableRelease, Version = UpdateEngine.ParseVersion(availableRelease.tag_name) }, false);
        }

        void BuildVersionRecoveryControls(Control card)
        {
            var panel = new TableLayoutPanel { Name = "versionRecoveryControls", Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label { Text = "Previous versions", AutoSize = true, Font = new Font(UiTokens.FontFamily, UiTokens.SectionTitleFontSize, FontStyle.Bold), Padding = new Padding(0, 8, 0, 4) }, 0, 0);
            panel.Controls.Add(new Label { Text = "Recover from a UI problem by installing an earlier verified portable version. Saved lists and preferences stay in place.\r\nOnly compatible packages with checksums are listed; older releases are available on GitHub.", AutoSize = true, Dock = DockStyle.Fill, ForeColor = TextMuted, Padding = new Padding(0, 4, 0, 8) }, 0, 1);
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, Height = 42, ColumnCount = 3, Margin = new Padding(0) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 172));
            browseVersionsButton = ActionButton("Load previous versions", false, 172); browseVersionsButton.Name = "loadPreviousVersions";
            previousVersionCombo = new ComboBox { Name = "previousVersion", AccessibleName = "Previous NetStuck version", AccessibleDescription = "Choose the exact GitHub version to install.", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(8, 5, 8, 3) };
            downgradeButton = ActionButton("Downgrade & restart", false, 164); downgradeButton.Name = "downgradeVersion"; downgradeButton.Enabled = false;
            browseVersionsButton.Click += async delegate { await LoadPreviousVersionsAsync(); };
            previousVersionCombo.SelectedIndexChanged += delegate { downgradeButton.Enabled = !updateBusy && previousVersionCombo.SelectedItem is ReleasePackage; };
            downgradeButton.Click += async delegate { var choice = previousVersionCombo.SelectedItem as ReleasePackage; if (choice != null) await InstallReleaseAsync(choice, true); };
            actions.Controls.Add(browseVersionsButton, 0, 0); actions.Controls.Add(previousVersionCombo, 1, 0); actions.Controls.Add(downgradeButton, 2, 0);
            previousVersionStatus = new Label { Text = "Choose Load previous versions to fetch the GitHub list. Shortcut: Ctrl+Shift+U opens version recovery.", AutoSize = true, Dock = DockStyle.Fill, ForeColor = TextMuted, Padding = new Padding(0, 4, 0, 4), AccessibleName = "Version recovery status" };
            panel.Controls.Add(actions, 0, 2); panel.Controls.Add(previousVersionStatus, 0, 3);
            card.Controls.Add(panel);
        }

        async Task LoadPreviousVersionsAsync()
        {
            if (updateBusy || appClosing) return;
            updateBusy = true; RefreshUpdateActions(); previousVersionStatus.Text = "Loading previous GitHub versions...";
            string file = Path.Combine(Path.GetTempPath(), "NetStuck-versions-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(maintenanceCancellation.Token))
                {
                    timeout.CancelAfter(20000);
                    await UpdateEngine.Download(UpdateEngine.VersionsUrl, file, 4 * 1024 * 1024, timeout.Token, null);
                }
                if (appClosing) return;
                var choices = await Task.Run(() => UpdateEngine.PreviousPackages(File.ReadAllText(file), GetType().Assembly.GetName().Version));
                if (appClosing) return;
                previousVersionCombo.Items.Clear(); foreach (var choice in choices) previousVersionCombo.Items.Add(choice);
                if (choices.Count > 0) previousVersionCombo.SelectedIndex = 0;
                previousVersionStatus.Text = choices.Count > 0 ? choices.Count + " verified-package choices. Review the selected version before downgrading." : "No compatible earlier packages found. See the GitHub releases page.";
            }
            catch { if (!appClosing) previousVersionStatus.Text = "Could not load previous versions. Check connectivity or try again later."; }
            finally { try { if (File.Exists(file)) File.Delete(file); } catch { } updateBusy = false; if (!appClosing) RefreshUpdateActions(); }
        }

        void RefreshUpdateActions()
        {
            checkUpdatesButton.Enabled = !updateBusy;
            updateNowButton.Enabled = !updateBusy && availableRelease != null;
            if (browseVersionsButton == null) return;
            browseVersionsButton.Enabled = previousVersionCombo.Enabled = !updateBusy;
            downgradeButton.Enabled = !updateBusy && previousVersionCombo.SelectedItem is ReleasePackage;
        }

        async Task InstallReleaseAsync(ReleasePackage choice, bool downgrade)
        {
            if (updateBusy || choice == null || appClosing) return;
            UpdateEngine.ValidateTransition(GetType().Assembly.GetName().Version, choice.Version, downgrade, null);
            if (ActiveNetworkWork()) { MessageBox.Show(this, "Stop active Ping, Traceroute, DNS, lookup and Collector work before updating.", "NetStuck update"); return; }
            string action = downgrade ? "Downgrade" : "Update";
            if (MessageBox.Show(this, action + " from " + AppVersion + " to v" + choice.Version.ToString(3) + " and restart NetStuck?\r\nThe current program files will be backed up. Saved lists and settings remain in place; earlier versions may not recognize newer preferences.", "NetStuck " + action.ToLowerInvariant(), MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            updateBusy = true; RefreshUpdateActions();
            updateProgress.Visible = true;
            string root = Path.Combine(Path.GetDirectoryName(statePath), "updates", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                Version version = choice.Version;
                string name = "NetStuck-v." + version.ToString(3) + ".zip";
                ReleaseAsset zip = UpdateEngine.Asset(choice.Release, name), checksum = UpdateEngine.Asset(choice.Release, name + ".sha256.txt");
                string zipPath = Path.Combine(root, name), shaPath = Path.Combine(root, "zip.sha256");
                updateStatus.Text = "Downloading v" + version.ToString(3) + "...";
                var progress = new Progress<long>(count => { if (!appClosing) updateProgress.Value = (int)Math.Min(100, count * 100 / zip.size); });
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(maintenanceCancellation.Token))
                {
                    timeout.CancelAfter(TimeSpan.FromMinutes(10));
                    await UpdateEngine.Download(zip.browser_download_url, zipPath, zip.size, timeout.Token, count => ((IProgress<long>)progress).Report(count));
                    await UpdateEngine.Download(checksum.browser_download_url, shaPath, 4096, timeout.Token, null);
                }
                if (appClosing) return;
                string expected = File.ReadAllText(shaPath).Trim();
                if (new FileInfo(zipPath).Length != zip.size || expected != UpdateEngine.Hash(zipPath) + "  " + name) throw new InvalidDataException("Downloaded ZIP checksum mismatch.");
                string payload = Path.Combine(root, "payload");
                updateStatus.Text = "Verifying package...";
                await Task.Run(() => UpdateEngine.ExtractPackage(zipPath, payload, version));
                if (appClosing) return;
                if (ActiveNetworkWork()) throw new InvalidOperationException("Network work started during download. Stop it and retry the update.");
                SaveAppState();
                if (!stateSaveSucceeded) throw new IOException("Settings could not be saved; update cancelled.");
                string target = Path.GetDirectoryName(Application.ExecutablePath);
                // Probe write permission before exiting the usable application.
                string probe = Path.Combine(target, ".netstuck-write-" + Guid.NewGuid().ToString("N"));
                using (File.Create(probe)) { } File.Delete(probe);
                string helper = Path.Combine(root, "NetStuck.UpdateHelper.exe");
                File.Copy(Application.ExecutablePath, helper);
                if (UpdateEngine.Hash(helper) != UpdateEngine.Hash(Application.ExecutablePath)) throw new IOException("Updater copy failed verification.");
                using (var current = Process.GetCurrentProcess())
                    AtomicJson.Write(Path.Combine(root, "job.json"), new UpdateJob { ProcessId = current.Id, ProcessStartedUtcTicks = current.StartTime.ToUniversalTime().Ticks, Target = target, Payload = payload, Version = version.ToString(3), AllowDowngrade = downgrade, ExpectedInstalledVersion = GetType().Assembly.GetName().Version.ToString(4) });
                Process.Start(new ProcessStartInfo(helper, "--apply-update") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true });
                Close();
            }
            catch (Exception ex) { if (!appClosing) { updateStatus.Text = "Update failed; the running installation has not been replaced."; MessageBox.Show(this, ex.Message, "NetStuck update", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
            finally
            {
                updateBusy = false;
                if (!appClosing) { RefreshUpdateActions(); updateProgress.Visible = false; }
            }
        }
    }
}
