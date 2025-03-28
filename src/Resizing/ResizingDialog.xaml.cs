using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Controls;

using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Threading;

namespace MadsKristensen.ImageOptimizer.Resizing
{
    public partial class ResizingDialog : DialogWindow
    {
        private readonly string _imageFilePath;
        private float _width, _height;

        public ResizingDialog(string imageFilePath)
        {
            _imageFilePath = imageFilePath;
            InitializeComponent();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            using (System.Drawing.Image image = Bitmap.FromFile(_imageFilePath))
            {
                _width = image.Width;
                _height = image.Height;

                tbWidth.Text = image.Width.ToString();
                tbHeight.Text = image.Height.ToString();
                tbDpi.Text = Math.Round(image.HorizontalResolution, 1).ToString();
            }

            base.OnContentRendered(e);
        }

        private void OnWidthTextChanged(object sender, TextChangedEventArgs e)
        {
            if (tbWidth.IsFocused && cbAspectRatio.IsChecked == true && float.TryParse(tbWidth.Text, out var width))
            {
                var ratio = width / _width;
                tbHeight.Text = ((int)(_height * ratio)).ToString();
            }
        }

        private void OnHeightTextChanged(object sender, TextChangedEventArgs e)
        {
            if (tbHeight.IsFocused && cbAspectRatio.IsChecked == true && double.TryParse(tbHeight.Text, out var height))
            {
                var ratio = height / _height;
                tbWidth.Text = ((int)(_width * ratio)).ToString();
            }
        }

        private void OnGotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.SelectAll();
            }
        }

        private void btnOk_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (int.TryParse(tbWidth.Text, out var width) && int.TryParse(tbHeight.Text, out var height) && float.TryParse(tbDpi.Text, out var dpi))
            {
                VS.StatusBar.ShowMessageAsync($"Resizing {Path.GetFileName(_imageFilePath)}...").FireAndForget();
                ResizeAsync(width, height, dpi).FireAndForget();

                DialogResult = true;
                Close();
            }
            else
            {
                _ = VS.MessageBox.ShowError("Only numeric values are allowed");
            }
        }

        private async Task ResizeAsync(int width, int height, float dpi)
        {
            await TaskScheduler.Default;

            try
            {
                using (System.Drawing.Image image = Bitmap.FromFile(_imageFilePath))
                using (Bitmap resizedImage = ResizeImage(image, width, height, dpi))
                {
                    image.Dispose();
                    resizedImage.Save(_imageFilePath);
                }

                var compressor = new Compressor();
                CompressionResult result = compressor.CompressFile(_imageFilePath, CompressionType.Lossless);

                if (result.Saving > 0)
                {
                    File.Copy(result.ResultFileName, result.OriginalFileName, true);
                }

                await VS.StatusBar.ShowMessageAsync($"{Path.GetFileName(_imageFilePath)} was resized to {width}x{height} at {dpi} DPI");
            }
            catch (Exception ex)
            {
                await VS.StatusBar.ShowMessageAsync(ex.Message);
                await ex.LogAsync();
            }
        }

        public static Bitmap ResizeImage(System.Drawing.Image image, int width, int height, float dpi)
        {
            var destinationRect = new Rectangle(0, 0, width, height);
            var destinationImage = new Bitmap(width, height);

            destinationImage.SetResolution(dpi, dpi);

            using (var graphics = Graphics.FromImage(destinationImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destinationRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destinationImage;
        }
    }
}
