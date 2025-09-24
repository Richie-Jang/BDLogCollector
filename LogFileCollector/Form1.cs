using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using Argus.IO;
using System.Globalization;
using Tango.Linq;
using FileWatcherEx;
using Sublib;

namespace LogFileCollector
{
    public partial class Form1 : Form
    {
        FileWatcherEx.FileWatcherEx logWatcher = null;
        // ollytest path
        string checkPath = "";
        // app path
        string curAppPath = "";
        public Form1()
        {
            InitializeComponent();

            Application.ThreadException += Application_ThreadException;

            // ollytest path find
            var tp2Path = FindPath.GetTP2();
            if (String.IsNullOrEmpty(tp2Path))
            {
                MessageBox.Show("Testplayer2 path not found. Please check Testplayer2.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                progressBar1.Style = ProgressBarStyle.Blocks;
                return;
            }

            checkPath = Path.Combine(tp2Path, "ollytest");

            if (!Directory.Exists(checkPath))
            {
                MessageBox.Show("Testplayer2/OllyTest path not found. Please check Testplayer2.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                progressBar1.Style = ProgressBarStyle.Blocks;
                return;
            }

            // save ini file
            curAppPath = Path.GetDirectoryName(Application.ExecutablePath);
            IniFile.SaveIniFile(curAppPath, "path", "ollypath", checkPath);

            textBox2.Text = checkPath;
            startMonitoring();
        }

        private void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            MessageBox.Show(e.ToString(), "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void startMonitoring()
        {
            if (!Directory.Exists(checkPath))
            {
                MessageBox.Show($"{checkPath} is not existed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                progressBar1.Style = ProgressBarStyle.Blocks;
                return;
            }

            textBox1.AppendText($"Log Watch {checkPath} is started\r\n");

            if (logWatcher != null)
            {
                stopLogWatcher();
            }

            logWatcher = new FileWatcherEx.FileWatcherEx(checkPath);
            logWatcher.Filter = "*.txt";
            logWatcher.OnChanged += LogWatcher_OnChanged;
            logWatcher.OnCreated += LogWatcher_OnCreated;
            logWatcher.SynchronizingObject = this;

            button4.Enabled = true;
            button4.Text = "Watch Stop";

            logWatcher.OnError += LogWatcher_OnError;
            logWatcher.Start();
            progressBar1.Style = ProgressBarStyle.Marquee;
        }

        private void LogWatcher_OnError(object sender, ErrorEventArgs e)
        {
            MessageBox.Show(e.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void LogWatcher_OnCreated(object sender, FileChangedEvent e)
        {
            // nothing
            notifyIcon1.BalloonTipTitle = "[Info]File Created";
            notifyIcon1.BalloonTipText = $"{e.FullPath} is created";
            notifyIcon1.ShowBalloonTip(1000);
        }

        private void LogWatcher_OnChanged(object sender, FileChangedEvent e)
        {            
            var curFile = new FileInfo(e.FullPath);
            var fileName = e.FullPath.ToLower();
            Console.WriteLine("{0} FileName", fileName);
            if (fileName.Contains("calibr") || fileName.Contains("ctest"))
            {
                // balloontip
                notifyIcon1.BalloonTipTitle = "[Modify] File changed";
                notifyIcon1.BalloonTipText = $"{Path.GetFileName(e.FullPath)} is changed.";
                notifyIcon1.ShowBalloonTip(1000);

                var timeStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                Console.WriteLine("TimeCode : {0}", timeStamp);
                var isDone = checkFileComplete(curFile);
                Console.WriteLine("isDone : {0}", isDone);

                if (isDone)
                {
                    Console.WriteLine("Update UI");

                    textBox1.Invoke(new Action(() => {
                        if (textBox1.Lines.Length > 10)
                        {
                            textBox1.Clear();
                        }
                        textBox1.AppendText($"[{timeStamp}] {fileName} is modified.\r\n");
                    }));

                    Console.WriteLine("start Log handle");
                    // handle logging
                    handleHistoryForLog(fileName, timeStamp);
                }                
                
            }
        }

        private string getBackdrillSetting()
        {
            var ollySetPath = @"c:\windows\ollytest.ini";
            if (!File.Exists(ollySetPath)) return "";
            var strLine = File.ReadLines(ollySetPath).TryFind(s => s.Contains("Backdrill capacity"));
            var r = strLine.Match(
                methodWhenSome: s => s,
                methodWhenNone: () => ""
            );
            if (r == "") return "";
            var l1 = r.Split('=');
            if (l1.Length <= 1 || l1[1] == "") return "";
            var l2 = l1[1].Split(',');
            var peakZ = 0.0;
            var peakC = 0.0;
            var freq = "";
            var freqIndex = 1;
            if (l2.Length == 4)
            {                
                Int32.TryParse(l2[3], out freqIndex);
            }
            if (l2.Length >= 2)
            {
                Double.TryParse(l2[0], out peakC);
                Double.TryParse(l2[1], out peakZ);
            }
            switch (freqIndex)
            {                
                case 0:
                    freq = "2K";
                    break;
                case 2:
                    freq = "8K";
                    break;
                case 3:
                    freq = "16K";
                    break;
                default:
                    freq = "4K";
                    break;
            }
            var peakCStr = String.Format("{0:f1}", peakC) + "fF";
            return String.Format("{0}_{1:f2}mm_{2}", peakCStr, peakZ, freq);
        }
               
        private void handleHistoryForLog(string logfile, string timeCode)
        {
            var isCTest = logfile.Contains("ctest");
            var logBackUpPath = Path.Combine(curAppPath, "logBackup");
            if (!Directory.Exists(logBackUpPath))
            {
                Directory.CreateDirectory(logBackUpPath);
            }            
            if (isCTest)
            {
                // check setting
                var bdSet = getBackdrillSetting();
                var nameIt = bdSet == "" ? "CAdjustLog" : $"CAdjustLog_{bdSet}";
                File.Copy(logfile, Path.Combine(logBackUpPath, $"{timeCode}-{nameIt}.txt"));
                
            } else
            {
                File.Copy(logfile, Path.Combine(logBackUpPath, $"{timeCode}-CalibrationLog.txt"));
            }
        }
        
        private bool checkFileComplete(FileInfo f)
        {
            Console.WriteLine("checkfileComplete");
            try
            {
                using (var rf = new ReverseFileReader(f.OpenRead()))
                {
                    var line = rf.ReadLine().Trim();
                    if (line == null) return false;
                    while (line == "")
                    {
                        line = rf.ReadLine().Trim();
                        if (line == null) { return false; }
                    }
                    if (line.Contains("Bye!") || (line.Contains("Finish on")))
                    {
                        return true;
                    }
                }                
            } catch (Exception)
            {
                Console.WriteLine("File Exception error");
                return false;
            }
            return false;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {            
            stopLogWatcher();
            notifyIcon1.Visible = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // 경로 변경
        private void button1_Click(object sender, EventArgs e)
        {            
            if (textBox2.Text == "" || !Directory.Exists(textBox2.Text))
            {
                MessageBox.Show($"{textBox2.Text} Path not found!\r\nPlease change Path again", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            stopLogWatcher();

            checkPath = textBox2.Text;
            startMonitoring();
        }

        private void stopLogWatcher()
        {
            if (logWatcher == null) return;
            logWatcher.Stop();
            logWatcher.Dispose();
            logWatcher = null;
            textBox1.AppendText($"Log Watch {checkPath} is closed\r\n");
            progressBar1.Style = ProgressBarStyle.Blocks;
            button4.Text = "Watch Start";
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void showFormToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Visible = true;
        }

        // 감추기
        private void button3_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        // watch stop
        private void button4_Click(object sender, EventArgs e)
        {
            var t = button4.Text;
            var isStop = t.Contains("Stop");

            if (isStop)
            {
                if (logWatcher == null)
                {
                    return;
                }
                stopLogWatcher();
            } else
            {
                startMonitoring();
            }
        }
    }
}
