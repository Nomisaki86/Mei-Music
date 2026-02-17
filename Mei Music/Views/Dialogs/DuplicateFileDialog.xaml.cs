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
    public partial class DuplicateFileDialog
        : Window
    {
        public enum DuplicateFileAction
        {
            Rename,
            Replace,
            Cancel
        }

        public DuplicateFileAction SelectedAction { get; private set; }
        public DuplicateFileDialog()
        {
            InitializeComponent();
            SelectedAction = DuplicateFileAction.Cancel; //default cancel when closed
        }

        private void RenameBtn_Click(Object sender, RoutedEventArgs e)
        {
            SelectedAction = DuplicateFileAction.Rename;
            DialogResult = true;
        }
        private void ReplaceBtn_Click(Object sender, RoutedEventArgs e)
        {
            SelectedAction = DuplicateFileAction.Replace;
            DialogResult = true;
        }
        private void CancelBtn_Click(Object sender, RoutedEventArgs e)
        {
            SelectedAction = DuplicateFileAction.Cancel;
            DialogResult = false;
        }
    }
}
