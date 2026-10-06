using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using NetStuck;

static class VersionRecoveryTests
{
    static int failed;
    static readonly Assembly Assembly = typeof(MainForm).Assembly;
    static readonly Type Engine = Assembly.GetType("NetStuck.UpdateEngine");
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static readonly string[] Files = { "CHANGELOG.md", "NetStuck-Icon.png", "NetStuck.exe", "README-TH.md", "README.md", "TEST-REPORT.txt", "tools/plink.exe", "tools/PuTTY-LICENCE.txt", "SHA256SUMS.txt" };
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) failed++; }
    static object Call(string name, params object[] args) { return Engine.GetMethod(name).Invoke(null, args); }
    static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
    static string Hash(string path) { return (string)Call("Hash", path); }
    static object Release(string tag, string package, bool draft, bool prerelease, string prefix, bool checksum)
    {
        string name = "NetStuck-v." + package + ".zip";
        var assets = new List<object> { new { name = name, size = 100, browser_download_url = prefix + tag + "/" + name } };
        if (checksum) assets.Add(new { name = name + ".sha256.txt", size = 87, browser_download_url = prefix + tag + "/" + name + ".sha256.txt" });
        return new { tag_name = tag, draft = draft, prerelease = prerelease, assets = assets };
    }
    static IList Packages(object[] releases, Version installed) { return (IList)Call("PreviousPackages", Json.Serialize(releases), installed); }
    static string PackageVersion(object package) { return ((Version)package.GetType().GetProperty("Version").GetValue(package, null)).ToString(3); }
    static void Transition(Version installed, Version desired, bool downgrade, string expected) { Call("ValidateTransition", installed, desired, downgrade, expected); }
    static void Fixture(string path, string exe)
    {
        Directory.CreateDirectory(Path.Combine(path, "tools"));
        foreach (string relative in Files.Where(f => f != "SHA256SUMS.txt")) File.WriteAllText(Path.Combine(path, relative), "Synthetic application fixture: " + relative);
        File.Copy(exe, Path.Combine(path, "NetStuck.exe"), true);
        File.WriteAllLines(Path.Combine(path, "SHA256SUMS.txt"), Files.Where(f => f != "SHA256SUMS.txt").Select(f => Hash(Path.Combine(path, f)) + "  " + f).ToArray());
    }
    static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "NetStuck-version-recovery-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var current = Assembly.GetName().Version; var old = new Version(1, 3, 5, 0); var future = new Version(2, 1, 0, 0);
            string prefix = "https://github.com/pk-wrk-sea/netstuck-platform/releases/download/";
            var release = Release("v1.3.5", "1.3.5", false, false, prefix, true);
            Check("previous release list accepts a stable verified-package choice", Packages(new[] { release }, current).Count == 1);
            Check("numeric downgrade order does not use string sorting", PackageVersion(Packages(new[] { Release("v1.3.9", "1.3.9", false, false, prefix, true), Release("v1.3.10", "1.3.10", false, false, prefix, true) }, current)[0]) == "1.3.10");
            var archive = Packages(new[] { Release("v1.3.3", "1.3.1", false, false, prefix, true) }, current);
            Check("archived v1.3.1 is selectable with its actual GitHub release location", archive.Count == 1 && PackageVersion(archive[0]) == "1.3.1" && archive[0].ToString().Contains("archive on v1.3.3"));
            Check("draft and prerelease packages are never downgrade choices", Packages(new[] { Release("v1.3.5", "1.3.5", true, false, prefix, true), Release("v1.3.3", "1.3.3", false, true, prefix, true) }, current).Count == 0);
            Check("missing checksum companion excludes a historical package", Packages(new[] { Release("v1.3.5", "1.3.5", false, false, prefix, false) }, current).Count == 0);
            Check("foreign-host historical asset is excluded", Packages(new[] { Release("v1.3.5", "1.3.5", false, false, "https://example.com/", true) }, current).Count == 0);
            Check("current and newer versions are excluded from downgrade", Packages(new[] { Release("v2.0.0", "2.0.0", false, false, prefix, true), Release("v2.1.0", "2.1.0", false, false, prefix, true) }, current).Count == 0);
            Check("an archive cannot claim a version newer than its release", Packages(new[] { Release("v1.3.3", "1.3.5", false, false, prefix, true) }, current).Count == 0);
            Check("incompatible legacy portable inventories are excluded", Packages(new[] { Release("v1.3.0", "1.3.0", false, false, prefix, true), Release("v1.2.3", "1.2.3", false, false, prefix, true) }, current).Count == 0);
            var duplicate = Packages(new[] { Release("v1.3.5", "1.3.3", false, false, prefix, true), Release("v1.3.3", "1.3.3", false, false, prefix, true) }, current);
            Check("duplicate version prefers its canonical release over an archive", duplicate.Count == 1 && !duplicate[0].ToString().Contains("archive"));
            Check("malformed release JSON fails closed", Reject(() => Call("PreviousPackages", "{}", current)));
            Check("ordinary update still rejects every lower version", Reject(() => Transition(current, old, false, current.ToString(4))));
            Check("downgrade requires explicit lower-version mode", !Reject(() => Transition(current, old, true, current.ToString(4))));
            Check("equal-version replacement is rejected in both modes", Reject(() => Transition(current, current, false, null)) && Reject(() => Transition(current, current, true, null)));
            Check("future version cannot pass as a downgrade", Reject(() => Transition(current, future, true, null)));
            Check("ordinary newer-version update remains accepted", !Reject(() => Transition(current, future, false, current.ToString(4))));
            Check("changed installed identity cancels replacement", Reject(() => Transition(current, old, true, old.ToString(4))));
            object legacyJob = Activator.CreateInstance(Assembly.GetType("NetStuck.UpdateJob"));
            Check("old jobs default to upgrade mode without downgrade permission", !(bool)legacyJob.GetType().GetProperty("AllowDowngrade").GetValue(legacyJob, null));

            string target = Path.Combine(root, "target"), payload = Path.Combine(root, "payload"), backup = Path.Combine(root, "backup");
            Fixture(target, Assembly.Location); Fixture(payload, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LegacyVersionStub.exe"));
            var original = Files.ToDictionary(f => f, f => Hash(Path.Combine(target, f)));
            File.WriteAllText(Path.Combine(target, "operator-notes.txt"), "Synthetic unrelated file"); File.WriteAllText(Path.Combine(target, "state.json"), "Synthetic saved state");
            Check("older executable manifest and exact inventory validate", !Reject(() => Call("VerifyPayload", payload, old)));
            Call("Install", payload, target, backup, old, null);
            Check("sandbox downgrade installs the requested older executable", FileVersionInfo.GetVersionInfo(Path.Combine(target, "NetStuck.exe")).FileVersion == "1.3.5.0");
            Check("sandbox downgrade backs up every prior application file", Files.All(f => Hash(Path.Combine(backup, f)) == original[f]));
            Check("sandbox downgrade preserves state and unrelated operator files", File.ReadAllText(Path.Combine(target, "state.json")) == "Synthetic saved state" && File.ReadAllText(Path.Combine(target, "operator-notes.txt")) == "Synthetic unrelated file");
            Call("Recover", backup);
            Check("downgrade recovery restores the complete original installation", Files.All(f => Hash(Path.Combine(target, f)) == original[f]));
            string failedBackup = Path.Combine(root, "failed-backup");
            Check("injected mid-downgrade copy failure is observable", Reject(() => Call("Install", payload, target, failedBackup, old, new Action<int>(i => { if (i == 3) throw new IOException("Injected copy failure"); }))));
            Check("failed downgrade rolls back all application hashes", Files.All(f => Hash(Path.Combine(target, f)) == original[f]));
            Check("failed downgrade also preserves state and unrelated files", File.ReadAllText(Path.Combine(target, "state.json")) == "Synthetic saved state" && File.Exists(Path.Combine(target, "operator-notes.txt")));
        }
        catch (Exception ex) { Check("unexpected recovery test exception " + ex, false); }
        finally { try { Directory.Delete(root, true); Check("version recovery test-owned state and packages removed", !Directory.Exists(root)); } catch { Check("version recovery cleanup", false); } }
        Console.WriteLine("Failures: " + failed); return failed == 0 ? 0 : 1;
    }
}
