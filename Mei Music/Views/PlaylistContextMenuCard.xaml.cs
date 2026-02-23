using System;
using System.Windows;
using System.Windows.Controls;

namespace Mei_Music
{
    /// <summary>
    /// Context-menu card for playlist actions.
    /// This control emits intents while MainWindow/ViewModel perform the actual operations.
    /// </summary>
    public partial class PlaylistContextMenuCard : UserControl
    {
        /// <summary>Raised when the user clicks the Delete option.</summary>
        public event EventHandler? DeleteRequested;

        /// <summary>
        /// Initializes playlist context menu card visuals.
        /// </summary>
        public PlaylistContextMenuCard()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Raises delete request event for the currently targeted playlist.
        /// </summary>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
