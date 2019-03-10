using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Viscera_Cleanup_DJ
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        App App;
        ObservableCollection<Song> SongView;

        public MainWindow()
        {
            App = (App) System.Windows.Application.Current;
            InitializeComponent();

            SongView = new ObservableCollection<Song>();
            dataGrid.ItemsSource = SongView;
            dataGrid.CellEditEnding += SongList_Edited;

            Closing += Window_Closing;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Global.GamePath.Value == "" | !Directory.Exists(Global.GamePath.Value))
            {
                string GamePath = FindGameWizard();
                if (GamePath == null) { Close(); return; }
                if (GamePath != "")
                {
                    Global.GamePath.Value = GamePath;
                }
                else
                {
                    //TODO: Fix message;
                    MessengerBox.Information(this, "Continuing without Game. This is a bad idea.");
                }
            }

            PlaylistEditor.Read();
            LoadSongsToView();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            FFmpegRunner.RemoveFFmpeg();
        }

        public void LoadSongsToView()
        {
            SongView.Clear();
            foreach (Song song in PlaylistEditor.SongList)
            {
                SongView.Add(song);
            }

            noSongsLabel.Visibility = SongView.Count == 0 ? Visibility.Visible : Visibility.Hidden;
        }

        private void SongList_Edited(object sender, EventArgs e)
        {
            //PlaylistEditor.Write();
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void AddFilesButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            dialog.Multiselect = true;
            dialog.Filter = (
                "Audio files|*.mp3;*.wma;*.ogg;*.flac;*.wav|" +
                "Video files|*.mp4;*.webm;*.mkv;*.wmv;*.avi;*.mov;|" +
                "All files|*.*"
            );
            dialog.Title = "Add music";

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            ConversionDialog conversionDialog = new ConversionDialog();
            conversionDialog.Owner = this;

            BackgroundConverter converter = new BackgroundConverter();
            converter.RunWorkerCompleted += delegate (object s, RunWorkerCompletedEventArgs rwcea)
            {
                conversionDialog.Close();
            };
            converter.ProgressChanged += delegate (object s, ProgressChangedEventArgs prog)
            {
                //Song newSong = (Song) prog.UserState;
                //PlaylistEditor.SongList.Add(newSong);
                //PlaylistEditor.Write();
            };
            converter.RunWorkerAsync(dialog.FileNames);

            conversionDialog.ShowDialog();

            LoadSongsToView();
        }

        private void AddOnlineButton_Click(object sender, RoutedEventArgs e)
        {

        }

        public string FindGameWizard()
        {
            string GamePath = App.TryFindGame();
            if (GamePath != "") { return GamePath; }
            
            // -----------------------------------------------------------------------------------

            MessengerBox messenger = new MessengerBox(this);
            messenger.Title = "Hello!";
            messenger.Message.Text = (
                "We need to find Viscera Cleanup Detail on your PC.\n" +
                "Do you know where it might be installed?"
            );
            var yesButton = messenger.AddButton("Yes", true);
            messenger.AddButton("No", false);
            messenger.ShowDialog();
            if (messenger.Escaped) { return null; }

            if (messenger.ClickedButton == yesButton)
            {
                GamePath = App.GetLegitGamePath(App.BrowseForGame());
                if (GamePath != "")
                {
                    return GamePath;
                }
            }

            // -----------------------------------------------------------------------------------

            GameSniffer sniffer = new GameSniffer();
            sniffer.RunWorkerCompleted += delegate (object s, RunWorkerCompletedEventArgs rwcea)
            {
                messenger.Escaped = false;
                messenger.Close();
            };
            sniffer.RunWorkerAsync();

            string currentDir = Directory.GetCurrentDirectory();
            messenger = new MessengerBox(this);
            messenger.Message.Text = (
                "Can you start the game for a sec and we'll detect it?"
            );
            var skipButton = messenger.AddButton("No, Skip");

            messenger.ShowDialog();
            sniffer.CancelAsync();
            if (messenger.Escaped) { Close(); return null; }

            if (sniffer.GamePath != null) {
                MessengerBox.Information(this, "The game was detected. Thanks!");
                Activate();
                return sniffer.GamePath;
            }
            return "";
        }

        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsDialog dialog = new SettingsDialog();
            dialog.Owner = this;
            dialog.LoadSettings();
            dialog.SettingsChanged += PostSettingsEvent;

            dialog.ShowDialog();
        }

        private void PostSettingsEvent(object sender, EventArgs e)
        {
            PlaylistEditor.Read();
            LoadSongsToView();
        }

        private void FFmpegLinkClick(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)sender;
            Process.Start(link.ToolTip.ToString());
        }
    }
}
