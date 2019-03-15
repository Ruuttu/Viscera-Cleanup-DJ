using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
            songDataGrid.ItemsSource = SongView;
            songDataGrid.CellEditEnding += SongList_Edited;

            Closing += Window_Closing;
        }

        // Ensure a game path is configured. 
        // Otherwise configure automatically, or ask the user for help.
        // Returns 1 for success and 0 for failure.
        // Returns -1 if the user has "escaped" from the configuration wizard.
        public int GamePathPlease()
        {
            if (Global.GamePath.Value == "" | !Directory.Exists(Global.GamePath.Value))
            {
                string GamePath = App.TryFindGame();

                if (GamePath == "")
                {
                    GamePath = FindGameWizard();
                    if (GamePath == null) { return -1; }
                }

                if (GamePath != "")
                {
                    Global.GamePath.Value = GamePath;
                } else
                {
                    return 0;
                }
            }

            PlaylistEditor.Read();
            LoadSongsToView();
            return 1;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            GamePathPlease();
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
            statusText.Text = string.Format("{0} / 50 songs", SongView.Count);
            
        }

        private void SongList_Edited(object sender, EventArgs e)
        {
            PlaylistEditor.Write();
        }

        private void AddFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (GamePathPlease() != 1)
            {
                return;
            }
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

            string[] filenames = dialog.FileNames;

            int maxAdd = 50 - PlaylistEditor.SongList.Count;
            if (filenames.Length > maxAdd)
            {
                MessengerBox.Information(this, "Sorry, you can have max 50 songs.");
                Array.Resize(ref filenames, maxAdd);
            }

            if (filenames.Length < 1)
            {
                return;
            }

            // --------------------------------

            ConversionDialog conversionDialog = new ConversionDialog();
            conversionDialog.Owner = this;

            BlockingCollection<string> conversionQueue = new BlockingCollection<string>();
            foreach (string source in filenames)
            {
                conversionQueue.Add(source);
            }
            conversionQueue.CompleteAdding();

            // --------------------------------

            Stopwatch sw = new Stopwatch();
            sw.Start();

            int nThreads = 3;
            int finishedSteps = 0;
            int totalSteps = filenames.Length * BackgroundConverter.StepsPerSong;

            System.Windows.Threading.DispatcherTimer intervalTimer = new System.Windows.Threading.DispatcherTimer();
            double prediction = -0.5;
            intervalTimer.Tick += delegate (object s, EventArgs tickEvent)
            {
                prediction = Math.Min(0.8, prediction + 0.03);
                double progress = (finishedSteps + Math.Max(0, prediction)) / totalSteps;
                conversionDialog.progressBar.Value = progress * 98;
            };
            intervalTimer.Interval = new TimeSpan(200000);
            intervalTimer.Start();

            // --------------------------------

            List<BackgroundConverter> threads = new List<BackgroundConverter>();
            CountdownEvent threadsFinished = new CountdownEvent(nThreads);

            for (int t = 0; t < nThreads; t++)
            {
                BackgroundConverter converterThread = new BackgroundConverter();
                threads.Add(converterThread);

                converterThread.RunWorkerCompleted += delegate (object s, RunWorkerCompletedEventArgs rwcea)
                {
                    threadsFinished.Signal();
                    if (threadsFinished.IsSet)
                    {
                        Debug.Print(sw.Elapsed.ToString());
                        conversionDialog.Close();
                    }
                };
                converterThread.ProgressChanged += delegate (object s, ProgressChangedEventArgs prog)
                {
                    finishedSteps += prog.ProgressPercentage;
                    prediction = 0;
                };
                converterThread.RunWorkerAsync(conversionQueue);
            }

            // --------------------------------

            conversionDialog.Closing += delegate (object cancelSender, CancelEventArgs cancelEvent)
            {
                foreach (BackgroundConverter thread in threads)
                {
                    thread.CancelAsync();
                }
            };

            // --------------------------------

            conversionDialog.ShowDialog();
            LoadSongsToView();

        }

        private void AddOnlineButton_Click(object sender, RoutedEventArgs e)
        {

        }

        public string FindGameWizard()
        {
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
                string GamePath = App.GetLegitGamePath(App.BrowseForGame());
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
            if (messenger.Escaped) { return null; }

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

        private void LinkClick(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)sender;
            Process.Start(link.ToolTip.ToString());
        }

        private void DeleteSongs(object sender, RoutedEventArgs e)
        {
            if (songDataGrid.SelectedItems.Count <= 0)
            {
                return;
            }

            MessengerBox dialog = new MessengerBox(this);
            if (songDataGrid.SelectedItems.Count == 1)
            {
                Song selectedSong = (Song)songDataGrid.SelectedItem;
                dialog.Message.Text = string.Format(
                    "Are you sure you want to delete this song?\n{0} by {1}",
                    selectedSong.Title, selectedSong.Artist
                );
            } else
            {
                dialog.Message.Text = string.Format("Are you sure you want to delete {0} songs?", songDataGrid.SelectedItems.Count);
            }
            
            var deleteButton = dialog.AddButton("Delete");
            var cancelButton = dialog.AddButton("Cancel");
            dialog.ShowDialog();
            if (dialog.ClickedButton != deleteButton)
            {
                return;
            }

            foreach (Song selectedSong in songDataGrid.SelectedItems) {
                if (File.Exists(selectedSong.PackageFile()))
                {
                    File.Delete(selectedSong.PackageFile());
                }
                PlaylistEditor.SongList.Remove(selectedSong);
            }
            PlaylistEditor.Write();
            LoadSongsToView();
        }

        private void songDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.EditingEventArgs is TextCompositionEventArgs)
            {
                e.Cancel = true;
            }

        }

        private void EditSongCell(object sender, RoutedEventArgs e)
        {
            songDataGrid.BeginEdit();
        }

        private void songDataGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // When right-clicking on a cell, select that cell.
            if (e.RightButton == MouseButtonState.Pressed)
            {
                try
                {
                    System.Windows.Controls.DataGridCell cell = (System.Windows.Controls.DataGridCell)
                    VisualTreeHelper.GetParent(
                        VisualTreeHelper.GetParent(
                            VisualTreeHelper.GetParent(
                                (UIElement)e.OriginalSource)));

                    songDataGrid.CurrentColumn = cell.Column;
                } catch(InvalidCastException)
                {
                    // Mouse was pressed over something else than a cell.
                }
            }
        }
    }
}
