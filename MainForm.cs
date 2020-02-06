using AForge;
using AForge.Controls;
using AForge.Imaging;
using AForge.Imaging.Filters;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;
using VideoOS.Platform;
using VideoOS.Platform.Client;
using VideoOS.Platform.Messaging;
using VideoOS.Platform.UI;

namespace VideoViewer
{
    public partial class MainForm : Form
    {


        private Item _selectItem1;
        protected ImageViewerControl _imageViewerControl1;
        private Item _selectItem2;
        private ImageViewerControl _imageViewerControl2;
        private double _speed = 0.0;



        double cr = 0.2125;
        double cg = 0.7154;
        double cb = 0.0721;
        int thresholdValue = 45;
        int minWidth = 50;
        int minHeight = 50;

        Graphics gr;
        Graphics gr2;


        public MainForm()
        {
            InitializeComponent();

            EnvironmentManager.Instance.RegisterReceiver(PlaybackTimeChangedHandler,
                                                         new MessageIdFilter(MessageId.SmartClient.PlaybackCurrentTimeIndication));

        }


        #region ImageViewer 1 select

        private void buttonSelect1_Click(object sender, EventArgs e)
        {
            if (_imageViewerControl1 != null)
            {
                _imageViewerControl1.Disconnect();
                _imageViewerControl1.Close();
                _imageViewerControl1.Dispose();
                _imageViewerControl1 = null;
            }

            ItemPickerForm form = new ItemPickerForm();
            form.KindFilter = Kind.Camera;
            form.AutoAccept = true;
            form.Init(Configuration.Instance.GetItems());
            if (form.ShowDialog() == DialogResult.OK)
            {
                _selectItem1 = form.SelectedItem;
                buttonSelect1.Text = _selectItem1.Name;

                _imageViewerControl1 = ClientControl.Instance.GenerateImageViewerControl();
                _imageViewerControl1.Dock = DockStyle.Fill;
                _imageViewerControl1.ClickEvent += new EventHandler(ImageViewerControl1_ClickEvent);
                panel1.Controls.Clear();
                panel1.Controls.Add(_imageViewerControl1);
                _imageViewerControl1.CameraFQID = _selectItem1.FQID;
                // Lets enable/disable the header based on the tick mark.  Could also disable LiveIndicator or CameraName.
                _imageViewerControl1.EnableVisibleHeader = checkBoxHeader.Checked;
                _imageViewerControl1.EnableVisibleLiveIndicator = EnvironmentManager.Instance.Mode == Mode.ClientLive;
                _imageViewerControl1.EnableMousePtzEmbeddedHandler = true;
                _imageViewerControl1.MaintainImageAspectRatio = true;
                _imageViewerControl1.ImageOrPaintInfoChanged += ImageOrPaintChangedHandler;

                _imageViewerControl1.EnableRecordedImageDisplayedEvent = true;

                _imageViewerControl1.EnableVisibleTimeStamp = true;
                _imageViewerControl1.AdaptiveStreaming = checkBoxAdaptiveStreaming.Checked;

                _imageViewerControl1.Initialize();
                _imageViewerControl1.Connect();
                _imageViewerControl1.Selected = true;
                _imageViewerControl1.EnableDigitalZoom = checkBoxDigitalZoom.Checked;



                gr = pictureBoxHisto.CreateGraphics();
                gr2 = pictureBoxHistoV.CreateGraphics();




                int width = pictureBoxHisto.Width;
                int height = pictureBoxHisto.Height;
                // Make a transformation to the PictureBox.
                RectangleF data_bounds =
                    new RectangleF(0, 0, 320, MAX_VALUE);
                PointF[] points =
                {
               new PointF(0, height),
              new PointF(width, height),
                new PointF(0, 0)
                 };
                Matrix transformation = new Matrix(data_bounds, points);
                gr.Transform = transformation;




                Thread thread1 = new Thread(DoWork);
                thread1.Start();

            }
        }
        Bitmap backgroundImg = null;


        public void DoWork()
        {

            Bitmap img;
            Bitmap grayScaleImg;
            Bitmap binaryImg;
            Bitmap openingFilterImg = null;
            Bitmap blobsFilteringImg = null;
            Bitmap lastimg = null;
            Bitmap difImage = null;
            while (true)
            {
                try
                {
                    if (openingFilterImg != null) lastimg = openingFilterImg;

                    img = _imageViewerControl1.GetCurrentDisplayedImageAsBitmap();
                    grayScaleImg = ProcessImageGrayscale(img);
                    binaryImg = PocessImageBinary(grayScaleImg);
                    openingFilterImg = ProcessOpeningFilter(binaryImg);
                    if (backgroundImg != null)
                    {
                        difImage = ProcessImageDiference(backgroundImg, openingFilterImg);
                        blobsFilteringImg = ProcessImageBlobsFiltering(difImage);
                        DrawHistogram(blobsFilteringImg);
                        DrawHistogramV(blobsFilteringImg);
                    }


                    pictureBoxGrayScale.Image = grayScaleImg;
                    pictureBoxBinary.Image = binaryImg;
                   pictureBoxBlobsFilter.Image = blobsFilteringImg;
                    this.PicuteOpeningFilterImg.Image = openingFilterImg;

                    pictureBoxDiference.Image = difImage;



                }
                catch (Exception e)
                {
                    //TODO log subsystem
                    System.Console.WriteLine(e.Message);
                }

                Thread.Sleep(100);
            }
        }



        private Bitmap ProcessImageGrayscale(Bitmap img)
        {
            Grayscale gray = new Grayscale(cr, cg, cb);

            return gray.Apply(img);

        }


        private Bitmap PocessImageBinary(Bitmap img)
        {
            Threshold threshold = new Threshold(thresholdValue);
            return threshold.Apply(img);
        }

        private Bitmap ProcessImageBlobsFiltering(Bitmap img)
        {
            BlobsFiltering blobsFiltering = new BlobsFiltering();
            blobsFiltering.CoupledSizeFiltering = true;
            blobsFiltering.MinWidth = minWidth;
            blobsFiltering.MinHeight = minHeight;
            return blobsFiltering.Apply(img);
        }


        private Bitmap ProcessOpeningFilter(Bitmap img)
        {
            Opening openingFilter = new Opening();
            return openingFilter.Apply(img);
        }



        private Bitmap ProcessImageDiference(Bitmap lastimg, Bitmap openingFilterImg)
        {
            // create filter
            Difference filter = new Difference(openingFilterImg);
            // apply the filter
            Bitmap resultImage = filter.Apply(lastimg);
            // create filter
            Invert filter2 = new Invert();
            // apply the filter
            filter2.ApplyInPlace(resultImage);
            return resultImage;
        }



        // Conver list of AForge.NET's points to array of .NET points
        private System.Drawing.Point[] ToPointsArray(List<IntPoint> points)
        {
            System.Drawing.Point[] array = new System.Drawing.Point[points.Count];

            for (int i = 0, n = points.Count; i < n; i++)
            {
                array[i] = new System.Drawing.Point(points[i].X, points[i].Y);
            }

            return array;
        }

        // Convert list of AForge.NET's IntPoint to array of .NET's Point
        private static System.Drawing.Point[] PointsListToArray(List<IntPoint> list)
        {
            System.Drawing.Point[] array = new System.Drawing.Point[list.Count];

            for (int i = 0, n = list.Count; i < n; i++)
            {
                array[i] = new System.Drawing.Point(list[i].X, list[i].Y);
            }

            return array;
        }



        void ImageOrPaintChangedHandler(object sender, EventArgs e)
        {
            Debug.WriteLine("ImageSize:" + _imageViewerControl1.ImageSize.Width + "x" + _imageViewerControl1.ImageSize.Height + ", PaintSIze:" +
                _imageViewerControl1.PaintSize.Width + "x" + _imageViewerControl1.PaintSize.Height +
                ", PaintLocation:" + _imageViewerControl1.PaintLocation.X + "-" + _imageViewerControl1.PaintLocation.Y);
        }

        void ImageViewerControl1_ClickEvent(object sender, EventArgs e)
        {
            if (_imageViewerControl2 != null)
                _imageViewerControl2.Selected = false;
            _imageViewerControl1.Selected = true;
        }
        #endregion

        #region ImageViewer 2 select

        private void buttonSelect2_Click(object sender, EventArgs e)
        {
            if (_imageViewerControl2 != null)
            {
                _imageViewerControl2.Disconnect();
                _imageViewerControl2.Close();
                _imageViewerControl2.Dispose();
                _imageViewerControl2 = null;
            }

            ItemPickerForm form = new ItemPickerForm();
            form.KindFilter = Kind.Camera;
            form.AutoAccept = true;
            form.Init(Configuration.Instance.GetItems());
            if (form.ShowDialog() == DialogResult.OK)
            {
                _selectItem2 = form.SelectedItem;

                _imageViewerControl2 = ClientControl.Instance.GenerateImageViewerControl();
                _imageViewerControl2.Dock = DockStyle.Fill;
                _imageViewerControl2.ClickEvent += new EventHandler(_imageViewerControl2_ClickEvent);
                _imageViewerControl2.CameraFQID = _selectItem2.FQID;
                _imageViewerControl2.EnableVisibleHeader = checkBoxHeader.Checked;
                _imageViewerControl2.EnableMousePtzEmbeddedHandler = true;
                _imageViewerControl2.AdaptiveStreaming = checkBoxAdaptiveStreaming.Checked;
                _imageViewerControl2.Initialize();
                _imageViewerControl2.Connect();
                _imageViewerControl2.Selected = true;

                _imageViewerControl2.EnableDigitalZoom = checkBoxDigitalZoom.Checked;
            }
        }

        void _imageViewerControl2_ClickEvent(object sender, EventArgs e)
        {
            if (_imageViewerControl1 != null)
                _imageViewerControl1.Selected = false;
            _imageViewerControl2.Selected = true;
        }
        #endregion

        #region Time changed event handler

        private void HandleTimeChanged(DateTime time)
        {
            textBoxTime.Text = time.ToShortDateString() + "  " + time.ToLongTimeString();

            _imageViewerControl1.Invalidate();
        }

        private object PlaybackTimeChangedHandler(VideoOS.Platform.Messaging.Message message, FQID dest, FQID sender)
        {
            DateTime time = ((DateTime)message.Data).ToLocalTime();
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => HandleTimeChanged(time)));
            }
            else
                HandleTimeChanged(time);
            return null;
        }

        #endregion

        #region Playback Click handling

        private void checkBoxDigitalZoom_CheckedChanged(object sender, EventArgs e)
        {
            if (_imageViewerControl1 != null)
                _imageViewerControl1.EnableDigitalZoom = checkBoxDigitalZoom.Checked;
            if (_imageViewerControl2 != null)
                _imageViewerControl2.EnableDigitalZoom = checkBoxDigitalZoom.Checked;
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                        MessageId.SmartClient.PlaybackCommand,
                                                        new PlaybackCommandData() { Command = PlaybackData.PlayStop }));
            EnvironmentManager.Instance.Mode = Mode.ClientPlayback;
            buttonMode.Text = "Current mode: Playback";
            _speed = 0.0;
        }

        private void OnModeClick(object sender, EventArgs e)
        {
            if (EnvironmentManager.Instance.Mode == Mode.ClientLive)
            {
                if (_imageViewerControl1 != null)
                    _imageViewerControl1.EnableVisibleLiveIndicator = false;
                EnvironmentManager.Instance.Mode = Mode.ClientPlayback;
                buttonMode.Text = "Current mode: Playback";
            }
            else
            {
                if (_imageViewerControl1 != null)
                    _imageViewerControl1.EnableVisibleLiveIndicator = true;
                EnvironmentManager.Instance.Mode = Mode.ClientLive;
                buttonMode.Text = "Current mode: Live";
            }
        }

        private void buttonReverse_Click(object sender, EventArgs e)
        {
            if (_speed == 0.0)
                _speed = 1.0;
            else
                _speed *= 2;
            EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                        MessageId.SmartClient.PlaybackCommand,
                                                        new PlaybackCommandData() { Command = PlaybackData.PlayReverse, Speed = _speed }));
        }

        private void buttonForward_Click(object sender, EventArgs e)
        {
            if (_speed == 0.0)
                _speed = 1.0;
            else
                _speed *= 2;
            EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                        MessageId.SmartClient.PlaybackCommand,
                                                        new PlaybackCommandData() { Command = PlaybackData.PlayForward, Speed = _speed }));
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                        MessageId.SmartClient.PlaybackCommand,
                                                        new PlaybackCommandData() { Command = PlaybackData.Begin }));
        }

        private void buttonEnd_Click(object sender, EventArgs e)
        {
            EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                        MessageId.SmartClient.PlaybackCommand,
                                                        new PlaybackCommandData() { Command = PlaybackData.End }));
        }

        private void OnPrevSequence(object sender, EventArgs e)
        {
            EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                        MessageId.SmartClient.PlaybackCommand,
                                                        new PlaybackCommandData() { Command = PlaybackData.PreviousSequence }));
        }

        private void OnNextSequence(object sender, EventArgs e)
        {
            EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                        MessageId.SmartClient.PlaybackCommand,
                                                        new PlaybackCommandData() { Command = PlaybackData.NextSequence }));
        }

        private void OnPreviousFrame(object sender, EventArgs e)
        {
            EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                        MessageId.SmartClient.PlaybackCommand,
                                                        new PlaybackCommandData() { Command = PlaybackData.Previous }));
        }

        private void OnNextFrame(object sender, EventArgs e)
        {
            EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                        MessageId.SmartClient.PlaybackCommand,
                                                        new PlaybackCommandData() { Command = PlaybackData.Next }));
        }

        #endregion

        private void OnStartRecording1(object sender, EventArgs e)
        {
            if (_selectItem1 != null)
                EnvironmentManager.Instance.SendMessage(
                    new VideoOS.Platform.Messaging.Message(MessageId.Control.StartRecordingCommand), _selectItem1.FQID);
        }

        private void OnStopRecording1(object sender, EventArgs e)
        {
            if (_selectItem1 != null)
                EnvironmentManager.Instance.SendMessage(
                    new VideoOS.Platform.Messaging.Message(MessageId.Control.StopRecordingCommand), _selectItem1.FQID);
        }

        private void OnStartRecording2(object sender, EventArgs e)
        {
            if (_selectItem2 != null)
                EnvironmentManager.Instance.SendMessage(
                    new VideoOS.Platform.Messaging.Message(MessageId.Control.StartRecordingCommand), _selectItem2.FQID);
        }

        private void OnStopRecording2(object sender, EventArgs e)
        {
            if (_selectItem2 != null)
                EnvironmentManager.Instance.SendMessage(
                    new VideoOS.Platform.Messaging.Message(MessageId.Control.StopRecordingCommand), _selectItem2.FQID);
        }

        private void OnClose(object sender, EventArgs e)
        {
            Close();
        }

        private string _selectedStoragePath = "";
        private void button11_Click(object sender, EventArgs e)
        {
            try
            {
                if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                {
                    _selectedStoragePath = folderBrowserDialog1.SelectedPath;
                    if (System.IO.File.Exists(System.IO.Path.Combine(_selectedStoragePath, "cache.xml")) ||
                        System.IO.File.Exists(System.IO.Path.Combine(_selectedStoragePath, "archives_cache.xml")))
                    {
                        var uri = new Uri("file:\\" + _selectedStoragePath);
                        VideoOS.Platform.SDK.Environment.AddServer(uri, System.Net.CredentialCache.DefaultNetworkCredentials);

                        VideoOS.Platform.SDK.Environment.LoadConfiguration(uri);
                    }
                    else
                    {
                        MessageBox.Show("No cache.xml or archives_cache.xml file were found in selected folder.");
                    }
                }
            }
            catch (Exception ex)
            {
                EnvironmentManager.Instance.ExceptionDialog("Folder select", ex);
            }

        }

        private void buttonLiftMask_Click(object sender, EventArgs e)
        {
            Configuration.Instance.ServerFQID.ServerId.UserContext.SetPrivacyMaskLifted(!Configuration.Instance.ServerFQID.ServerId.UserContext.PrivacyMaskLifted);
        }

        private void checkBoxAdaptiveStreaming_CheckedChanged(object sender, EventArgs e)
        {
            if (_imageViewerControl1 != null)
            {
                _imageViewerControl1.AdaptiveStreaming = checkBoxAdaptiveStreaming.Checked;
            }
        }


        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            cr = (double)trackBarCR.Value / 5000;
            cb = (double)trackBarCB.Value / 5000;
            cg = (double)trackBarCG.Value / 5000;
            labelcb.Text = cb.ToString();
            labelcr.Text = cr.ToString();
            labelcg.Text = cg.ToString();
        }

        private void trackBarThresholdValue_Scroll(object sender, EventArgs e)
        {
            thresholdValue = trackBarThresholdValue.Value;
            labelThresholdValue.Text = trackBarThresholdValue.Value.ToString();
        }

        private void trackBarMinWidth_Scroll(object sender, EventArgs e)
        {
            minWidth = trackBarMinWidth.Value;
            minHeight = trackBarMinHeight.Value;

            labelMinMinHeight.Text = trackBarMinHeight.Value.ToString();
            labelMinWidth.Text = trackBarMinWidth.Value.ToString();

        }






        private const int MIN_VALUE = 0;
        private const int MAX_VALUE = 100;

        private float[] DataValues = new float[10];


        // Draw a histogram.
        private void DrawHistogram(Bitmap img)
        {

            Color back_color = Color.White;

            Invert filter = new Invert();
            // apply the filter
            Bitmap inv = filter.Apply(img);
   
            HorizontalIntensityStatistics his = new HorizontalIntensityStatistics(inv);
            AForge.Math.Histogram histogram = his.Gray;
   
            int[] values = histogram.Values;




            Color[] Colors = new Color[] {
        Color.Red, Color.LightGreen, Color.Blue,
        Color.Pink, Color.Green, Color.LightBlue,
        Color.Orange, Color.Yellow, Color.Purple
    };



            gr.Clear(back_color);

            // Draw the histogram.
            using (Pen thin_pen = new Pen(Color.Black, 0))
            {
                for (int i = 0; i < values.Length; i++)
                {

                    float f2 =650F;
                    float fl = (float)values[i] / f2;
                    gr.DrawLine(thin_pen, i, 0, i, fl);
                }
            }
        }

        private void button12_Click(object sender, EventArgs e)
        {
            backgroundImg = (Bitmap) PicuteOpeningFilterImg.Image;
        }

        //      gr.ResetTransform();
        //      gr.DrawRectangle(Pens.Black, 0, 0, width - 1, height - 1);


        private void DrawHistogramV(Bitmap img)
        {

            Color back_color = Color.White;

            Invert filter = new Invert();
            // apply the filter
            Bitmap inv = filter.Apply(img);

            VerticalIntensityStatistics his = new VerticalIntensityStatistics(inv);
            AForge.Math.Histogram histogram = his.Gray;

            int[] values = histogram.Values;




            Color[] Colors = new Color[] {
        Color.Red, Color.LightGreen, Color.Blue,
        Color.Pink, Color.Green, Color.LightBlue,
        Color.Orange, Color.Yellow, Color.Purple
    };



            gr2.Clear(back_color);

            // Draw the histogram.
            using (Pen thin_pen = new Pen(Color.Black, 0))
            {
                for (int i = 0; i < values.Length; i++)
                {

                    float f2 = 420F;
                    float fl = (float)values[i] / f2;
                    gr2.DrawLine(thin_pen, 0, i, fl, i);
                }
            }
        }

  }


}
