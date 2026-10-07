using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
            var viewer = new DocumentViewer();
            if (_doc != null)
            {
                viewer.Document = _doc;
            }
            DocumentHost.Child = viewer;
            InfoText.Text = (_doc == null ? "0" : _doc.Pages.Count.ToString(
                System.Globalization.CultureInfo.InvariantCulture)) + " - " + Strings.PrintSent;
            DirectButton.Content = Strings.PrintSend;
            ChooseButton.Content = Strings.PrintChoose;
            CloseButton.Content = Strings.FormCancel;
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