using System.Drawing;
using MadsKristensen.ImageOptimizer.Resizing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class ResizingTests
    {
        [TestMethod]
        public void ResizeImage_AppliesDimensionsDpiAndPixelContent()
        {
            using (var source = new Bitmap(2, 2))
            {
                using (Graphics graphics = Graphics.FromImage(source))
                {
                    graphics.Clear(Color.FromArgb(255, 20, 40, 60));
                }

                using (Bitmap resized = ResizingDialog.ResizeImage(source, 8, 6, 144))
                {
                    Assert.AreEqual(8, resized.Width);
                    Assert.AreEqual(6, resized.Height);
                    Assert.AreEqual(144f, resized.HorizontalResolution, 0.1f);
                    Assert.AreEqual(144f, resized.VerticalResolution, 0.1f);

                    Color pixel = resized.GetPixel(4, 3);
                    Assert.AreEqual(20, pixel.R, 1);
                    Assert.AreEqual(40, pixel.G, 1);
                    Assert.AreEqual(60, pixel.B, 1);
                    Assert.AreEqual(255, pixel.A);
                }
            }
        }
    }
}
