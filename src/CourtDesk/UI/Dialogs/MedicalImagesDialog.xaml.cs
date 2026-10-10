using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.UI.Dialogs;

namespace CourtDesk.UI.Dialogs
{
    public sealed class PendingShot
    {
        public string FileName { get; set; }
        public string SourcePath { get; set; }
        public string Caption { get { return ""; } }
    }

    public partial class MedicalImagesDialog : Window
    {
        private readonly MedicalService _medical;
        private readonly int _defendantId;
        private readonly List<MedicalImage> _images = new List<MedicalImage>();
        private readonly List<PendingShot> _pending = new List<PendingShot>();

        public System.Collections.Generic.IReadOnlyList<string> PendingSources
        {
            get
            {
                var outp = new List<string>();
                foreach (PendingShot p in _pending)
                {
                    outp.Add(p.SourcePath);
                }
                return outp.AsReadOnly();
            }
        }

        public MedicalImagesDialog(MedicalService medical, int defendantId)
        {
            InitializeComponent();
            _medical = medical;
            _defendantId = defendantId;
            Reload();
        }

        private void Reload()
        {
            _images.Clear();
            if (_medical != null && _defendantId > 0)
            {
                _images.AddRange(_medical.Images(_defendantId));
            }
            var all = new List<object>();
            foreach (MedicalImage m in _images)
            {
                all.Add(m);
            }
            foreach (PendingShot p in _pending)
            {
                all.Add(p);
            }
            List.ItemsSource = null;
            List.ItemsSource = all;
            EmptyText.Visibility = all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            List.UpdateLayout();
            foreach (object o in all)
            {
                string path = null;
                MedicalImage img = o as MedicalImage;
                if (img != null)
                {
                    path = img.AbsolutePath;
                }
                else
                {
                    PendingShot shot = o as PendingShot;
                    if (shot != null)
                    {
                        path = shot.SourcePath;
                    }
                }
                if (path == null)
                {
                    continue;
                }
                var panel = List.ItemContainerGenerator.ContainerFromItem(o) as FrameworkElement;
                if (panel == null)
                {
                    continue;
                }
                Image image = FindImage(panel);
                if (image != null)
                {
                    TryLoad(image, path);
                }
            }
        }

        private static Image FindImage(DependencyObject root)
        {
            if (root == null)
            {
                return null;
            }
            var img = root as Image;
            if (img != null)
            {
                return img;
            }
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                Image deep = FindImage(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
                if (deep != null)
                {
                    return deep;
                }
            }
            return null;
        }

        private static void TryLoad(Image target, string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return;
                }
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                target.Source = bmp;
            }
            catch (Exception)
            {
            }
        }

        private void AddClick(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = CourtDesk.Resources.Strings.MedicalPick,
                Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp|All Files (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true
            };
            if (dlg.ShowDialog(this) != true)
            {
                return;
            }
            int added = 0;
            int failed = 0;
            foreach (string f in dlg.FileNames)
            {
                if (_defendantId <= 0)
                {
                    if (File.Exists(f) && CourtDesk.Application.Ports.MedicalFiles.IsAllowedImage(f))
                    {
                        _pending.Add(new PendingShot
                        {
                            FileName = Path.GetFileName(f),
                            SourcePath = f
                        });
                        added++;
                    }
                    else
                    {
                        failed++;
                    }
                    continue;
                }
                try
                {
                    _medical.AddImage(_defendantId, f, null, "");
                    added++;
                }
                catch (Exception)
                {
                    failed++;
                }
            }
            Reload();
            if (failed > 0)
            {
                MessageDialog.Show(this, CourtDesk.Resources.Strings.MedicalTitle,
                    string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        CourtDesk.Resources.Strings.MedicalSavedPartial, added, failed));
            }
        }

        private void DeleteClick(object sender, RoutedEventArgs e)
        {
            var b = sender as Button;
            if (b == null)
            {
                return;
            }
            PendingShot shot = b.Tag as PendingShot;
            if (shot != null)
            {
                if (ConfirmDialog.Ask(this,
                        CourtDesk.Resources.Strings.MedicalTitle,
                        CourtDesk.Resources.Strings.MedicalDeleteAsk,
                        CourtDesk.Resources.Strings.MedicalDelete,
                        CourtDesk.Resources.Strings.FormCancel))
                {
                    _pending.Remove(shot);
                    Reload();
                }
                return;
            }
            MedicalImage img = b.Tag as MedicalImage;
            if (img == null)
            {
                return;
            }
            if (ConfirmDialog.Ask(this,
                    CourtDesk.Resources.Strings.MedicalTitle,
                    CourtDesk.Resources.Strings.MedicalDeleteAsk,
                    CourtDesk.Resources.Strings.MedicalDelete,
                    CourtDesk.Resources.Strings.FormCancel))
            {
                _medical.DeleteImage(img.Id);
                Reload();
            }
        }

        private void OpenClick(object sender, RoutedEventArgs e)
        {
            var b = sender as Button;
            if (b == null)
            {
                return;
            }
            string path = null;
            PendingShot shot = b.Tag as PendingShot;
            if (shot != null)
            {
                path = shot.SourcePath;
            }
            else
            {
                MedicalImage img = b.Tag as MedicalImage;
                if (img != null)
                {
                    path = img.AbsolutePath;
                }
            }
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception)
            {
            }
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}