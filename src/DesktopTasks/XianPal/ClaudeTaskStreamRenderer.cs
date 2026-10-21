using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;

namespace XianPal
{
    // Public for the private PowerShell task host. It renders the CLI wire
    // protocol, not a second Claude session and not an interactive TUI scrape.
    public sealed class ClaudeTaskStreamRenderer
    {
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 8000000 };
        private readonly HashSet<string> tools = new HashSet<string>();
        private readonly HashSet<string> toolDetails = new HashSet<string>();
        private readonly HashSet<string> results = new HashSet<string>();
        private readonly Dictionary<string, string> textByMessage = new Dictionary<string, string>();
        private readonly Dictionary<string, string> streamedBlocks = new Dictionary<string, string>();
        private readonly HashSet<string> assistantChunks = new HashSet<string>();
        private readonly Dictionary<int, Dictionary<string, object>> blocks = new Dictionary<int, Dictionary<string, object>>();
        private readonly Dictionary<int, StringBuilder> inputs = new Dictionary<int, StringBuilder>();
        private string messageId = "";
        private bool hasText;
        private bool lineOpen;
        public string Status { get; private set; }
        public string SessionId { get; private set; }
        public string Reaction { get; private set; }

        public ClaudeTaskStreamRenderer() { Status = "Claude 正在连接…"; Reaction = "UserPromptSubmit"; SessionId = ""; }

        public string Consume(string line)
        {
            if (String.IsNullOrWhiteSpace(line)) return "";
            Dictionary<string, object> root;
            try { root = json.Deserialize<Dictionary<string, object>>(line); }
            catch { return ClaudeCliRunner.ShouldSuppressDiagnostic(line) ? "" : Line(line); }
            if (root == null) return Line(line);
            string session = Text(root, "session_id");
            if (session.Length != 0) SessionId = session;
            string type = Text(root, "type");
            if (type == "system")
            {
                if (Text(root, "subtype") == "init") { Status = "Claude CLI 已连接，正在处理…"; return Line("[已连接] Claude Code"); }
                string info = Text(root, "message");
                return info.Length == 0 ? "" : Line("[状态] " + info);
            }
            if (type == "stream_event")
            {
                Dictionary<string, object> evt = Map(root, "event"), delta = Map(evt, "delta");
                string eventType = Text(evt, "type");
                int index = 0; Int32.TryParse(Text(evt, "index"), out index);
                if (eventType == "message_start")
                {
                    messageId = Text(Map(evt, "message"), "id"); blocks.Clear(); inputs.Clear();
                    Status = "Claude 正在思考…"; Reaction = "UserPromptSubmit";
                }
                if (eventType == "content_block_start")
                {
                    Dictionary<string, object> block = Map(evt, "content_block");
                    blocks[index] = block;
                    if (Text(block, "type") == "tool_use") { inputs[index] = new StringBuilder(); return Tool(block, false); }
                    if (Text(block, "type") == "thinking") { Status = "Claude 正在思考…"; return Line("[思考] 正在分析任务…"); }
                }
                if (Text(delta, "type") == "text_delta")
                {
                    Status = "Claude 正在回复…"; Reaction = "PostToolUse";
                    string fragment = Text(delta, "text"), old;
                    string blockKey = messageId + ":" + index;
                    streamedBlocks.TryGetValue(blockKey, out old);
                    streamedBlocks[blockKey] = (old ?? "") + fragment;
                    textByMessage.TryGetValue(messageId, out old);
                    textByMessage[messageId] = (old ?? "") + fragment;
                    hasText = true; lineOpen = true; return fragment;
                }
                if (Text(delta, "type") == "input_json_delta" && inputs.ContainsKey(index)) inputs[index].Append(Text(delta, "partial_json"));
                if (eventType == "content_block_stop" && inputs.ContainsKey(index))
                {
                    Dictionary<string, object> block = blocks[index];
                    try { block["input"] = json.Deserialize<Dictionary<string, object>>(inputs[index].ToString()); } catch (ArgumentException) { }
                    inputs.Remove(index); return Tool(block, true);
                }
                return "";
            }
            if (type == "assistant")
            {
                Dictionary<string, object> message = Map(root, "message");
                string id = Text(message, "id"); if (id.Length == 0) id = messageId;
                StringBuilder output = new StringBuilder(), fullText = new StringBuilder();
                foreach (Dictionary<string, object> block in Content(message))
                {
                    if (Text(block, "type") == "text") fullText.Append(Text(block, "text"));
                    if (Text(block, "type") == "tool_use") output.Append(Tool(block, true));
                }
                string text = fullText.ToString(), rendered;
                textByMessage.TryGetValue(id, out rendered);
                bool blockAlreadyStreamed = false;
                foreach (KeyValuePair<string, string> block in streamedBlocks)
                    if (block.Key.StartsWith(id + ":", StringComparison.Ordinal) && block.Value == text) { blockAlreadyStreamed = true; break; }
                if (text.Length > 0 && text != rendered && !blockAlreadyStreamed && assistantChunks.Add(id + ":" + text))
                {
                    // Partial streaming events and full assistant messages are
                    // alternate representations of the same reply, not two replies.
                    string remainder = String.IsNullOrEmpty(rendered) ? text : (text.StartsWith(rendered, StringComparison.Ordinal) ? text.Substring(rendered.Length) : text);
                    if (remainder.Length > 0) { output.Append(remainder); lineOpen = true; hasText = true; }
                    textByMessage[id] = (rendered ?? "") + remainder;
                }
                return output.ToString();
            }
            if (type == "user")
            {
                StringBuilder output = new StringBuilder();
                foreach (Dictionary<string, object> block in Content(Map(root, "message")))
                    if (Text(block, "type") == "tool_result" && results.Add(Text(block, "tool_use_id")))
                    {
                        bool error = Text(block, "is_error").Equals("True", StringComparison.OrdinalIgnoreCase);
                        Status = error ? "Claude 工具执行失败" : "Claude 工具已返回，继续处理…";
                        Reaction = error ? "PostToolUseFailure" : "PostToolUse";
                        object content; block.TryGetValue("content", out content);
                        output.Append(Line((error ? "[工具失败] " : "[工具结果] ") + UiText.TailCodeUnits(PlainContent(content), 2400)));
                    }
                return output.ToString();
            }
            if (type == "tool_progress") { Status = "Claude 正在执行工具：" + Text(root, "tool_name"); Reaction = "PreToolUse"; return ""; }
            if (type == "result")
            {
                string output = !hasText ? Line(Text(root, "result")) : "";
                bool failed = Text(root, "is_error").Equals("True", StringComparison.OrdinalIgnoreCase);
                Status = failed ? "Claude 本轮执行失败" : "Claude 已完成回复";
                Reaction = failed ? "StopFailure" : "Stop";
                object errors; if (root.TryGetValue("errors", out errors)) output += Line("[执行失败] " + PlainContent(errors));
                object denials;
                if (root.TryGetValue("permission_denials", out denials) && denials is ICollection && ((ICollection)denials).Count > 0)
                { Status = "Claude 有工具权限未获批准"; Reaction = "PermissionRequest"; output += Line("[权限受限] 部分工具未获批准，请检查本机 Claude Code 的权限设置。"); }
                return output;
            }
            return "";
        }

        private string Tool(Dictionary<string, object> block, bool details)
        {
            string id = Text(block, "id"), name = Text(block, "name"), output = "";
            if (tools.Add(id)) { Status = "Claude 正在执行工具：" + name; Reaction = "PreToolUse"; output = Line("[工具] " + name); }
            if (details)
            {
                Dictionary<string, object> input = Map(block, "input");
                foreach (string key in new[] { "command", "file_path", "path", "pattern", "description" })
                {
                    string value = Text(input, key);
                    if (value.Length > 0 && toolDetails.Add(id + ":" + key)) output += Line("  " + key + ": " + UiText.TailCodeUnits(value, 1000));
                }
            }
            return output;
        }
        private string Line(string text)
        {
            if (String.IsNullOrEmpty(text)) return "";
            string prefix = lineOpen ? Environment.NewLine : ""; lineOpen = false;
            return prefix + UiText.Normalize(text) + Environment.NewLine;
        }
        private static Dictionary<string, object> Map(Dictionary<string, object> parent, string key)
        { object value; return parent != null && parent.TryGetValue(key, out value) ? value as Dictionary<string, object> : null; }
        private static string Text(Dictionary<string, object> parent, string key)
        { object value; return parent != null && parent.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : ""; }
        private static IEnumerable<Dictionary<string, object>> Content(Dictionary<string, object> message)
        {
            object value; if (message == null || !message.TryGetValue("content", out value) || value is string) yield break;
            IEnumerable list = value as IEnumerable; if (list == null) yield break;
            foreach (object item in list) { Dictionary<string, object> block = item as Dictionary<string, object>; if (block != null) yield return block; }
        }
        private static string PlainContent(object value)
        {
            if (value == null) return ""; if (value is string) return (string)value;
            StringBuilder text = new StringBuilder(); IEnumerable list = value as IEnumerable;
            if (list != null) foreach (object item in list)
            { Dictionary<string, object> block = item as Dictionary<string, object>; if (block != null) text.Append(Text(block, "text")); else if (item is string) text.Append((string)item); }
            return text.ToString();
        }
    }
}
