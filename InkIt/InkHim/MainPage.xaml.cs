using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// Die Vorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 dokumentiert.

namespace InkHim
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet werden kann oder auf die innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const int minPenSize = 2;
        const int penSizeIncrement = 2;

        public MainPage()
        {
            InitializeComponent();
            ink.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Touch; //per default just pen
            if(ink.InkPresenter != null)
            {
                InkDrawingAttributes drawingAttributes = ink.InkPresenter.CopyDefaultDrawingAttributes();
                var PenThickness = 1.5;
                var penSize = minPenSize + penSizeIncrement * PenThickness;
                Color inkColor = (Color)Application.Current.Resources["InkColor01"];
                drawingAttributes.Color = inkColor;

                // Ballpoint
                drawingAttributes.Size = new Size(10, 10);
                drawingAttributes.PenTip = PenTipShape.Circle;
                drawingAttributes.DrawAsHighlighter = false;
                drawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.Identity;

                // Calligraphy

                //drawingAttributes.Size = new Size(penSize, penSize * 2);
                //drawingAttributes.PenTip = PenTipShape.Rectangle;
                //drawingAttributes.DrawAsHighlighter = false;               
                //// Set a 45 degree rotation on the pen tip
                //double radians = 45.0 * Math.PI / 180;
                //drawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.CreateRotation((float)radians);

                ink.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
            }
            
        }

        #region Drag and Drop
        /// <summary>
        /// Handle DragOver event, which fires when a user has dragged an item over the app, but not yet dropped it. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            //Customize the UI
            e.DragUIOverride.Caption = "Copy picture here"; // Sets custom UI text
            e.DragUIOverride.IsCaptionVisible = true; // Sets if the caption is visible
            e.DragUIOverride.IsContentVisible = true; // Sets if the dragged content is visible     
        }

        /// <summary>
        /// Process the drop event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var storageFile = items[0] as StorageFile;
                    var bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(await storageFile.OpenAsync(FileAccessMode.Read));
                    // Set the image on the main page to the dropped image
                    Image.Source = bitmapImage;
                }
            }
        }

        #endregion

        #region Upload
        private async void PickAFileButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");
            StorageFile file = await openPicker.PickSingleFileAsync();
            var bitmapImage = new BitmapImage();
            bitmapImage.SetSource(await file.OpenAsync(FileAccessMode.Read));
            if (file != null)
            {
                Image.Source = bitmapImage;
            }
            else
            {
                Debug.WriteLine("Operation cancelled.");
            }
        }
        #endregion

        #region Clear
        private void clearAll_Click(object sender, RoutedEventArgs e)
        {
            ink.InkPresenter.StrokeContainer.Clear();
        }
        #endregion

        #region Save

        private async void save_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("PNG", new List<string>() { ".png" });
            // Default file name if the user does not type one in or select a file to replace
            savePicker.SuggestedFileName = "InkItArt";
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                // Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);
                // write to file
                if (ink.InkPresenter.StrokeContainer.GetStrokes().Count > 0)
                {
                    try
                    {
                        using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            await ink.InkPresenter.StrokeContainer.SaveAsync(stream);

                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                    // Let Windows know that we're finished changing the file so the other app can update the remote version of the file.
                    // Completing updates may require Windows to ask for user input.
                    FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                    if (status == FileUpdateStatus.Complete)
                    {
                        Debug.WriteLine("File saved");
                    }
                    else
                    {
                        Debug.WriteLine("File couldn't be saved");
                    }
                }
                else
                {
                    Debug.WriteLine("Operation canceled");
                }
            }

        }
        #endregion

        #region Recognize
        private async void reco_Click(object sender, RoutedEventArgs e)
        {
            if (ink.InkPresenter.StrokeContainer.GetStrokes().Count > 0)
            {
                var recognizer = new InkRecognizerContainer();
                var recognitionResults = await recognizer.RecognizeAsync(ink.InkPresenter.StrokeContainer, InkRecognitionTarget.All);

                string result = "";
                //justACanvas.Children.Clear();

                if (recognitionResults.Count > 0)
                {
                    foreach (var r in recognitionResults)
                    {
                        var candidates = r.GetTextCandidates();
                        result += " " + candidates[0];
                        
                    }
                }
                else
                    result = "NO TEXT RECOGNIZED!";

                RecoResult.Text = result;
            }
        }

        #endregion
    }
}
