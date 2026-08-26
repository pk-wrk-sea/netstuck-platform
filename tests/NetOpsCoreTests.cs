using System;
using System.Linq;
using NetStuck;

static class NetOpsCoreTests
{
    static int failures;

    static void Check(string name, bool condition)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (!condition) failures++;
    }

    static int Main()
    {
        var targets = NetOpsCore.ParseTargets("10.0.0.1 SW-01\r\nabc.local Application VIP\r\n10.0.0.1 duplicate\r\n# comment\r\n");
        Check("target count and duplicate removal", targets.Count == 2);
        Check("target description", targets[0].Host == "10.0.0.1" && targets[0].Description == "SW-01");
        Check("hostname target", targets[1].Host == "abc.local" && targets[1].Description == "Application VIP");

        var macs = NetOpsCore.ExtractMacs("junk 00:11:22:33:44:55 x 0011.2233.4455 x AABBCCDDEEFF");
        Check("MAC extraction formats", macs.SequenceEqual(new[] { "00:11:22:33:44:55", "AA:BB:CC:DD:EE:FF" }));

        var ips = NetOpsCore.ExtractIPv4("8.8.8.8 invalid 999.1.1.1 and 1.1.1.1");
        Check("IPv4 validation", ips.SequenceEqual(new[] { "8.8.8.8", "1.1.1.1" }));

        var cidr = NetOpsCore.CalculateSubnet("192.168.1.99/24");
        Check("CIDR network", cidr.Network == "192.168.1.0" && cidr.Broadcast == "192.168.1.255" && cidr.Usable == 254);
        var cidrSpace = NetOpsCore.CalculateSubnet("192.168.1.99 /24");
        Check("spaced CIDR", cidrSpace.Network == "192.168.1.0");
        var mask = NetOpsCore.CalculateSubnet("10.10.10.5 255.255.255.252");
        Check("subnet mask input", mask.Prefix == 30 && mask.Network == "10.10.10.4" && mask.Broadcast == "10.10.10.7");
        var hostRoute = NetOpsCore.CalculateSubnet("10.0.0.1/32");
        Check("/32 calculation", hostRoute.Total == 1 && hostRoute.Usable == 1);
        var sweep = NetOpsCore.ExpandTargets("10.100.10.0/24 Branch LAN", 1024);
        Check("/24 target expansion count", sweep.Count == 254);
        Check("/24 excludes network and broadcast", sweep.First().Host == "10.100.10.1" && sweep.Last().Host == "10.100.10.254");
        Check("CIDR description preserved", sweep.All(t => t.Description == "Branch LAN"));
        var pointToPoint = NetOpsCore.ExpandTargets("192.0.2.10/31 P2P", 10);
        Check("/31 includes both addresses", pointToPoint.Count == 2 && pointToPoint[0].Host == "192.0.2.10" && pointToPoint[1].Host == "192.0.2.11");
        bool limitRejected = false;
        try { NetOpsCore.ExpandTargets("10.0.0.0/16", 1024); } catch (InvalidOperationException) { limitRejected = true; }
        Check("CIDR safety limit", limitRejected);

        Check("unit conversion", Math.Abs(NetOpsCore.ConvertUnit(1000, "Mbit", "Gbit") - 1d) < 0.0000001);
        Check("CSV escaping", NetOpsCore.CsvEscape("a,\"b\"") == "\"a,\"\"b\"\"\"");

        Console.WriteLine("Failures: " + failures);
        return failures == 0 ? 0 : 1;
    }
}
