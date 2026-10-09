using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CourtDesk.Application;
using CourtDesk.UI.Dialogs;

namespace CourtDesk.UI.Dialogs
{
    public partial class MedicalImagesDialog : Window
    {
        private readonly MedicalService _medical;
        private readonly int _defendantId;
        private readonly List<MedicalImage> _images = new List<MedicalImage>();

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
            List.ItemsSource = null;
            List.ItemsSource = _images;
            EmptyText.Visibility = _images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            List.UpdateLayout();
            foreach (object o in _images)
            {
                MedicalImage img = o as MedicalImage;
                if (img == null)
                {
                    continue;
                }
                var panel = List.ItemContainerGenerator.ContainerFromItem(img) as FrameworkElement;
                if (panel == null)
                {
                    continue;
                }
                Image image = FindImage(panel);
                if (image != null)
                {
                    TryLoad(image, img.AbsolutePath);
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
                Title = "اختر صور الكشف الطبي",
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
                MessageDialog.Show(this, "الكشف الطبي",
                    string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "تمت إضافة {0} صورة، وتعذر إضافة {1}.", added, failed));
            }
        }

        private void DeleteClick(object sender, RoutedEventArgs e)
        {
            var b = sender as Button;
            if (b == null)
            {
                return;
            }
            MedicalImage img = b.Tag as MedicalImage;
            if (img == null)
            {
                return;
            }
            if (ConfirmDialog.Ask(this, "الكشف الطبي", "هل تريد حذف الصورة؟", "حذف", "إلغاء"))
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
            MedicalImage img = b.Tag as MedicalImage;
            if (img == null || string.IsNullOrEmpty(img.AbsolutePath))
            {
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(img.AbsolutePath)
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