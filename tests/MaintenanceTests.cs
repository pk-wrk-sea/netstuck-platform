using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetStuck;

static class MaintenanceTests
{
    static int failed;
    static Assembly assembly = typeof(MainForm).Assembly;
    static string root;
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) failed++; }
    static object Call(string type, string method, params object[] args)
    { return assembly.GetType("NetStuck." + type).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args); }
    static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
    static IEnumerable<Control> Flat(Control c) { yield return c; foreach (Control child in c.Controls) foreach (var item in Flat(child)) yield return item; }
    static void Pump() { for (int i = 0; i < 8; i++) { Application.DoEvents(); Thread.Sleep(10); } }
    static void Save(string path, object value) { Call("AtomicJson", "Write", path, value); }
    static Dictionary<string,string> Read(string path)
    { return (Dictionary<string,string>)assembly.GetType("NetStuck.AtomicJson").GetMethod("Read").MakeGenericMethod(typeof(Dictionary<string,string>)).Invoke(null, new object[] { path }); }
    static string Hash(string path) { return (string)Call("UpdateEngine", "Hash", path); }
    static string[] files = { "CHANGELOG.md", "NetStuck-Icon.png", "NetStuck.exe", "README-TH.md", "README.md", "TEST-REPORT.txt", "tools/plink.exe", "tools/PuTTY-LICENCE.txt", "SHA256SUMS.txt" };
    static string Fixture(string name)
    {
        string path = Path.Combine(root, name); Directory.CreateDirectory(Path.Combine(path,"tools"));
        foreach (string file in files) File.WriteAllText(Path.Combine(path,file), "fixture " + name + " " + file);
        File.Copy(assembly.Location, Path.Combine(path,"NetStuck.exe"), true);
        Manifest(path); return path;
    }
    static void Manifest(string path) { File.WriteAllLines(Path.Combine(path,"SHA256SUMS.txt"), files.Where(f => f != "SHA256SUMS.txt").Select(f => Hash(Path.Combine(path,f)) + "  " + f)); }
    static string Release(string tag, bool draft, bool prerelease, string urlPrefix)
    {
        string zip = "NetStuck-v." + tag.TrimStart('v') + ".zip";
        return "{\"tag_name\":\"" + tag + "\",\"draft\":" + draft.ToString().ToLower() + ",\"prerelease\":" + prerelease.ToString().ToLower() + ",\"assets\":[{\"name\":\"" + zip + "\",\"size\":100,\"browser_download_url\":\"" + urlPrefix + tag + "/" + zip + "\"},{\"name\":\"" + zip + ".sha256.txt\",\"size\":87,\"browser_download_url\":\"" + urlPrefix + tag + "/" + zip + ".sha256.txt\"}]}";
    }
    [STAThread]
    static int Main(string[] args)
    {
        root = Path.Combine(Path.GetTempPath(), "NetStuck-maintenance-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT",root);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            string state = Path.Combine(root,"atomic.json");
            Save(state,new Dictionary<string,string>{{"value","first"}}); Save(state,new Dictionary<string,string>{{"value","second"}});
            Check("atomic save roundtrip and retained backup", Read(state)["value"] == "second" && File.Exists(state+".bak"));
            File.WriteAllText(state,"{broken"); Check("corrupt primary recovers backup",Read(state)["value"] == "first");
            Save(state,new Dictionary<string,string>{{"value","third"}}); File.WriteAllText(state,"broken");
            Check("recovery does not rotate corrupt primary into backup",Read(state)["value"] == "first");
            File.WriteAllText(state+".bak","broken"); Check("both corrupt copies fail explicitly", Reject(()=>Read(state)));
            Save(state,new Dictionary<string,string>{{"value","start"}});
            Parallel.For(0,24,i=>Save(state,new Dictionary<string,string>{{"value",i.ToString()}}));
            Check("concurrent writers leave valid JSON and no temporary residue",Read(state).ContainsKey("value") && Directory.GetFiles(root,"*.tmp").Length == 0);
            using(var locked = new FileStream(state,FileMode.Open,FileAccess.Read,FileShare.None))
                Check("locked state rejects save instead of reporting success",Reject(()=>Save(state,new Dictionary<string,string>{{"value","lost"}})));

            var current = assembly.GetName().Version;
            Check("numeric versions compare 1.3.10 above 1.3.9",(Version)Call("UpdateEngine","ParseVersion","v1.3.10") > (Version)Call("UpdateEngine","ParseVersion","1.3.9"));
            Check("invalid and prerelease version labels rejected",Reject(()=>Call("UpdateEngine","ParseVersion","v1.4.0-beta")) && Reject(()=>Call("UpdateEngine","ParseVersion","../1.3.1")));
            string prefix = "https://github.com/pk-wrk-sea/netstuck-platform/releases/download/";
            Check("stable GitHub release metadata accepted",Call("UpdateEngine","ParseRelease",Release("v1.3.2",false,false,prefix)) != null);
            Check("draft and prerelease metadata rejected",Reject(()=>Call("UpdateEngine","ParseRelease",Release("v1.3.2",true,false,prefix))) && Reject(()=>Call("UpdateEngine","ParseRelease",Release("v1.3.2",false,true,prefix))));
            Check("foreign download host rejected",Reject(()=>Call("UpdateEngine","ParseRelease",Release("v1.3.2",false,false,"https://example.com/"))));
            Check("missing release assets rejected",Reject(()=>Call("UpdateEngine","ParseRelease","{\"tag_name\":\"v1.3.2\"}")));
            var cancelled = new CancellationToken(true);
            var download = (Task)Call("UpdateEngine","Download","https://api.github.com/repos/pk-wrk-sea/netstuck-platform/releases/latest",Path.Combine(root,"cancelled"),1024L,cancelled,null);
            Check("cancelled update request creates no output",Reject(()=>download.GetAwaiter().GetResult()) && !File.Exists(Path.Combine(root,"cancelled")));
            var unsafeDownload = (Task)Call("UpdateEngine","Download","https://example.com/asset",Path.Combine(root,"unsafe"),1024L,CancellationToken.None,null);
            Check("download transport refuses unrelated hosts",Reject(()=>unsafeDownload.GetAwaiter().GetResult()));

            string payload = Fixture("payload");
            Check("complete payload manifest and version accepted",!Reject(()=>Call("UpdateEngine","VerifyPayload",payload,current)));
            Check("wrong executable version rejected",Reject(()=>Call("UpdateEngine","VerifyPayload",payload,new Version(9,0,0,0))));
            File.AppendAllText(Path.Combine(payload,"README.md"),"tampered");
            Check("payload tampering rejected",Reject(()=>Call("UpdateEngine","VerifyPayload",payload,current))); Manifest(payload);
            string zip = Path.Combine(root,"package.zip");
            using(var archive = ZipFile.Open(zip,ZipArchiveMode.Create))
                foreach(string file in files) archive.CreateEntryFromFile(Path.Combine(payload,file),"NetStuck-v."+current.ToString(3)+"/"+file);
            string extracted = Path.Combine(root,"extracted");
            Check("valid ZIP extracts only complete expected payload",!Reject(()=>Call("UpdateEngine","ExtractPackage",zip,extracted,current)));
            using(var archive = ZipFile.Open(zip,ZipArchiveMode.Update)) archive.CreateEntry("NetStuck-v."+current.ToString(3)+"/../escape.txt");
            Check("ZIP traversal rejected without escaping staging",Reject(()=>Call("UpdateEngine","ExtractPackage",zip,Path.Combine(root,"badzip"),current)) && !File.Exists(Path.Combine(root,"escape.txt")));
            string target=Fixture("target"); File.WriteAllText(Path.Combine(target,"operator-note.txt"),"preserve");
            var before=files.ToDictionary(f=>f,f=>Hash(Path.Combine(target,f)));
            Action<int> failure = count => { if(count==3) throw new IOException("Injected write failure"); };
            Check("installation fault reports failure",Reject(()=>Call("UpdateEngine","Install",payload,target,Path.Combine(root,"rollback"),current,failure)));
            Check("rollback restores every original package byte",files.All(f=>Hash(Path.Combine(target,f))==before[f]));
            Check("rollback preserves unrelated user file",File.ReadAllText(Path.Combine(target,"operator-note.txt"))=="preserve");
            Check("successful install replaces complete portable package",!Reject(()=>Call("UpdateEngine","Install",payload,target,Path.Combine(root,"backup"),current,null)) && files.All(f=>Hash(Path.Combine(target,f))==Hash(Path.Combine(payload,f))));
            Check("successful update keeps backup and user data",File.Exists(Path.Combine(root,"backup","NetStuck.exe")) && File.Exists(Path.Combine(target,"operator-note.txt")));
            Call("UpdateEngine", "Recover", Path.Combine(root,"backup"));
            Check("durable recovery restores original installation after interrupted update", files.All(f=>Hash(Path.Combine(target,f))==before[f]) && File.Exists(Path.Combine(target,"operator-note.txt")));
            using(var locked = new FileStream(Path.Combine(target,"NetStuck.exe"),FileMode.Open,FileAccess.Read,FileShare.Read))
                Check("locked executable cannot produce a successful installation",Reject(()=>Call("UpdateEngine","Install",payload,target,Path.Combine(root,"lockedbackup"),current,null)));

            var utc = new DateTime(2026,1,2,3,4,5,DateTimeKind.Utc);
            var west = TimeZoneInfo.CreateCustomTimeZone("west",TimeSpan.FromHours(-5),"west","west");
            Check("clock uses actual timezone and negative offset",((string)Call("MainForm","ClockText",utc,west)).EndsWith("UTC-05:00"));
            Check("clock handles fractional positive offset",((string)Call("MainForm","ClockText",utc,TimeZoneInfo.CreateCustomTimeZone("east",TimeSpan.FromMinutes(345),"east","east"))).EndsWith("UTC+05:45"));
            using(var form=new MainForm())
            {
                form.AutoScaleMode=AutoScaleMode.None; form.Show(); Pump();
                var all=Flat(form).ToArray(); var tabs=all.OfType<TabControl>().First(t=>t.TabCount==8);
                Check("update check and install actions present",all.OfType<Button>().Any(b=>b.Name=="checkForUpdates") && all.OfType<Button>().Any(b=>b.Name=="updateNow" && !b.Enabled));
                Check("independent Traceroute column actions present",all.OfType<Button>().Count(b=>b.Name.StartsWith("traceColumns"))==2);
                var trace=(DataGridView)typeof(MainForm).GetField("traceGrid",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                tabs.SelectedIndex = 1; Pump();
                var mouseFlags=BindingFlags.Instance|BindingFlags.NonPublic;
                trace.GetType().GetMethod("OnMouseDown",mouseFlags).Invoke(trace,new object[]{new MouseEventArgs(MouseButtons.Left,1,20,10,0)});
                trace.GetType().GetMethod("OnMouseMove",mouseFlags).Invoke(trace,new object[]{new MouseEventArgs(MouseButtons.Left,0,85,10,0)});
                Check("column dragging sets full-height guideline",Convert.ToInt32(trace.GetType().GetField("guide",mouseFlags).GetValue(trace))==85);
                trace.GetType().GetMethod("OnMouseUp",mouseFlags).Invoke(trace,new object[]{new MouseEventArgs(MouseButtons.Left,1,85,10,0)});
                Check("column guideline clears on mouse release",Convert.ToInt32(trace.GetType().GetField("guide",mouseFlags).GetValue(trace))==-1);
                trace.Columns["Hostname"].Visible=false;
                Call("MainForm","RestoreColumns",trace,new List<string>{"missing-column"});
                Check("invalid saved column list cannot hide all columns",trace.Columns.Cast<DataGridViewColumn>().Any(c=>c.Visible));
                foreach(var size in new[]{new Size(1460,900),new Size(1100,700),new Size(1024,600)})
                {
                    form.MinimumSize=new Size(800,500); form.Size=size; Pump();
                    bool safe=true, inputsFit=true;
                    foreach(TabPage page in tabs.TabPages)
                    {
                        tabs.SelectedTab=page; Pump();
                        foreach(var grid in Flat(page).OfType<DataGridView>()) if(grid.Visible && (grid.Height<90 || grid.Width<100)) safe=false;
                        var viewport=Flat(page).OfType<Panel>().First(p=>p.Name=="responsiveViewport");
                        if(!viewport.AutoScroll) safe=false;
                        foreach(var frame in Flat(page).OfType<Panel>().Where(p=>Convert.ToString(p.Tag)=="TraceInputFrame"))
                            foreach(Control input in frame.Controls)
                                if(input.Right>frame.ClientSize.Width || input.Left<0) { inputsFit=false; Console.WriteLine("EVIDENCE clipped input: "+input.GetType().Name+" "+input.Bounds+" frame="+frame.ClientSize); }
                        if(args.Length>0)
                        {
                            foreach(var status in Flat(form).OfType<StatusStrip>()) foreach(ToolStripItem item in status.Items)
                                if(item.Text.StartsWith("My ")) item.Text="Network identity hidden";
                            string folder=Path.GetFullPath(args[0]); Directory.CreateDirectory(folder);
                            using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size)); bitmap.Save(Path.Combine(folder,size.Width+"x"+size.Height+"-"+tabs.SelectedIndex+".png")); }
                        }
                    }
                    Check("all result grids remain accessible at "+size,safe);
                    Check("Traceroute inputs fit their frames at "+size,inputsFit);
                }
                var methods=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(MainForm).GetMethod("SaveAppState",methods).Invoke(form,null);
                form.Close();
            }
            using(var restored=new MainForm())
            {
                var trace=(DataGridView)typeof(MainForm).GetField("traceGrid",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(restored);
                Check("Traceroute column selection survives restart",!trace.Columns["Hostname"].Visible);
            }
        }
        catch(Exception ex) { Check("unexpected maintenance exception "+ex,false); }
        finally { try { Directory.Delete(root,true); } catch { Check("owned temporary cleanup",false); } }
        Console.WriteLine("Failures: "+failed); return failed==0?0:1;
    }
}
