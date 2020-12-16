using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace GTAV_Helper
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SynchronizationContext _sync;
        private bool ServerRunning = false;
        private Process server;
        private List<Process> watches = new List<Process>();
        private Dictionary<Process, ScrollViewer> watchLogs = new Dictionary<Process, ScrollViewer>();
        public Dictionary<string, string> commands;
        public MainWindow()
        {
            Dictionary<string, string> dictionary1 = new Dictionary<string, string>();
            dictionary1["Server"] = "cd ./app/server & npm run watch";
            dictionary1["Client"] = "cd ./app/client & npm run watch";
            dictionary1["CEF"] = "cd ./app/cef & npm run watch";
            dictionary1["CEF_dev"] = "cd ./app/cef & npm run serve";
            commands = dictionary1;
            _sync = SynchronizationContext.Current;
            AppDomain.CurrentDomain.ProcessExit += delegate (object sender, EventArgs e) {
                if (server != null && !server.HasExited)
                {
                    server.Kill();
                    server.Dispose();
                }
                foreach (Process process in this.watches)
                {
                    process.Kill();
                    process.Dispose();
                    Process process2 = new Process
                    {
                        StartInfo = {
                            FileName = "cmd.exe",
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }
                    };
                    process2.Start();
                    process2.StandardInput.WriteLine("taskkill /F /IM node.exe");
                    process2.BeginOutputReadLine();
                    process2.WaitForExit();
                }
            };
            InitializeComponent();
            foreach (KeyValuePair<string, string> command in this.commands)
            {
                Thread thread = new Thread((object q) => {
                    Process process1 = new Process();
                    process1.StartInfo.FileName = "cmd.exe";
                    process1.StartInfo.RedirectStandardInput = true;
                    process1.StartInfo.RedirectStandardOutput = true;
                    process1.StartInfo.CreateNoWindow = true;
                    process1.StartInfo.UseShellExecute = false;
                    process1.OutputDataReceived += new DataReceivedEventHandler((object sender, DataReceivedEventArgs e) => this._sync.Send((object w) => {
                        string str;
                        Process process = (Process)sender;
                        ScrollViewer item = this.watchLogs[process];
                        object content = item.Content;
                        str = (content != null ? content.ToString() : null);
                        item.Content = string.Concat(str, e.Data, "\n");
                    }, null));
                    process1.Start();
                    process1.StandardInput.WriteLine(command.Value);
                    _sync.Send((object w) => {
                        TabItem tabItem = new TabItem()
                        {
                            Header = command.Key
                        };
                        ScrollViewer scrollViewer = new ScrollViewer()
                        {
                            Name = command.Key
                        };
                        watchLogs.Add(process1, scrollViewer);
                        tabItem.Content = scrollViewer;
                        Tabs.Items.Add(tabItem);
                        ListBoxItem listBoxItem = new ListBoxItem()
                        {
                            Style = base.FindResource("TabItemButton") as Style,
                            Tag = tabItem,
                            Content = command.Key,
                            Width = 100
                        };
                        listBoxItem.Selected += new RoutedEventHandler(TabItem_Selected);
                        ControlItems.Items.Add(listBoxItem);
                        ListBox controlItems = this.ControlItems;
                        controlItems.Width = controlItems.Width + (listBoxItem.Width + 13);
                    }, null);
                    watches.Add(process1);
                    process1.BeginOutputReadLine();
                    process1.WaitForExit();
                })
                {
                    IsBackground = true
                };
                thread.Start();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                base.DragMove();
            }
            catch
            {
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnServerToggled()
        {
            _sync.Send(delegate (object w) {
                RestartServer.IsEnabled = ServerRunning;
                RestartServer.Style = FindResource(this.ServerRunning ? "Button" : "ButtonDanger") as Style;
                SendCommand.IsEnabled = ServerRunning;
            }, null);
        }

        private void RestartServer_Click(object sender, RoutedEventArgs e)
        {
            if (!server.HasExited)
            {
                server.Kill();
            }
            ServerRunning = false;
            server.Dispose();
            ToggleServer_Click(null, null);
        }

        private void SendCommand_Click(object sender, RoutedEventArgs e)
        {
            server.StandardInput.WriteLine(ServerCommand.Text);
        }

        private void SettingConf_Clicked(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("conf.json");
            }
            catch (Exception exception1)
            {
                MessageBox.Show(exception1.Message, "Ошибка открытия файла", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        private void TabItem_Selected(object sender, RoutedEventArgs e)
        {
            if (Tabs != null)
            {
                Tabs.SelectedItem = (sender as FrameworkElement).Tag as TabItem;
            }
        }

        private void ToggleServer_Click(object sender, RoutedEventArgs e)
        {
            if (!ServerRunning)
            {
                Log.Content = "";
                new Thread(delegate (object q) {
                    try
                    {
                        server = new Process();
                        server.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
                        server.StartInfo.FileName = "ragemp-server.exe";
                        server.StartInfo.RedirectStandardInput = true;
                        server.StartInfo.RedirectStandardOutput = true;
                        server.StartInfo.CreateNoWindow = true;
                        server.StartInfo.UseShellExecute = false;
                        server.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                        server.OutputDataReceived += (sender2, e2) => _sync.Send(delegate (object w) {
                            string text1;
                            object content = this.Log.Content;
                            if (content != null)
                            {
                                text1 = content.ToString();
                            }
                            else
                            {
                                object local1 = content;
                                text1 = null;
                            }
                            this.Log.Content = text1 + e2.Data + "\n";
                        }, null);
                        ServerRunning = true;
                        _sync.Send(w => this.ToggleServer.Content = "Stop server", null);
                        server.Start();
                        server.BeginOutputReadLine();
                        OnServerToggled();
                    }
                    catch (Exception exception1)
                    {
                        MessageBox.Show(exception1.Message, "Ошибка запуска сервера", MessageBoxButton.OK, MessageBoxImage.Hand);
                        ServerRunning = false;
                        server = null;
                        OnServerToggled();
                    }
                })
                { IsBackground = true }.Start();
            }
            else
            {
                if (!server.HasExited)
                {
                    server.Kill();
                }
                ServerRunning = false;
                server.Dispose();
                _sync.Send(w => ToggleServer.Content = "Start server", null);
                OnServerToggled();
            }
        }
    }
}
