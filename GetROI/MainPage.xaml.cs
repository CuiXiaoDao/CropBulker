using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace GetROI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private IReadOnlyList<StorageFile> fileList;
        private int count, totalImageNumber;
        private uint cropRegionWidth, cropRegionHeight, widthRatio, heightRatio, wheelIncrement;
        private double imageScale = 1;
        private bool noMoreImage;
        private StorageFolder clippedFolder;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void OpenPictureButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear previous returned folder name, if it exists, between iterations of this scenario
            FolderPicker folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add(".png");
            folderPicker.FileTypeFilter.Add(".jpg");
            folderPicker.FileTypeFilter.Add(".jpeg");
            folderPicker.FileTypeFilter.Add(".bmp");
            StorageFolder folder = await folderPicker.PickSingleFolderAsync();

            if (folder != null)
            {
                clippedFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync("Cliped" + folder.DisplayName, CreationCollisionOption.GenerateUniqueName);
                fileList = await folder.GetFilesAsync();
                totalImageNumber = fileList.Count;
                count = 0;                
                //getDefaultCropRegionSize();
                await GotoNextHandGesture();
            }
        }

        private async void notifyUser(string str)
        {
            var message = new Windows.UI.Popups.MessageDialog(str);
            await message.ShowAsync();
        }

        private async void NextImage_Click(object sender, PointerRoutedEventArgs e)
        {
            if (!noMoreImage)//判断有误剩余图片
            {
                var ptr = e.GetCurrentPoint(ContentCanvas);
                if (ptr.Properties.IsLeftButtonPressed)
                {
                    await SaveCroppedBitmapAsync();
                }
                await GotoNextHandGesture();
            }
            else
            {
                OutputTextBlock.Text = "未选择图片所在文件夹或所选文件夹中图片已经全部截图";
            }
        }

        private async Task SaveCroppedBitmapAsync()
        {
            Point p = CropRegion.TransformToVisual(ContentCanvas).TransformPoint(new Point(0, 0));

            // Convert start point and size to integer.
            uint startPointX = (uint)Math.Floor(p.X * imageScale);
            uint startPointY = (uint)Math.Floor(p.Y * imageScale);

            StorageFile originalImageFile = fileList[count - 1];
            StorageFile newImageFile = await clippedFolder.CreateFileAsync(OutputTextBlock.Tag.ToString() + ".jpg", CreationCollisionOption.GenerateUniqueName);

            using (IRandomAccessStream originalImgFileStream = await originalImageFile.OpenReadAsync())
            {
                // Create a decoder from the stream. With the decoder, we can get
                // the properties of the image.
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(originalImgFileStream);

                // Refine the start point and the size.
                if (startPointX + cropRegionWidth > decoder.PixelWidth)
                {
                    startPointX = decoder.PixelWidth - cropRegionWidth;
                }

                if (startPointY + cropRegionHeight > decoder.PixelHeight)
                {
                    startPointY = decoder.PixelHeight - cropRegionHeight;
                }

                // Get the cropped pixels.
                byte[] pixels = await GetPixelData(decoder, startPointX, startPointY, cropRegionWidth, cropRegionHeight,
                    decoder.PixelWidth, decoder.PixelHeight);

                using (IRandomAccessStream newImgFileStream = await newImageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    // Create a bitmap encoder
                    BitmapEncoder bmpEncoder = await BitmapEncoder.CreateAsync(
                        BitmapEncoder.JpegEncoderId,
                        newImgFileStream);

                    // Set the pixel data to the cropped image.
                    bmpEncoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Straight,
                        cropRegionWidth,
                        cropRegionHeight,
                        decoder.DpiX,
                        decoder.DpiY,
                        pixels);

                    // Flush the data to file.
                    await bmpEncoder.FlushAsync();
                }
            }
        }

        private async static Task<byte[]> GetPixelData(BitmapDecoder decoder, uint startPointX, uint startPointY,
            uint width, uint height, uint scaledWidth, uint scaledHeight)
        {
            BitmapTransform transform = new BitmapTransform();
            BitmapBounds bounds = new BitmapBounds();
            bounds.X = startPointX;
            bounds.Y = startPointY;
            bounds.Height = height;
            bounds.Width = width;
            transform.Bounds = bounds;
            transform.ScaledWidth = scaledWidth;
            transform.ScaledHeight = scaledHeight;

            // Get the cropped pixels within the bounds of transform.
            PixelDataProvider pix = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.ColorManageToSRgb);
            byte[] pixels = pix.DetachPixelData();
            return pixels;
        }

        private async Task GotoNextHandGesture()
        {
            if (count < totalImageNumber)
            {
                using (IRandomAccessStream imageStream = await fileList[count].OpenReadAsync())
                {
                    BitmapImage handPicture = new BitmapImage();
                    handPicture.SetSource(imageStream);
                    handGesture.Source = handPicture;

                    if (handPicture.PixelHeight > ImageGrid.ActualHeight || handPicture.PixelWidth > ImageGrid.ActualWidth)
                    {
                        imageScale = Math.Max(handPicture.PixelHeight / ImageGrid.ActualHeight,
                            handPicture.PixelWidth / ImageGrid.ActualWidth);
                        handGesture.Stretch = Windows.UI.Xaml.Media.Stretch.Uniform;
                    }
                    else
                    {
                        imageScale = 1;
                        handGesture.Stretch = Windows.UI.Xaml.Media.Stretch.None;
                    }
                    ContentCanvas.Height = handPicture.PixelHeight / imageScale;
                    ContentCanvas.Width = handPicture.PixelWidth / imageScale;
                }

                OutputTextBlock.Text = "目前图片： " + fileList[count].Name;
                OutputTextBlock.Tag = fileList[count].DisplayName;
                count++;
                CropRegion.Visibility = Visibility.Visible;
                setCropRegionScaledSize();
            }
            else
            {
                noMoreImage = true;
                CropRegion.Visibility = Visibility.Collapsed;
            }
        }

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            Pointer ptr = e.Pointer;
            if (ptr.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                // To get mouse state, we need extended pointer details.
                // We get the pointer info through the getCurrentPoint method
                // of the event argument.
                Windows.UI.Input.PointerPoint ptrPt = e.GetCurrentPoint(ContentCanvas);
                double newLeft = ptrPt.Position.X - 0.5 * CropRegion.ActualWidth;
                double newTop = ptrPt.Position.Y - 0.5 * CropRegion.ActualHeight;

                Canvas.SetLeft(CropRegion, Math.Min(Math.Max(newLeft,0), handGesture.ActualWidth - CropRegion.ActualWidth));
                Canvas.SetTop(CropRegion, Math.Min(Math.Max(newTop, 0), handGesture.ActualHeight - CropRegion.ActualHeight));
            }
        }

        private void CropRegionSizeInfo_TextChanged(object sender, TextChangedEventArgs e)
        {
            getDefaultCropRegionSize();
            setCropRegionScaledSize();
        }

        private void WheelIncrementTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            wheelIncrement = uint.Parse(WheelIncrementTextBox.Text);
        }

        private void ContentCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var ptr = e.GetCurrentPoint(ContentCanvas);
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {                
                var deltaLength = ptr.Properties.MouseWheelDelta / 120 * wheelIncrement;  //120 per delta

                uint newWidth = (uint)(cropRegionWidth + deltaLength * widthRatio);
                var newHeight = (uint)(cropRegionHeight + deltaLength * heightRatio);
                if (newWidth > 0 && newHeight > 0 )
                {                   
                    cropRegionWidth = newWidth;
                    cropRegionHeight = newHeight;
                    setCropRegionScaledSize();
                }
            }
        }

        private void CropOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CropOption.SelectedIndex == 0)
            {
                FixedRatioOption.Visibility = Visibility.Visible;
                FixedSizeOption.Visibility = Visibility.Collapsed;
                wheelIncrement = uint.Parse(WheelIncrementTextBox.Text);
            }
            else
            {
                FixedRatioOption.Visibility = Visibility.Collapsed;
                FixedSizeOption.Visibility = Visibility.Visible;
            }
            getDefaultCropRegionSize();
        }

        private void getDefaultCropRegionSize()
        {
            if (CropOption.SelectedIndex == 0)
            {
                widthRatio = uint.Parse(WidthRationTextBox.Text);
                heightRatio = uint.Parse(HeightRationTextBox.Text);

                cropRegionWidth = uint.Parse(DefaultCropRegionLength.Text);
                cropRegionHeight = (uint)(cropRegionWidth / widthRatio * heightRatio);
            }
            else
            {
                cropRegionWidth = uint.Parse(CropRegionWidth.Text);
                cropRegionHeight = uint.Parse(CropRegionHeight.Text);
            }            
        }

        private void setCropRegionScaledSize()
        {
            CropRegion.Width = cropRegionWidth / imageScale;
            CropRegion.Height = cropRegionHeight / imageScale;
        }
    }
}