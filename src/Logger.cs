// NauTilus Monitor - Registro de temperaturas a CSV
// Separado de la UI para poder reutilizarlo y probarlo sin ventana.
// Usa el separador de lista y los decimales del Windows del usuario, para que
// el CSV se abra limpio en su Excel (p. ej. ';' y coma decimal en espanol).

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
        private List<string> _names;
        private readonly string _sep;

        public string Path { get; private set; }
        public bool Active { get; private set; }

        public TempLogger() { _sep = Sep(); }

        // path = null -> autonombre en Documentos\NauTilus Monitor\
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
                        path = System.IO.Path.Combine(dir, "temperaturas-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
                    }
                    else
                    {
                        string d = System.IO.Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                    }
                    _w = new StreamWriter(path, false, new UTF8Encoding(true)); // BOM: Excel detecta UTF-8 y respeta acentos
                    _w.AutoFlush = true;
                    _header = false;
                    _names = null;
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
                try
                {
                    var ci = CultureInfo.CurrentCulture;
                    if (!_header)
                    {
                        _names = new List<string>();
                        foreach (TempReading t in s.Temps) _names.Add(t.Name);
                        var h = new StringBuilder();
                        h.Append(Csv(Loc.T("rec.coltime")));
                        h.Append(_sep + "CPU %");
                        h.Append(_sep + "GPU %");
                        h.Append(_sep + "RAM %");
                        foreach (string n in _names) h.Append(_sep + Csv(n + " (°C)"));
                        _w.WriteLine(h.ToString());
                        _header = true;
                    }
                    var row = new StringBuilder();
                    row.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    row.Append(_sep + s.CpuTotal.ToString("0", ci));
                    row.Append(_sep + (s.GpuLoadPct >= 0 ? s.GpuLoadPct.ToString("0", ci) : ""));
                    row.Append(_sep + s.RamUsedPct.ToString("0", ci));
                    foreach (string n in _names)
                    {
                        string v = "";
                        foreach (TempReading t in s.Temps) { if (t.Name == n) { v = t.Celsius.ToString("0", ci); break; } }
                        row.Append(_sep + v);
                    }
                    _w.WriteLine(row.ToString());
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
