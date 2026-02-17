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
using System.Windows.Shapes;

namespace Mei_Music
{
    /// <summary>
    /// Interaction logic for DeleteSongConfirmationWindow.xaml
    /// </summary>
    public partial class DeleteSongConfirmationWindow : Window
    {
        public bool IsConfirmed { get; private set; }

        public DeleteSongConfirmationWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
            IsConfirmed = false;
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            this.Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            this.Close();
        }
    }
}
