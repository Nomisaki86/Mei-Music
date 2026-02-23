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
    /// Confirmation dialog used before deleting a song from library and disk.
    /// Encapsulates a yes/no decision and exposes result through <see cref="IsConfirmed"/>.
    /// </summary>
    public partial class DeleteSongConfirmationWindow : Window
    {
        /// <summary>
        /// True when user confirms deletion; false when canceled/declined.
        /// </summary>
        public bool IsConfirmed { get; private set; }

        /// <summary>
        /// Initializes dialog and sets message text shown to the user.
        /// </summary>
        public DeleteSongConfirmationWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
            IsConfirmed = false;
        }

        /// <summary>
        /// Handles Yes action by marking confirmation and closing dialog.
        /// </summary>
        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            this.Close();
        }

        /// <summary>
        /// Handles No action by clearing confirmation and closing dialog.
        /// </summary>
        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            this.Close();
        }
    }
}
