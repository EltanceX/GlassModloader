using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GlassLoader
{
    public class LoadingForm
    {
        private static SynchronizationContext _syncContext;
        public static Form GMLForm;
        public const int LoadingItems = 10;
        public static int LoadedItems = 0;
        public static void LoadingFinished()
        {
            ManuallyClose = false;
            _syncContext.Post(_ => GMLForm?.Close(), null);
            //GMLForm?.Close();
        }

        public static bool ManuallyClose = true;
        public static float m_LoadingProcess = 0.00f;
        public static string m_LoadingData = "Initialize GML... ";
        public static float LoadingProcess
        {
            get { return m_LoadingProcess; }
            set
            {
                m_LoadingProcess = value;
                GMLForm?.Invalidate();
            }
        }
        public static string LoadingData
        {
            get { return m_LoadingData; }
            set
            {
                m_LoadingData = value;
                GMLForm?.Invalidate();
            }
        }
        public static void UpdateLoadingData(string loadingData, int plus = 1)
        {
            m_LoadingData = loadingData;
            LoadedItems += plus;
            LoadingProcess = (float)LoadedItems / LoadingItems;
            Thread.Sleep(10);
        }
        public static void CreateForm()
        {
            Application.SetCompatibleTextRenderingDefault(false);

            // 创建一个新的同步上下文
            var syncContext = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(syncContext);
            _syncContext = SynchronizationContext.Current;

            GLog.Info("GML Window Initialize");
            //Application.SetCompatibleTextRenderingDefault(false);
            Application.EnableVisualStyles();
            Icon icon = null;
            try
            {
                icon = new Icon(FileManager.AbsoluteGMLIconPath);
            }
            catch (Exception ex)
            {
                GLog.Warn(ex);
            }
            Form form = new Form
            {
                Text = "Glass ModLoader [Preloading]",
                Width = 600,
                Height = 400,
                Icon = icon,
                MaximizeBox = false
            };
            GMLForm = form;

            //PictureBox pictureBox = new PictureBox
            //{
            //    ImageLocation = "GML\\Assets\\logo.png", // 图片路径
            //    SizeMode = PictureBoxSizeMode.Zoom, // 适应窗口大小
            //    Dock = DockStyle.Fill // 填充整个窗口
            //};

            //form.Controls.Add(pictureBox);

            string relativePath = FileManager.AbsoluteGMLLogoPath;
            string fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
            //pictureBox.Image = Image.FromFile(fullPath);
            Image img = null;
            try
            {
                img = Image.FromFile(relativePath); // logo
            }
            catch (Exception ex)
            {
                GLog.Warn(ex);
            }
            form.Paint += (sender, e) =>
            {


                Graphics g = e.Graphics;
                g.Clear(form.BackColor);
                // 最近邻插值，避免模糊
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.None;

                int w = form.ClientSize.Width;
                int h = form.ClientSize.Height;

                if (img != null)
                {
                    int targetWidth = img.Width / 2;
                    int targetHeight = img.Height / 2;
                    int x = (w - targetWidth) / 2; // x居中
                                                   //int y = (form.ClientSize.Height - targetHeight) / 2;
                    g.DrawImage(img, new Rectangle(x, 50, targetWidth, targetHeight));
                }

                Pen bluepen = new Pen(Color.FromArgb(255, 52, 152, 219));
                Pen graypen = new Pen(Color.FromArgb(255, 44, 62, 80));
                Font f = new Font("微软雅黑", 10);


                int left = (w - 400) / 2;
                int top = h - 100;
                int outline = 3;
                g.DrawRectangle(bluepen, left, top, 400, 21);
                g.FillRectangle(bluepen.Brush, left + outline, top + outline, 400 * LoadingProcess - outline * 2, 21 - outline * 2);
                g.DrawString($"[{GUtil.GetTimeString()}] Loading: {LoadingData}   {Math.Round(LoadingProcess * 100, 2)} %", f, graypen.Brush, left, top - 30);
            };
            // 当窗口大小变化时，强制重绘
            form.Resize += (sender, e) => form.Invalidate();
            form.FormClosing += (sender, e) =>
            {
                GMLForm?.Dispose();
                GMLForm = null;
                if (ManuallyClose)
                {
                    GLog.Info("Exiting glass modloader...");
                    Environment.Exit(0);
                }
            };

            Application.Run(form);
        }
    }
}
