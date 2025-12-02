using System.Windows;

namespace TeamGipsy
{
    public partial class DeepenMemoryWindow : Window
    {
        string _raw;
        string _cn;
        public DeepenMemoryWindow()
        {
            InitializeComponent();
            var style = "body{font-family:Segoe UI, Microsoft YaHei; font-size:30px; line-height:2.0; padding:18px;}";
            Browser.DocumentText = $"<html><head><meta charset='utf-8'/><style>{style}</style></head><body><p>正在生成内容，请稍候...</p></body></html>";
        }
        public DeepenMemoryWindow(string content, string translation)
        {
            InitializeComponent();
            _raw = content ?? "";
            _cn = translation ?? "";
            var doc = BuildMarkdownHtml(_raw, _cn);
            Browser.DocumentText = doc;
        }

        public void SetContent(string content, string translation)
        {
            _raw = content ?? "";
            _cn = translation ?? "";
            Browser.DocumentText = BuildMarkdownHtml(_raw, _cn);
        }

        private string BuildMarkdownHtml(string md1, string md2)
        {
            string esc1 = EscapeForJs(md1 ?? "");
            string esc2 = EscapeForJs(md2 ?? "");
            string style = "body{font-family:Segoe UI, Microsoft YaHei; font-size:30px; line-height:2.0; padding:18px;} p{margin:0 0 16px;} strong{font-weight:700;} h2{font-size:32px; margin:18px 0 12px;}";
            string marked = @"/* marked.js minimal embed */
!function(e){function t(e){return o(e)?e:typeof e==""string""?document.getElementById(e):e}function n(e){return e.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')}function r(e){return e.replace(/\*\*(.*?)\*\*/g,'<strong>$1</strong>').replace(/\*(.*?)\*/g,'<em>$1</em>').replace(/`([^`]+)`/g,'<code>$1</code>').replace(/^\s*#\s+(.*)$/gm,'<h1>$1</h1>').replace(/^\s*##\s+(.*)$/gm,'<h2>$1</h2>').replace(/^\s*###\s+(.*)$/gm,'<h3>$1</h3>').replace(/^\s*\*\s+(.*)$/gm,'<ul><li>$1</li></ul>').replace(/\n\n/g,'</p><p>').replace(/\n/g,'<br/>')}e.marked={parse:function(e){if(!e)return'';var s=e.replace(/\r\n/g,'\n');s=s.replace(/\t/g,'    ');return '<p>'+r(n(s))+'</p>'}}}(window);";
            string html = $@"<html><head><meta charset='utf-8'/><style>{style}</style></head><body></body><script>{marked}</script><script>var md1='{esc1}';var md2='{esc2}';document.body.innerHTML = '<div class=""md"">'+marked.parse(md1)+'</div><hr/><h2>译文</h2><div class=""md"">'+marked.parse(md2)+'</div>'; </script></html>";
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
                    Clipboard.SetText((_raw ?? "") + "\n\n" + (_cn ?? ""));
            }
            catch { }
        }
    }
}
