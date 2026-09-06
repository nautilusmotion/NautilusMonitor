// NauTilus Monitor - Motor de sensores
// Filosofia: lo fiable se lee con APIs nativas de Windows (independientes del
// idioma del sistema); los extras (por-nucleo, disco, GPU, temperaturas) se
// intentan con contadores de rendimiento o LibreHardwareMonitor y se degradan
// con elegancia si el equipo no los expone.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NautilusMotion.Monitor
{
    // ---- Lecturas puntuales ----
    public sealed class TempReading { public string Name; public double Celsius; }
    public sealed class FanReading  { public string Name; public int Rpm; }

    // ---- Instantanea de una muestra ----
    public sealed class Snapshot
    {
        public double CpuTotal;                 // 0-100
        public double[] CpuCores;               // por procesador logico (puede ser null)
        public double CpuClockMhz;              // efectiva (0 = desconocida)
        public double RamUsedGB;
        public double RamTotalGB;
        public double RamUsedPct;
        public double GpuLoadPct = -1;          // -1 = desconocida
        public double DiskActivePct = -1;       // -1 = desconocida
        public double DiskReadMBs = -1;
        public double DiskWriteMBs = -1;
        public double NetDownKBs;
        public double NetUpKBs;
        public int ProcCount;
        public TimeSpan Uptime;
        public List<TempReading> Temps = new List<TempReading>();
        public List<FanReading> Fans = new List<FanReading>();
    }

    // ---- Datos fijos del equipo (se leen una vez) ----
    public sealed class StaticInfo
    {
        public string CpuName = "";
        public int Cores;
        public int Threads;
        public double BaseClockGhz;
        public string GpuName = "";
        public double RamTotalGB;
        public string Os = "";
        public string Board = "";

        public static StaticInfo GetOnce()
        {
            var s = new StaticInfo();
            s.Threads = Environment.ProcessorCount;
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed FROM Win32_Processor"))
                    foreach (ManagementObject mo in mos.Get())
                    {
                        s.CpuName = Clean(AsString(mo["Name"]));
                        s.Cores = AsInt(mo["NumberOfCores"]);
                        int lp = AsInt(mo["NumberOfLogicalProcessors"]);
                        if (lp > 0) s.Threads = lp;
                        int mhz = AsInt(mo["MaxClockSpeed"]);
                        if (mhz > 0) s.BaseClockGhz = mhz / 1000.0;
                        break;
                    }
            }
            catch { }

            try
            {
                var names = new List<string>();
                using (var mos = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                    foreach (ManagementObject mo in mos.Get())
                    {
                        string n = Clean(AsString(mo["Name"]));
                        if (!string.IsNullOrEmpty(n)) names.Add(n);
                    }
                s.GpuName = string.Join(" / ", names.ToArray());
            }
            catch { }

            try
            {
                var m = new Native.MEMORYSTATUSEX();
                if (Native.GlobalMemoryStatusEx(m))
                    s.RamTotalGB = Math.Round(m.ullTotalPhys / 1073741824.0, 1);
            }
            catch { }

            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT Caption,BuildNumber FROM Win32_OperatingSystem"))
                    foreach (ManagementObject mo in mos.Get())
                    {
                        s.Os = Clean(AsString(mo["Caption"]));
                        string b = AsString(mo["BuildNumber"]);
                        if (!string.IsNullOrEmpty(b)) s.Os += "  ·  build " + b;
                        break;
                    }
            }
            catch { }

            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT Manufacturer,Product FROM Win32_BaseBoard"))
                    foreach (ManagementObject mo in mos.Get())
                    {
                        s.Board = Clean((AsString(mo["Manufacturer"]) + " " + AsString(mo["Product"])).Trim());
                        break;
                    }
            }
            catch { }

            return s;
        }

        private static string Clean(string s) { return s == null ? "" : System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim(); }
        private static string AsString(object o) { return o == null ? "" : o.ToString(); }
        private static int AsInt(object o) { int n; return (o != null && int.TryParse(o.ToString(), out n)) ? n : 0; }
    }

    // ---- Muestreador en vivo ----
    public sealed class Sampler
    {
        private long[] _idlePrev, _kernPrev, _userPrev; // por procesador logico
        private long _netRxPrev, _netTxPrev;
        private DateTime _netPrevTime;
        private bool _cpuPrimed, _netPrimed;

        // Contadores de rendimiento opcionales (pueden fallar segun el idioma/version)
        private PerformanceCounter _diskActive, _diskRead, _diskWrite;
        private bool _diskTried, _diskOk;

        public void Warmup()
        {
            ReadCpuTimes(out _idlePrev, out _kernPrev, out _userPrev);
            _cpuPrimed = _idlePrev != null;
            ReadNetTotals(out _netRxPrev, out _netTxPrev);
            _netPrevTime = DateTime.UtcNow;
            _netPrimed = true;
            TryInitDisk();
        }

        public Snapshot Sample()
        {
            var s = new Snapshot();

            // ---- CPU total y por nucleo (ntdll, independiente del idioma) ----
            long[] idle, kern, user;
            ReadCpuTimes(out idle, out kern, out user);
            if (idle != null && _cpuPrimed && _idlePrev != null && idle.Length == _idlePrev.Length)
            {
                int n = idle.Length;
                var cores = new double[n];
                double totBusy = 0, totAll = 0;
                for (int i = 0; i < n; i++)
                {
                    double dIdle = idle[i] - _idlePrev[i];
                    double dAll = (kern[i] - _kernPrev[i]) + (user[i] - _userPrev[i]); // kernel ya incluye idle
                    double busy = dAll - dIdle;
                    cores[i] = dAll > 0 ? Clamp(100.0 * busy / dAll) : 0;
                    totBusy += busy; totAll += dAll;
                }
                s.CpuCores = cores;
                s.CpuTotal = totAll > 0 ? Clamp(100.0 * totBusy / totAll) : 0;
            }
            if (idle != null) { _idlePrev = idle; _kernPrev = kern; _userPrev = user; _cpuPrimed = true; }

            // ---- Reloj efectivo (powrprof, independiente del idioma) ----
            s.CpuClockMhz = ReadCurrentMhz();

            // ---- RAM (kernel32, independiente del idioma) ----
            try
            {
                var m = new Native.MEMORYSTATUSEX();
                if (Native.GlobalMemoryStatusEx(m))
                {
                    s.RamTotalGB = m.ullTotalPhys / 1073741824.0;
                    s.RamUsedGB = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
                    s.RamUsedPct = m.dwMemoryLoad;
                }
            }
            catch { }

            // ---- Red (NetworkInformation, independiente del idioma) ----
            try
            {
                long rx, tx;
                ReadNetTotals(out rx, out tx);
                DateTime now = DateTime.UtcNow;
                double secs = (now - _netPrevTime).TotalSeconds;
                if (_netPrimed && secs > 0.05)
                {
                    double d = Math.Max(0, rx - _netRxPrev);
                    double u = Math.Max(0, tx - _netTxPrev);
                    s.NetDownKBs = d / secs / 1024.0;
                    s.NetUpKBs = u / secs / 1024.0;
                }
                _netRxPrev = rx; _netTxPrev = tx; _netPrevTime = now; _netPrimed = true;
            }
            catch { }

            // ---- Disco (contadores; best-effort) ----
            if (_diskOk)
            {
                try
                {
                    s.DiskActivePct = Clamp(_diskActive.NextValue());
                    s.DiskReadMBs = _diskRead.NextValue() / 1048576.0;
                    s.DiskWriteMBs = _diskWrite.NextValue() / 1048576.0;
                }
                catch { s.DiskActivePct = -1; s.DiskReadMBs = -1; s.DiskWriteMBs = -1; }
            }

            // ---- Procesos y uptime ----
            try { s.ProcCount = System.Diagnostics.Process.GetProcesses().Length; } catch { }
            try { s.Uptime = TimeSpan.FromMilliseconds(Native.GetTickCount64()); } catch { }

            // ---- Temperaturas / ventiladores / GPU ----
            if (AdvancedSensors.Enabled)
            {
                AdvancedSensors.Read(s);
            }
            else
            {
                LiteTemps.Read(s); // WMI ACPI + nvidia-smi (best-effort)
            }

            return s;
        }

        private void TryInitDisk()
        {
            if (_diskTried) return;
            _diskTried = true;
            try
            {
                _diskActive = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
                _diskRead = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                _diskWrite = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
                _diskActive.NextValue(); _diskRead.NextValue(); _diskWrite.NextValue(); // ceba
                _diskOk = true;
            }
            catch { _diskOk = false; }
        }

        // ---- CPU: NtQuerySystemInformation(SystemProcessorPerformanceInformation=8) ----
        private static void ReadCpuTimes(out long[] idle, out long[] kern, out long[] user)
        {
            idle = null; kern = null; user = null;
            try
            {
                int cpus = Environment.ProcessorCount;
                int stride = Marshal.SizeOf(typeof(Native.SPPI));
                int size = stride * cpus;
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    int ret;
                    int status = Native.NtQuerySystemInformation(8, buf, size, out ret);
                    if (status != 0) return;
                    int count = ret / stride;
                    if (count <= 0) count = cpus;
                    idle = new long[count]; kern = new long[count]; user = new long[count];
                    for (int i = 0; i < count; i++)
                    {
                        var p = (Native.SPPI)Marshal.PtrToStructure(new IntPtr(buf.ToInt64() + (long)i * stride), typeof(Native.SPPI));
                        idle[i] = p.IdleTime;
                        kern[i] = p.KernelTime;
                        user[i] = p.UserTime;
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { idle = null; kern = null; user = null; }
        }

        // ---- Reloj: CallNtPowerInformation(ProcessorInformation=11) ----
        private static double ReadCurrentMhz()
        {
            try
            {
                int cpus = Environment.ProcessorCount;
                int stride = Marshal.SizeOf(typeof(Native.PPI));
                int size = stride * cpus;
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    uint status = Native.CallNtPowerInformation(11, IntPtr.Zero, 0, buf, (uint)size);
                    if (status != 0) return 0;
                    double maxMhz = 0;
                    for (int i = 0; i < cpus; i++)
                    {
                        var p = (Native.PPI)Marshal.PtrToStructure(new IntPtr(buf.ToInt64() + (long)i * stride), typeof(Native.PPI));
                        if (p.CurrentMhz > maxMhz) maxMhz = p.CurrentMhz;
                    }
                    return maxMhz;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { return 0; }
        }

        private static void ReadNetTotals(out long rx, out long tx)
        {
            rx = 0; tx = 0;
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    IPv4InterfaceStatistics st = ni.GetIPv4Statistics();
                    rx += st.BytesReceived;
                    tx += st.BytesSent;
                }
            }
            catch { }
        }

        private static double Clamp(double v) { return v < 0 ? 0 : (v > 100 ? 100 : v); }
    }

    // ---- Temperaturas "Lite" (sin driver): WMI ACPI + nvidia-smi ----
    internal static class LiteTemps
    {
        private static bool _nvChecked, _nvPresent;
        private static string _nvPath;

        // Cache: las lecturas caras (WMI + nvidia-smi) se refrescan cada 2,5 s.
        private static DateTime _last = DateTime.MinValue;
        private static List<TempReading> _cacheTemps = new List<TempReading>();
        private static double _cacheGpu = -1;

        public static void Read(Snapshot s)
        {
            if ((DateTime.UtcNow - _last).TotalMilliseconds < 2500)
            {
                foreach (TempReading t in _cacheTemps) s.Temps.Add(t);
                if (_cacheGpu >= 0) s.GpuLoadPct = _cacheGpu;
                return;
            }

            var temps = new List<TempReading>();
            double gpu = -1;

            // Temperatura de zona termica ACPI (rara vez fiable, pero gratis)
            try
            {
                using (var mos = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                    foreach (ManagementObject mo in mos.Get())
                    {
                        object o = mo["CurrentTemperature"];
                        if (o == null) continue;
                        double deci; // decimas de Kelvin
                        if (!double.TryParse(o.ToString(), out deci)) continue;
                        double c = deci / 10.0 - 273.15;
                        if (c > 0 && c < 130)
                        {
                            temps.Add(new TempReading { Name = "ACPI", Celsius = Math.Round(c, 0) });
                            break;
                        }
                    }
            }
            catch { }

            // GPU NVIDIA via nvidia-smi (si esta instalado el driver)
            try
            {
                string path = FindNvidiaSmi();
                if (path != null)
                {
                    string outp = RunQuick(path, "--query-gpu=temperature.gpu,utilization.gpu --format=csv,noheader,nounits", 1500);
                    if (!string.IsNullOrEmpty(outp))
                    {
                        string line = outp.Split('\n')[0].Trim();
                        string[] parts = line.Split(',');
                        if (parts.Length >= 1)
                        {
                            double t;
                            if (double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out t) && t > 0)
                                temps.Add(new TempReading { Name = "GPU", Celsius = Math.Round(t, 0) });
                        }
                        if (parts.Length >= 2)
                        {
                            double l;
                            if (double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out l))
                                gpu = l;
                        }
                    }
                }
            }
            catch { }

            // Guarda en cache y aplica a la instantanea actual
            _cacheTemps = temps;
            _cacheGpu = gpu;
            _last = DateTime.UtcNow;
            foreach (TempReading t in temps) s.Temps.Add(t);
            if (gpu >= 0) s.GpuLoadPct = gpu;
        }

        private static string FindNvidiaSmi()
        {
            if (_nvChecked) return _nvPresent ? _nvPath : null;
            _nvChecked = true;
            try
            {
                string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
                string p1 = Path.Combine(sys, "nvidia-smi.exe");
                if (File.Exists(p1)) { _nvPresent = true; _nvPath = p1; return p1; }
                string pf = Environment.GetEnvironmentVariable("ProgramW6432");
                if (string.IsNullOrEmpty(pf)) pf = Environment.GetEnvironmentVariable("ProgramFiles");
                if (!string.IsNullOrEmpty(pf))
                {
                    string p2 = Path.Combine(pf, @"NVIDIA Corporation\NVSMI\nvidia-smi.exe");
                    if (File.Exists(p2)) { _nvPresent = true; _nvPath = p2; return p2; }
                }
            }
            catch { }
            return null;
        }

        private static string RunQuick(string file, string args, int timeoutMs)
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

    // ---- Sensores avanzados: puente por reflexion a LibreHardwareMonitorLib ----
    // No hay dependencia en tiempo de compilacion: si la DLL (MIT) esta junto al
    // .exe se carga y da temperaturas, ventiladores, cargas y relojes reales.
    public static class AdvancedSensors
    {
        public static bool Enabled;                 // sensores activos y funcionando
        public static string Status = "off";        // off | dll_missing | error | ok

        private static object _computer;
        private static object[] _hardware;
        private static MethodInfo _hwUpdate;
        private static PropertyInfo _hwSensors, _hwSubHw, _hwName, _hwType;
        private static PropertyInfo _snType, _snName, _snValue;

        public static string DllPath()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(dir, "LibreHardwareMonitorLib.dll");
        }

        public static bool DllPresent() { try { return File.Exists(DllPath()); } catch { return false; } }

        // Intenta activar los sensores avanzados. Devuelve true si quedaron listos.
        public static bool Enable()
        {
            if (Enabled) return true;
            if (!DllPresent()) { Status = "dll_missing"; return false; }
            try
            {
                Assembly asm = Assembly.LoadFrom(DllPath());
                Type tComputer = asm.GetType("LibreHardwareMonitor.Hardware.Computer");
                _computer = Activator.CreateInstance(tComputer);
                SetBool(tComputer, _computer, "IsCpuEnabled", true);
                SetBool(tComputer, _computer, "IsGpuEnabled", true);
                SetBool(tComputer, _computer, "IsMemoryEnabled", true);
                SetBool(tComputer, _computer, "IsMotherboardEnabled", true);
                SetBool(tComputer, _computer, "IsStorageEnabled", true);
                tComputer.GetMethod("Open").Invoke(_computer, null);

                PropertyInfo hwProp = tComputer.GetProperty("Hardware");
                var hwEnum = (System.Collections.IEnumerable)hwProp.GetValue(_computer, null);
                var list = new List<object>();
                foreach (object h in hwEnum) list.Add(h);
                _hardware = list.ToArray();

                Type tHw = asm.GetType("LibreHardwareMonitor.Hardware.IHardware");
                Type tSn = asm.GetType("LibreHardwareMonitor.Hardware.ISensor");
                _hwUpdate = tHw.GetMethod("Update");
                _hwSensors = tHw.GetProperty("Sensors");
                _hwSubHw = tHw.GetProperty("SubHardware");
                _hwName = tHw.GetProperty("Name");
                _hwType = tHw.GetProperty("HardwareType");
                _snType = tSn.GetProperty("SensorType");
                _snName = tSn.GetProperty("Name");
                _snValue = tSn.GetProperty("Value");

                Enabled = true; Status = "ok";
                return true;
            }
            catch { Status = "error"; Enabled = false; return false; }
        }

        public static void Read(Snapshot s)
        {
            if (!Enabled || _hardware == null) return;
            try
            {
                double gpuLoadMax = -1;
                foreach (object hw in _hardware)
                    gpuLoadMax = ReadHardware(hw, s, gpuLoadMax);
                if (gpuLoadMax >= 0) s.GpuLoadPct = gpuLoadMax;
            }
            catch { }
        }

        private static double ReadHardware(object hw, Snapshot s, double gpuLoadMax)
        {
            try
            {
                _hwUpdate.Invoke(hw, null);
                string hwName = ToStr(_hwName.GetValue(hw, null));
                string hwType = ToStr(_hwType.GetValue(hw, null));
                bool isGpu = hwType.IndexOf("Gpu", StringComparison.OrdinalIgnoreCase) >= 0;

                var sensors = (System.Collections.IEnumerable)_hwSensors.GetValue(hw, null);
                if (sensors != null)
                    foreach (object sn in sensors)
                    {
                        string type = ToStr(_snType.GetValue(sn, null));
                        string name = ToStr(_snName.GetValue(sn, null));
                        object v = _snValue.GetValue(sn, null);
                        if (v == null) continue;
                        double val = Convert.ToDouble(v);

                        if (type == "Temperature" && val > 0 && val < 150)
                            s.Temps.Add(new TempReading { Name = Trim(hwName, name), Celsius = Math.Round(val, 0) });
                        else if (type == "Fan" && val > 0)
                            s.Fans.Add(new FanReading { Name = Trim(hwName, name), Rpm = (int)Math.Round(val) });
                        else if (type == "Load" && isGpu && name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                            { if (val > gpuLoadMax) gpuLoadMax = val; }
                    }

                var subs = (System.Collections.IEnumerable)_hwSubHw.GetValue(hw, null);
                if (subs != null)
                    foreach (object sub in subs)
                        gpuLoadMax = ReadHardware(sub, s, gpuLoadMax);
            }
            catch { }
            return gpuLoadMax;
        }

        private static void SetBool(Type t, object obj, string prop, bool val)
        {
            try { PropertyInfo p = t.GetProperty(prop); if (p != null && p.CanWrite) p.SetValue(obj, val, null); }
            catch { }
        }

        private static string ToStr(object o) { return o == null ? "" : o.ToString(); }

        private static string Trim(string hw, string sensor)
        {
            if (string.IsNullOrEmpty(sensor)) return hw;
            if (string.IsNullOrEmpty(hw)) return sensor;
            return hw + " · " + sensor;
        }
    }

    // ---- P/Invoke ----
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SPPI
        {
            public long IdleTime;
            public long KernelTime;   // incluye IdleTime
            public long UserTime;
            public long DpcTime;
            public long InterruptTime;
            public uint InterruptCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PPI
        {
            public uint Number;
            public uint MaxMhz;
            public uint CurrentMhz;
            public uint MhzLimit;
            public uint MaxIdleState;
            public uint CurrentIdleState;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public sealed class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [DllImport("ntdll.dll")]
        public static extern int NtQuerySystemInformation(int infoClass, IntPtr info, int length, out int returnLength);

        [DllImport("powrprof.dll")]
        public static extern uint CallNtPowerInformation(int level, IntPtr inBuf, uint inSize, IntPtr outBuf, uint outSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX m);

        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();
    }
}
