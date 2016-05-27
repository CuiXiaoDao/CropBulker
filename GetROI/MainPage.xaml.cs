using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace GetROI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        IReadOnlyList<StorageFile> fileList;
        int count=0;
        int totalImageNumber = 0;
        BitmapImage handPicture = new BitmapImage();
        StorageFolder clippedFolder;

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
                await GotoNextHandGesture();
                ////test
                //StringBuilder outputText = new StringBuilder();
                //outputText.AppendLine("Files:");
                //foreach (StorageFile file in fileList)
                //{
                //    outputText.Append(file.Name + "\n");
                //}
                //OutputTextBlock.Text = outputText.ToString();
                ////test end

            }
            else
            {
                OutputTextBlock.Text = "Operation cancelled.";
            }

        }

        private async void NextImage_Click(object sender, PointerRoutedEventArgs e)
        {
            //if (fileList != null)
            if (CropRegion.Visibility==Visibility.Visible)//CropRegion.Visibility相当于一个bool值，判断有误剩余图片
            {              
                var ptr = e.GetCurrentPoint(ContentGrid);
                if (ptr.Properties.IsLeftButtonPressed)
                {
                    await SaveCroppedBitmapAsync();
                }                   
                await GotoNextHandGesture();
            }
            else
            {
                OutputTextBlock.Text = "请先选择图片所在文件夹再进行裁剪";
            }
        }

        async public Task SaveCroppedBitmapAsync()
        {
            GeneralTransform gt = CropRegion.TransformToVisual(ContentGrid);
            Point p = gt.TransformPoint(new Point(0, 0));

            // Convert start point and size to integer.
            uint startPointX = (uint)Math.Floor(p.X);
            uint startPointY = (uint)Math.Floor(p.Y);
            uint height = (uint)Math.Floor(CropRegion.Height);
            uint width = (uint)Math.Floor(CropRegion.Width);

            StorageFile originalImageFile = fileList[count - 1];
            StorageFile newImageFile = await clippedFolder.CreateFileAsync(OutputTextBlock.Tag.ToString()+".jpg", CreationCollisionOption.GenerateUniqueName);

            using (IRandomAccessStream originalImgFileStream = await originalImageFile.OpenReadAsync())
            {


                // Create a decoder from the stream. With the decoder, we can get 
                // the properties of the image.
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(originalImgFileStream);

                // Refine the start point and the size. 
                if (startPointX + width > decoder.PixelWidth)
                {
                    startPointX = decoder.PixelWidth - width;
                }

                if (startPointY + height > decoder.PixelHeight)
                {
                    startPointY = decoder.PixelHeight - height;
                }

                // Get the cropped pixels.
                byte[] pixels = await GetPixelData(decoder, startPointX, startPointY, width, height,
                    decoder.PixelWidth, decoder.PixelHeight);

                using (IRandomAccessStream newImgFileStream = await newImageFile.OpenAsync(FileAccessMode.ReadWrite))
                {

                    Guid encoderID = Guid.Empty;

                    switch (newImageFile.FileType.ToLower())
                    {
                        case ".png":
                            encoderID = BitmapEncoder.PngEncoderId;
                            break;
                        case ".bmp":
                            encoderID = BitmapEncoder.BmpEncoderId;
                            break;
                        default:
                            encoderID = BitmapEncoder.JpegEncoderId;
                            break;
                    }

                    // Create a bitmap encoder

                    BitmapEncoder bmpEncoder = await BitmapEncoder.CreateAsync(
                        encoderID,
                        newImgFileStream);

                    // Set the pixel data to the cropped image.
                    bmpEncoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Straight,
                        width,
                        height,
                        decoder.DpiX,
                        decoder.DpiY,
                        pixels);

                    // Flush the data to file.
                    await bmpEncoder.FlushAsync();
                }
            }

        }

        async static private Task<byte[]> GetPixelData(BitmapDecoder decoder, uint startPointX, uint startPointY,
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


        private async Task<bool> GotoNextHandGesture()
        {
            if (count < totalImageNumber && totalImageNumber != 0)
            {
                CropRegion.Visibility = Visibility.Visible;
                handGesture.Clip = null;

                IRandomAccessStream imageStream = await fileList[count].OpenReadAsync();
                handPicture.SetSource(imageStream);
                //handPicture.UriSource = new Uri("ms-appx:///Assets/Logo.scale-100.png");
                handGesture.Width = handPicture.PixelWidth;
                handGesture.Height = handPicture.PixelHeight;
                Canvas.SetLeft(handGesture, 0);
                Canvas.SetTop(handGesture, 0);

                handGesture.Source = handPicture;
                ContentGrid.Width = handPicture.PixelWidth;
                ContentGrid.Height = handPicture.PixelHeight;

                OutputTextBlock.Text = "目前图片名称： " + fileList[count].Name;
                OutputTextBlock.Tag = fileList[count].DisplayName;
                count++;
                return true;
            }
            else
            {
                CropRegion.Visibility = Visibility.Collapsed;
                return false;
            }
            
        }

        private void Grid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            Windows.UI.Xaml.Input.Pointer ptr = e.Pointer;
            if (ptr.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                // To get mouse state, we need extended pointer details.
                // We get the pointer info through the getCurrentPoint method
                // of the event argument. 
               
                Windows.UI.Input.PointerPoint ptrPt = e.GetCurrentPoint(ContentGrid);
                 Canvas.SetLeft(CropRegion, Math.Min(ptrPt.Position.X, handGesture.ActualWidth));
                 Canvas.SetTop(CropRegion, Math.Min(ptrPt.Position.Y, handGesture.ActualHeight));
            }        
        }

        private void ContentGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var ptr = e.GetCurrentPoint(ContentGrid);
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                var deltaLength=ptr.Properties.MouseWheelDelta;
                if (CropRegion.Width + deltaLength/8>0)
                {
                    CropRegion.Width += deltaLength/8;
                    CropRegion.Height += deltaLength/8;
                }
            }
        }
    }
}
