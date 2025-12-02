using System.Windows;
using Markdig;

namespace TeamGipsy
{
    public partial class DeepenMemoryWindow : Window
    {
        string _raw;
        string _cn;
        public DeepenMemoryWindow(string content, string translation)
        {
            InitializeComponent();
            _raw = content ?? "";
            _cn = translation ?? "";
            var html1 = Markdown.ToHtml(_raw);
            var html2 = Markdown.ToHtml(_cn);
            var doc = "<html><head><meta charset='utf-8'/><style>body{font-family:Segoe UI, Microsoft YaHei; font-size:30px; line-height:2.0; padding:18px;} p{margin:0 0 16px;} strong{font-weight:700;} h2{font-size:32px; margin:18px 0 12px;}</style></head><body>" + html1 + "<hr/><h2>译文</h2>" + html2 + "</body></html>";
            Browser.DocumentText = doc;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool done = false;
                if (Browser.Document != null)
                {
                    try
                    {
                        Browser.Focus();
                        Browser.Document.ExecCommand("Copy", false, null);
                        done = true;
                    }
                    catch { done = false; }
                }
                if (!done)
                    Clipboard.SetText((_raw ?? "") + "\n\n" + (_cn ?? ""));
            }
            catch { }
        }
    }
}
