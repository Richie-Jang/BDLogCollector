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
using Microsoft.FSharp.Core;
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
            // cal or ctest log file
            var curFile = new FileInfo(e.FullPath);
            var fileName = e.FullPath.ToLower();
            Console.WriteLine("{0} FileName", fileName);
            if (fileName.Contains("calibr") || fileName.Contains("ctest"))
            {
                // balloontip
                notifyIcon1.BalloonTipTitle = "[Modify] File changed";
                notifyIcon1.BalloonTipText = $"{Path.GetFileName(e.FullPath)} is changed.";
                notifyIcon1.ShowBalloonTip(1000);

                var n = DateTime.Now;
                var timeStamp = n.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                var dateStamp = n.ToString("yyyyMM", CultureInfo.InvariantCulture);

                // check backup folder exists
                var logBackUpPath1 = Path.Combine(curAppPath, "logBackup");
                if (!Directory.Exists(logBackUpPath1))
                {
                    Directory.CreateDirectory(logBackUpPath1);
                }
                var logBackUpPath = Path.Combine(curAppPath, "logBackup", dateStamp);
                if (!Directory.Exists(logBackUpPath))
                {
                    Directory.CreateDirectory(logBackUpPath);
                }

                Console.WriteLine("TimeCode : {0}", timeStamp);
                var isLogSavedOk = checkFileComplete(curFile);

                textBox1.Invoke(new Action(() => {
                    textBox1.AppendText($"Log saved Done : {isLogSavedOk}");
                }));

                if (isLogSavedOk)
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
                    handleHistoryForLog(fileName, timeStamp, dateStamp);
                }                
            }
        }

        // loading olly ini file..
        private Task<string> getBackdrillSetting()
        {
            return Task.Run(() => {
                var os = IniFile.GetBackDrillSetFromOllyTestIni();
                if (FSharpOption<IniFile.OllySet>.get_IsNone(os))
                {
                    return "";
                }
                return os.Value.ToStr();
            });
        }
               
        private async void handleHistoryForLog(string logfile, string timeCode, string dateStampCode)
        {
            var isCTest = logfile.Contains("ctest");
            var logBackUpPath = Path.Combine(curAppPath, "logBackup", dateStampCode);
            if (!Directory.Exists(logBackUpPath))
            {
                Directory.CreateDirectory(logBackUpPath);
            }       
            // is Ctest log?
            if (isCTest)
            {
                // check setting
                var bdSet = await getBackdrillSetting();
                var nameIt = "";
                // log checking 
                var task2 = Task.Run(() => {
                    return Cadjust.loadLogFile(logfile);
                });

                Cadjust.LogData logData = null;

                try
                {
                    logData = await task2;
                } catch(Exception e)
                {
                    MessageBox.Show(e.Message+"\r\nSkip handle", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    logData = null;
                }

                if (logData == null) return;

                if (logData.expectedCap == 0.0 && logData.statsArr.Length == 0)
                {
                    // wrong data received. cancelled
                    return;
                }

                var isCadjustLog = logData.measArr[0].Length == 0;

                if (bdSet == "")
                {
                    bdSet = $"{logData.expectedCap:F2}fF_{logData.zCoord:F2}mm_{logData.freq}KHz";
                }

                if (isCadjustLog)
                {
                    // isCadjustLog
                    nameIt = bdSet == "" ? "CAdjustLog" : $"CAdjustLog_{bdSet}";
                }
                else
                {
                    nameIt = bdSet == "" ? "CVerifyLog" : $"CVerifyLog_{bdSet}";
                }

                var task1 = Task.Run(() => { 
                    File.Copy(logfile, Path.Combine(logBackUpPath, $"{timeCode}-{nameIt}.txt"));
                });

                await task1;
                
                // evaluate log file
                var task3 = Task.Run(async () => {
                    var (a, b, c) = IniFile.GetVerifyCheckSetting(curAppPath);
                    var mm = isCadjustLog ? "CAdjust" : "CVerify";
                    var resultFile = $"{timeCode}-{mm}_Result.txt";
                    var nlogfile = Path.Combine(logBackUpPath, resultFile);
                    var resMsg = "";
                    try
                    {
                        var (resArr, msg) = Cadjust.evaluateLogData(logData,a, b, c);
                        resMsg = msg;
                        this.Invoke(new Action(() => {
                            var timer = new Timer();
                            timer.Interval = 15000;
                            var form2 = new Form2(resArr, Tuple.Create(a,b,c), resultFile, isCadjustLog);
                            form2.TopMost = true;

                            timer.Tick += (s, e) => {
                                timer.Stop();
                                timer.Dispose();
                                form2.TopMost = false;
                                if (form2.Visible)
                                {
                                    form2.Close();
                                }
                            };

                            timer.Start();
                            form2.ShowDialog(this);
                        }));
                    } catch (Exception ex)
                    {
                        this.Invoke(new Action(() => {
                            MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }

                    var task4 = Task.Run(() => {
                        if (resMsg != "") File.WriteAllText(nlogfile, resMsg);
                    });

                    await task4;
                });

                await task3;               
            } else
            {
                // is Calibration log
                File.Copy(logfile, Path.Combine(logBackUpPath, $"{timeCode}-CalibrationLog.txt"));
            }
        }
        
        // log file completed ok or not check
        private bool checkFileComplete(FileInfo f)
        {
            Console.WriteLine("checkfileComplete started");
            try
            {
                using (var rf = new ReverseFileReader(f.OpenRead()))
                {
                    var line = rf.ReadLine().Trim();
                    while (line == "")
                    {
                        // if line is empty, read again.
                        line = rf.ReadLine().Trim();
                    }                   
                    if (line.Contains("Bye!") || (line.Contains("Finish on")))
                    {
                        // log saved successfully
                        return true;
                    } else
                    {
                        // something wrong.. no finished line found.
                        return false;
                    }
                }                
            } catch (Exception)
            {
                Console.WriteLine("File Exception error");
                return false;
            }
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
            stopLogWatcher();

            if (textBox2.Text == "" || !Directory.Exists(textBox2.Text))
            {
                MessageBox.Show($"{textBox2.Text} Path not found!\r\nPlease change Path again", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }            

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
