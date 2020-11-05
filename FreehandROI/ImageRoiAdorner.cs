using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FreehandROI
{
    public class ImageRoiAdorner : System.Windows.Documents.Adorner
    {
        const int SNAP_DISTANCE = 5;

        public static DependencyProperty PenProperty = DependencyProperty.RegisterAttached("Pen", typeof(Pen), typeof(ImageRoiAdorner),
            new FrameworkPropertyMetadata(new Pen(new SolidColorBrush(Colors.YellowGreen), 2.0), FrameworkPropertyMetadataOptions.AffectsRender));

        private Rect bounds = Rect.Empty;
        private bool closed = false;
        private Point cursor = new Point();
        private List<Point> pathPoints = new List<Point>();
        private bool showBoundingBox;

        /// <summary>
        /// Creates a new instance of the <see cref="ImageRoiAdorner"/> class.
        /// </summary>
        /// <param name="adornedElement">The <see cref="Image"/> control to adorn.</param>
        /// <param name="showBoundingBox">TRUE to show the bounding box while drawing the ROI; otherwise, FALSE. The default is FALSE.</param>
        public ImageRoiAdorner(Image adornedElement, bool showBoundingBox = false)
            : base(adornedElement)
        {
            this.IsHitTestVisible = true;
            this.showBoundingBox = showBoundingBox;
        }

        /// <summary>
        /// Event raised when the ROI is closed or escaped via right-click.
        /// </summary>
        public event EventHandler<RoiResultEventArgs> RoiComplete;

        private Image AdornedImage
        {
            get { return this.AdornedElement as Image; }
        }

        public static Pen GetPen(Image target) => target.GetValue(PenProperty) as Pen;

        public static void SetPen(Image target, Pen value)
        {
            target.SetValue(PenProperty, value);
        }

        /// <summary>
        /// Get the ROI mask bitmap with a binary palette using the specified colors.
        /// </summary>
        /// <param name="background">The background <see cref="Color"/> value.</param>
        /// <param name="roi">The ROI <see cref="Color"/> value.</param>
        /// <returns>A new <see cref="BitmapSource"/> containing the mask.</returns>
        public async Task<BitmapSource> GetRoiMaskAsync(Color background, Color roi)
        {
            if (!this.closed)
            {
                throw new InvalidOperationException();
            }

            return await Task.Run<BitmapSource>(() =>
            {
                return CreateRoiMask(background, roi);
            });
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            // Get the cursor position
            this.cursor = e.GetPosition(this.AdornedElement);

            if (this.pathPoints.Count > 0)
            {
                this.InvalidateVisual();
            }
        }

        /// <summary>
        /// The user is adding a point to the ROI drawing.
        /// </summary>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance.</param>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (this.closed)
            {
                this.OnMouseRightButtonUp(e);
            }    

            // Get the cursor position relative to the adorned element
            var coord = e.GetPosition(this.AdornedElement);
            
            if (this.pathPoints.Count > 1 && Point.Subtract(this.pathPoints[0], coord).Length <= SNAP_DISTANCE)
            {
                this.closed = true;

                // Raise RoiComplete event for closed region
                this.RoiComplete?.Invoke(this, new RoiResultEventArgs(RoiResult.Closed));
            }
            else
            {
                // Only add the point when not closing the ROI
                this.pathPoints.Add(coord);

                // Update bounds
                this.UpdateBounds(coord);

            }

            this.InvalidateVisual();
        }

        /// <summary>
        /// The user aborted ROI creation.
        /// </summary>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance.</param>
        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            // Clear points and bounds
            this.pathPoints.Clear();
            this.bounds = Rect.Empty;
            this.closed = false;

            this.InvalidateVisual();

            // Raise RoiComplete event for escaped region
            this.RoiComplete?.Invoke(this, new RoiResultEventArgs(RoiResult.Escaped));
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            // Transparent background for hit testing
            drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF)), null, new Rect(this.RenderSize));

            // Draw all points with connected line segments
            for (int i = 0; i < this.pathPoints.Count; i++)
            {
                var point = this.pathPoints[i];

                if (!this.closed)
                {
                    drawingContext.DrawEllipse(null, GetPen(this.AdornedImage), point, SNAP_DISTANCE, SNAP_DISTANCE);
                }

                if (i > 0)
                {
                    drawingContext.DrawLine(GetPen(this.AdornedImage), this.pathPoints[i - 1], this.pathPoints[i]);
                }
            }

            // Draw the last connection if closed
            if (this.closed)
            {
                drawingContext.DrawLine(GetPen(this.AdornedImage), this.pathPoints.Last(), this.pathPoints.First());
            }
            else if (this.pathPoints.Count > 0)
            {
                drawingContext.DrawLine(GetPen(this.AdornedImage), this.pathPoints.Last(), this.cursor);
            }

            if (this.showBoundingBox)
            {
                // Draw bounding box
                drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)), null, this.bounds);
            }
        }

        private static byte ReverseByte(byte input)
        {
            byte result = 0x00;

            for (byte mask = 0x80; Convert.ToInt32(mask) > 0; mask >>= 1)
            {
                // Shift result right 1 bit
                result = (byte)(result >> 1);

                // Bitwise AND to get value of masked bit
                var tempbyte = (byte)(input & mask);
                if (tempbyte != 0x00)
                {
                    // Set most significant digit to 1
                    result = (byte)(result | 0x80);
                }
            }

            return result;
        }

        private BitmapSource CreateRoiMask(Color background, Color roi)
        {
            // Create a PathGeometry containing all of the points
            PathGeometry path = new PathGeometry();
            PathFigure pathFigure = new PathFigure();
            pathFigure.IsClosed = true;
            pathFigure.StartPoint = this.pathPoints.First();
            foreach (var point in this.pathPoints.Skip(1))
            {
                pathFigure.Segments.Add(new LineSegment(point, false));
            }
            pathFigure.Segments.Add(new LineSegment(this.pathPoints.First(), false));
            path.Figures.Add(pathFigure);

            // Calculate byte-aligned stride using 2 bits per pixel
            var pixelFormat = PixelFormats.Indexed1;
            int roiStride = (Convert.ToInt32(this.bounds.Width * pixelFormat.BitsPerPixel) + 7) / 8;

            // Create a BitArray covering the ROI bounds using stride
            BitArray roiMask = new BitArray(Convert.ToInt32(roiStride * 8 * this.bounds.Height), false);

            // Iterate within the ROI bounds and test each coordinate against the drawn ROI
            int roiWidth = Convert.ToInt32(this.bounds.Width);
            int roiHeight = Convert.ToInt32(this.bounds.Height);

            for (int row = 0; row < roiHeight; row++)
            {
                int rowOffset = row * roiStride * 8;
                for (int col = 0; col < roiWidth; col++)
                {
                    // Create Point relative to full image
                    var testPoint = new Point(col + this.bounds.Left, row + this.bounds.Top);

                    // Test coordinate inside the path created by pathPoints
                    if (path.FillContains(testPoint))
                    {
                        int pixelIndex = rowOffset + (col * pixelFormat.BitsPerPixel); // Each pixel is 2 bits
                        roiMask[pixelIndex] = true;
                    }
                }
            }

            // Create a WriteableBitmap with a transparent and white palette
            var palette = new BitmapPalette(new List<Color> { background, roi });
            var imgSize = this.AdornedImage.RenderSize;
            WriteableBitmap maskBitmap = new WriteableBitmap(Convert.ToInt32(imgSize.Width), Convert.ToInt32(imgSize.Height), 96, 96, pixelFormat, palette);

            // Convert the roiMask into a byte array
            byte[] roiMaskBuffer = new byte[roiMask.Length / 8];
            roiMask.CopyTo(roiMaskBuffer, 0);
            // Adjust the roiMaskBuffer by flipping each byte!
            for (int i = 0; i < roiMaskBuffer.Length; i++)
            {
                roiMaskBuffer[i] = ReverseByte(roiMaskBuffer[i]);
            }

            // Update the ROI with the roiMask
            var roiRect = new Int32Rect(Convert.ToInt32(this.bounds.Left), Convert.ToInt32(this.bounds.Top), roiStride * 8 / pixelFormat.BitsPerPixel, Convert.ToInt32(this.bounds.Height));
            maskBitmap.WritePixels(roiRect, roiMaskBuffer, roiStride, 0);
            maskBitmap.Freeze();
            return maskBitmap;
        }

        private void UpdateBounds(Point coord)
        {
            if (Rect.Empty == this.bounds)
            {
                bounds = new Rect(coord, new Size(1, 1));
            }
            else
            {
                this.bounds.Union(coord);
            }
        }
    }
}