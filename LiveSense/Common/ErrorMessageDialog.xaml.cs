using System.Windows.Controls;

namespace LiveSense.Common
{
    /// <summary>
    /// Interaction logic for ErrorMessageDialog.xaml
    /// </summary>
    public partial class ErrorMessageDialog : UserControl
    {
        public ErrorMessageDialog(string message)
        {
            InitializeComponent();
            Message.Text = message;
        }
    }
}
