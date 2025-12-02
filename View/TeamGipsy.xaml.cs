// 说明：主窗口负责托盘菜单、快捷键、提醒与学习流程入口
// - ContextMenu：构建并维护托盘菜单（含“学习记录”“加深记忆”“AI配置”等）
// - Begin_Click：启动学习线程（SM2 模式）
// - DeepenMemoryAsync：汇总今日单词，调用 AI 生成英文短文与中文译文并展示
// - ScheduleDailyReminder：每日学习提醒逻辑
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using TeamGipsy.ViewModel;
using TeamGipsy.Resources;
using System.Windows.Forms;
using TeamGipsy.Model.SqliteControl;
using System.Threading;
using TeamGipsy.Model.Mp3;
using System.Diagnostics;
using TeamGipsy.Model.PushControl;
using TeamGipsy.Model.Log;

using TeamGipsy.Model.StartWithWindows;
using System.IO;
using System.Windows.Xps.Packaging;
using System.Windows.Input;
using System.Timers;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using TeamGipsy.Model.Ai;

namespace TeamGipsy
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        ToastFishModel Vm = new ToastFishModel();
        Select Se = new Select();
        bool _isGeneratingEssay = false;
        PushWords pushWords = new PushWords();
        Thread thread = new Thread(new ParameterizedThreadStart(PushWords.Recitation));
        Dictionary<string, string> TablelDictionary = new Dictionary<string, string>(){
        {"CET4_1", "四级核心词汇"},{"CET4_3", "四级完整词汇"},{"CET6_1", "六级核心词汇"},
        {"CET6_3", "六级完整词汇"},{"IELTS_3", "IELTS词汇"},{"TOEFL_2", "TOEFL词汇"},
        {"KaoYan_1", "考研必考词汇"},{"KaoYan_2", "考研完整词汇"} };
        // private NotifyIcon _notifyIcon = null;
        //HotKey _hotKey0, _hotKey1, _hotKey2, _hotKey3, _hotKey4;
        public MainWindow()
        {
            Form_Load();
            var asmName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            System.Windows.Application.LoadComponent(this, new Uri($"/{asmName};component/View/TeamGipsy.xaml", UriKind.Relative));
            DataContext = Vm;
            SetNotifyIcon();
            this.Visibility = Visibility.Hidden;
            Se.LoadGlobalConfig();
            Se.CleanupJapaneseData();
            Se.CleanupRemovedEnglishBooks();
            ContextMenu();
            new HotKey(Key.Oem3, KeyModifier.Alt, OnHotKeyHandler);
            new HotKey(Key.D1, KeyModifier.Alt, OnHotKeyHandler);
            new HotKey(Key.D2, KeyModifier.Alt, OnHotKeyHandler);
            new HotKey(Key.D3, KeyModifier.Alt, OnHotKeyHandler);
            new HotKey(Key.D4, KeyModifier.Alt, OnHotKeyHandler);
            new HotKey(Key.Q, KeyModifier.Alt, OnHotKeyHandler);

            // 谜之bug，如果不先播放一段音频，那么什么声音都播不出来。
            // 所以播个没声音的音频先。
            PlayMute();
            //this.WindowState = (WindowState)FormWindowState.Minimized;

            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                var args = ToastArguments.Parse(toastArgs.Argument);
                string action = "";
                try { action = args["action"]; } catch { }
                if (action == "daily_begin")
                {
                    Begin_Click(null, null);
                }
            };

            ScheduleDailyReminder();
        }

        private void OnHotKeyHandler(HotKey hotKey)
        {
            string key = hotKey.Key.ToString();
            Debug.WriteLine("key pressed:" + key);
            switch (key)
            {
                case "Q":
                    Begin_Click(null, null);
                    break;
                case "D1":
                    PushWords.HotKeytObservable.raiseEvent("1");
                    break;
                case "D2":
                    PushWords.HotKeytObservable.raiseEvent("2");
                    break;
                case "D3":
                    PushWords.HotKeytObservable.raiseEvent("3");
                    break;
                case "D4":
                    PushWords.HotKeytObservable.raiseEvent("4");
                    break;
                case "Oem3":
                    PushWords.HotKeytObservable.raiseEvent("S");
                    break;
                default:
                    break;
            }
        }

        private void Form_Load()

        {
            //获取当前活动进程的模块名称
            string moduleName = Process.GetCurrentProcess().MainModule.ModuleName;
            //返回指定路径字符串的文件名
            string processName = System.IO.Path.GetFileNameWithoutExtension(moduleName);
            //根据文件名创建进程资源数组
            Process[] processes = Process.GetProcessesByName(processName);
            //如果该数组长度大于1，说明多次运行
            if (processes.Length > 1)
            {
                System.Windows.Forms.MessageBox.Show("程序已经在运行了，不能运行两次。\n如果右下角软件已经退出，请在任务管理器中结束TeamGipsy任务。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);//弹出提示信息
                this.Close();//关闭当前窗体
            }
        }

        private void SetNotifyIcon()
        {
            Vm.notifyIcon = new NotifyIcon();
            Vm.notifyIcon.Text = "TeamGipsy";
            System.Drawing.Icon icon = IconChika.chika16;

            Vm.notifyIcon.Icon = icon;
            Vm.notifyIcon.Visible = true;
            Vm.notifyIcon.DoubleClick += Begin_Click;
            //Vm.notifyIcon.DoubleClick += NotifyIconDoubleClick;
        }

        public void PlayMute()
        {
            MUSIC Temp = new MUSIC();
            Temp.FileName = ".\\Resources\\mute.mp3";
            Temp.play();
        }

        private void NotifyIconDoubleClick(object sender, EventArgs e)
        {
            this.Activate();
            this.WindowState = WindowState.Normal;
            this.ShowInTaskbar = true;
            this.Topmost = true;
            this.Show();
        }

        #region 托盘右键菜单

        System.Windows.Forms.ToolStripMenuItem Begin = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem Settings = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem SetNumber = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem SetEngType = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem ImportWords = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem SelectBook = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem RandomTest = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem Dashboard = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem DeepenMemory = new System.Windows.Forms.ToolStripMenuItem();

        System.Windows.Forms.ToolStripMenuItem GotoHtml = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem Start = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem ExitMenuItem = new System.Windows.Forms.ToolStripMenuItem();

        System.Windows.Forms.ToolStripMenuItem SetAutoPlay = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem SetAutoLog = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem AiSettings = new System.Windows.Forms.ToolStripMenuItem();

        private new void ContextMenu()
        {
            ContextMenuStrip Cms = new ContextMenuStrip();

            Vm.notifyIcon.ContextMenuStrip = Cms;

            Cms.Renderer = new DarkToolStripRenderer(new TrayColorTable());
            Cms.Font = new System.Drawing.Font("Microsoft YaHei UI", 10f, System.Drawing.FontStyle.Regular);
            Cms.ShowCheckMargin = true;
            Cms.ShowImageMargin = false;
            Cms.ForeColor = System.Drawing.Color.Black;
            Cms.Padding = new System.Windows.Forms.Padding(2);
            Cms.Opening += (s, e) =>
            {
                ApplyRoundedRegion(Cms, 12);
                try
                {
                    int today = Se.ReviewedTodayCount();
                    DeepenMemory.Visible = today >= Select.WORD_NUMBER;
                }
                catch { DeepenMemory.Visible = false; }
            };


            Begin.Text = "开始！";
            Begin.Click += new EventHandler(Begin_Click);
            Begin.ForeColor = System.Drawing.Color.Black;
            Settings.Text = "参数设置";
            Settings.ForeColor = System.Drawing.Color.Black;


            SetNumber.Text = "单词个数";
            SetNumber.Click += new EventHandler(SetNumber_Click);
            SetNumber.ForeColor = System.Drawing.Color.Black;

            SetEngType.Text = "英标类型";
            SetEngType.Click += new EventHandler(SetEngType_Click);
            SetEngType.ForeColor = System.Drawing.Color.Black;

            SetAutoPlay.Text = "自动播放";
            SetAutoPlay.Click += new EventHandler(AutoPlay_Click);
            if (Select.AUTO_PLAY != 0)
                SetAutoPlay.Checked = true;
            else
                SetAutoPlay.Checked = false;
            SetAutoPlay.ForeColor = System.Drawing.Color.Black;

            SetAutoLog.Text = "自动日志";
            SetAutoLog.Click += new EventHandler(AutoLog_Click);
            if (Select.AUTO_LOG != 0)
                SetAutoLog.Checked = true;
            else
                SetAutoLog.Checked = false;
            SetAutoLog.ForeColor = System.Drawing.Color.Black;


            ImportWords.Text = "导入单词";
            ImportWords.Click += new EventHandler(ImportWords_Click);
            ImportWords.ForeColor = System.Drawing.Color.Black;

            SelectBook.Text = "英语词汇";
            SelectBook.ForeColor = System.Drawing.Color.Black;

            RandomTest.Text = "随机测试";
            RandomTest.ForeColor = System.Drawing.Color.Black;

            Dashboard.Text = "学习记录";
            Dashboard.ForeColor = System.Drawing.Color.Black;
            Dashboard.Click += new EventHandler(Dashboard_Click);

            DeepenMemory.Text = "加深记忆";
            DeepenMemory.ForeColor = System.Drawing.Color.Black;
            DeepenMemory.Click += new EventHandler(DeepenMemory_Click);

            GotoHtml.Text = "快捷说明";
            GotoHtml.Click += new EventHandler(ShortCuts_Click);
            GotoHtml.ForeColor = System.Drawing.Color.Black;

            Start.Text = "开机启动";
            Start.Click += new EventHandler(Start_Click);
            if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "TeamGipsy.lnk")))
                Start.Checked = true;
            else
                Start.Checked = false;
            Start.ForeColor = System.Drawing.Color.Black;

            ExitMenuItem.Text = "退出";
            ExitMenuItem.Click += new EventHandler(ExitApp_Click);
            ExitMenuItem.ForeColor = System.Drawing.Color.Black;

            ToolStripItem CET4_1 = new ToolStripMenuItem("四级核心词汇");
            CET4_1.Click += new EventHandler(SelectBook_Click);
            ToolStripItem CET4_3 = new ToolStripMenuItem("四级完整词汇");
            CET4_3.Click += new EventHandler(SelectBook_Click);
            ToolStripItem CET6_1 = new ToolStripMenuItem("六级核心词汇");
            CET6_1.Click += new EventHandler(SelectBook_Click);
            ToolStripItem CET6_3 = new ToolStripMenuItem("六级完整词汇");
            CET6_3.Click += new EventHandler(SelectBook_Click);

            ToolStripItem IELTS_3 = new ToolStripMenuItem("IELTS词汇");
            IELTS_3.Click += new EventHandler(SelectBook_Click);
            ToolStripItem TOEFL_2 = new ToolStripMenuItem("TOEFL词汇");
            TOEFL_2.Click += new EventHandler(SelectBook_Click);
            ToolStripItem KaoYan_1 = new ToolStripMenuItem("考研必考词汇");
            KaoYan_1.Click += new EventHandler(SelectBook_Click);
            ToolStripItem KaoYan_2 = new ToolStripMenuItem("考研完整词汇");
            KaoYan_2.Click += new EventHandler(SelectBook_Click);
            ToolStripItem RandomWord = new ToolStripMenuItem("随机单词测试");
            RandomWord.Click += new EventHandler(RandomWordTest_Click);
            ToolStripItem ResetLearingStatus = new ToolStripMenuItem("重置进度");
            ResetLearingStatus.Click += new EventHandler(ResetLearingStatus_Click);

            if (Select.TABLE_NAME == "CET4_1")
                CET4_1.PerformClick();
            else if (Select.TABLE_NAME == "CET4_3")
                CET4_3.PerformClick();
            else if (Select.TABLE_NAME == "CET6_1")
                CET6_1.PerformClick();
            else if (Select.TABLE_NAME == "CET6_3")
                CET6_3.PerformClick();
            else if (Select.TABLE_NAME == "IELTS_3")
                IELTS_3.PerformClick();
            else if (Select.TABLE_NAME == "TOEFL_2")
                TOEFL_2.PerformClick();
            else if (Select.TABLE_NAME == "KaoYan_1")
                KaoYan_1.PerformClick();
            else if (Select.TABLE_NAME == "KaoYan_2")
                KaoYan_2.PerformClick();


            Cms.Items.Add(Begin);
            Cms.Items.Add(new ToolStripSeparator());
            //Cms.Items.Add(SetNumber);
            //Cms.Items.Add(SetEngType);
            Cms.Items.Add(ImportWords);
            Cms.Items.Add(SelectBook);
            Cms.Items.Add(RandomTest);
            Cms.Items.Add(Dashboard);
            Cms.Items.Add(DeepenMemory);
            Cms.Items.Add(new ToolStripSeparator());
            Cms.Items.Add(Settings);
            Cms.Items.Add(GotoHtml);
            Cms.Items.Add(new ToolStripSeparator());
            Cms.Items.Add(Start);
            Cms.Items.Add(new ToolStripSeparator());
            Cms.Items.Add(ExitMenuItem);

            ((ToolStripDropDownItem)SelectBook).DropDownItems.Add(CET4_1);
            ((ToolStripDropDownItem)SelectBook).DropDownItems.Add(CET4_3);
            ((ToolStripDropDownItem)SelectBook).DropDownItems.Add(CET6_1);
            ((ToolStripDropDownItem)SelectBook).DropDownItems.Add(CET6_3);
            ((ToolStripDropDownItem)SelectBook).DropDownItems.Add(IELTS_3);
            ((ToolStripDropDownItem)SelectBook).DropDownItems.Add(TOEFL_2);
            ((ToolStripDropDownItem)SelectBook).DropDownItems.Add(KaoYan_1);
            ((ToolStripDropDownItem)SelectBook).DropDownItems.Add(KaoYan_2);
            ((ToolStripDropDownItem)RandomTest).DropDownItems.Add(RandomWord);
            ((ToolStripDropDownItem)Settings).DropDownItems.Add(SetNumber);
            ((ToolStripDropDownItem)Settings).DropDownItems.Add(SetEngType);
            ((ToolStripDropDownItem)Settings).DropDownItems.Add(SetAutoPlay);
            ((ToolStripDropDownItem)Settings).DropDownItems.Add(SetAutoLog);
            ((ToolStripDropDownItem)Settings).DropDownItems.Add(ResetLearingStatus);

            AiSettings.Text = "AI配置";
            AiSettings.Click += new EventHandler(AiSettings_Click);
            ((ToolStripDropDownItem)Settings).DropDownItems.Add(AiSettings);

        }

        private void Dashboard_Click(object sender, EventArgs e)
        {
            try
            {
                Vm.RefreshDashboard.Execute(null);
            }
            catch { }
            this.Visibility = Visibility.Visible;
            this.ShowInTaskbar = true;
            this.WindowState = WindowState.Normal;
            this.Topmost = true;
            this.Show();
            this.Activate();
        }

        private void DeepenMemory_Click(object sender, EventArgs e)
        {
            DeepenMemoryAsync();
        }

        private async void DeepenMemoryAsync()
        {
            if (_isGeneratingEssay)
            {
                pushWords.PushMessage("正在生成中...");
                return;
            }
            _isGeneratingEssay = true;
            try
            {
                Se.SelectWordList();
                var all = Se.AllWordList != null ? Se.AllWordList.ToList() : new List<Word>();
                DateTime today = DateTime.Now.Date;
                var todayWords = new List<Word>();
                foreach (var w in all)
                {
                    if (!string.IsNullOrEmpty(w.dateLastReviewed))
                    {
                        DateTime dt;
                        if (DateTime.TryParse(w.dateLastReviewed, out dt))
                        {
                            if (dt.Date == today)
                                todayWords.Add(w);
                        }
                    }
                }
                if (todayWords.Count == 0)
                {
                    pushWords.PushMessage("今天没有可用单词");
                    return;
                }
                var wordsToUse = todayWords.Where(x => !string.IsNullOrWhiteSpace(x.headWord))
                    .GroupBy(x => x.headWord)
                    .Select(g => g.First())
                    .OrderBy(x => x.headWord)
                    .Take(Select.WORD_NUMBER)
                    .ToList();
                var wordsJoined = string.Join(",", wordsToUse.Select(x => x.headWord));

                string todayStr = DateTime.Now.ToString("yyyy-MM-dd");
                if (!string.IsNullOrEmpty(Select.AI_ESSAY_TEXT) && Select.AI_ESSAY_DATE == todayStr && Select.AI_ESSAY_WORDS == wordsJoined)
                {
                    var cachedWin = new DeepenMemoryWindow(Select.AI_ESSAY_TEXT, Select.AI_ESSAY_TRANSLATION);
                    cachedWin.Topmost = true;
                    cachedWin.Show();
                    return;
                }

                if (string.IsNullOrWhiteSpace(Select.AI_API_BASE) || string.IsNullOrWhiteSpace(Select.AI_API_KEY))
                {
                    pushWords.PushMessage("请先在 参数设置 → AI配置 中设置接口地址与密钥");
                    Thread thread = new Thread(new ThreadStart(pushWords.SetAiConfig));
                    thread.Start();
                    return;
                }

                pushWords.ShowLoadingToast("正在生成内容，请稍候...");
                var client = new DeepseekClient();
                var essay = await client.GenerateEssayAsync(wordsToUse);
                var translation = await client.TranslateAsync(essay);
                pushWords.DismissLoadingToast();
                var win = new DeepenMemoryWindow(essay, translation);
                win.Topmost = true;
                win.Show();
                Se.SaveAiEssayCache(essay, translation, wordsJoined, todayStr);
            }
            catch (Exception ex)
            {
                pushWords.PushMessage("生成失败：" + ex.Message);
            }
            finally
            {
                _isGeneratingEssay = false;
            }
        }

        private void AiSettings_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(new ThreadStart(pushWords.SetAiConfig));
            thread.Start();
        }

        class TrayColorTable : ProfessionalColorTable
        {
            public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.White;
            public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(230, 240, 255);
            public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.FromArgb(200, 200, 200);
            public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.White;
            public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.White;
            public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.White;
            public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(245, 245, 245);
            public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(245, 245, 245);
            public override System.Drawing.Color MenuItemSelectedGradientBegin => System.Drawing.Color.FromArgb(230, 240, 255);
            public override System.Drawing.Color MenuItemSelectedGradientEnd => System.Drawing.Color.FromArgb(230, 240, 255);
            public override System.Drawing.Color SeparatorDark => System.Drawing.Color.FromArgb(220, 220, 220);
            public override System.Drawing.Color SeparatorLight => System.Drawing.Color.FromArgb(220, 220, 220);
        }

        class DarkToolStripRenderer : ToolStripProfessionalRenderer
        {
            public DarkToolStripRenderer(ProfessionalColorTable colorTable) : base(colorTable) { }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = System.Drawing.Color.Black;
                base.OnRenderItemText(e);
            }
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = CreateRoundRect(new RectangleF(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 12))
                using (var brush = new SolidBrush(System.Drawing.Color.White))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = CreateRoundRect(new RectangleF(0.5f, 0.5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 12))
                using (var pen = new Pen(System.Drawing.Color.FromArgb(200, 200, 200)))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var item = e.Item;
                var rect = new RectangleF(2, 1, item.Bounds.Width - 4, item.Bounds.Height - 2);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (item.Selected || item.Pressed)
                {
                    using (var path = CreateRoundRect(rect, 10))
                    using (var brush = new SolidBrush(System.Drawing.Color.FromArgb(230, 240, 255)))
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                }
            }
            public static GraphicsPath CreateRoundRect(RectangleF rect, float radius)
            {
                float d = radius * 2;
                var path = new GraphicsPath();
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
        }

        private void ApplyRoundedRegion(ContextMenuStrip cms, int radius)
        {
            try
            {
                using (var path = DarkToolStripRenderer.CreateRoundRect(new RectangleF(0, 0, cms.Width - 1, cms.Height - 1), radius))
                {
                    cms.Region = new Region(path);
                }
            }
            catch { }
        }

        private void Begin_Click(object sender, EventArgs e)
        {
            if (!System.IO.Directory.Exists("Log"))
            {
                System.IO.Directory.CreateDirectory("Log");
            }
            // System.IO.Directory.CreateDirectory("Log");

            var state = thread.ThreadState;

            WordType Words = new WordType();
            Words.Number = Select.WORD_NUMBER;

            if (state == System.Threading.ThreadState.WaitSleepJoin || state == System.Threading.ThreadState.Stopped)
            {
                thread.Abort();
                while (thread.ThreadState != System.Threading.ThreadState.Aborted)
                {
                    Thread.Sleep(100);
                }
                thread = new Thread(new ParameterizedThreadStart(PushWords.RecitationSM2));

                thread.Start(Words);
            }
            else
            {
                thread = new Thread(new ParameterizedThreadStart(PushWords.RecitationSM2));

                thread.Start(Words);
            }
        }

        private void SetNumber_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(new ThreadStart(pushWords.SetWordNumber));
            thread.Start();
        }

        private void SetEngType_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(new ThreadStart(pushWords.SetEngType));
            thread.Start();
        }

        private void ImportWords_Click(object sender, EventArgs e)
        {
            OpenFileDialog Dialog = new OpenFileDialog();
            Dialog.Filter = "xlsx files (*.xlsx)|*.xlsx|xls files (*.xls)|*.xls";
            if (Dialog.ShowDialog() == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }
            String FileName = Dialog.FileName;
            CreateLog Log = new CreateLog();
            WordType Words = new WordType();
            Words.Number = Select.WORD_NUMBER;
            object lstObj = Log.ImportExcel(FileName);
            string typeObj = lstObj.ToString();
            string typeWord = typeof(List<Word>).ToString();
            string typeCustWord = typeof(List<CustomizeWord>).ToString();
            try
            {
                if (typeObj == typeWord)
                {
                    Words.WordList = (List<Word>)lstObj;
                    Select.TABLE_NAME = "CET4_1";
                }
                else if (typeObj == typeCustWord)
                {
                    Words.CustWordList = (List<CustomizeWord>)lstObj;
                    Select.TABLE_NAME = "自定义";
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("导入文件出错！", "TeamGipsy");
                    return;
                }
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show("导入文件出错！", "TeamGipsy");
                return;
            }

            if (!Directory.Exists("Log"))
            {
                System.IO.Directory.CreateDirectory("Log");
            }


            var state = thread.ThreadState;

            if (state == System.Threading.ThreadState.WaitSleepJoin || state == System.Threading.ThreadState.Stopped)
            {
                thread.Abort();
                while (thread.ThreadState != System.Threading.ThreadState.Aborted)
                {
                    Thread.Sleep(100);
                }
                if (Select.TABLE_NAME == "自定义")
                    thread = new Thread(new ParameterizedThreadStart(PushCustomizeWords.Recitation));
                else
                    thread = new Thread(new ParameterizedThreadStart(PushWords.Recitation));

                thread.Start(Words);
            }
            else
            {
                if (Select.TABLE_NAME == "自定义")
                    thread = new Thread(new ParameterizedThreadStart(PushCustomizeWords.Recitation));
                else
                    thread = new Thread(new ParameterizedThreadStart(PushWords.Recitation));

                thread.Start(Words);
            }
        }

        private void SelectBook_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem curitem = sender as ToolStripMenuItem;
            if (curitem != null && curitem.OwnerItem != null)
            {
                var Cms = (curitem.OwnerItem as ToolStripMenuItem).Owner as ContextMenuStrip;
                //int index = (curitem.OwnerItem as ToolStripMenuItem).DropDownItems.IndexOf(item);
                foreach (var itemi in ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems)
                {
                    (itemi as ToolStripMenuItem).Checked = false;
                }
            }
            curitem.Checked = true;
            // (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            string TempName = "";
            if (sender.ToString() == "四级核心词汇")
                TempName = "CET4_1";
            else if (sender.ToString() == "四级完整词汇")
                TempName = "CET4_3";
            else if (sender.ToString() == "六级核心词汇")
                TempName = "CET6_1";
            else if (sender.ToString() == "六级完整词汇")
                TempName = "CET6_3";
            else if (sender.ToString() == "IELTS词汇")
                TempName = "IELTS_3";
            else if (sender.ToString() == "TOEFL词汇")
                TempName = "TOEFL_2";
            else if (sender.ToString() == "考研必考词汇")
                TempName = "KaoYan_1";
            else if (sender.ToString() == "考研完整词汇")
                TempName = "KaoYan_2";
            Select.TABLE_NAME = TempName;
            Se.UpdateBookName(TempName);
            Se.UpdateTableCount();
            //if (sender.ToString() == "顺序五十音")
            //{
            //     int Progress = Se.GetGoinProgress();
            //     PushWords.PushMessage("当前词库：" + sender.ToString() + "\n当前进度：" + Progress.ToString() + "/104");
            // }
            // else
            //{
            List<int> res = Se.SelectCount();
            pushWords.PushMessage("当前词库：" + sender.ToString() + "\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
            // }
        }

        private void RandomWordTest_Click(object sender, EventArgs e)
        {
            var state = thread.ThreadState;
            if (state == System.Threading.ThreadState.WaitSleepJoin || state == System.Threading.ThreadState.Stopped)
            {
                thread.Abort();
                while (thread.ThreadState != System.Threading.ThreadState.Aborted)
                {
                    Thread.Sleep(100);
                }
            }
            thread = new Thread(new ParameterizedThreadStart(pushWords.UnorderWord));
            thread.Start(Select.WORD_NUMBER);
        }



        public void ResetLearingStatus_Click(object sender, EventArgs e)
        {
            string TableName;
            bool isok = TablelDictionary.TryGetValue(Select.TABLE_NAME, out TableName);
            if (!isok)
            {
                return;
            }

            DialogResult result = System.Windows.Forms.MessageBox.Show(
            $"是否要重置“{TableName}”的学习进度?", $"进度重置：{Select.TABLE_NAME}",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == System.Windows.Forms.DialogResult.Yes)
            {

                try
                {
                    Se.ResetTableCount();
                    pushWords.PushMessage($"重置{TableName}完成！");
                    //System.Windows.Forms.MessageBox.Show($"重置{TableName}完成！");

                }
                catch
                {
                    pushWords.PushMessage($"重置{TableName}出错！");
                    // System.Windows.Forms.MessageBox.Show($"重置{TableName}出错！");
                }
            }
            //this.Se. 
        }
        private void ShortCuts_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.MessageBox.Show("ALT+Q     ：开始内置单词学习\nALT+~     ：英语单词发音\nALT+1到4：对应点击按钮1到4", "版本号：1.0.0.0");
        }

        private void AutoPlay_Click(object sender, EventArgs e)
        {
            //sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            if (Select.AUTO_PLAY == 0)
            {
                Select.AUTO_PLAY = 1;
                (sender as ToolStripMenuItem).Checked = true;
            }
            else
            {
                Select.AUTO_PLAY = 0;
                (sender as ToolStripMenuItem).Checked = false;
            }
            Se.UpdateGlobalConfig();
        }

        private void AutoLog_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            if (Select.AUTO_LOG == 0)
            {
                Select.AUTO_LOG = 1;
                (sender as ToolStripMenuItem).Checked = true;
            }
            else
            {
                Select.AUTO_LOG = 0;
                (sender as ToolStripMenuItem).Checked = false;
            }
            Se.UpdateGlobalConfig();
        }


        private void ExitApp_Click(object sender, EventArgs e)
        {
            ToastNotificationManagerCompat.History.Clear();
            Environment.Exit(0);
        }

        private void Start_Click(object sender, EventArgs e)
        {
            //StartWithWindows.SetMeStart(true);
            String startupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "TeamGipsy.lnk");
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            StartWithWindows.CreateShortcut(startupPath);
        }
        #endregion

        System.Timers.Timer dailyReminderTimer;

        private void ScheduleDailyReminder()
        {
            DateTime now = DateTime.Now;
            DateTime target = new DateTime(now.Year, now.Month, now.Day, 23, 40, 0);
            if (now >= target) target = target.AddDays(1);
            double ms = (target - now).TotalMilliseconds;
            if (dailyReminderTimer != null)
            {
                dailyReminderTimer.Stop();
                dailyReminderTimer.Dispose();
            }
            dailyReminderTimer = new System.Timers.Timer(ms);
            dailyReminderTimer.AutoReset = false;
            dailyReminderTimer.Elapsed += (s, e) =>
            {
                ShowDailyReminderIfNoStudy();
                dailyReminderTimer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;
                dailyReminderTimer.AutoReset = true;
                dailyReminderTimer.Start();
            };
            dailyReminderTimer.Start();
        }

        private void ShowDailyReminderIfNoStudy()
        {
            try
            {
                int cnt = Se.ReviewedTodayCount();
                if (cnt <= 0)
                {
                    new ToastContentBuilder()
                        .AddText("今天还没有背单词")
                        .AddText("现在是23:40，来学习一下吧")
                        .AddButton(new ToastButton().SetContent("开始学习").AddArgument("action", "daily_begin").SetBackgroundActivation())
                        .Show();
                }
            }
            catch
            {
            }
        }
    }
}

