using Microsoft.Win32;
using Prism.Commands;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FreehandROI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public static string DrawRoiToolTip => "Click to enable freehand ROI drawing.\nClick again or use the right-mouse button to cancel.";

        private bool isDrawingRoi;
        private ImageRoiAdorner roiAdorner;

        /// <summary>
        /// Creates a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            this.DrawRoiCommand = new DelegateCommand(this.ExecuteDrawRoi, this.CanExecuteDrawRoi);
            this.OpenImageCommand = new DelegateCommand(this.ExecuteOpenImage, this.CanExecuteOpenImage);
            this.SaveMaskCommand = new DelegateCommand(this.ExecuteSaveMask, this.CanExecuteSaveMask);

            this.InitializeComponent();

            this.DataContext = this;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the user is drawing an ROI.
        /// </summary>
        public bool IsDrawingRoi
        {
            get { return this.isDrawingRoi; }
            set
            {
                if (this.isDrawingRoi != value)
                {
                    this.isDrawingRoi = value;
                    this.RaisePropertyChanged(nameof(IsDrawingRoi));
                }
            }
        }

        #region Commands
        /// <summary>
        /// Gets the command to draw an ROI.
        /// </summary>
        public DelegateCommand DrawRoiCommand { get; private set; }


        /// <summary>
        /// Gets the command to open an image file.
        /// </summary>
        public DelegateCommand OpenImageCommand { get; private set; }

        /// <summary>
        /// Gets the command to save the ROI mask.
        /// </summary>
        public DelegateCommand SaveMaskCommand { get; private set; }

        private bool CanExecuteDrawRoi()
        {
            return null != this.img.Source;
        }

        private bool CanExecuteOpenImage()
        {
            return !this.IsDrawingRoi;
        }

        private bool CanExecuteSaveMask()
        {
            return null != this.img.GetValue(OpacityMaskProperty);
        }

        private void ExecuteDrawRoi()
        {
            if (this.isDrawingRoi)
            {
                this.ClearRoiAdorner();
            }
            else
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(this.img);
                if (null != adornerLayer)
                {
                    this.roiAdorner = new ImageRoiAdorner(img);
                    this.roiAdorner.Cursor = Cursors.Cross;
                    this.roiAdorner.RoiComplete += this.OnRoiComplete;
                    adornerLayer.Add(this.roiAdorner);
                    this.img.ForceCursor = true;

                    this.IsDrawingRoi = true;
                }
            }
        }

        private void ExecuteOpenImage()
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.CheckPathExists = true;
            openDialog.Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png";
            var result = openDialog.ShowDialog(this);
            if (result.HasValue && result.Value)
            {
                this.img.Source = new BitmapImage(new Uri(openDialog.FileName));
                ClearRoiAdorner();
            }
        }

        private async void ExecuteSaveMask()
        {
            var mask = await roiAdorner.GetRoiMaskAsync(Colors.Black, Colors.White);

            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.AddExtension = true;
            saveDialog.DefaultExt = ".bmp";
            saveDialog.Filter = "Bitmap Files|*.bmp";
            var result = saveDialog.ShowDialog(this);
            if (result.HasValue && result.Value)
            {
                BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(mask));

                using (var fs = saveDialog.OpenFile())
                {
                    encoder.Save(fs);
                }
            }
        }
        #endregion

        private void ClearRoiAdorner()
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(this.img);
            if (null != this.roiAdorner && null != adornerLayer)
            {
                adornerLayer.Remove(this.roiAdorner);
                this.roiAdorner.RoiComplete -= this.OnRoiComplete;
                this.roiAdorner = null;
            }

            this.img.SetValue(OpacityMaskProperty, null);

            this.IsDrawingRoi = false;

            this.DrawRoiCommand.RaiseCanExecuteChanged();
            this.SaveMaskCommand.RaiseCanExecuteChanged();
        }

        private async void OnRoiComplete(object sender, RoiResultEventArgs e)
        {
            if (e.Result == RoiResult.Closed)
            {
                var roiAdorner = sender as ImageRoiAdorner;
                var mask = await roiAdorner.GetRoiMaskAsync(Colors.Transparent, Color.FromArgb(0xFF, 0x00, 0x00, 0x00));

                // Use mask as opacity for image
                this.img.SetValue(OpacityMaskProperty, new ImageBrush(mask));
                this.SaveMaskCommand.RaiseCanExecuteChanged();
            }
            else
            {
                this.ClearRoiAdorner();
                this.IsDrawingRoi = false;
            }
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (propertyName == nameof(IsDrawingRoi))
            {
                if (!this.IsDrawingRoi)
                {
                    this.ClearRoiAdorner();
                }
            }
        }

        private void RaisePropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            this.OnPropertyChanged(propertyName);
        }
        #endregion
    }
}