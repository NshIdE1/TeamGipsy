using System.Windows;
using Markdig;

namespace TeamGipsy
{
    public partial class DeepenMemoryWindow : Window
    {
        string _raw;
        public DeepenMemoryWindow(string content)
        {
            InitializeComponent();
            _raw = content ?? "";
            var html = Markdown.ToHtml(_raw ?? "");
            var doc = "<html><head><meta charset='utf-8'/><style>body{font-family:Segoe UI, Microsoft YaHei; font-size:34px; line-height:2.4; padding:22px;} p{margin:0 0 20px;} strong{font-weight:700;}</style></head><body>" + html + "</body></html>";
            Browser.DocumentText = doc;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(_raw ?? ""); } catch { }
        }
    }
}
