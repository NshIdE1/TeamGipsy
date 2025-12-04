// 说明：DeepseekClient 负责与深度求索（Deepseek）接口交互
// - GenerateEssayAsync：根据今日单词生成符合要求的小短文（英文，支持难度与美/英式拼写偏好）
// - TranslateAsync：将英文短文翻译为简体中文，并尽量保留 Markdown 格式
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

        public string GenerateStudyReportPrompt(Dictionary<string, object> studyData)
        {
            string totalWords = studyData.ContainsKey("TotalWords") ? studyData["TotalWords"].ToString() : "0";
            string learnedWords = studyData.ContainsKey("LearnedWords") ? studyData["LearnedWords"].ToString() : "0";
            string todayLearned = studyData.ContainsKey("TodayLearned") ? studyData["TodayLearned"].ToString() : "0";
            string accuracy = studyData.ContainsKey("Accuracy") ? studyData["Accuracy"].ToString() : "0%";
            string streak = studyData.ContainsKey("Streak") ? studyData["Streak"].ToString() : "0";
            
            string prompt = $"作为一位专业的英语学习顾问，请根据以下学习数据为我生成一份详细的学习情况汇报和学习规划：\n" +
                           $"- 总词汇量：{totalWords}\n" +
                           $"- 已学习词汇：{learnedWords}\n" +
                           $"- 今日学习词汇：{todayLearned}\n" +
                           $"- 学习准确率：{accuracy}\n" +
                           $"- 连续学习天数：{streak}\n\n" +
                           "请分析我的学习情况，包括优势和需要改进的地方，并制定一份个性化的学习规划，帮助我更高效地学习英语词汇。";
            
            return prompt;
        }
        public async Task<string> SendMessageAsync(string prompt)
        {
            var api = Select.AI_API_BASE;
            var key = Select.AI_API_KEY;
            if (string.IsNullOrWhiteSpace(api) || string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("AI配置未设置");

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            var body = "{\"model\":\"deepseek-chat\",\"messages\":[{\"role\":\"user\",\"content\":\"" + EscapeJson(prompt) + "\"}],\"temperature\":0.7}";
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(api, content);
            var txt = await resp.Content.ReadAsStringAsync();
            var response = TryExtractContentStrict(txt);
            if (string.IsNullOrWhiteSpace(response))
                response = TryExtractContent(txt);
            if (string.IsNullOrWhiteSpace(response))
                throw new InvalidOperationException("AI返回解析失败");
            return response;
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
