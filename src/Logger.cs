// NauTilus Monitor - Registro completo de sensores a CSV.
// Separado de la UI para reutilizarlo y probarlo sin ventana. Vuelca TODO el
// snapshot (cargas, relojes, GPU, disco, red, batería y cada sensor de
// temperatura/ventilador/voltaje/potencia como columna). Usa el separador de
// lista y los decimales del Windows del usuario, para que abra limpio en Excel.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NautilusMotion.Monitor
{
    public sealed class TempLogger
    {
        private readonly object _lock = new object();
        private StreamWriter _w;
        private bool _header;
        private readonly string _sep;

        // Nombres de columna dinámicas capturados al escribir la cabecera.
        private List<string> _tempNames, _fanNames, _voltNames, _powerNames;

        public string Path { get; private set; }
        public bool Active { get; private set; }
        public int IntervalSec = 1;              // segundos entre filas
        private DateTime _lastWrite = DateTime.MinValue;

        public TempLogger() { _sep = Sep(); }

        public bool Start(string path)
        {
            lock (_lock)
            {
                if (Active) return true;
                try
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NauTilus Monitor");
                        Directory.CreateDirectory(dir);
                        path = System.IO.Path.Combine(dir, "sensores-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
                    }
                    else
                    {
                        string d = System.IO.Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                    }
                    _w = new StreamWriter(path, false, new UTF8Encoding(true)); // BOM: Excel detecta UTF-8
                    _w.AutoFlush = true;
                    _header = false;
                    _lastWrite = DateTime.MinValue;
                    Path = path;
                    Active = true;
                    return true;
                }
                catch
                {
                    if (_w != null) { try { _w.Dispose(); } catch { } _w = null; }
                    Active = false;
                    return false;
                }
            }
        }

        public void Write(Snapshot s)
        {
            lock (_lock)
            {
                if (!Active || _w == null || s == null) return;
                DateTime now = DateTime.Now;
                if (_header && (now - _lastWrite).TotalSeconds < IntervalSec - 0.5) return; // respeta el intervalo
                try
                {
                    var ci = CultureInfo.CurrentCulture;
                    if (!_header)
                    {
                        _tempNames = Names(s.Temps); _fanNames = FanNames(s.Fans);
                        _voltNames = SensorNames(s.Volts); _powerNames = SensorNames(s.Powers);
                        var h = new StringBuilder();
                        h.Append(Csv(Loc.T("rec.coltime")));
                        string[] fixedCols = { "CPU %", "CPU MHz", "GPU %", "GPU MEM MB", "GPU W", "RAM %", "RAM GB", "Commit GB", "Disk %", "Read MB/s", "Write MB/s", "Down KB/s", "Up KB/s", "Battery %" };
                        foreach (string c in fixedCols) h.Append(_sep + c);
                        foreach (string n in _tempNames) h.Append(_sep + Csv(n + " (°C)"));
                        foreach (string n in _fanNames) h.Append(_sep + Csv(n + " (RPM)"));
                        foreach (string n in _voltNames) h.Append(_sep + Csv(n + " (V)"));
                        foreach (string n in _powerNames) h.Append(_sep + Csv(n + " (W)"));
                        _w.WriteLine(h.ToString());
                        _header = true;
                    }

                    var r = new StringBuilder();
                    r.Append(now.ToString("yyyy-MM-dd HH:mm:ss"));
                    r.Append(_sep + Num(s.CpuTotal, 0, ci));
                    r.Append(_sep + (s.CpuClockMhz > 0 ? Num(s.CpuClockMhz, 0, ci) : ""));
                    r.Append(_sep + (s.GpuLoadPct >= 0 ? Num(s.GpuLoadPct, 0, ci) : ""));
                    r.Append(_sep + (s.GpuMemUsedMB >= 0 ? Num(s.GpuMemUsedMB, 0, ci) : ""));
                    r.Append(_sep + (s.GpuPowerW >= 0 ? Num(s.GpuPowerW, 1, ci) : ""));
                    r.Append(_sep + Num(s.RamUsedPct, 0, ci));
                    r.Append(_sep + Num(s.RamUsedGB, 1, ci));
                    r.Append(_sep + (s.PageUsedGB >= 0 ? Num(s.PageUsedGB, 1, ci) : ""));
                    r.Append(_sep + (s.DiskActivePct >= 0 ? Num(s.DiskActivePct, 0, ci) : ""));
                    r.Append(_sep + (s.DiskReadMBs >= 0 ? Num(s.DiskReadMBs, 1, ci) : ""));
                    r.Append(_sep + (s.DiskWriteMBs >= 0 ? Num(s.DiskWriteMBs, 1, ci) : ""));
                    r.Append(_sep + Num(s.NetDownKBs, 0, ci));
                    r.Append(_sep + Num(s.NetUpKBs, 0, ci));
                    r.Append(_sep + (s.HasBattery && s.BatteryPercent >= 0 ? s.BatteryPercent.ToString(ci) : ""));

                    foreach (string n in _tempNames) r.Append(_sep + TempVal(s.Temps, n, ci));
                    foreach (string n in _fanNames) r.Append(_sep + FanVal(s.Fans, n, ci));
                    foreach (string n in _voltNames) r.Append(_sep + SensorVal(s.Volts, n, 3, ci));
                    foreach (string n in _powerNames) r.Append(_sep + SensorVal(s.Powers, n, 1, ci));

                    _w.WriteLine(r.ToString());
                    _lastWrite = now;
                }
                catch { }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                Active = false;
                if (_w != null) { try { _w.Flush(); _w.Dispose(); } catch { } _w = null; }
            }
        }

        // ---- utilidades ----
        private static List<string> Names(List<TempReading> l) { var r = new List<string>(); if (l != null) foreach (var t in l) r.Add(t.Name); return r; }
        private static List<string> FanNames(List<FanReading> l) { var r = new List<string>(); if (l != null) foreach (var t in l) r.Add(t.Name); return r; }
        private static List<string> SensorNames(List<SensorReading> l) { var r = new List<string>(); if (l != null) foreach (var t in l) r.Add(t.Name); return r; }

        private static string TempVal(List<TempReading> l, string name, CultureInfo ci) { if (l != null) foreach (var t in l) if (t.Name == name) return Num(t.Celsius, 0, ci); return ""; }
        private static string FanVal(List<FanReading> l, string name, CultureInfo ci) { if (l != null) foreach (var t in l) if (t.Name == name) return t.Rpm.ToString(ci); return ""; }
        private static string SensorVal(List<SensorReading> l, string name, int dec, CultureInfo ci) { if (l != null) foreach (var t in l) if (t.Name == name) return Num(t.Value, dec, ci); return ""; }

        private static string Num(double v, int dec, CultureInfo ci) { return v.ToString(dec == 0 ? "0" : "0." + new string('0', dec), ci); }

        private string Csv(string s)
        {
            if (s == null) return "";
            if (s.IndexOf(_sep, StringComparison.Ordinal) >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static string Sep()
        {
            try { string s = CultureInfo.CurrentCulture.TextInfo.ListSeparator; return string.IsNullOrEmpty(s) ? "," : s; }
            catch { return ","; }
        }
    }
}
