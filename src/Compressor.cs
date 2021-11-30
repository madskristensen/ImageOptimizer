using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MadsKristensen.ImageOptimizer
{
    public class Compressor
    {
        private static readonly string[] _supported = { ".png", ".jpg", ".jpeg", ".gif" };
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
            var targetFile = Path.ChangeExtension(Path.GetTempFileName(), Path.GetExtension(fileName));

            var start = new ProcessStartInfo("cmd")
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = _cwd,
                Arguments = GetArguments(fileName, targetFile, lossy),
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var stopwatch = Stopwatch.StartNew();

            using (var process = Process.Start(start))
            {
                process.WaitForExit();
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

                    if (lossy)
                    {
                        return $"/c pingo -s9 -pngpalette=79 -q \"{targetFile}\"";
                    }
                    else
                    {
                        return $"/c pingo -s9 -q \"{targetFile}\"";
                    }

                case ".jpg":
                case ".jpeg":
                    File.Copy(sourceFile, targetFile);
                    if (lossy)
                    {
                        return $"/c pingo -auto -jpgquality=79 -jpgtype=1 -jpgsub -q \"{targetFile}\"";
                    }
                    else
                    {
                        return $"/c pingo -s9 -q \"{targetFile}\"";
                    }

                case ".gif":
                    if (lossy)
                    {
                        return $"/c gifsicle -O3 --lossy \"{sourceFile}\" --output=\"{targetFile}\"";

                    }
                    else
                    {
                        return $"/c gifsicle -O3 \"{sourceFile}\" --output=\"{targetFile}\"";
                    }
            }

            return null;
        }

        public static bool IsFileSupported(string fileName)
        {
            var ext = Path.GetExtension(fileName);

            return _supported.Any(s => s.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}
