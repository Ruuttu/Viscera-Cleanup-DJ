using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace Viscera_Cleanup_DJ
{
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        public App App;

        public event EventHandler SettingsChanged;

        public SettingsDialog()
        {
            InitializeComponent();
        }

        public string ValidationError()
        {
            if (!Directory.Exists(gamePath.Text))
            {
                return "Game Install Location is invalid (does not exist).";
            }

            if (!Path.IsPathRooted(gamePath.Text))
            {
                return "Game Install Location must be a full path.";
            }

            return "";
        }

        private void gamePathBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            string path = App.BrowseForGame();
            if (path != "")
            {
                gamePath.Text = path;
            }
        }

        public void LoadSettings()
        {
            if (Global.GroupMask.Value != "")
            {
                groupMask.Text = Global.GroupMask.Value;
            }
            gamePath.Text = Global.GamePath.Value;

            packageName.Text = Global.PackageName.Value;
        }

        public bool CheckAndSaveSettings()
        {
            string errorMessage = ValidationError();

            if (errorMessage != "")
            {
                MessengerBox.Error(this, "Can't save. " + errorMessage);
                return false;
            }

            Global.GamePath.Value = gamePath.Text;

            if (groupMask.SelectedIndex == 0) {
                Global.GroupMask.Value = "";
            } else
            {
                Global.GroupMask.Value = groupMask.Text;
            }

            Global.PackageName.Value = packageName.Text;

            return true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckAndSaveSettings())
            {
                SettingsChanged.Invoke(this, new EventArgs());
            }
        }
    }
}
