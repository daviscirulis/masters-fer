using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Drawing;
using Intel.RealSense.Face;
using Intel.RealSense.Utility;
using Intel.RealSense.Hand;
using Intel.RealSense.HandCursor;
using Intel.RealSense.Segmentation;
using Intel.RealSense;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Controls.Primitives;

namespace FER
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Session session;
        private Projection projection;
        private Thread processingThread;
        private SenseManager senseManager;
        private HandCursorModule cursor;
        private CursorConfiguration cursorConfig;
        private CursorData cursorData;
        private Intel.RealSense.HandCursor.GestureData gestureData;
        private SampleReader reader;
        private bool handWaving;
        private bool handTrigger;
        private int msgTimer;
        private int WIDTH = 640;
        private int HEIGHT = 480;
        private int FRAME_RATE = 0;
        private double DPI_X = 72.0;
        private double DPI_Y = 72.0;
        private Intel.RealSense.Image depth;
        private Intel.RealSense.Image color;
        private Intel.RealSense.PixelFormat colorPixelFormat;
        private Intel.RealSense.PixelFormat depthPixelFormat;
        private ImageData depthData;
        private ImageData colorData;
        private FaceModule faceModule;
        private int maxTrackedFaces = 1;
        private bool detectionEnabled = true;
        private bool landmarksEnabled = true;
        private bool terminate = false;
        private WriteableBitmap colorBitmap;
        private WriteableBitmap depthBitmap;
        enum ImageType { COLOR, DEPTH };
        List<RectI32> boundingBoxes;
        List<LandmarkPoint[]> landmarks;
        List<RectI32> landmarkBoundingBoxes;
        List<System.Windows.Media.Color> colors;
        List<String> depthFormats;
        List<String> colorFormats;
        PopupMenu popupMenu = null;
        LandmarksGroupType landmarkGroup;
        bool extractLandmarkGroup;
        int landmarkOffset;
        bool drawFaceBoundingBox;
        bool drawLandmarkPoints;
        bool drawLandmarkBoundingBox;
        int saveSeriesDelay;
        bool captureImage;
        int seriesToCapture;
        int seriesCaptured;
        bool captureSeries;
        String dirName;
        int skippedFrames;

        public MainWindow()
        {
            InitializeComponent();
            InitVariables();
            InitCamera();

            // Start the worker thread
            processingThread = new Thread(new ThreadStart(ProcessingThread));
            processingThread.Start();

        }

        private void InitVariables()
        {
            this.handWaving = false;
            this.handTrigger = false;
            this.msgTimer = 0;
            this.boundingBoxes = new List<RectI32>();
            this.landmarks = new List<LandmarkPoint[]>();
            this.landmarkBoundingBoxes = new List<RectI32>();
            this.colors = new List<System.Windows.Media.Color>();
            this.depthFormats = new List<String>();
            this.depthFormats.Add("Depth");
            this.depthFormats.Add("Depth Raw");
            this.depthFormats.Add("RGB32");
            this.colorFormats = new List<String>();
            this.colorFormats.Add("RGB32");
            this.colorFormats.Add("RGB24");
            this.landmarkGroup = LandmarksGroupType.LANDMARK_GROUP_JAW;
            this.extractLandmarkGroup = false;
            this.landmarkOffset = 0;
            this.drawFaceBoundingBox = false;
            this.drawLandmarkPoints = false;
            this.drawLandmarkBoundingBox = false;
            this.saveSeriesDelay = 0;
            this.captureImage = false;
            this.captureSeries = false;
            this.seriesCaptured = 0;
            this.seriesToCapture = 0;
            this.streamBox.SelectedIndex = 0;
            this.dirName = "";
            this.colorPixelFormat = Intel.RealSense.PixelFormat.PIXEL_FORMAT_RGB32;
            this.depthPixelFormat = Intel.RealSense.PixelFormat.PIXEL_FORMAT_RGB32;
            this.skippedFrames = 0;
        }

        private void InitCamera()
        {
            session = Session.CreateInstance();
            System.Diagnostics.Debug.WriteLine("Version: " + session.Version.major);

            // Instantiate and initialize the SenseManager
            senseManager = session.CreateSenseManager();


            reader = SampleReader.Activate(senseManager);

            reader.EnableStream(StreamType.STREAM_TYPE_COLOR, WIDTH, HEIGHT, FRAME_RATE, StreamOption.STREAM_OPTION_STRONG_STREAM_SYNC);
            reader.EnableStream(StreamType.STREAM_TYPE_DEPTH, WIDTH, HEIGHT, FRAME_RATE, StreamOption.STREAM_OPTION_STRONG_STREAM_SYNC);


            // Configure the Hand Module
            cursor = HandCursorModule.Activate(senseManager);
            cursorConfig = cursor.CreateActiveConfiguration();
            cursorConfig.EngagementEnabled = true;
            cursorConfig.EnableGesture(GestureType.CURSOR_HAND_OPENING);
            cursorConfig.EnableAllAlerts();
            cursorConfig.ApplyChanges();

            //Configure the Face Module
            faceModule = FaceModule.Activate(senseManager);
            FaceConfiguration faceConfig = faceModule.CreateActiveConfiguration();
            faceConfig.Detection.isEnabled = detectionEnabled;
            faceConfig.Detection.maxTrackedFaces = maxTrackedFaces;
            faceConfig.Landmarks.isEnabled = landmarksEnabled;
            faceConfig.Landmarks.maxTrackedFaces = maxTrackedFaces;
            faceConfig.TrackingMode = Intel.RealSense.Face.TrackingModeType.FACE_MODE_COLOR_PLUS_DEPTH;
            faceConfig.EnableAllAlerts();
            faceConfig.ApplyChanges();

            //init senseManager
            senseManager.Init();
            projection = senseManager.CaptureManager.Device.CreateProjection();

            System.Diagnostics.Debug.WriteLine("IsConnected: " + senseManager.IsConnected());


        }

        private void ProcessingThread()
        {
            // Start AcquireFrame/ReleaseFrame loop
            for (;;)
            {
                if (terminate)
                {
                    System.Diagnostics.Debug.WriteLine("Application window closed, shutting down");
                    break;
                }


                Status status = senseManager.AcquireFrame(true);

                if (status.IsError())
                {
                    System.Diagnostics.Debug.WriteLine("Failed to Acquire Frame: " + status.ToString());
                    break;
                }



                //process gesture
                ProcessGesture();

                //process image streams
                ProcessImages();

                //process face
                ProcessLandmarks();
                CreateLandmarkBoundingBoxes();

                //save image
                if (captureImage)
                {
                    SaveSingleImageToDisk();
                }

                if (captureSeries)
                {
                    if (skippedFrames < saveSeriesDelay)
                    {
                        skippedFrames++;
                    }
                    else
                    {
                        SaveImageSeriesToDisk(dirName);
                        skippedFrames = 0;
                    }

                }

                //Release the frame
                senseManager.ReleaseFrame();


            }

            depth.Dispose();
            color.Dispose();
            cursor.Dispose();
            cursorConfig.Dispose();
            projection.Dispose();
            senseManager.Close();
            session.Dispose();
        }

        private void ProcessLandmarks()
        {
            if (colorBitmap == null)
            {
                return;
            }

            FaceData faceData = faceModule.CreateOutput();
            faceData.Update();
            int numOfFaces = faceData.NumberOfDetectedFaces > maxTrackedFaces ? maxTrackedFaces : faceData.NumberOfDetectedFaces;

            boundingBoxes.Clear();
            landmarks.Clear();
            Face face;
            RectI32 boundingRect;
            LandmarkPoint[] groupPoints;
            for (int i = 0; i < numOfFaces; i++)
            {
                face = faceData.QueryFaceByIndex(i);
                boundingRect = face.Detection.BoundingRect;
                boundingBoxes.Add(boundingRect);

                if (extractLandmarkGroup)
                {
                    //System.Diagnostics.Debug.WriteLine("Extracting Group: " + landmarkGroup);
                    face.Landmarks.QueryPointsByGroup(landmarkGroup, out groupPoints);
                }
                else
                {
                    //System.Diagnostics.Debug.WriteLine("Extracting Group: ALL");
                    groupPoints = face.Landmarks.Points;
                }
                landmarks.Add(groupPoints);
            }
        }

        private void ProcessGesture()
        {
            if (cursor != null)
            {
                // Retrieve the most recent processed data
                cursorData = cursor.CreateOutput();
                cursorData.Update();
                handWaving = cursorData.IsGestureFired(GestureType.CURSOR_HAND_OPENING, out gestureData);
            }

            //release curor data
            if (cursorData != null) cursorData.Dispose();
        }

        private void ProcessImages()
        {
            Sample sample = reader.Sample;
            color = sample.Color;
            depth = projection.CreateDepthImageMappedToColor(sample.Depth, color); //create depth mapped to color image

            color.AcquireAccess(ImageAccess.ACCESS_READ, colorPixelFormat, out colorData);
            depth.AcquireAccess(ImageAccess.ACCESS_READ, depthPixelFormat, out depthData);

            colorBitmap = colorData.ToWritableBitmap(color.Info.width, color.Info.height, DPI_X, DPI_Y);
            depthBitmap = depthData.ToWritableBitmap(depth.Info.width, depth.Info.height, DPI_X, DPI_Y);



            // Update the user interface
            UpdateUI(colorData, color.Info, DPI_X, DPI_Y, ImageType.COLOR);
            UpdateUI(depthData, depth.Info, DPI_X, DPI_Y, ImageType.DEPTH);


            //release access
            color.ReleaseAccess(colorData);
            depth.ReleaseAccess(depthData);


        }

        private void UpdateUI(ImageData imageData, ImageInfo imageInfo, double dpiX, double dpiY, ImageType type)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
            {

                if (imageData != null && imageInfo != null)
                {
                    System.Windows.Controls.Image imageBox;

                    if (type.Equals(ImageType.COLOR))
                    {
                        imageBox = imgColorStream;
                        UpdateColorCanvas();
                    }
                    else
                    {
                        imageBox = imgDepthStream;
                    }

                    // Mirror the color stream Image control
                    imageBox.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                    imageBox.RenderTransform = new ScaleTransform(-1, 1);

                    // Display the color stream
                    imageBox.Source = GetBitmap(imageData, imageInfo, dpiX, dpiY, type);

                    // Update the screen message
                    if (handWaving)
                    {
                        lblMessage.Content = "Hello World!";
                        handTrigger = true;
                    }

                    // Reset the screen message
                    if (handTrigger)
                    {
                        msgTimer++;

                        if (msgTimer >= 200)
                        {
                            lblMessage.Content = "(Wave Your Hand)";
                            msgTimer = 0;
                            handTrigger = false;
                        }
                    }
                }
            }));
        }

        private void UpdateColorCanvas()
        {
            imgColorCanvas.Children.Clear();
            UpdateBoundingBoxes();
            if (drawLandmarkPoints)
            {
                UpdateLandmarks();
            }
        }

        private void UpdateLandmarks()
        {
            AddNewColorIfRequired(landmarks.Count);
            double scaleX = imgColorStream.ActualWidth / WIDTH;
            double scaleY = imgColorStream.ActualHeight / HEIGHT;

            //System.Diagnostics.Debug.WriteLine("landmark list count: " + landmarks.Count);
            for (int i = 0; i < landmarks.Count; i++)
            {
                LandmarkPoint[] points = landmarks.ElementAt(i);
                Ellipse ellipse;
                for (int j = 0; j < points.Length; j++)
                {
                    ellipse = new Ellipse();
                    ellipse.Width = 3;
                    ellipse.Height = 3;
                    ellipse.Stroke = new SolidColorBrush(colors.ElementAt(i));
                    Canvas.SetTop(ellipse, points[j].image.y * scaleY);
                    Canvas.SetRight(ellipse, points[j].image.x * scaleX);

                    imgColorCanvas.Children.Add(ellipse);
                }
            }
        }

        private void CreateLandmarkBoundingBoxes()
        {
            landmarkBoundingBoxes.Clear();
            LandmarkPoint[] points;
            float minX = float.MaxValue;
            float maxX = 0;
            float minY = float.MaxValue;
            float maxY = 0;
            RectI32 boundingBox;

            for (int i = 0; i < landmarks.Count; i++)
            {
                bool looped = false;
                points = landmarks.ElementAt(i);
                for (int j = 0 + landmarkOffset; j < points.Length - landmarkOffset; j++)
                {
                    if (!looped)
                    {
                        looped = true;
                    }
                    minX = Math.Min(minX, points[j].image.x);
                    maxX = Math.Max(maxX, points[j].image.x);
                    minY = Math.Min(minY, points[j].image.y);
                    maxY = Math.Max(maxY, points[j].image.y);
                }

                //minY = padMinValue(minY, 50, 0);
                //maxY = padMaxValue(maxY, padBoundinBoxY, HEIGHT);
                if (looped)
                {
                    boundingBox = new RectI32((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
                    landmarkBoundingBoxes.Add(boundingBox);
                }
            }
        }

        private void UpdateBoundingBoxes(List<RectI32> bBoxes)
        {
            AddNewColorIfRequired(bBoxes.Count);
            double scaleX = imgColorStream.ActualWidth / WIDTH;
            double scaleY = imgColorStream.ActualHeight / HEIGHT;

            //System.Diagnostics.Debug.WriteLine("bounding box list count: " + bBoxes.Count);
            System.Windows.Shapes.Rectangle rect;
            for (int i = 0; i < bBoxes.Count; i++)
            {
                //System.Diagnostics.Debug.WriteLine("scale x: " + scaleX + ", scaleY: " + scaleY);
                //System.Diagnostics.Debug.WriteLine("bounding box: x -> " + bBoxes[i].x + " y -> " + bBoxes[i].y + " h -> " + bBoxes[i].h + " w -> " + bBoxes[i].w);
                rect = new System.Windows.Shapes.Rectangle();
                rect.Width = bBoxes[i].w * scaleX;
                rect.Height = bBoxes[i].h * scaleY;
                rect.Stroke = new SolidColorBrush(colors.ElementAt(i));
                Canvas.SetTop(rect, bBoxes[i].y * scaleY);
                Canvas.SetRight(rect, bBoxes[i].x * scaleX);

                imgColorCanvas.Children.Add(rect);
            }
        }

        private void UpdateBoundingBoxes()
        {
            if (drawFaceBoundingBox)
            {
                UpdateBoundingBoxes(boundingBoxes);
            }

            if (drawLandmarkBoundingBox)
            {
                UpdateBoundingBoxes(landmarkBoundingBoxes);
            }
        }

        private void AddNewColorIfRequired(int count)
        {
            if (count > colors.Count)
            {
                Random rnd = new Random();
                Byte[] b = new Byte[3];
                rnd.NextBytes(b);
                System.Windows.Media.Color color = System.Windows.Media.Color.FromRgb(b[0], b[1], b[2]);
                colors.Add(color);
            }
        }

        private WriteableBitmap GetBitmap(ImageData imageData, ImageInfo info, double dpiX, double dpiY, ImageType type)
        {

            WriteableBitmap bitmap = imageData.ToWritableBitmap(info.width, info.height, dpiX, dpiY);
            if (type.Equals(ImageType.COLOR))
            {
                colorBitmap = bitmap;
            }
            else
            {
                depthBitmap = bitmap;
            }
            return bitmap;
        }

        private void WriteToFile(String path)
        {

            Point3DF32[] vertices = new Point3DF32[depth.Info.width * depth.Info.height];
            projection.QueryVertices(depth, vertices);


            long timestamp = depth.TimeStamp;

            File.WriteAllLines(path + "." + timestamp + ".txt", vertices.Select(d => d.x.ToString() + " " + d.y.ToString() + " " + d.z.ToString()).ToArray());

        }

        private void WindowClosed(object sender, EventArgs e)
        {
            terminate = true;
            //processingThread.Abort();
        }

        private void SaveImage(object sender, RoutedEventArgs e)
        {
            //WriteToFile("./imageDataArray");
            captureImage = true;
        }

        private void SetComboBoxItems(List<String> items)
        {
            ComboBoxItem comboItem;
            pixelFormatBox.Items.Clear();
            foreach (String item in items)
            {
                comboItem = new ComboBoxItem()
                {
                    Content = item
                };
                pixelFormatBox.Items.Add(comboItem);
            }
        }

        private void SaveImagesToDisk(Intel.RealSense.ImageData imageData, Intel.RealSense.ImageInfo imageInfo, Intel.RealSense.PixelFormat pixelFormat, String directoryName, int imageId, String imgPrefix)
        {
            WriteableBitmap imageBitmap;
            Int32 unixTimestamp;
            RectI32 bRect;
            CroppedBitmap cropped;
            String dirName;
            JpegBitmapEncoder encoder;
            FileStream stream;

            //image.AcquireAccess(ImageAccess.ACCESS_READ, pixelFormat, out ImageData imageData);
            imageBitmap = imageData.ToWritableBitmap(imageInfo.width, imageInfo.height, DPI_X, DPI_Y);

            for (int i = 0; i < landmarkBoundingBoxes.Count; i++)
            {
                unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                bRect = landmarkBoundingBoxes.ElementAt(i);
                cropped = new CroppedBitmap(imageBitmap, new Int32Rect(bRect.x, bRect.y, bRect.w, bRect.h));
                dirName = CheckDirectoryName(directoryName);
                using (stream = new FileStream(dirName + imgPrefix + pixelFormat.ToString() + "_" + unixTimestamp + "_" + imageId + ".jpeg", FileMode.Create))
                {
                    encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(cropped));
                    encoder.Save(stream);
                }

            }

            //image.ReleaseAccess(imageData);
        }

        private String CheckDirectoryName(String directoryName)
        {
            String dirName;
            if (directoryName == null || directoryName.Length == 0)
            {
                dirName = "";
            }
            else
            {
                dirName = directoryName + System.IO.Path.DirectorySeparatorChar;
            }

            return dirName;
        }

        private void StreamBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            String streamType = (streamBox.SelectedItem as ComboBoxItem).Content.ToString();
            //System.Diagnostics.Debug.WriteLine("streamType: " + streamType);
            switch (streamType.ToLower())
            {
                case "color":
                    SetComboBoxItems(colorFormats);
                    pixelFormatBox.SelectedIndex = GetIndexFromPixelFormat(colorPixelFormat);
                    break;
                case "depth":
                    SetComboBoxItems(depthFormats);
                    pixelFormatBox.SelectedIndex = GetIndexFromPixelFormat(depthPixelFormat);
                    break;
            }
        }

        private void SaveSeries(object sender, RoutedEventArgs e)
        {
            popupMenu = new PopupMenu();
            bool? dialogResult = popupMenu.ShowDialog();

            if (dialogResult.HasValue && dialogResult.Value)
            {
                seriesToCapture = (int)popupMenu.timerSlider.Value;
                dirName = popupMenu.GetDirectoryPath();
                captureSeries = true;
            }

        }

        private void LandmarkOffsetSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            landmarkOffset = (int)landmarkOffsetSlider.Value;
            landmarkOffsetLabel.Content = landmarkOffset.ToString();
        }

        private void LandmarkGroupBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem selectedLandmarkGroup = (ComboBoxItem)landmarkGroupBox.SelectedItem;
            switch (selectedLandmarkGroup.Content.ToString().ToLower())
            {
                case "all":
                    extractLandmarkGroup = false;
                    break;
                case "yaw":
                    extractLandmarkGroup = true;
                    landmarkGroup = LandmarksGroupType.LANDMARK_GROUP_JAW;
                    break;
                case "mouth":
                    extractLandmarkGroup = true;
                    landmarkGroup = LandmarksGroupType.LANDMARK_GROUP_MOUTH;
                    break;
            }
        }

        private void DrawLandmarksChecked(object sender, RoutedEventArgs e)
        {
            drawLandmarkPoints = true;
        }

        private void DrawLandmarksUnhecked(object sender, RoutedEventArgs e)
        {
            drawLandmarkPoints = false;
        }

        private void DrawFaceBoundingBoxesUnchecked(object sender, RoutedEventArgs e)
        {
            drawFaceBoundingBox = false;
        }

        private void DrawFaceBoundingBoxesChecked(object sender, RoutedEventArgs e)
        {
            drawFaceBoundingBox = true;
        }

        private void DrawLandmarkBoundingBoxesChecked(object sender, RoutedEventArgs e)
        {
            drawLandmarkBoundingBox = true;
        }

        private void DrawLandmarkBoundingBoxesUnchecked(object sender, RoutedEventArgs e)
        {
            drawLandmarkBoundingBox = false;
        }

        private void SaveSeriesDelaySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            saveSeriesDelay = (int)saveSeriesDelaySlider.Value;
            saveSeriesDelayLabel.Content = saveSeriesDelay.ToString();
        }

        private void SaveSingleImageToDisk()
        {

            SaveImagesToDisk(colorData, color.Info, colorPixelFormat, "", 1, "color_");
            SaveImagesToDisk(depthData, depth.Info, depthPixelFormat, "", 1, "depth_");
            captureImage = false;
        }

        private void UpdateMessageLabel(String message)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
            {
                lblMessage.Content = message;
            }));
        }

        private void SaveImageSeriesToDisk(String dirName)
        {
            ++seriesCaptured;
            UpdateMessageLabel("Remaining Frames: " + (seriesToCapture - seriesCaptured));
            SaveImagesToDisk(colorData, color.Info, colorPixelFormat, dirName, seriesCaptured, "color_");
            SaveImagesToDisk(depthData, depth.Info, depthPixelFormat, dirName, seriesCaptured, "depth_");
            if (seriesCaptured >= seriesToCapture)
            {
                UpdateMessageLabel("");
                captureSeries = false;
                seriesCaptured = 0;
            }

        }

        private void PixelFormatBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (streamBox.SelectedIndex != -1 && pixelFormatBox.SelectedIndex != -1)
            {
                String streamType = (streamBox.SelectedItem as ComboBoxItem).Content.ToString();
                String pixelType = (pixelFormatBox.SelectedItem as ComboBoxItem).Content.ToString();

                if (streamType.ToLower().Equals("depth"))
                {
                    depthPixelFormat = GetPixelFormatFromString(pixelType);
                }
                else
                {
                    colorPixelFormat = GetPixelFormatFromString(pixelType);
                }
            }
        }

        private Intel.RealSense.Image GetImageStreamFromString(String streamType)
        {
            if (streamType.ToLower().Equals("depth"))
            {
                return depth;
            }
            else
            {
                return color;
            }
        }

        private int GetIndexFromPixelFormat(Intel.RealSense.PixelFormat pixelFormat)
        {
            int index = 0;
            if (streamBox.SelectedIndex != -1)
            {
                String streamType = (streamBox.SelectedItem as ComboBoxItem).Content.ToString();
                if (streamType.ToLower().Equals("depth"))
                {
                    index = GetDepthIndexFromPixelFormat(pixelFormat);
                }
                else
                {
                    index = GetColorIndexFromPixelFormat(pixelFormat);
                }
            }
            return index;
        }

        private int GetColorIndexFromPixelFormat(Intel.RealSense.PixelFormat pixelFormat)
        {
            int index = 0;
            if (streamBox.SelectedIndex != -1)
            {
                String streamType = (streamBox.SelectedItem as ComboBoxItem).Content.ToString();

                if (pixelFormat == Intel.RealSense.PixelFormat.PIXEL_FORMAT_RGB24)
                {
                    index = 1;
                }
            }
            return index;
        }

        private int GetDepthIndexFromPixelFormat(Intel.RealSense.PixelFormat pixelFormat)
        {
            int index = 0;
            if (streamBox.SelectedIndex != -1)
            {
                String streamType = (streamBox.SelectedItem as ComboBoxItem).Content.ToString();

                switch (pixelFormat)
                {
                    case Intel.RealSense.PixelFormat.PIXEL_FORMAT_DEPTH_RAW:
                        index = 1;
                        break;
                    case Intel.RealSense.PixelFormat.PIXEL_FORMAT_RGB32:
                        index = 2;
                        break;

                }
            }
            return index;
        }

        private Intel.RealSense.PixelFormat GetPixelFormatFromString(String format)
        {
            Intel.RealSense.PixelFormat pixelFormat;
            switch (format.ToLower())
            {
                case "depth":
                    pixelFormat = Intel.RealSense.PixelFormat.PIXEL_FORMAT_DEPTH;
                    break;
                case "depth raw":
                    pixelFormat = Intel.RealSense.PixelFormat.PIXEL_FORMAT_DEPTH_RAW;
                    break;
                case "rgb24":
                    pixelFormat = Intel.RealSense.PixelFormat.PIXEL_FORMAT_RGB24;
                    break;
                default:
                    pixelFormat = Intel.RealSense.PixelFormat.PIXEL_FORMAT_RGB32;
                    break;
            }

            return pixelFormat;
        }

        private void CancelSave(object sender, RoutedEventArgs e)
        {
            captureImage = false;
            captureSeries = false;
            seriesCaptured = 0;
            seriesToCapture = 0;
        }
    }

}
