using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace XianPal
{
    internal sealed class PetBrainResponse
    {
        internal string Speech;
        internal string TrailStyle;
        internal Color[] TrailColors;
        internal string PathCurve;
        internal PointF[] PathControlPoints;
        internal PetPathPlan[] PathPlans;
    }

    internal sealed class PetPathPlan
    {
        internal string Curve;
        internal PointF[] ControlPoints;
    }

    internal static class PetBrain
    {
        internal static string BuildConversationPrompt(PetCharacter character, string userPrompt)
        {
            return "你是用户桌面上的原创国风AI伴侣，不扮演或模仿任何已有作品角色，当前角色是“" + character.Name + "”。" + character.Persona +
                "\n保持角色的措辞、情绪和称呼方式，但首先准确、直接地帮助用户完成请求；不要捏造已执行的操作，不要解释这段角色设定。" +
                "\n用户消息：\n" + (userPrompt ?? "");
        }

        internal static string BuildProactivePrompt(PetCharacter character, DateTime now, bool requestAiPath)
        {
            string pathInstruction = requestAiPath
                ? "\n这次需要一次性补充24条全屏运动路线，供宠物之后在本地逐条使用；不要只返回一条，也不要要求后续再次调用你。每条从 smooth、bezier、wave、orbit 中选择一种，给出5到8个不重复的归一化控制点。坐标范围0.04到0.96，每条路线至少横跨屏幕宽度70%和高度55%，路线之间要有明显差异。" +
                  "\n连续输出恰好24行路线，每行格式如下：" +
                  "\nPATH|curve=曲线类型|points=0.10,0.50;0.28,0.12;0.72,0.88;0.92,0.35;0.50,0.08"
                : "";
            return "你是用户桌面上的原创国风AI伴侣，不扮演或模仿任何已有作品角色，当前角色是“" + character.Name + "”。" + character.Persona +
                "\n现在是 " + now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                "。用户刚刚双击唤醒你。主动说一句符合人物特征、自然、有趣且不重复的中文短句（15到55字），可以关心、吐槽、鼓励或邀请互动。" +
                "\n再为你即将在桌面奔跑留下的光轨选择4到7个协调且清晰的颜色，并选择 rainbow、aurora、neon、candy 中的一种风格。" +
                "\n严格按照下面格式返回，不要Markdown，不要额外说明：" +
                "\nTRAIL|style=风格|colors=#RRGGBB,#RRGGBB,#RRGGBB,#RRGGBB" +
                pathInstruction +
                "\nSAY|人物发言";
        }

        internal static string BuildCarePrompt(PetCharacter character, DateTime now, string reason, int continuousScreenMinutes)
        {
            bool breakReminder = String.Equals(reason, "break", StringComparison.OrdinalIgnoreCase);
            string task = breakReminder
                ? "用户已经连续保持屏幕开启并使用电脑约 " + Math.Max(1, continuousScreenMinutes).ToString(CultureInfo.InvariantCulture) + " 分钟。请用符合人物性格的方式自然提醒用户休息一下，短暂离开屏幕、眺望远处、喝水或活动身体。语气关心但不要说教，20到60字。"
                : "你决定主动和用户说一句话。请根据当前时间，用符合人物性格的方式自然地关心、鼓励、吐槽或发起轻松互动，避免重复固定台词，15到55字。";
            return "你是用户桌面上的原创国风AI伴侣，不扮演或模仿任何已有作品角色，当前角色是“" + character.Name + "”。" + character.Persona +
                "\n现在是 " + now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "。" + task +
                "\n严格只返回一行，不要Markdown，不要额外说明：\nSAY|人物发言";
        }

        internal static string BuildHistoryTodayPrompt(PetCharacter character, DateTime today)
        {
            string date = today.ToString("M月d日", CultureInfo.GetCultureInfo("zh-CN"));
            return "你是用户桌面上的原创国风AI伴侣，不扮演或模仿任何已有作品角色，当前角色是“" + character.Name + "”。" + character.Persona +
                "\n今天是" + date + "。请介绍历史上发生在这一天的3到5件重要事件。优先选择有可靠共识、影响较大且彼此领域不同的事件；每件必须给出年份、事件和一句简短意义。" +
                "\n保持当前角色的口吻和称呼习惯，但事实必须准确，不要为了角色效果篡改年份、人物或事件。对无法确认的细节宁可省略，不要编造。总长度控制在180到320个中文字符。" +
                "\n把内容写成适合桌面气泡阅读的一段中文，可用全角分号分隔事件；不要Markdown、标题、项目符号或换行。" +
                "\n严格只返回一行：\nSAY|人物发言";
        }

        internal static PetBrainResponse Parse(string value, PetCharacter character)
        {
            string raw = UiText.Normalize(value ?? "");
            string style = "rainbow";
            Match styleMatch = Regex.Match(raw, @"style\s*=\s*([a-zA-Z]+)", RegexOptions.IgnoreCase);
            if (styleMatch.Success)
            {
                string candidate = styleMatch.Groups[1].Value.ToLowerInvariant();
                if (candidate == "rainbow" || candidate == "aurora" || candidate == "neon" || candidate == "candy") style = candidate;
            }

            List<Color> colors = new List<Color>();
            foreach (Match match in Regex.Matches(raw, @"#[0-9a-fA-F]{6}"))
            {
                Color color;
                if (TryParseColor(match.Value, out color) && !colors.Contains(color)) colors.Add(color);
                if (colors.Count == 7) break;
            }
            if (colors.Count < 3) colors.AddRange(DefaultColors(character == null ? "orbit" : character.Id));

            List<PetPathPlan> pathPlans = new List<PetPathPlan>();
            foreach (Match pathMatch in Regex.Matches(raw, @"(?:^|\n)\s*PATH\s*\|\s*curve\s*=\s*([a-zA-Z]+)\s*\|\s*points\s*=\s*([^\r\n]+)", RegexOptions.IgnoreCase))
            {
                string curve = "smooth";
                string candidateCurve = pathMatch.Groups[1].Value.ToLowerInvariant();
                if (candidateCurve == "smooth" || candidateCurve == "bezier" || candidateCurve == "wave" || candidateCurve == "orbit") curve = candidateCurve;
                List<PointF> pathPoints = new List<PointF>();
                string[] pairs = pathMatch.Groups[2].Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string pair in pairs)
                {
                    string[] coordinates = pair.Split(',');
                    float x, y;
                    if (coordinates.Length != 2 ||
                        !Single.TryParse(coordinates[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                        !Single.TryParse(coordinates[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y)) continue;
                    x = Math.Max(0.04f, Math.Min(0.96f, x));
                    y = Math.Max(0.04f, Math.Min(0.96f, y));
                    PointF point = new PointF(x, y);
                    if (pathPoints.Count == 0 || DistanceSquared(pathPoints[pathPoints.Count - 1], point) > 0.0025f) pathPoints.Add(point);
                    if (pathPoints.Count == 8) break;
                }
                if (pathPoints.Count >= 4)
                    pathPlans.Add(new PetPathPlan { Curve = curve, ControlPoints = pathPoints.ToArray() });
                if (pathPlans.Count == 32) break;
            }

            string speech = "";
            Match speechMatch = Regex.Match(raw, @"(?:^|\n)\s*SAY\s*\|\s*(.+)", RegexOptions.IgnoreCase);
            if (speechMatch.Success) speech = speechMatch.Groups[1].Value.Trim();
            if (String.IsNullOrWhiteSpace(speech))
            {
                string[] lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.IndexOf("TRAIL|", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (line.IndexOf("PATH|", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    speech = Regex.Replace(line, @"^\s*SAY\s*\|\s*", "", RegexOptions.IgnoreCase).Trim();
                    if (speech.Length > 0) break;
                }
            }
            if (String.IsNullOrWhiteSpace(speech) && character != null && character.Quotes.Length > 0) speech = character.Quotes[0];
            speech = Regex.Replace(speech ?? "", @"^[`*_#>\s]+|[`*_#>\s]+$", "");
            PetPathPlan firstPath = pathPlans.Count == 0 ? null : pathPlans[0];
            return new PetBrainResponse
            {
                Speech = speech,
                TrailStyle = style,
                TrailColors = colors.ToArray(),
                PathCurve = firstPath == null ? "smooth" : firstPath.Curve,
                PathControlPoints = firstPath == null ? new PointF[0] : firstPath.ControlPoints,
                PathPlans = pathPlans.ToArray()
            };
        }

        private static float DistanceSquared(PointF first, PointF second)
        {
            float dx = first.X - second.X, dy = first.Y - second.Y;
            return (dx * dx) + (dy * dy);
        }

        internal static Color[] DefaultColors(string characterId)
        {
            if (String.Equals(characterId, "orbit", StringComparison.OrdinalIgnoreCase))
                return new[] { Color.FromArgb(85, 169, 185), Color.FromArgb(117, 213, 209), Color.FromArgb(222, 185, 108), Color.FromArgb(111, 156, 216) };
            if (String.Equals(characterId, "spark", StringComparison.OrdinalIgnoreCase))
                return new[] { Color.FromArgb(240, 154, 125), Color.FromArgb(255, 207, 157), Color.FromArgb(220, 149, 175), Color.FromArgb(230, 190, 112) };
            if (String.Equals(characterId, "pippin", StringComparison.OrdinalIgnoreCase))
                return new[] { Color.FromArgb(192, 166, 107), Color.FromArgb(131, 170, 194), Color.FromArgb(209, 204, 182), Color.FromArgb(158, 193, 177) };
            if (String.Equals(characterId, "moss", StringComparison.OrdinalIgnoreCase))
                return new[] { Color.FromArgb(143, 186, 143), Color.FromArgb(205, 214, 165), Color.FromArgb(212, 197, 150), Color.FromArgb(133, 185, 176) };
            return new[] { Color.FromArgb(255, 91, 154), Color.FromArgb(255, 192, 68), Color.FromArgb(72, 218, 204), Color.FromArgb(92, 137, 255), Color.FromArgb(180, 105, 255) };
        }

        private static bool TryParseColor(string value, out Color color)
        {
            color = Color.Empty;
            if (String.IsNullOrEmpty(value) || value.Length != 7) return false;
            int rgb;
            if (!Int32.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb)) return false;
            color = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            return true;
        }
    }
}
