using System;
using System.Windows;
using System.Windows.Controls;

namespace Mei_Music
{
    public partial class PlaylistContextMenuCard : UserControl
    {
        /// <summary>Raised when the user clicks the Delete option.</summary>
        public event EventHandler? DeleteRequested;

        public PlaylistContextMenuCard()
        {
            InitializeComponent();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
