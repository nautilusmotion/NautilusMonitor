// NauTilus Monitor - Salud de disco (SMART) vía la API de almacenamiento de Windows.
// Lee MSFT_PhysicalDisk + reliability counters con PowerShell (una sola vez, no por
// segundo), que da datos limpios: estado, temperatura, horas de encendido y desgaste
// del SSD, sin tener que parsear los atributos SMART crudos.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace NautilusMotion.Monitor
{
    public sealed class DiskInfo
    {
        public string Name = "";
        public string Media = "";   // SSD / HDD / SCM
        public string Bus = "";     // NVMe / SATA / USB / ...
        public double SizeGB;
        public int Health = -1;     // 0 saludable, 1 advertencia, 2/3 en riesgo, -1 desconocido
        public int TempC = -1;
        public long PowerOnHours = -1;
        public int WearPct = -1;    // % de vida usada (SSD)
        public int SpindleRpm = -1; // RPM (HDD)
    }

    public static class DiskHealth
    {
        public static List<DiskInfo> GetAll()
        {
            var list = new List<DiskInfo>();
            try
            {
                // MediaType/BusType/HealthStatus llegan como texto ("HDD","RAID","Healthy")
                // o como número segun el equipo: el switch -Regex acepta ambos.
                string script =
@"$ErrorActionPreference='SilentlyContinue'
function V($x){ if($null -eq $x){''}else{$x} }
Get-PhysicalDisk | ForEach-Object {
  $d=$_
  $rc = $d | Get-StorageReliabilityCounter
  $media = switch -Regex (''+$d.MediaType) {'^(HDD|3)$'{'HDD'} '^(SSD|4)$'{'SSD'} '^(SCM|5)$'{'SCM'} default{''}}
  $bus = switch -Regex (''+$d.BusType) {'^(NVMe|17)$'{'NVMe'} '^(SATA|11)$'{'SATA'} '^(USB|7)$'{'USB'} '^(RAID|8)$'{'RAID'} '^(SAS|10)$'{'SAS'} '^(iSCSI|9)$'{'iSCSI'} '^(SD|12)$'{'SD'} default{''+$d.BusType}}
  $health = switch -Regex (''+$d.HealthStatus) {'^(Healthy|0)$'{0} '^(Warning|1)$'{1} '^(Unhealthy|2|3)$'{2} default{-1}}
  Write-Output ('DISK|NAME=' + (V $d.FriendlyName) + '|MEDIA=' + $media + '|BUS=' + $bus + '|SIZE=' + (V $d.Size) + '|HEALTH=' + $health + '|SPINDLE=' + (V $d.SpindleSpeed) + '|TEMP=' + (V $rc.Temperature) + '|HOURS=' + (V $rc.PowerOnHours) + '|WEAR=' + (V $rc.Wear))
}";
                string tmp = Path.Combine(Path.GetTempPath(), "nautilus_diskhealth.ps1");
                File.WriteAllText(tmp, script, new System.Text.UTF8Encoding(false));
                string outp = RunHidden("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + tmp + "\"", 20000);
                try { File.Delete(tmp); } catch { }

                if (outp != null)
                    foreach (string raw in outp.Split('\n'))
                    {
                        string line = raw.Replace("\r", "").Trim();
                        if (!line.StartsWith("DISK|")) continue;
                        var d = Parse(line.Substring(5));
                        if (d != null) list.Add(d);
                    }
            }
            catch { }
            return list;
        }

        private static DiskInfo Parse(string body)
        {
            try
            {
                var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string tok in body.Split('|'))
                {
                    int eq = tok.IndexOf('=');
                    if (eq > 0) kv[tok.Substring(0, eq)] = tok.Substring(eq + 1);
                }
                var d = new DiskInfo();
                d.Name = Get(kv, "NAME");
                d.Media = Get(kv, "MEDIA");
                d.Bus = Get(kv, "BUS");
                double bytes = ParseD(kv, "SIZE");
                if (bytes > 0) d.SizeGB = bytes / 1000000000.0; // GB comerciales
                d.Health = (int)ParseD(kv, "HEALTH", -1);
                int t = (int)ParseD(kv, "TEMP", -1); if (t > 0 && t < 130) d.TempC = t;
                long h = (long)ParseD(kv, "HOURS", -1); if (h > 0) d.PowerOnHours = h;
                int w = (int)ParseD(kv, "WEAR", -1); if (w >= 0 && w <= 100) d.WearPct = w;
                int rpm = (int)ParseD(kv, "SPINDLE", -1); if (rpm > 0 && rpm < 100000) d.SpindleRpm = rpm;
                if (string.IsNullOrEmpty(d.Name)) return null;
                return d;
            }
            catch { return null; }
        }

        private static string Get(Dictionary<string, string> d, string k) { string v; return d.TryGetValue(k, out v) ? (v ?? "").Trim() : ""; }

        private static double ParseD(Dictionary<string, string> d, string k, double def = 0)
        {
            string v; if (!d.TryGetValue(k, out v) || string.IsNullOrEmpty(v)) return def;
            v = v.Trim().Replace(',', '.');
            double r; return double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out r) ? r : def;
        }

        private static string RunHidden(string file, string args, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo(file, args);
                psi.CreateNoWindow = true; psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(timeoutMs);
                    return outp;
                }
            }
            catch { return null; }
        }
    }
}
