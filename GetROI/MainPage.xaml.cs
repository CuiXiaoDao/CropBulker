using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
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
        private List<StorageFile> imageList;
        private int count, totalImageNumber;
        private uint cropRegionWidth, cropRegionHeight, wheelIncrement;
        private uint widthRatio = 1, heightRatio = 1;
        private double imageScale = 1;
        private List<string> supportedFileType = new List<string> { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico" };
        private StorageFolder clippedFolder;
        private Rect outerRect = new Rect(0, 0, 100, 100);
        private Rect selectedRect = new Rect(0, 0, 10, 10);
        private readonly ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView("Resources");

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
                clippedFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync("Cliped" +
                    folder.DisplayName, CreationCollisionOption.GenerateUniqueName);

                imageList = new List<StorageFile>();
                foreach (var file in await folder.GetFilesAsync())
                {
                    if (supportedFileType.Contains(file.FileType))
                    {
                        imageList.Add(file);
                    }
                }

                count = 0;
                totalImageNumber = imageList.Count;

                if (totalImageNumber > 0)
                {
                    await GotoNextHandGesture();
                    SelectionLayer.Visibility = Visibility.Visible;
                }
                else
                {
                    await notifyUser(resourceLoader.GetString("NoImageFound"));
                }
            }
        }

        private async Task notifyUser(string str)
        {
            var message = new Windows.UI.Popups.MessageDialog(str);
            await message.ShowAsync();
        }

        private async void NextImage_Click(object sender, PointerRoutedEventArgs e)
        {
            count++;
            var ptr = e.GetCurrentPoint(ContentCanvas);
            if (count <= totalImageNumber && ptr.Properties.IsLeftButtonPressed)
            {
                await SaveCroppedBitmapAsync();
            }

            if (count < totalImageNumber)//判断有误剩余图片
            {
                await GotoNextHandGesture();
            }
            else
            {
                SelectionLayer.Visibility = Visibility.Collapsed;
                CropInfoTextBlock.Text = resourceLoader.GetString("NoMoreImage");
            }
        }

        private async Task GotoNextHandGesture()
        {
            using (IRandomAccessStream imageStream = await imageList[count].OpenReadAsync())
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

                ContentCanvas.Height = outerRect.Height = handPicture.PixelHeight / imageScale;
                ContentCanvas.Width = outerRect.Width = handPicture.PixelWidth / imageScale;
                OuterRect.Rect = outerRect;
            }

            CurrentImageInfo.Text = string.Format(resourceLoader.GetString("CurrentImageInfo"),
                imageList[count].Name);
            setCropRegionScaledSize();
        }

        private void ContentCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            Pointer ptr = e.Pointer;
            if (ptr.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                // To get mouse state, we need extended pointer details.
                // We get the pointer info through the getCurrentPoint method
                // of the event argument.
                Windows.UI.Input.PointerPoint ptrPt = e.GetCurrentPoint(ContentCanvas);
                double newLeft = ptrPt.Position.X - 0.5 * selectedRect.Width;
                double newTop = ptrPt.Position.Y - 0.5 * selectedRect.Height;

                selectedRect.X = Math.Min(Math.Max(newLeft, 0), handGesture.ActualWidth - selectedRect.Width);
                selectedRect.Y = Math.Min(Math.Max(newTop, 0), handGesture.ActualHeight - selectedRect.Height);
                SelectedRect.Rect = selectedRect;
            }
        }

        private void ContentCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var ptr = e.GetCurrentPoint(ContentCanvas);
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                var deltaLength = ptr.Properties.MouseWheelDelta / 120 * wheelIncrement;
                uint newWidth = (uint)(cropRegionWidth + deltaLength * widthRatio);
                var newHeight = (uint)(cropRegionHeight + deltaLength * heightRatio);
                if (newWidth > 0 && newHeight > 0)
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
                ContentCanvas.PointerWheelChanged += ContentCanvas_PointerWheelChanged;
            }
            else
            {
                FixedRatioOption.Visibility = Visibility.Collapsed;
                FixedSizeOption.Visibility = Visibility.Visible;
                ContentCanvas.PointerWheelChanged -= ContentCanvas_PointerWheelChanged;
            }
            getDefaultCropRegionSize();
            setCropRegionScaledSize();
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
            selectedRect.Width = cropRegionWidth / imageScale;
            selectedRect.Height = cropRegionHeight / imageScale;
            SelectedRect.Rect = selectedRect;

            CropInfoTextBlock.Text = string.Format(resourceLoader.GetString("CropInfo"),
                cropRegionWidth, cropRegionHeight);
        }

        private async Task SaveCroppedBitmapAsync()
        {
            // Convert start point and size to integer.
            uint startPointX = (uint)Math.Floor(selectedRect.X * imageScale);
            uint startPointY = (uint)Math.Floor(selectedRect.Y * imageScale);

            StorageFile originalImageFile = imageList[count - 1];
            StorageFile newImageFile = await clippedFolder.CreateFileAsync(originalImageFile.DisplayName + ".jpg",
                CreationCollisionOption.GenerateUniqueName);

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
    }
}