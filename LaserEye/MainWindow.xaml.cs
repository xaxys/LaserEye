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

namespace LaserEye
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SelectButtonClick(object sender, RoutedEventArgs e)
        {
            // select image file
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".jpg";
            dlg.Filter = "Image Files (*.jpg, *.png, *.bmp)|*.jpg;*.png;*.bmp";
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                // display image
                tipsText.Content = "图片选择成功";
                string filename = dlg.FileName;
                filePathText.Text = filename;
                SetImage(filename);
            }
        }

        private void FilePathTextChanged(object sender, TextChangedEventArgs e)
        {
            // if stack trace shows SelectButtonClick, then it is a valid image file path, do not trigger this event again
            var st = new System.Diagnostics.StackTrace();
            var sfs = st.GetFrames();
            foreach (var sf in sfs)
            {
                if (sf.GetMethod().Name == "SelectButtonClick")
                {
                    filePathText.Foreground = Brushes.Black;
                    return;
                }
            }

            // or validate the file path
            string filename = filePathText.Text;
            if (!System.IO.File.Exists(filename))
            {
                filePathText.Foreground = Brushes.Red;
            }
            else
            {
                tipsText.Content = "图片选择成功";
                filePathText.Foreground = Brushes.Black;
                SetImage(filename);
            }
        }

        private void UnitScaleTextChanged(object sender, TextChangedEventArgs e)
        {
            // validate the unit scale
            if (!double.TryParse(unitScaleText.Text, out var scale))
            {
                unitScaleText.Foreground = Brushes.Red;
            }
            else
            {
                tipsText.Content = "单位距离设置成功";
                unitScaleText.Foreground = Brushes.Black;
                coordSys.UnitScale = scale;
                Recalculate();
            }
        }

        private void SetImage(string file)
        {
            var bitmap = new BitmapImage(new Uri(file));
            img.Source = bitmap;
            coordSys.Pos1 = coordSys.Pos2 = null;
            measurePoint1 = measurePoint2 = null;

            Recalculate();
            Redraw();
        }

        CoordSys coordSys = new CoordSys();

        private void cnv_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (img.Source == null)
            {
                tipsText.Content = "请先选择图片";
                return;
            }

            // draw a point on the canvas
            var point = e.GetPosition(img);
            var pos = point.ToPixPoint(img.RenderSize, new Size(img.Source.Width, img.Source.Height));
            if (pos == null) return;

            if ((coordSys.Pos1 == null) == (coordSys.Pos2 == null)) // both null or both not null
            {
                tipsText.Content = "基准坐标1设置成功";
                coordSys.Pos1 = pos;
                coordSys.Pos2 = null;
            }
            else if (coordSys.Pos2 == null)
            {
                tipsText.Content = "基准坐标2设置成功";
                coordSys.Pos2 = pos;
            }

            Recalculate();
            Redraw();
        }

        Point? measurePoint1 = null;
        Point? measurePoint2 = null;

        private void cnv_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (img.Source == null)
            {
                tipsText.Content = "请先选择图片";
                return;
            }

            if (coordSys.Pos1 == null || coordSys.Pos2 == null)
            {
                tipsText.Content = "请先右键选择两个基准坐标";
                return;
            }

            var point = e.GetPosition(img);
            var pos = point.ToPixPoint(img.RenderSize, new Size(img.Source.Width, img.Source.Height));
            if (pos == null) return;

            if ((measurePoint1 == null) == (measurePoint2 == null)) // both null or both not null
            {
                measurePoint1 = pos;
                measurePoint2 = null;
                tipsText.Content = "测量坐标1设置成功";
            }
            else
            {
                measurePoint2 = pos;
                tipsText.Content = "测量坐标2设置成功";
            }
            
            Recalculate();
            Redraw();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        private void Recalculate()
        {
            if (coordSys.Pos1 == null ||
                coordSys.Pos2 == null ||
                measurePoint1 == null ||
                measurePoint2 == null)
            {
                if (distanceText != null) distanceText.Content = "";
                return;
            }
            var distance = coordSys.Measure(measurePoint1.Value, measurePoint2.Value);
            distanceText.Content = distance.ToString("F2");
            tipsText.Content = "计算成功";
        }

        private void Redraw()
        {
            // redraw the points on the canvas
            void drawPoint(Point? pos, Brush color)
            {
                if (pos == null) return;
                var point = pos.Value.ToRenderPoint(img.RenderSize, new Size(img.Source.Width, img.Source.Height));
                if (point == null) return;
                var ellipse = new Ellipse();
                ellipse.Width = 5;
                ellipse.Height = 5;
                ellipse.Fill = color;
                ellipse.Stroke = color;
                ellipse.StrokeThickness = 1;
                // get relative position of img in cnv
                var imgPos = img.TranslatePoint(new Point(0, 0), cnv);
                Canvas.SetLeft(ellipse, imgPos.X + point.Value.X - ellipse.Width / 2);
                Canvas.SetTop(ellipse, imgPos.Y + point.Value.Y - ellipse.Height / 2);
                cnv.Children.Add(ellipse);
            }

            cnv.Children.Clear();
            drawPoint(coordSys.Pos1, Brushes.Red);
            drawPoint(coordSys.Pos2, Brushes.Red);
            if (measurePoint1 == null || measurePoint2 == null)
            {
                drawPoint(measurePoint1, Brushes.Yellow);
                drawPoint(measurePoint2, Brushes.Yellow);
            }
            else
            {
                // draw the line
                var point1 = measurePoint1.Value.ToRenderPoint(img.RenderSize, new Size(img.Source.Width, img.Source.Height));
                if (point1 == null) return;
                var point2 = measurePoint2.Value.ToRenderPoint(img.RenderSize, new Size(img.Source.Width, img.Source.Height));
                if (point2 == null) return;

                var line = new Line();
                line.Stroke = Brushes.Yellow;
                line.StrokeThickness = 2;
                line.X1 = point1.Value.X;
                line.Y1 = point1.Value.Y;
                line.X2 = point2.Value.X;
                line.Y2 = point2.Value.Y;
                // get relative position of img in cnv
                var imgPos = img.TranslatePoint(new Point(0, 0), cnv);
                Canvas.SetLeft(line, imgPos.X);
                Canvas.SetTop(line, imgPos.Y);
                cnv.Children.Add(line);
            }
        }
    }

    public static class CoordExtension
    {
        public static Point? ToPixPoint(this Point point, Size renderSize, Size imageSize)
        {
            if (point.X < 0 || point.Y < 0 || point.X > renderSize.Width || point.Y > renderSize.Height) return null;
            double x = point.X / renderSize.Width * imageSize.Width;
            double y = point.Y / renderSize.Height * imageSize.Height;
            return new Point(x, y);
        }

        public static Point? ToRenderPoint(this Point point, Size renderSize, Size imageSize)
        {
            if (point.X < 0 || point.Y < 0 || point.X > imageSize.Width || point.Y > imageSize.Height) return null;
            double x = point.X / imageSize.Width * renderSize.Width;
            double y = point.Y / imageSize.Height * renderSize.Height;
            return new Point(x, y);
        }
    }

    public class CoordSys
    {
        public Point? Pos1 { get; set; }
        public Point? Pos2 { get; set; }

        public double UnitScale { get; set; } = 1.0;

        public static double Distance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        public double Measure(Point p1, Point p2)
        {
            double unitDistance = Distance(Pos1.Value, Pos2.Value);
            double distance = Distance(p1, p2);
            return distance / unitDistance * UnitScale;
        }
    }
}
