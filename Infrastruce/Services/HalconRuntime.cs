using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Infrastruce.Services
{
    public static class HalconRuntime
    {
        private static bool _initialized;
        private static IntPtr _halconLibHandle;

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static void Initialize(string? halconPath = null)
        {
            if (_initialized) return;

            string binDir = ResolveBinDirectory(halconPath);

            SetDllDirectory(binDir);

            var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";
            if (!currentPath.Contains(binDir, StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("PATH", binDir + ";" + currentPath, EnvironmentVariableTarget.Process);
            }

            var halconDll = Path.Combine(binDir, "halcon.dll");
            if (File.Exists(halconDll))
            {
                try
                {
                    _halconLibHandle = NativeLibrary.Load(halconDll);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to load halcon.dll from '{halconDll}'. " +
                        $"Please ensure HALCON is properly installed and the path is correct.", ex);
                }
            }

            PreLoadDll(binDir, "hAcqGigEVision2.dll");

            _initialized = true;
        }

        private static string ResolveBinDirectory(string? halconPath)
        {
            if (!string.IsNullOrWhiteSpace(halconPath) && Directory.Exists(halconPath))
            {
                if (File.Exists(Path.Combine(halconPath, "halcon.dll")))
                {
                    return halconPath;
                }

                var binX64 = Path.Combine(halconPath, "bin", "x64-win64");
                if (File.Exists(Path.Combine(binX64, "halcon.dll")))
                {
                    return binX64;
                }

                var binNet = Path.Combine(halconPath, "bin");
                if (File.Exists(Path.Combine(binNet, "halcon.dll")))
                {
                    return binNet;
                }

                return halconPath;
            }

            var defaultRoot = @"E:\ProgramFiles\MVTec\HALCON-24.11-Progress-Steady";
            var defaultBin = Path.Combine(defaultRoot, "bin", "x64-win64");
            if (Directory.Exists(defaultBin))
            {
                return defaultBin;
            }

            var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                          ?? AppDomain.CurrentDomain.BaseDirectory;
            return baseDir;
        }

        private static void PreLoadDll(string directory, string dllName)
        {
            var dllPath = Path.Combine(directory, dllName);
            if (!File.Exists(dllPath)) return;

            try
            {
                NativeLibrary.Load(dllPath);
            }
            catch
            {
            }
        }
    }
}