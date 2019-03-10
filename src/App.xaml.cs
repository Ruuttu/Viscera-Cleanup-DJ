using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace Viscera_Cleanup_DJ
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    /// 
    
    public static class Global
    {
        public static Mutex singleInstanceMutex = new Mutex(true, "viscera_cleanup_dj_single_instance");

        public static RegistryString GamePath = new RegistryString("GamePath");
        public static RegistryString GroupMask = new RegistryString("GroupMask");
        public static RegistryString PackageName = new RegistryString("PackageName", "CleanupDJ");
    }

    public partial class App : Application
    {
        public App()
        {

            
        }

        public void App_Startup(object sender, StartupEventArgs e)
        {
            if (Global.singleInstanceMutex.WaitOne(TimeSpan.Zero, true))
            {
                MainWindow window = new MainWindow();
                window.Show();

            } else
            {
                MessageBox.Show("Viscera Cleanup DJ is already running.");
                
                Current.Shutdown();
            }
        }

        public static string GetLegitGamePath(string path) {
            if (path == "") { return ""; }
            for (int level = 0; level < 4; level++) {
                if (level > 0) {
                    path = Path.GetDirectoryName(path);
                    if (path == null) { break; }
                }
                if (CheckIfLegitGamePath(path))
                {
                    return path;
                }
            }
            return "";
        }

        public static bool CheckIfLegitGamePath(string path)
        {
            return Directory.Exists(
                Path.Combine(path, "UDKGame", "Content"));
        }

        public String TryFindGame()
        {
            String gamePath;

            gamePath = GetLegitGamePath(SniffRunningGame());
            if (gamePath != "") { return gamePath; }

            gamePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Steam\steamapps\common\Viscera");
            if (CheckIfLegitGamePath(gamePath)) { return gamePath; }

            gamePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Steam\steamapps\common\Viscera");
            if (CheckIfLegitGamePath(gamePath)) { return gamePath; }

            return "";
        }

        public static String SniffRunningGame()
        {
            Process[] matchingProcesses = Process.GetProcessesByName("UDK");

            foreach (Process p in matchingProcesses)
            {
                String image;
                try {
                    image = p.MainModule.FileName;
                } catch (Exception) {
                    continue;
                }
                if (image.ToLower().IndexOf("viscera") != -1)
                {
                    return image;
                }
            }

            return "";
        }

        public static string BrowseForGame()
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            dialog.Description = "Find the Viscera folder.\nIt's probably under ...\\SteamApps\\Common.";
            dialog.ShowNewFolderButton = false;
            dialog.ShowDialog();
            return dialog.SelectedPath;
        }
    }

    public class Ogg
    {
        public byte[] Encoded = new byte[] { };
        public float Duration = 0;
        public string Title = "";
        public string Artist = "";

        public void WriteToPackage(string outFile, string cue)
        {
            int templateOggLength = 3371;
            int totalLength = Encoded.Length + Properties.Resources.template_upk.Length - templateOggLength;
            byte[] package = new byte[totalLength];

            int sampleCount = (int) Duration * 44100;

            Buffer.BlockCopy(Properties.Resources.template_upk, 0, package, 0, Properties.Resources.template_upk.Length);

            Buffer.BlockCopy(Guid.NewGuid().ToByteArray(), 0, package, 69, 16);
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(cue.ToCharArray()), 0, package, 598, 14);
            Buffer.BlockCopy(BitConverter.GetBytes(Encoded.Length + 574), 0, package, 1403, 4);
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(cue.ToCharArray()), 0, package, 1508, 14);
            Buffer.BlockCopy(BitConverter.GetBytes(Duration), 0, package, 1696, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(44100), 0, package, 2066, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(sampleCount * 4), 0, package, 2094, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(Encoded.Length), 0, package, 2464, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(Encoded.Length), 0, package, 2468, 4);
            Buffer.BlockCopy(Encoded, 0, package, 2476, Encoded.Length);

            if (!Directory.Exists(Path.GetDirectoryName(outFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outFile));
            }
            File.WriteAllBytes(outFile, package);
        }
    }

    public class FFmpegRunner
    {
        static string tempFFmpegPath = Path.Combine(Path.GetTempPath(), "viscera-cleanup-dj-ffmpeg.exe");

        static public Ogg ConvertOgg(string sourceFile)
        {
            Ogg output = new Ogg();

            Process ff = new Process();
            ff.StartInfo.CreateNoWindow = true;
            ff.StartInfo.UseShellExecute = false;
            ff.StartInfo.FileName = GetFFmpeg();
            ff.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            ff.StartInfo.Arguments = string.Format(
                "-i \"{0}\" -ac 1 -f ogg -codec:a libvorbis -b:a 110k -ar 44100 pipe:1", sourceFile);
            ff.StartInfo.RedirectStandardOutput = true;
            ff.StartInfo.RedirectStandardError = true;
            ff.Start();

            // -------------------------

            string printout = "";
            Thread errorStreamReader = new Thread(() =>
            {
                StreamReader reader = new StreamReader(ff.StandardError.BaseStream); // Make a reader that decodes UTF-8.
                printout = reader.ReadToEnd();
            });
            errorStreamReader.Start();

            // -------------------------

            int index = 0;
            int readSize = 16384;
            int nBytesRead;
            do
            {
                if (output.Encoded.Length < index + readSize)
                {
                    Array.Resize(ref output.Encoded, output.Encoded.Length + readSize * 5);
                }
                nBytesRead = ff.StandardOutput.BaseStream.Read(output.Encoded, index, readSize);
                index += nBytesRead;
            } while (nBytesRead > 0);
            Array.Resize(ref output.Encoded, index);

            errorStreamReader.Join();

            // -------------------------

            string[] lines = printout.Split('\n');
            int a, b;
            foreach (string line in lines)
            {
                a = line.IndexOf("Duration:");
                if (a != -1)
                {
                    b = line.IndexOf(",", a);
                    if (b != -1)
                    {
                        string durationStr = line.Substring(a + 9, b - a - 9).Trim();
                        TimeSpan dur = TimeSpan.ParseExact(durationStr, "g", new CultureInfo("en-US"));
                        if (dur.TotalSeconds > output.Duration)
                        {
                            output.Duration = (float) dur.TotalSeconds;
                        }
                    }
                }

                a = line.IndexOf(" artist");
                b = line.IndexOf(":", a + 1);
                if (a != -1 && b > a)
                {
                    output.Artist = line.Substring(b+1).Trim();
                }

                a = line.IndexOf(" title");
                b = line.IndexOf(":", a + 1);
                if (a != -1 && b > a)
                {
                    output.Title = line.Substring(b+1).Trim();
                }
            }

            return output;
        }

        static public string GetFFmpeg()
        {
            FileInfo info = new FileInfo(tempFFmpegPath);
            if (!info.Exists || info.Length != Properties.Resources.ffmpeg_3_4_1.Length)
            {
                File.WriteAllBytes(tempFFmpegPath, Properties.Resources.ffmpeg_3_4_1);
            }

            return tempFFmpegPath;
        }

        static public bool RemoveFFmpeg()
        {
            if (File.Exists(tempFFmpegPath))
            {
                try
                {
                    File.Delete(tempFFmpegPath);
                    return true;
                } catch(IOException)
                {
                } catch(UnauthorizedAccessException) {
                }
                return false;
            }
            return true;
        }
    }

    public class GameSniffer : BackgroundWorker
    {
        public string GamePath;
        public GameSniffer()
        {
            WorkerSupportsCancellation = true;
            DoWork += GameSniffingWork;
        }

        public void GameSniffingWork(object s, DoWorkEventArgs args)
        {
            String sniffed;
            while (true)
            {
                if (CancellationPending)
                {
                    break;
                }
                sniffed = App.SniffRunningGame();

                if (sniffed != "")
                {
                    sniffed = App.GetLegitGamePath(sniffed);
                    if (sniffed != "")
                    {
                        GamePath = sniffed;
                        break;
                    }
                }

                Thread.Sleep(1000);
            }
        }
    }

    public class BackgroundConverter : BackgroundWorker
    {
        public BackgroundConverter()
        {
            WorkerReportsProgress = true;
            WorkerSupportsCancellation = true;
            DoWork += ConvertQueue;
        }

        public void ConvertQueue(object s, DoWorkEventArgs args)
        {
            string[] filenames = (string[]) args.Argument;

            int i = 0;
            foreach (string source in filenames)
            {
                int packIndex = PlaylistEditor.GetFreeIndex();
                string packName = Global.PackageName.Value;
                if (Global.GroupMask.Value != "")
                {
                    packName += "_" + Global.GroupMask.Value;
                }
                packName += "_" + string.Format("{0:0000}", packIndex);

                Song song = new Song();
                song.Package = packName;
                song.SoundCue = Song.MakeSoundCue(packIndex);

                Ogg output = FFmpegRunner.ConvertOgg(source);
                output.WriteToPackage(song.PackageFile(), song.SoundCue);

                song.Title = output.Title;
                song.Artist = output.Artist;
                PlaylistEditor.SongList.Add(song);
                PlaylistEditor.Write();

                ReportProgress(i);
                i++;
            }
        }
    }

    public class Song
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string GroupMask { get; set; }

        public string Package { get; set; }
        public string SoundCue { get; set; }

        static public Song ParseFromIni(string iniStr)
        {
            // (SongTitle="MyTitle",SongArtist="MyArtist",SongSoundCue="MySong0001.mysong0001-cue",BeatTracks=((StartTime=0.0,Frequency=3,Intensity=0.1)))

            Song song = new Song();

            int a, b;

            a = iniStr.IndexOf('"');
            b = iniStr.IndexOf('"', a + 1);
            song.Title = iniStr.Substring(a + 1, b - a - 1);

            a = iniStr.IndexOf('"', b + 1);
            b = iniStr.IndexOf('"', a + 1);
            song.Artist = iniStr.Substring(a + 1, b - a - 1);

            a = iniStr.IndexOf('"', b + 1);
            b = iniStr.IndexOf('"', a + 1);
            string soundCueStr = iniStr.Substring(a + 1, b - a - 1);
            a = soundCueStr.IndexOf(".");
            song.Package = soundCueStr.Substring(0, a);
            song.SoundCue = soundCueStr.Substring(a + 1);

            return song;
        }

        public string PackageFile()
        {
            return Path.Combine(Global.GamePath.Value, @"UDKGame\Content\Mods", Global.PackageName.Value, Package + ".upk");
        }

        public string ToIniString()
        {
            return string.Format("(SongTitle=\"{0}\",SongArtist=\"{1}\",SongSoundCue=\"{2}\")", 
                Title.Replace('"', '\''), Artist.Replace('"', '\''), Package + "." + SoundCue);
        }

        public static string MakeSoundCue(int index)
        {
            return string.Format("cleanupdj_{0:0000}", index);
        }
    }

    public class PlaylistEditor
    {
        public static List<Song> SongList = new List<Song>();

        public static void Read()
        {
            string radio = "";
            string groupMask = "";
            string iniFile = IniFilePath(Global.PackageName.Value);

            SongList.Clear();

            if (!File.Exists(iniFile))
            {
                return;
            }

            foreach (string rawLine in File.ReadLines(iniFile))
            {
                string line = rawLine.Trim();

                // -----------------------------------------

                if (line.StartsWith("["))
                {
                    string[] words = line.Split(' ');
                    if (words[words.Length-1] == "VCUIDataProvider_RadioInfo]")
                    {
                        radio = words[0].Substring(1);
                        groupMask = "";
                    }
                    continue;
                }

                // -----------------------------------------

                int eqi = line.IndexOf('=');
                if (eqi == -1) {continue;}

                string key = line.Substring(0, eqi).TrimEnd();
                string value = line.Substring(eqi + 1).TrimStart();

                if (key == "GroupMask")
                {
                    groupMask = value.Trim('"');

                } else if (key == "Songs")
                {
                    Song song = Song.ParseFromIni(value);
                    song.GroupMask = groupMask;
                    SongList.Add(song);
                }
            }
        }

        public static void Write()
        {
            string iniFile = IniFilePath(Global.PackageName.Value);

            List<string> iniLines = new List<string>();

            iniLines.Add(string.Format("[{0} VCUIDataProvider_RadioInfo]", Global.PackageName.Value));

            if (Global.GroupMask.Value != "")
            {
                iniLines.Add(string.Format("GroupMask=\"{0}\"", Global.GroupMask.Value));
            }

            foreach (Song song in SongList)
            {
                iniLines.Add("Songs=" + song.ToIniString());
            }

            File.WriteAllLines(iniFile, iniLines);
        }

        public static int GetFreeIndex()
        {
            for (int i = 0; i <= 9999; i++)
            {
                string cue = Song.MakeSoundCue(i);
                bool taken = false;
                foreach (Song song in SongList)
                {
                    if (song.SoundCue == cue)
                    {
                        taken = true;
                        break;
                    }
                }

                if (!taken)
                {
                    return i;
                }
            }
            throw new Exception();
        }

        public static string IniFilePath(string channel)
        {
            string name = channel;
            if (Global.GroupMask.Value != "")
            {
                name += "_" + Global.GroupMask.Value;
            }
            return Path.Combine(Global.GamePath.Value, @"UDKGame\Config", name + ".ini");
        }
    }

    public class RegistryString
    {
        string Name;
        string InitialValue;

        public RegistryString(string name, string initial = "")
        {
            Name = name;
            InitialValue = initial;
        }

        public string Value
        {
            get
            {
                RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\VisceraCleanupDJ");
                if (Key == null)
                {
                    return InitialValue;
                }
                string Value = (string)Key.GetValue(Name, InitialValue);
                Key.Close();
                return Value;
            }

            set
            {
                RegistryKey Key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\VisceraCleanupDJ");
                Key.SetValue(Name, value);
                Key.Close();
            }

        }
    }

}
