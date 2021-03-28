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
using Hardcodet.Wpf.TaskbarNotification;
using KeyPressEventArgs = WindowsHook.KeyPressEventArgs;

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
        /// Delegation for backspacing and replacing texts
        /// </summary>
        /// <param name="source">Source string for snippet keycode</param>
        /// <param name="target">Snippet content to insert</param>
        private delegate void NeedBackspace(string source, string target);
        private event NeedBackspace BackspaceTrigger;
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

        /// <summary>
        /// Key word snippets map
        /// </summary>
        private Dictionary<string, string> m_Snippets = new Dictionary<string, string>();

        public MainWindow()
        {
            InitializeComponent();
            Winfred_SetStartupPosition();
            Load();
            Subscribe();
            this.Visibility = Visibility.Hidden;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Unsubscribe();
            WinfredNotifyIcon.Dispose();
        }

        #region 配置加载相关函数
        private void Load()
        {
            m_Snippets.Clear();
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
                                    m_Snippets.Add(snippetName, snippetText);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("ERROR Reading snippet files.");
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

            Combination combination = Combination.TriggeredBy(Keys.Space).With(Keys.Alt).With(Keys.LControlKey);
            Action actionShowWindow = Winfred_Show;
            m_ActionList.Add(combination, actionShowWindow);
            Hook.GlobalEvents().OnCombination(m_ActionList);

            this.BackspaceTrigger += new NeedBackspace(ReplaceSourceByTarget);
        }
        
        public void Unsubscribe()
        {
            m_GlobalHook.KeyPress -= GlobalHookKeyPress;
            m_GlobalHook.Dispose();
        }
        #endregion

        #region snippet功能代码
        private void GlobalHookKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '$')
            {
                m_CurrentString = "$";
            }
            else
            {
                m_CurrentString += e.KeyChar.ToString();
                bool IsSuccess = m_Snippets.TryGetValue(m_CurrentString, out string snippetText);
                if (IsSuccess && snippetText != null)
                {
                    BackspaceTrigger.BeginInvoke(m_CurrentString, snippetText, null, null);
                }
                else if (m_CurrentString.Length >= 10)
                {
                    m_CurrentString = "";
                }
            }
        }

        private void ReplaceSourceByTarget(string source, string target)
        {
            System.Threading.Thread.Sleep(100);
            int n = source.Length;
            for (int i = 0; i < n; i++)
            {
                var sim = new InputSimulator();
                sim.Keyboard.KeyPress(VirtualKeyCode.BACK);
            }
            SetText2Clipboard(target);
        }

        [STAThread]
        public static void SetText2Clipboard(string text)
        {
            Thread th = new Thread(new ThreadStart(delegate ()
            {
                Clipboard.SetText(text);
            }));
            th.TrySetApartmentState(ApartmentState.STA);
            th.Start();
            th.Join();
            var sim = new InputSimulator();
            sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_V);
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
            }
        }

        private void Winfred_Deactivated(object sender, EventArgs e)
        {
            Winfred_Hide();
        }

        private void Winfred_LostFocus(object sender, RoutedEventArgs e)
        {
            Winfred_Hide();
        }

        private void Winfred_Show()
        {
            this.Visibility = Visibility.Visible;
            this.Activate();
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
        }

        private void tray_menu_display_Click(object sender, RoutedEventArgs e)
        {
            Winfred_Show();
        }

        private void tray_menu_reload_Click(object sender, RoutedEventArgs e)
        {
            Load();
        }
        #endregion
    }
}
