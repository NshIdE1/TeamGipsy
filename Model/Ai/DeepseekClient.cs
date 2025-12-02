using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TeamGipsy.Model.SqliteControl;
using System.Web.Script.Serialization;

namespace TeamGipsy.Model.Ai
{
    public class DeepseekClient
    {
        readonly HttpClient _http = new HttpClient();

        public async Task<string> GenerateEssayAsync(List<TeamGipsy.Model.SqliteControl.Word> words)
        {
            var api = Select.AI_API_BASE;
            var key = Select.AI_API_KEY;
            if (string.IsNullOrWhiteSpace(api) || string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("AI配置未设置");

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            var uniq = words.Select(w => w.headWord).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            var count = uniq.Count;
            var joined = string.Join(", ", uniq);

            string levelHint = "请使用中等难度的词汇与句型";
            var book = Select.TABLE_NAME ?? "";
            if (book.StartsWith("CET4")) levelHint = "请使用较为简单易懂的词汇与句型";
            else if (book.StartsWith("CET6")) levelHint = "请使用中等难度的词汇与句型";
            else if (book.StartsWith("IELTS") || book.StartsWith("TOEFL")) levelHint = "可以适当使用较高级词汇，但保持可读性";

            string spellingHint = Select.ENG_TYPE == 1 ? "请使用美式拼写与表达" : "请使用英式拼写与表达";

            var prompt =
                "请用英语写一篇大约 150–200 词 的小短文。\n" +
                "要求：\n\n" +
                $"必须使用下面这{count}个单词，每个至少出现一次，并且放在自然的句子里：\n" + joined + "。\n\n" +
                "不限制主题，你可以自由选择故事或场景；\n" +
                "文章要有开头、发展和结尾，语句通顺，不要只是列单词；\n" +
                levelHint + "；" + spellingHint + "。";

            var body = "{\"model\":\"deepseek-chat\",\"messages\":[{\"role\":\"user\",\"content\":\"" + EscapeJson(prompt) + "\"}],\"temperature\":0.7}";
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(api, content);
            var txt = await resp.Content.ReadAsStringAsync();
            var essay = TryExtractContentStrict(txt);
            if (string.IsNullOrWhiteSpace(essay))
                essay = TryExtractContent(txt);
            if (string.IsNullOrWhiteSpace(essay))
                throw new InvalidOperationException("AI返回解析失败");
            return essay;
        }

        public async Task<string> TranslateAsync(string text)
        {
            var api = Select.AI_API_BASE;
            var key = Select.AI_API_KEY;
            if (string.IsNullOrWhiteSpace(api) || string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("AI配置未设置");

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            var prompt = "请将下面的英文短文翻译成简体中文，准确自然、通顺易读，不要逐词对照，不要返回任何额外说明，仅返回译文。如果原文包含 Markdown 格式（如加粗、段落），请在中文中保留这些格式。原文：\n" + text;
            var body = "{\"model\":\"deepseek-chat\",\"messages\":[{\"role\":\"user\",\"content\":\"" + EscapeJson(prompt) + "\"}],\"temperature\":0.3}";
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(api, content);
            var txt = await resp.Content.ReadAsStringAsync();
            var cn = TryExtractContentStrict(txt);
            if (string.IsNullOrWhiteSpace(cn)) cn = TryExtractContent(txt);
            if (string.IsNullOrWhiteSpace(cn)) throw new InvalidOperationException("AI返回解析失败");
            return cn;
        }

        static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        static string TryExtractContent(string json)
        {
            var m = Regex.Match(json, "\\\"content\\\"\\s*:\\s*\\\"([\\\\s\\\\S]*?)\\\"", RegexOptions.Multiline);
            if (m.Success)
            {
                var s = m.Groups[1].Value;
                s = s.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\t", "\t");
                return s;
            }
            return null;
        }

        static string TryExtractContentStrict(string json)
        {
            try
            {
                var ser = new JavaScriptSerializer();
                var obj = ser.Deserialize<ChatCompletionResponse>(json);
                var msg = obj?.choices?.FirstOrDefault()?.message;
                return msg?.content;
            }
            catch { return null; }
        }

        class ChatCompletionResponse
        {
            public List<Choice> choices { get; set; }
        }
        class Choice
        {
            public Message message { get; set; }
        }
        class Message
        {
            public string role { get; set; }
            public string content { get; set; }
        }
    }
}
