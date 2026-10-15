namespace nvidiaProfileInspector.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    // Best-effort ring log for dropdown/profile diagnostics. Never throws.
    internal static class DropdownTrace
    {
        private const int Capacity = 200;
        private static readonly object _sync = new object();
        private static readonly Queue<string> _ring = new Queue<string>();
        private static string _path;
        private static bool _headerWritten;

        public static void Log(string message)
        {
            try
            {
                var line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message;
                lock (_sync)
                {
                    _ring.Enqueue(line);
                    while (_ring.Count > Capacity)
                        _ring.Dequeue();

                    if (_path == null)
                    {
                        var product = "nvidiaProfileInspector";
                        try
                        {
                            product = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(
                                Assembly.GetExecutingAssembly(), typeof(AssemblyProductAttribute))).Product;
                        }
                        catch { }

                        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), product, "Logs");
                        Directory.CreateDirectory(dir);
                        _path = Path.Combine(dir, "dropdown-trace.log");
                    }

                    if (!_headerWritten)
                    {
                        File.AppendAllText(_path, "=== session " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===" + Environment.NewLine);
                        _headerWritten = true;
                    }

                    File.AppendAllText(_path, line + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
