using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using WindowsHook;
using WindowsInput;
using WindowsInput.Native;
using KeyPressEventArgs = WindowsHook.KeyPressEventArgs;
using Winfred.ViewModel;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Documents;
using System.Runtime.InteropServices;
using WK.Libraries.SharpClipboardNS;
using System.Drawing.Imaging;

namespace Winfred
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// global hook for key events
        /// </summary>
        private IKeyboardMouseEvents _GlobalHook;

        private static InputSimulator _InputSimulator = new InputSimulator();

        /// <summary>
        /// Clipboard watcher
        /// </summary>
        private SharpClipboard _SharpClipboard = new SharpClipboard();

        /// <summary>
        /// Delegation for backspacing and replacing texts
        /// </summary>
        /// <param name="source">Source string for snippet keycode</param>
        /// <param name="target">Snippet content to insert</param>
        private delegate void NeedBackspace(string source, string target);
        private event NeedBackspace BackspaceTrigger;

        /// <summary>
        /// Key combinations and actions list on watching
        /// </summary>
        private Dictionary<Combination, Action> _ActionList = new Dictionary<Combination, Action>();

        /// <summary>
        /// Configuration file
        /// </summary>
        private string _ConfigFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Winfred\\Winfred.ini");
        /// <summary>
        /// Snippets configuration directory
        /// </summary>
        private string _SnippetsDir = "";

        /// <summary>
        /// String watching on user's input
        /// </summary>
        private string _CurrentString = "";

        /// <summary>
        /// Key word snippets map
        /// </summary>
        private ResultsViewModel _SnippetsViewModel = new ResultsViewModel();

        private readonly ResultsViewModel _ClipboardResults = new ResultsViewModel();

        private Dictionary<int, BitmapSource> _ClipboardImages = new Dictionary<int, BitmapSource>();

        public MainWindow()
        {
            InitializeComponent();
            Winfred_SetStartupPosition();
            Load();
            Subscribe();
            this.Visibility = Visibility.Hidden;

            _SharpClipboard.ClipboardChanged += SharpClipboard_ClipboardChanged;

            this._ClipboardResults.PropertyChanged += M_ClipboardResults_PropertyChanged;
            this.ResultsListBox.SelectionChanged += ResultsListBox_SelectionChanged;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Unsubscribe();
            WinfredNotifyIcon.Visibility = Visibility.Collapsed;
            WinfredNotifyIcon.Dispose();
        }

        #region 配置加载相关函数
        private void Load()
        {
            _CurrentString = "";
            _ClipboardImages.Clear();
            _ClipboardResults.Clear();
            _SnippetsViewModel.Clear();
            LoadConfiguration();
            LoadSnippets();
        }

        /// <summary>
        /// Load global configuration from config file
        /// </summary>
        private void LoadConfiguration()
        {
            if (File.Exists(_ConfigFilePath))
            {
                using (StreamReader sr = File.OpenText(_ConfigFilePath))
                {
                    string s;
                    while ((s = sr.ReadLine()) != null)
                    {
                        string[] strSplitted = s.Split('=');
                        if (strSplitted.Length < 2)
                        {
                            continue;
                        }
                        strSplitted[0] = strSplitted[0].Trim(); // conf name
                        strSplitted[1] = strSplitted[1].Trim(); // conf content

                        if (strSplitted[0] == "snippets")
                        {
                            _SnippetsDir = strSplitted[1];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Load snippet name and content from snippet directory
        /// </summary>
        private void LoadSnippets()
        {
            if (Directory.Exists(_SnippetsDir))
            {
                var Dirs = Directory.EnumerateDirectories(_SnippetsDir);
                foreach (string currentDir in Dirs)
                {
                    try
                    {
                        var jsonFiles = Directory.EnumerateFiles(currentDir, "*.json");

                        foreach (string currentFile in jsonFiles)
                        {
                            if (File.Exists(currentFile))
                            {
                                using (StreamReader sr = File.OpenText(currentFile))
                                {
                                    string snippetText = "";
                                    string snippetName = "";

                                    string content = sr.ReadToEnd();
                                    JsonTextReader jsonReader = new JsonTextReader(new StringReader(content));
                                    while (jsonReader.Read())
                                    {
                                        if (jsonReader.Value != null)
                                        {
                                            if (jsonReader.TokenType is JsonToken.PropertyName
                                                && jsonReader.Value.ToString() == "snippet")
                                            {
                                                // Read the snippet text itself
                                                jsonReader.Read();
                                                if (jsonReader.Value != null
                                                    && jsonReader.TokenType is JsonToken.String)
                                                {
                                                    snippetText = jsonReader.Value.ToString();
                                                }
                                            }
                                            else
                                            if (jsonReader.TokenType is JsonToken.PropertyName && jsonReader.Value.ToString() == "keyword")
                                            {
                                                // Read the snippet name
                                                jsonReader.Read();
                                                if (jsonReader.Value != null
                                                    && jsonReader.TokenType is JsonToken.String)
                                                {
                                                    snippetName = jsonReader.Value.ToString();
                                                }
                                            }
                                        }
                                    }
                                    _SnippetsViewModel.Results.Add(new ResultViewModel(snippetName,
                                                                                        snippetText,
                                                                                        ResultTypeEnum.Text,
                                                                                        snippetName.GetHashCode()));
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("ERROR Reading snippet files.\n{0}", e.Message.ToString());
                    }
                }
            }
        }
        #endregion

        #region 全局键鼠事件监听
        public void Subscribe()
        {
            _GlobalHook = Hook.GlobalEvents();

            _GlobalHook.KeyPress += GlobalHookKeyPress;

            // Display clipboard contents
            Combination clipboardCombination = Combination.TriggeredBy(Keys.Space).With(Keys.Alt).With(Keys.LControlKey);
            Action actionClipboard = DisplayClipboardContents;
            _ActionList.Add(clipboardCombination, actionClipboard);

            Hook.GlobalEvents().OnCombination(_ActionList);

            this.BackspaceTrigger += new NeedBackspace(ReplaceSourceByTarget);
        }

        public void Unsubscribe()
        {
            _GlobalHook.KeyPress -= GlobalHookKeyPress;
            _GlobalHook.Dispose();
        }
        #endregion

        #region 界面相关事件处理
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            }
            catch
            {
                Console.WriteLine("ERROR Moving Window");
            }
        }

        private void Winfred_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Winfred_Hide();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                this._ClipboardResults.SelectNext();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                this._ClipboardResults.SelectPrevious();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                try
                {
                    this._SharpClipboard.ClipboardChanged -= SharpClipboard_ClipboardChanged;

                    ResultViewModel target = this._ClipboardResults.SelectedResultViewModel;

                    if (target.MainTypeEnum == ResultTypeEnum.Text)
                    {
                        SetText2Clipboard(target.ResultPreview);
                    }
                    else if (target.MainTypeEnum == ResultTypeEnum.Image)
                    {
                        if (_ClipboardImages.ContainsKey(target.HashCode))
                        {
                            DataObject dataObject = new DataObject(DataFormats.Bitmap, _ClipboardImages[target.HashCode]);
                            Clipboard.SetDataObject(dataObject, true);
                            GC.Collect();
                        }
                        else
                        {
                            Clipboard.Clear();
                        }
                    }

                    Application.Current.MainWindow.Hide();

                    _InputSimulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_V);

                    this._SharpClipboard.ClipboardChanged += SharpClipboard_ClipboardChanged;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.Message.ToString());
                }

                e.Handled = true;
            }
        }

        private void Winfred_Show()
        {
            this.query_text.Clear();
            this.Visibility = Visibility.Visible;
            this.Activate();
            this.query_text.Focus();
            this.ResultsListBox.SelectedIndex = -1;
        }

        private void Winfred_Hide()
        {
            this.Visibility = Visibility.Hidden;
        }

        private void Winfred_SetStartupPosition()
        {
            double screenWidth = SystemParameters.WorkArea.Width;
            double screenHeight = SystemParameters.WorkArea.Height;

            this.Top = screenHeight - screenHeight * 7 / 10.0;
            this.Left = (screenWidth - this.Width) / 2;
        }

        private void tray_menu_quit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
            e.Handled = true;
        }

        private void tray_menu_display_Click(object sender, RoutedEventArgs e)
        {
            Winfred_Show();
            e.Handled = true;
        }

        private void tray_menu_reload_Click(object sender, RoutedEventArgs e)
        {
            Load();
            e.Handled = true;
        }
        #endregion

        #region snippet功能代码
        private void GlobalHookKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '$')
            {
                _CurrentString = "$";
            }
            else if (e.KeyChar == '\b')
            {
                if (_CurrentString.Length > 0)
                {
                    _CurrentString = _CurrentString.Remove(_CurrentString.Length - 1, 1);
                }
            }
            else
            {
                _CurrentString += e.KeyChar.ToString();
                bool IsSuccess = _SnippetsViewModel.FindByResultName(_CurrentString, out ResultViewModel targetSnippet);
                if (IsSuccess)
                {
                    this._SharpClipboard.ClipboardChanged -= SharpClipboard_ClipboardChanged;

                    BackspaceTrigger.BeginInvoke(_CurrentString, targetSnippet.ResultPreview, BackspaceTriggerCallBack, null);
                }
                else if (_CurrentString.Length >= 15)
                {
                    _CurrentString = "";
                }
            }
        }

        private void BackspaceTriggerCallBack(IAsyncResult ar)
        {
            this._SharpClipboard.ClipboardChanged += SharpClipboard_ClipboardChanged;
        }

        private void ReplaceSourceByTarget(string source, string target)
        {
            int n = source.Length;
            for (int i = 0; i < n; i++)
            {
                _InputSimulator.Keyboard.KeyPress(VirtualKeyCode.BACK);
            }
            Thread.Sleep(10);
            SetText2Clipboard(target);

            _InputSimulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_V);
        }

        [STAThread]
        public static void SetText2Clipboard(string text)
        {
            Thread th = new Thread(new ThreadStart(delegate ()
            {
                DataObject dataObject = new DataObject(DataFormats.Text, text);
                Clipboard.SetDataObject(dataObject, true);
                GC.Collect();
            }));
            th.TrySetApartmentState(ApartmentState.STA);
            th.Start();
            th.Join();
        }
        #endregion

        #region clipboard功能代码
        private void DisplayClipboardContents()
        {
            this.Winfred_Show();
        }

        private void SharpClipboard_ClipboardChanged(object sender, SharpClipboard.ClipboardChangedEventArgs e)
        {
            if (e.ContentType == SharpClipboard.ContentTypes.Text)
            {
                string tempString = _SharpClipboard.ClipboardText;
                if (_ClipboardResults.FindByResultName(tempString, out ResultViewModel temp1))
                {
                    return;
                }

                _ClipboardResults.Results.Insert(0,
                                            new ResultViewModel(tempString,
                                                                tempString,
                                                                ResultTypeEnum.Text,
                                                                tempString.GetHashCode()));

                return;
            }
            else if (e.ContentType == SharpClipboard.ContentTypes.Image)
            {
                BitmapSource tempBitMap = Clipboard.GetImage();
                if (tempBitMap != null)
                {
                    string name = System.String.Format("Image {0}x{1}", tempBitMap.PixelWidth, tempBitMap.PixelHeight);
                    _ClipboardResults.Results.Insert(0,
                            new ResultViewModel(name,
                                                "",
                                                ResultTypeEnum.Image,
                                                tempBitMap.GetHashCode()));
                    _ClipboardImages.Add(tempBitMap.GetHashCode(), tempBitMap);
                }
            }
        }

        private void M_ClipboardResults_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            this.ResultsListBox.DataContext = this._ClipboardResults;
        }

        private void ResultsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.PreviewTextBlock.Document.Blocks.Clear();

            try
            {
                ResultViewModel target = this._ClipboardResults.Results[this.ResultsListBox.SelectedIndex];
                if (target.MainTypeEnum == ResultTypeEnum.Text)
                {
                    this.PreviewTextBlock.AppendText(target.ResultPreview);
                }
                else if (target.MainTypeEnum == ResultTypeEnum.Image)
                {
                    if (_ClipboardImages.ContainsKey(target.HashCode))
                    {
                        Image image = new Image
                        {
                            Source = _ClipboardImages[target.HashCode]
                        };

                        Paragraph paragraph = new Paragraph();
                        paragraph.Inlines.Add(image);
                        this.PreviewTextBlock.Document.Blocks.Add(paragraph);
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message.ToString());
            }

            e.Handled = true;
        }
        #endregion
    }
}
