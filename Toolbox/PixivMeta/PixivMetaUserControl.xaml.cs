using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Toolbox.PixivMeta
{
    /// <summary>
    /// PixivMetaUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class PixivMetaUserControl : UserControl, INotifyPropertyChanged
    {

        public PixivMetaUserControl()
        {
            InitializeComponent();
            DataGrid.ItemsSource = _pixivImageInfos;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string PropertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        private ObservableCollection<PixivImageInfo> _pixivImageInfos = new();
        private string SaveFolder { get; set; }
        private int currentProgress = 0;
        private bool controlEnable = true;
        private int maxProgress = 100;
        private string progressColor = "RoyalBlue";

        public int CurrentProgress
        {
            get { return currentProgress; }
            set
            {
                currentProgress = value;
                OnPropertyChanged("CurrentProgress");
            }
        }
        public bool ControlEnable
        {
            get { return controlEnable; }
            set
            {
                controlEnable = value;
                OnPropertyChanged("ControlEnable");
            }
        }
        public int MaxProgress
        {
            get { return maxProgress; }
            set
            {
                maxProgress = value;
                OnPropertyChanged("MaxProgress");
            }
        }
        public string ProgressColor
        {
            get { return progressColor; }
            set
            {
                progressColor = value;
                OnPropertyChanged("ProgressColor");
            }
        }

        private void PixivMeta_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }

        private void PixivMeta_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }

        private void PixivMeta_Drop(object sender, DragEventArgs e)
        {
            string[] filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (TextBox_SaveFolder.Text == "")
            {
                TextBox_SaveFolder.Text = filenames[0].Substring(0, filenames[0].LastIndexOf("\\"));
            }
            // 排除重复项
            foreach (var filename in filenames)
            {
                int i = 0;
                for (; i < _pixivImageInfos.Count; i++)
                {
                    if (_pixivImageInfos[i].FullName == filename)
                    {
                        break;
                    }
                }
                if (i == _pixivImageInfos.Count)
                {
                    _pixivImageInfos.Add(new PixivImageInfo(filename));
                }
            }
        }

        private void Button_AddFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*jpeg;*.png)|*.jpg;*.jpeg;*.png|All files (*.*)|*.*",
                Multiselect = true
            };
            if (openFileDialog.ShowDialog() == true)
            {
                if (TextBox_SaveFolder.Text == "")
                {
                    TextBox_SaveFolder.Text = openFileDialog.FileNames[0].Substring(0, openFileDialog.FileNames[0].LastIndexOf("\\"));
                }
                foreach (var filename in openFileDialog.FileNames)
                {
                    int i = 0;
                    for (; i < _pixivImageInfos.Count; i++)
                    {
                        if (_pixivImageInfos[i].FullName == filename)
                        {
                            break;
                        }
                    }
                    if (i == _pixivImageInfos.Count)
                    {
                        _pixivImageInfos.Add(new PixivImageInfo(filename));
                    }
                }
            }
        }

        private void Button_AddFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextBox_SaveFolder.Text = folderBrowserDialog.SelectedPath;
                List<string> fileNames = PixivImageInfo.GetMatchFiles(folderBrowserDialog.SelectedPath);
                foreach (var filename in fileNames)
                {
                    int i = 0;
                    for (; i < _pixivImageInfos.Count; i++)
                    {
                        if (_pixivImageInfos[i].FullName == filename)
                        {
                            break;
                        }
                    }
                    if (i == _pixivImageInfos.Count)
                    {
                        _pixivImageInfos.Add(new PixivImageInfo(filename));
                    }
                }
            }
        }

        private void Button_SaveFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextBox_SaveFolder.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private void Button_Reset_Click(object sender, RoutedEventArgs e)
        {
            TextBox_SaveFolder.Text = null;
            _pixivImageInfos.Clear();
            ProgressBar_Export.Value = 0;
        }

        private void Button_Start_Click(object sender, RoutedEventArgs e)
        {
            ControlEnable = false;
            ProgressBar_Export.Visibility = Visibility.Visible;
            MaxProgress = _pixivImageInfos.Count;
            CurrentProgress = 0;
            ProgressColor = "RoyalBlue";
            Thread thread = new Thread(new ThreadStart(TaskStart));
            thread.Start();
        }

        private void TaskStart()
        {
            Parallel.ForEach(_pixivImageInfos, (image) =>
            {
                if (image.State == null)
                {
                    try
                    {
                        image.Log += "start\n";
                        image.ChangeInfo();
                        image.Log += "finish changeinfo\n";
                        image.SaveFile(SaveFolder);
                        image.Log += "finish savefile\n";
                        image.State = true;
                    }
                    catch (Exception ex)
                    {
                        image.ErrorInfo = ex.ToString();
                        image.State = false;
                    }
                }
                CurrentProgress++;
            });
            CurrentProgress = MaxProgress;
            ProgressColor = "ForestGreen";
            ControlEnable = true;
            GC.Collect();
        }

        private void DataGrid_Info_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // HandyControl.Controls.Dialog dialog = new HandyControl.Controls.Dialog();
            // TextBlock textBlock = new TextBlock();
            // dialog.Content = textBlock;
            // textBlock.Text = ((ImageInfo)this.date_grid.CurrentItem).Log;
            // textBlock.Text += ((ImageInfo)this.date_grid.CurrentItem).ErrorInfo;
            // dialog.BeginInit();
        }

        private void TextBox_SaveFolder_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveFolder = TextBox_SaveFolder.Text;
        }

        private void Button_Clear_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < _pixivImageInfos.Count; i++)
            {
                if (_pixivImageInfos[i].State == true)
                {
                    _pixivImageInfos.Remove(_pixivImageInfos[i]);
                    i--;
                }
                else
                {
                    _pixivImageInfos[i].State = null;
                }
            }
            ProgressBar_Export.Visibility = Visibility.Hidden;
        }

    }
}
