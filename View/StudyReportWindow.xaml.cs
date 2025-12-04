using System.Windows;

namespace TeamGipsy
{
    public partial class StudyReportWindow : Window
    {
        string _reportContent;
        
        public StudyReportWindow()
        {
            InitializeComponent();
            var style = "body{font-family:Segoe UI, Microsoft YaHei; font-size:24px; line-height:1.8; padding:18px;}";
            Browser.DocumentText = $"<html><head><meta charset='utf-8'/><style>{style}</style></head><body><p>正在生成学习情况汇报，请稍候...</p></body></html>";
        }
        
        public StudyReportWindow(string reportContent)
        {
            InitializeComponent();
            _reportContent = reportContent ?? "";
            Browser.DocumentText = BuildReportHtml(_reportContent);
        }

        public void SetContent(string reportContent)
        {
            _reportContent = reportContent ?? "";
            Browser.DocumentText = BuildReportHtml(_reportContent);
        }

        private string BuildReportHtml(string report)
        {
            string escapedReport = EscapeForJs(report ?? "");
            string style = "body{font-family:Segoe UI, Microsoft YaHei; font-size:24px; line-height:1.8; padding:18px;}" +
                           "h1,h2{color:#2c3e50; border-bottom:1px solid #eee; padding-bottom:10px;}" +
                           "h1{font-size:36px;}" +
                           "h2{font-size:28px; margin-top:30px;}" +
                           "ul,ol{margin-left:20px;}" +
                           "li{margin-bottom:8px;}" +
                           ".highlight{background-color:#f0f8ff; padding:5px 10px; border-radius:5px;}";
            
            string marked = @"/* marked.js minimal embed */
!function(e){function t(e){return o(e)?e:typeof e==""string""?document.getElementById(e):e}function n(e){return e.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')}function r(e){return e.replace(/\*\*(.*?)\*\*/g,'<strong>$1</strong>').replace(/\*(.*?)\*/g,'<em>$1</em>').replace(/`([^`]+)`/g,'<code>$1</code>').replace(/^\s*#\s+(.*)$/gm,'<h1>$1</h1>').replace(/^\s*##\s+(.*)$/gm,'<h2>$1</h2>').replace(/^\s*###\s+(.*)$/gm,'<h3>$1</h3>').replace(/^\s*\*\s+(.*)$/gm,'<ul><li>$1</li></ul>').replace(/^\s*\d+\.\s+(.*)$/gm,'<ol><li>$1</li></ol>').replace(/\n\n/g,'</p><p>').replace(/\n/g,'<br/>')}e.marked={parse:function(e){if(!e)return'';var s=e.replace(/\r\n/g,'\n');s=s.replace(/\t/g,'    ');return '<p>'+r(n(s))+'</p>'}}}(window);";
            
            string html = string.Format(@"<html><head><meta charset='utf-8'/><style>{0}</style></head><body></body><script>{1}</script><script>var report='{2}';document.body.innerHTML = marked.parse(report);</script></html>", style, marked, escapedReport);
            return html;
        }

        private string EscapeForJs(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", "\\n");
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
                    Clipboard.SetText(_reportContent ?? "");
            }
            catch { }
        }
    }
}