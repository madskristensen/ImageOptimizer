using BracketPipe;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MadsKristensen.ImageOptimizer
{
    public class Compressor
    {
        private static readonly string[] _supported = { ".png", ".jpg", ".jpeg", ".gif", ".svg" };
        private readonly string _cwd;

        public Compressor()
        {
            _cwd = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), @"Resources\Tools\");
        }

        public Compressor(string cwd)
        {
            _cwd = cwd;
        }

        public CompressionResult CompressFile(string fileName, bool lossy)
        {
            string fileExtension = Path.GetExtension(fileName);
            string targetFile = Path.ChangeExtension(Path.GetTempFileName(), fileExtension);
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (fileExtension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string source = File.ReadAllText(fileName);
                    string minified = Html.Minify(source);
                    File.WriteAllText(targetFile, minified);
                }
                catch (Exception)
                {
                    return new CompressionResult(fileName, targetFile, stopwatch.Elapsed);
                }
            }
            else
            {
                ProcessStartInfo start = new ProcessStartInfo("cmd")
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = _cwd,
                    Arguments = GetArguments(fileName, targetFile, lossy),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (Process process = Process.Start(start))
                {
                    process.WaitForExit();
                }
            }

            stopwatch.Stop();

            return new CompressionResult(fileName, targetFile, stopwatch.Elapsed);
        }

        private static string GetArguments(string sourceFile, string targetFile, bool lossy)
        {
            if (!Uri.IsWellFormedUriString(sourceFile, UriKind.RelativeOrAbsolute) && !File.Exists(sourceFile))
            {
                return null;
            }

            string ext;

            try
            {
                ext = Path.GetExtension(sourceFile).ToLowerInvariant();
            }
            catch (ArgumentException ex)
            {
                Debug.Write(ex);
                return null;
            }

            switch (ext)
            {
                case ".png":
                    File.Copy(sourceFile, targetFile);

                    return lossy ? $"/c pingo -s4 -q \"{targetFile}\"" : $"/c pingo -lossless -s4 -q \"{targetFile}\"";

                case ".jpg":
                case ".jpeg":
                    File.Copy(sourceFile, targetFile);
                    return lossy ? $"/c pingo -s4 -q \"{targetFile}\"" : $"/c pingo -lossless -s4 -q \"{targetFile}\"";

                case ".gif":
                    return lossy
                        ? $"/c gifsicle -O3 --lossy \"{sourceFile}\" --output=\"{targetFile}\""
                        : $"/c gifsicle -O3 \"{sourceFile}\" --output=\"{targetFile}\"";
            }

            return null;
        }

        public static bool IsFileSupported(string fileName)
        {
            string ext = Path.GetExtension(fileName);

            return _supported.Any(s => s.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}
