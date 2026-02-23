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
    /// Dialog shown when importing a file whose song name already exists.
    /// Lets user choose to rename incoming item, replace existing item, or cancel.
    /// </summary>
    public partial class DuplicateFileDialog
        : Window
    {
        /// <summary>
        /// Supported outcomes for duplicate-file resolution.
        /// </summary>
        public enum DuplicateFileAction
        {
            Rename,
            Replace,
            Cancel
        }

        /// <summary>
        /// Action selected by the user when the dialog closes.
        /// Defaults to <see cref="DuplicateFileAction.Cancel"/>.
        /// </summary>
        public DuplicateFileAction SelectedAction { get; private set; }

        /// <summary>
        /// Initializes dialog and sets safe default action to Cancel.
        /// </summary>
        public DuplicateFileDialog()
        {
            InitializeComponent();
            SelectedAction = DuplicateFileAction.Cancel; //default cancel when closed
        }

        /// <summary>
        /// Selects Rename action and closes dialog as accepted.
        /// </summary>
        private void RenameBtn_Click(Object sender, RoutedEventArgs e)
        {
            SelectedAction = DuplicateFileAction.Rename;
            DialogResult = true;
        }

        /// <summary>
        /// Selects Replace action and closes dialog as accepted.
        /// </summary>
        private void ReplaceBtn_Click(Object sender, RoutedEventArgs e)
        {
            SelectedAction = DuplicateFileAction.Replace;
            DialogResult = true;
        }

        /// <summary>
        /// Selects Cancel action and closes dialog as canceled.
        /// </summary>
        private void CancelBtn_Click(Object sender, RoutedEventArgs e)
        {
            SelectedAction = DuplicateFileAction.Cancel;
            DialogResult = false;
        }
    }
}
