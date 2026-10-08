using System;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CourtDesk.Resources;

namespace CourtDesk.UI.Dialogs
{
    public partial class PrintPreviewWindow : Window
    {
        private readonly FixedDocument _doc;

        public PrintPreviewWindow(object doc)
        {
            InitializeComponent();
            _doc = doc as FixedDocument;
            BuildPages();
            int pages = PageCount();
            InfoText.Text = pages.ToString(
                System.Globalization.CultureInfo.InvariantCulture) + " - " + Strings.PrintSent;
            DirectButton.Content = Strings.PrintSend;
            ChooseButton.Content = Strings.PrintChoose;
            CloseButton.Content = Strings.FormCancel;
        }

        private int PageCount()
        {
            if (_doc == null)
            {
                return 0;
            }
            int n = 0;
            foreach (PageContent pc in _doc.Pages)
            {
                n++;
            }
            return n;
        }

        private void BuildPages()
        {
            DocumentHost.Child = null;
            var panel = new StackPanel { Margin = new Thickness(4) };
            if (_doc != null)
            {
                int index = 0;
                int total = PageCount();
                foreach (PageContent pc in _doc.Pages)
                {
                    index++;
                    FixedPage page = pc.Child;
                    if (page == null)
                    {
                        continue;
                    }
                    page.Measure(new Size(page.Width, page.Height));
                    page.Arrange(new Rect(0, 0, page.Width, page.Height));
                    page.UpdateLayout();
                    int w = (int)Math.Ceiling(page.Width);
                    int h = (int)Math.Ceiling(page.Height);
                    ImageSource src;
                    try
                    {
                        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                        bmp.Render(page);
                        src = bmp;
                    }
                    catch (System.Exception)
                    {
                        continue;
                    }
                    var img = new Image
                    {
                        Source = src,
                        Width = w,
                        Height = h,
                        Stretch = Stretch.None,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 12)
                    };
                    var border = new Border
                    {
                        Child = img,
                        Background = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0xC6, 0xCE)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    panel.Children.Add(border);
                    var cap = new TextBlock
                    {
                        Text = index.ToString(
                            System.Globalization.CultureInfo.InvariantCulture)
                            + " / " + total.ToString(
                                System.Globalization.CultureInfo.InvariantCulture),
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x46, 0x50, 0x5E)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    panel.Children.Add(cap);
                }
            }
            var scroller = new ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            DocumentHost.Child = scroller;
        }

        private void DirectClick(object sender, RoutedEventArgs e)
        {
            try
            {
                WriteDirect();
                DialogResult = true;
            }
            catch (System.Exception)
            {
                MessageDialog.Show(this, Strings.PrintFail, Strings.PrintFail);
                DialogResult = false;
            }
        }

        private void ChooseClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new PrintDialog();
                using (PrintQueue queue = LocalPrintServer.GetDefaultPrintQueue())
                {
                    dialog.PrintQueue = queue;
                    dialog.PrintTicket = Ticket();
                }
                if (dialog.ShowDialog() == true)
                {
                    dialog.PrintDocument(_doc.DocumentPaginator, Strings.RosterTitle);
                    DialogResult = true;
                }
            }
            catch (System.Exception)
            {
                MessageDialog.Show(this, Strings.PrintFail, Strings.PrintFail);
            }
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static PrintTicket Ticket()
        {
            var ticket = new PrintTicket();
            ticket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4, 297.0, 210.0);
            ticket.PageOrientation = PageOrientation.Landscape;
            ticket.CopyCount = 1;
            return ticket;
        }

        private void WriteDirect()
        {
            using (PrintQueue queue = LocalPrintServer.GetDefaultPrintQueue())
            {
                var writer = PrintQueue.CreateXpsDocumentWriter(queue);
                writer.Write(_doc, Ticket());
            }
        }
    }
}