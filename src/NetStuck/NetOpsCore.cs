using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NetStuck
{
    public sealed class TargetSpec
    {
        public string Host { get; set; }
        public string Description { get; set; }
        public override string ToString() { return Host + (String.IsNullOrWhiteSpace(Description) ? "" : " " + Description); }
    }

    public sealed class SubnetResult
    {
        public string Address { get; set; }
        public int Prefix { get; set; }
        public string Mask { get; set; }
        public string Wildcard { get; set; }
        public string Network { get; set; }
        public string Broadcast { get; set; }
        public string FirstUsable { get; set; }
        public string LastUsable { get; set; }
        public ulong Total { get; set; }
        public ulong Usable { get; set; }
        public bool IsPrivate { get; set; }
        public string AddressClass { get; set; }
    }

    public static class NetOpsCore
    {
        static readonly Regex IPv4Regex = new Regex(@"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])", RegexOptions.Compiled);
        static readonly Regex MacRegex = new Regex(@"(?i)(?<![0-9a-f])(?:(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}|(?:[0-9a-f]{4}\.){2}[0-9a-f]{4}|[0-9a-f]{12})(?![0-9a-f])", RegexOptions.Compiled);

        public static List<TargetSpec> ParseTargets(string input)
        {
            var result = new List<TargetSpec>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in (input ?? "").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int comment = line.IndexOf(" #", StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment).Trim();
                Match match = Regex.Match(line, @"^(\S+)(?:\s+(.+))?$");
                if (!match.Success) continue;
                string host = match.Groups[1].Value.Trim();
                string description = match.Groups[2].Success ? match.Groups[2].Value.Trim() : "";
                if (host.EndsWith("=")) host = host.TrimEnd('=').Trim();
                if (description.StartsWith("=")) description = description.Substring(1).Trim();
                if (host.Length == 0 || !seen.Add(host)) continue;
                result.Add(new TargetSpec { Host = host, Description = description });
            }
            return result;
        }

        public static List<TargetSpec> ExpandTargets(string input, int maxTargets)
        {
            if (maxTargets < 1) throw new ArgumentOutOfRangeException("maxTargets");
            var expanded = new List<TargetSpec>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TargetSpec item in ParseTargets(input))
            {
                if (!item.Host.Contains("/"))
                {
                    if (seen.Add(item.Host)) expanded.Add(item);
                    continue;
                }

                SubnetResult subnet = CalculateSubnet(item.Host);
                uint network = IpToUInt(IPAddress.Parse(subnet.Network));
                uint broadcast = IpToUInt(IPAddress.Parse(subnet.Broadcast));
                uint first = subnet.Prefix >= 31 ? network : network + 1;
                uint last = subnet.Prefix >= 31 ? broadcast : broadcast - 1;
                ulong hostCount = (ulong)last - first + 1;
                if ((ulong)expanded.Count + hostCount > (ulong)maxTargets)
                    throw new InvalidOperationException("Target expansion exceeds the safety limit of " + maxTargets + " hosts. Split the network into smaller CIDR blocks.");

                string description = String.IsNullOrWhiteSpace(item.Description) ? "CIDR " + item.Host : item.Description;
                for (uint value = first; ; value++)
                {
                    string host = UIntToIp(value);
                    if (seen.Add(host)) expanded.Add(new TargetSpec { Host = host, Description = description });
                    if (value == last) break;
                }
            }
            return expanded;
        }

        public static List<string> ExtractIPv4(string input)
        {
            var result = new List<string>();
            var seen = new HashSet<string>();
            foreach (Match m in IPv4Regex.Matches(input ?? ""))
            {
                IPAddress ip;
                if (IPAddress.TryParse(m.Value, out ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && seen.Add(ip.ToString()))
                    result.Add(ip.ToString());
            }
            return result;
        }

        public static List<string> ExtractMacs(string input)
        {
            var result = new List<string>();
            var seen = new HashSet<string>();
            foreach (Match m in MacRegex.Matches(input ?? ""))
            {
                string hex = Regex.Replace(m.Value, "[^0-9A-Fa-f]", "").ToUpperInvariant();
                if (hex.Length != 12) continue;
                string normalized = String.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
                if (seen.Add(normalized)) result.Add(normalized);
            }
            return result;
        }

        public static SubnetResult CalculateSubnet(string rawInput)
        {
            if (String.IsNullOrWhiteSpace(rawInput)) throw new ArgumentException("Enter an IPv4 address and prefix or subnet mask.");
            string raw = Regex.Replace(rawInput.Trim(), @"\s+", " ");
            string ipText;
            int prefix;
            if (raw.Contains("/"))
            {
                string[] parts = raw.Split('/');
                if (parts.Length != 2) throw new FormatException("Invalid CIDR format.");
                ipText = parts[0].Trim();
                if (!Int32.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out prefix))
                    throw new FormatException("Invalid prefix.");
            }
            else
            {
                string[] parts = raw.Split(' ');
                if (parts.Length != 2) throw new FormatException("Use address/prefix or address subnet-mask.");
                ipText = parts[0];
                IPAddress maskAddress;
                if (!IPAddress.TryParse(parts[1], out maskAddress) || maskAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    throw new FormatException("Invalid subnet mask.");
                prefix = MaskToPrefix(maskAddress);
            }
            IPAddress address;
            if (!IPAddress.TryParse(ipText, out address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                throw new FormatException("Invalid IPv4 address.");
            if (prefix < 0 || prefix > 32) throw new ArgumentOutOfRangeException("prefix", "Prefix must be 0–32.");

            byte[] bytes = address.GetAddressBytes();
            uint value = BytesToUInt(bytes);
            uint mask = prefix == 0 ? 0u : UInt32.MaxValue << (32 - prefix);
            uint network = value & mask;
            uint broadcast = network | ~mask;
            ulong total = 1UL << (32 - prefix);
            ulong usable = prefix == 32 ? 1UL : prefix == 31 ? 2UL : total - 2UL;

            return new SubnetResult
            {
                Address = address.ToString(),
                Prefix = prefix,
                Mask = UIntToIp(mask),
                Wildcard = UIntToIp(~mask),
                Network = UIntToIp(network),
                Broadcast = UIntToIp(broadcast),
                FirstUsable = prefix >= 31 ? UIntToIp(network) : UIntToIp(network + 1),
                LastUsable = prefix >= 31 ? UIntToIp(broadcast) : UIntToIp(broadcast - 1),
                Total = total,
                Usable = usable,
                IsPrivate = IsPrivate(address),
                AddressClass = bytes[0] < 128 ? "A" : bytes[0] < 192 ? "B" : bytes[0] < 224 ? "C" : bytes[0] < 240 ? "D (multicast)" : "E"
            };
        }

        public static double ConvertUnit(double value, string from, string to)
        {
            var factors = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "bit", 1d }, { "Kbit", 1e3 }, { "Mbit", 1e6 }, { "Gbit", 1e9 }, { "Tbit", 1e12 },
                { "Byte", 8d }, { "KB", 8e3 }, { "MB", 8e6 }, { "GB", 8e9 }, { "TB", 8e12 },
                { "KiB", 8d * 1024 }, { "MiB", 8d * 1024 * 1024 }, { "GiB", 8d * 1024 * 1024 * 1024 }
            };
            if (!factors.ContainsKey(from) || !factors.ContainsKey(to)) throw new ArgumentException("Unknown unit.");
            return value * factors[from] / factors[to];
        }

        public static string CsvEscape(object value)
        {
            string text = value == null || value == DBNull.Value ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
            if (text.Contains("\"") || text.Contains(",") || text.Contains("\r") || text.Contains("\n"))
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            return text;
        }

        static int MaskToPrefix(IPAddress mask)
        {
            string bits = String.Join("", mask.GetAddressBytes().Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
            if (bits.Contains("01")) throw new FormatException("Subnet mask must be contiguous.");
            return bits.Count(c => c == '1');
        }

        static uint BytesToUInt(byte[] b) { return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3]; }
        static uint IpToUInt(IPAddress address) { return BytesToUInt(address.GetAddressBytes()); }
        static string UIntToIp(uint v) { return String.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.{3}", (v >> 24) & 255, (v >> 16) & 255, (v >> 8) & 255, v & 255); }
        static bool IsPrivate(IPAddress ip)
        {
            byte[] b = ip.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        }
    }
}
