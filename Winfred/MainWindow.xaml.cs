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
//using static WK.Libraries.SharpClipboardNS.SharpClipboard;

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
        private IKeyboardMouseEvents m_GlobalHook;

        /// <summary>
        /// Clipboard watcher
        /// </summary>
        private SharpClipboard sharpClipboard = new SharpClipboard();

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
        private Dictionary<Combination, Action> m_ActionList = new Dictionary<Combination, Action>();

        /// <summary>
        /// Configuration file
        /// </summary>
        private string m_ConfigFilePath = "Winfred.ini";
        /// <summary>
        /// Snippets configuration directory
        /// </summary>
        private string m_SnippetsDir = "";

        /// <summary>
        /// String watching on user's input
        /// </summary>
        private string m_CurrentString = "";

        private string m_TopTextInClipboard = "";

        private BitmapSource m_TopBitmapSource;

        /// <summary>
        /// Key word snippets map
        /// </summary>
        private ResultsViewModel m_SnippetsViewModel = new ResultsViewModel();

        private ResultsViewModel m_ClipboardResults = new ResultsViewModel();

        private Dictionary<int, BitmapSource> m_ClipboardImages = new Dictionary<int, BitmapSource>();

        //private ObservableCollection<ResultViewModel> m_Results = new ObservableCollection<ResultViewModel>();

        public MainWindow()
        {
            InitializeComponent();
            Winfred_SetStartupPosition();
            Load();
            Subscribe();
            this.Visibility = Visibility.Hidden;

            sharpClipboard.ClipboardChanged += SharpClipboard_ClipboardChanged;

            this.m_ClipboardResults.PropertyChanged += M_ClipboardResults_PropertyChanged;
            this.ResultsListBox.SelectionChanged += ResultsListBox_SelectionChanged;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Unsubscribe();
            WinfredNotifyIcon.Dispose();
        }

        #region 配置加载相关函数
        private void Load()
        {
            m_ClipboardImages.Clear();
            m_ClipboardResults.clear();
            m_SnippetsViewModel.clear();
            LoadConfiguration();
            LoadSnippets();
        }

        /// <summary>
        /// Load global configuration from config file
        /// </summary>
        private void LoadConfiguration()
        {
            if (File.Exists(m_ConfigFilePath))
            {
                using (StreamReader sr = File.OpenText(m_ConfigFilePath))
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
                            m_SnippetsDir = strSplitted[1];
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
            if (Directory.Exists(m_SnippetsDir))
            {
                var Dirs = Directory.EnumerateDirectories(m_SnippetsDir);
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
                                    m_SnippetsViewModel.Results.Add(new ResultViewModel(snippetName,
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
            m_GlobalHook = Hook.GlobalEvents();

            m_GlobalHook.KeyPress += GlobalHookKeyPress;

            // Display clipboard contents
            Combination clipboardCombination = Combination.TriggeredBy(Keys.Space).With(Keys.Alt).With(Keys.LControlKey);
            Action actionClipboard = DisplayClipboardContents;
            m_ActionList.Add(clipboardCombination, actionClipboard);

            Hook.GlobalEvents().OnCombination(m_ActionList);

            this.BackspaceTrigger += new NeedBackspace(ReplaceSourceByTarget);
        }

        public void Unsubscribe()
        {
            m_GlobalHook.KeyPress -= GlobalHookKeyPress;
            m_GlobalHook.Dispose();
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
                this.ResultsListBox.SelectedIndex++;
                if (this.ResultsListBox.SelectedIndex >= this.ResultsListBox.Items.Count)
                {
                    this.ResultsListBox.SelectedIndex = 0;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {

                if (this.ResultsListBox.SelectedIndex == 0)
                {
                    this.ResultsListBox.SelectedIndex = this.ResultsListBox.Items.Count;
                }
                else
                {
                    this.ResultsListBox.SelectedIndex--;
                }
                e.Handled = true;
            }
        }

        private void Winfred_Show()
        {
            this.Visibility = Visibility.Visible;
            this.Activate();
            this.query_text.Clear();
            this.query_text.Focus();
            this.ResultsListBox.SelectedIndex = 0;
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
                m_CurrentString = "$";
            }
            else if (e.KeyChar == '\b')
            {
                if (m_CurrentString.Length > 0)
                {
                    m_CurrentString = m_CurrentString.Remove(m_CurrentString.Length - 1, 1);
                }
            }
            else
            {
                m_CurrentString += e.KeyChar.ToString();
                bool IsSuccess = m_SnippetsViewModel.FindByResultName(m_CurrentString, out ResultViewModel targetSnippet);
                if (IsSuccess)
                {
                    this.sharpClipboard.ClipboardChanged -= SharpClipboard_ClipboardChanged;

                    IAsyncResult result = BackspaceTrigger.BeginInvoke(m_CurrentString, targetSnippet.ResultPreview, null, null);
                    BackspaceTrigger.EndInvoke(result);


                    this.sharpClipboard.ClipboardChanged += SharpClipboard_ClipboardChanged;
                }
                else if (m_CurrentString.Length >= 10)
                {
                    m_CurrentString = "";
                }
            }
        }

        private void BackspaceTriggerCallBack(IAsyncResult asyncResult)
        {
            this.sharpClipboard.ClipboardChanged += SharpClipboard_ClipboardChanged;
        }

        private void ReplaceSourceByTarget(string source, string target)
        {
            int n = source.Length;
            var sim = new InputSimulator();
            for (int i = 0; i < n; i++)
            {
                sim.Keyboard.KeyPress(VirtualKeyCode.BACK);
            }
            SetText2Clipboard(target);

            sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_V);
        }

        [STAThread]
        public static void SetText2Clipboard(string text)
        {
            Thread th = new Thread(new ThreadStart(delegate ()
            {
                DataObject dataObject = new DataObject(DataFormats.Text, text);
                Clipboard.SetDataObject(dataObject, true);
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
                string tempString = sharpClipboard.ClipboardText;
                if (tempString != m_TopTextInClipboard)
                {
                    if (m_ClipboardResults.FindByResultName(tempString, out ResultViewModel temp1))
                    {
                        return;
                    }

                    this.m_TopTextInClipboard = tempString;

                    m_ClipboardResults.Results.Insert(0,
                                                new ResultViewModel(this.m_TopTextInClipboard,
                                                                    this.m_TopTextInClipboard,
                                                                    ResultTypeEnum.Text,
                                                                    this.m_TopTextInClipboard.GetHashCode()));

                    return;
                }
            }
            else if (e.ContentType == SharpClipboard.ContentTypes.Image)
            {
                BitmapSource tempBitMap = Clipboard.GetImage();
                if (tempBitMap != null)
                {
                    m_TopBitmapSource = tempBitMap;
                    string name = System.String.Format("Image {0}x{1}", m_TopBitmapSource.PixelWidth, m_TopBitmapSource.PixelHeight);
                    m_ClipboardResults.Results.Insert(0,
                            new ResultViewModel(name,
                                                this.m_TopBitmapSource.GetHashCode().ToString(),
                                                ResultTypeEnum.Image,
                                                this.m_TopBitmapSource.GetHashCode()));
                    m_ClipboardImages.Add(this.m_TopBitmapSource.GetHashCode(), m_TopBitmapSource);
                }
            }
        }

        private void M_ClipboardResults_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            this.ResultsListBox.DataContext = this.m_ClipboardResults;
        }

        private void ResultsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.PreviewTextBlock.Document.Blocks.Clear();

            try
            {
                ResultViewModel target = this.m_ClipboardResults.Results[this.ResultsListBox.SelectedIndex];
                if (target.MainTypeEnum == ResultTypeEnum.Text)
                {
                    this.PreviewTextBlock.AppendText(target.ResultPreview);
                }
                else if (target.MainTypeEnum == ResultTypeEnum.Image)
                {
                    if (m_ClipboardImages.ContainsKey(target.HashCode))
                    {
                        Image image = new Image
                        {
                            Source = m_ClipboardImages[target.HashCode]
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

        private void ResultsListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                try
                {
                    this.sharpClipboard.ClipboardChanged -= SharpClipboard_ClipboardChanged;

                    ResultViewModel target = this.m_ClipboardResults.Results[this.ResultsListBox.SelectedIndex];

                    if (target.MainTypeEnum == ResultTypeEnum.Text)
                    {
                        SetText2Clipboard(target.ResultPreview);
                    }
                    else if (target.MainTypeEnum == ResultTypeEnum.Image)
                    {
                        if (m_ClipboardImages.ContainsKey(target.HashCode))
                        {
                            DataObject dataObject = new DataObject(DataFormats.Bitmap, m_ClipboardImages[target.HashCode]);
                            Clipboard.SetDataObject(dataObject, true);
                        }
                        else
                        {
                            Clipboard.Clear();
                        }
                    }

                    Application.Current.MainWindow.Hide();

                    var sim = new InputSimulator();
                    sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_V);

                    this.sharpClipboard.ClipboardChanged += SharpClipboard_ClipboardChanged;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.Message.ToString());
                }

                e.Handled = true;
            }
        }
        #endregion
    }
}
