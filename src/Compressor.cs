using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MadsKristensen.ImageOptimizer
{
    class Compressor
    {
        private static readonly string[] _supported = { ".png", ".jpg", ".jpeg", ".gif" };

        public async Task<CompressionResult> CompressFileAsync(string fileName, bool lossy)
        {
            string targetFile = Path.ChangeExtension(Path.GetTempFileName(), Path.GetExtension(fileName));

            ProcessStartInfo start = new ProcessStartInfo("cmd")
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), @"Resources\Tools\"),
                Arguments = GetArguments(fileName, targetFile, lossy),
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var process = new Process();
            process.StartInfo = start;
            process.Start();
            await process.WaitForExitAsync();

            return new CompressionResult(fileName, targetFile);
        }

        private static string GetArguments(string sourceFile, string targetFile, bool lossy)
        {
            if (!Uri.IsWellFormedUriString(sourceFile, UriKind.RelativeOrAbsolute) && !File.Exists(sourceFile))
                return null;

            string ext;

            try
            {
                ext = Path.GetExtension(sourceFile).ToLowerInvariant();
            }
            catch (ArgumentException ex)
            {
                Logger.Log(ex);
                return null;
            }

            switch (ext)
            {
                case ".png":
                    if (lossy)
                        return string.Format(CultureInfo.CurrentCulture, "/c png-lossy.cmd \"{0}\" \"{1}\"", sourceFile, targetFile);

                    return string.Format(CultureInfo.CurrentCulture, "/c png-lossless.cmd \"{0}\" \"{1}\"", sourceFile, targetFile);

                case ".jpg":
                case ".jpeg":
                    if (lossy)
                        return string.Format(CultureInfo.CurrentCulture, "/c cjpeg -quality 80,60 -dct float -restart 5 -smooth 5 -outfile \"{1}\" \"{0}\"", sourceFile, targetFile);

                    return string.Format(CultureInfo.CurrentCulture, "/c jpegtran -copy none -optimize -progressive -outfile \"{1}\" \"{0}\"", sourceFile, targetFile);

                case ".gif":
                    return string.Format(CultureInfo.CurrentCulture, "/c gifsicle --no-comments --no-extensions --no-names --optimize=3 --batch \"{0}\" --output=\"{1}\"", sourceFile, targetFile);
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
