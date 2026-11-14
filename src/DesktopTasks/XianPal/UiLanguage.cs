// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace XianPal
{
    internal static class UiLanguage
    {
        private static string selected;
        internal static event Action Changed;
        internal static string Current
        {
            get
            {
                if (selected != null) return selected;
                try { selected = File.ReadAllText(StoragePath).Trim(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                if (selected != "en" && selected != "zh-CN") selected = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en";
                return selected;
            }
        }
        internal static bool IsEnglish { get { return Current == "en"; } }
        private static string StoragePath
        {
            get
            {
                string test = Environment.GetEnvironmentVariable("FUFU_STATE_DIR");
                if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") == "1" && !String.IsNullOrEmpty(test) && Path.IsPathRooted(test)) return Path.Combine(test, "ui-language.txt");
#if XIANPAL_LINUX
                string root = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (String.IsNullOrEmpty(root) || !Path.IsPathRooted(root)) root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                return Path.Combine(root, "xianpal", "ui-language.txt");
#else
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(root, "XianPal", "ui-language.txt");
#endif
            }
        }

        internal static void Set(string value)
        {
            if (value != "en" && value != "zh-CN") throw new ArgumentException("Supported languages: en, zh-CN.");
            Directory.CreateDirectory(Path.GetDirectoryName(StoragePath));
            string temporary = StoragePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, value, new UTF8Encoding(false));
                if (File.Exists(StoragePath)) File.Replace(temporary, StoragePath, null); else File.Move(temporary, StoragePath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            selected = value;
            if (Changed != null) Changed();
        }

        internal static string Text(string source)
        {
            if (String.IsNullOrEmpty(source)) return source ?? "";
            string translated;
            if (IsEnglish) return Translations.TryGetValue(source, out translated) ? translated : source;
            return Reverse.TryGetValue(source, out translated) ? translated : source;
        }

        internal static string Format(string source, params object[] values)
        { return String.Format(IsEnglish ? CultureInfo.GetCultureInfo("en") : CultureInfo.GetCultureInfo("zh-CN"), Text(source), values); }

        internal static bool IsCaption(string source)
        { return source != null && (Translations.ContainsKey(source) || Reverse.ContainsKey(source)); }

        private static Dictionary<string, string> BuildReverse()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in Translations) if (!values.ContainsKey(pair.Value)) values.Add(pair.Value, pair.Key);
            return values;
        }

        private static readonly Dictionary<string, string> Translations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "青衡", "Qingheng" }, { "绯笺", "Feijian" }, { "苍芷", "Cangzhi" }, { "小墨", "Xiaomo" },
            { "默认设置", "Default settings" }, { "常规", "General" }, { "道友", "Companions" }, { "外观", "Appearance" }, { "行为", "Behavior" },
            { "语言", "Language" }, { "全局默认", "Global defaults" }, { "沿用全局配置", "Inherit global settings" },
            { "工作目录", "Working directory" }, { "Claude 默认工作目录", "Default Claude working directory" },
            { "Claude 配置文件", "Claude settings file" }, { "Claude 可执行文件", "Claude executable" },
            { "浏览…", "Browse…" }, { "选择目录…", "Choose folder…" }, { "选择 JSON…", "Choose JSON…" },
            { "保存", "Save" }, { "取消", "Cancel" }, { "应用", "Apply" }, { "关闭", "Close" }, { "隐藏", "Hide" },
            { "保持置顶", "Keep on top" }, { "减少动画", "Reduce motion" }, { "开机自动启动", "Start at login" },
            { "贴边不透明度", "Docked opacity" }, { "光轨透明度", "Trail opacity" }, { "宠物大小", "Companion size" },
            { "重置大小和位置", "Reset size and position" }, { "虚拟桌面显示范围", "Virtual desktop visibility" },
            { "仅在桌面 1 显示", "Desktop 1 only" }, { "在所有桌面显示", "All virtual desktops" },
            { "全屏跑动路线", "Roaming routes" }, { "内置随机曲线", "Built-in random curves" },
            { "Claude AI 路线池（一次生成24条）", "AI route pool (24 routes per request)" },
            { "AI 主动关怀", "Proactive care" }, { "允许主动说话与休息提醒", "Allow proactive messages and break reminders" },
            { "收集其他 Claude 会话（默认关闭）", "Monitor external Claude sessions (off by default)" },
            { "执行道友", "Run as" }, { "全部道友任务", "All companions' tasks" }, { "已保存任务", "Saved tasks" },
            { "Claude Code 定时任务", "Claude Code scheduled tasks" }, { "Claude Code 定时任务…", "Claude Code scheduled tasks…" },
            { "执行任务", "Prompt" }, { "运行情况", "Run history" }, { "任务运行情况", "Task run history" },
            { "目录", "Folder" }, { "目录：", "Folder: " }, { "运行桌面", "Desktop" }, { "当前桌面", "Current desktop" }, { "桌面 {0}", "Desktop {0}" },
            { "设为新任务默认", "Use as default" }, { "虚拟桌面（Win+Tab），需提前创建", "Create virtual desktops first (Win+Tab)." },
            { "PowerShell 窗口", "PowerShell" }, { "终端窗口", "Terminal" }, { "CLI 交互·PS", "CLI · PowerShell" },
            { "CLI 交互·终端", "CLI · Terminal" }, { "Claude BG·关窗", "Claude BG · close window" },
            { "任务由本机 claude CLI 执行；电脑休眠期间不会唤醒，恢复后补执行到期任务。", "Uses your local Claude CLI. Missed tasks run after the computer wakes." },
            { "Claude 原生 Background：创建成功后关窗；阻塞等待后台完成，需已支持 --bg 的 CLI。", "Native Claude BG: closes the launcher after dispatch. Requires a CLI with --bg support." },
            { "原生 CLI 可继续追问及确认权限；阻塞模式等待此 CLI 会话退出，不自动关窗。", "Interactive CLI supports follow-ups and permissions. Runs stay active until the session exits." },
            { "状态", "Status" }, { "方式", "Mode" }, { "开始时间", "Started" }, { "完成时间", "Finished" }, { "耗时", "Duration" }, { "退出码", "Exit code" },
            { "运行情况  ·  双击记录查看输出", "Run history · Double-click a run to view output" }, { "保存修改", "Save changes" },
            { "打开原生后台会话", "Open background session" }, { "定时任务运行详情", "Task transcript" },
            { "在 Claude CLI 打开", "Open in Claude CLI" },
            { "后台输出", "Background output" }, { "后台运行", "Background output" }, { "原生 CLI 终端", "Native CLI terminal" },
            { "上次未完成则跳过", "Skip while previous run is active" }, { "允许并行", "Allow parallel runs" },
            { "阻塞模式", "Skip overlapping runs" }, { "并行模式", "Allow parallel runs" },
            { "每天", "Daily" }, { "每周", "Weekly" }, { "固定间隔", "Interval" }, { "每隔一段时间", "Interval" },
            { "重复", "Repeat" }, { "每{0}", "Every {0}" }, { "定时提醒", "Scheduled reminders" },
            { "提示框", "Popup" }, { "演出", "Performance" }, { "已停用", "Disabled" },
            { "周日", "Sunday" }, { "周一", "Monday" }, { "周二", "Tuesday" }, { "周三", "Wednesday" },
            { "周四", "Thursday" }, { "周五", "Friday" }, { "周六", "Saturday" },
            { "分钟", "Minutes" }, { "小时", "Hours" }, { "天", "Days" },
            { "添加任务", "Add task" }, { "保存任务", "Save task" }, { "新任务", "New task" },
            { "立即执行", "Run now" }, { "停用", "Disable" }, { "启用", "Enable" }, { "删除", "Delete" }, { "编辑", "Edit" },
            { "启用 / 停用", "Enable / disable" }, { "运行中", "Running" }, { "正在运行", "Running" },
            { "已完成", "Completed" }, { "失败", "Failed" }, { "已跳过", "Skipped" }, { "需要输入", "Input needed" }, { "就绪", "Ready" },
            { "对话", "Chat" }, { "发送", "Send" }, { "停止", "Stop" }, { "新会话", "New conversation" },
            { "会话 ▾", "History ▾" }, { "仙伴 · 对话", "XianPal · Chat" }, { "正在回复", "Replying" },
            { "选择会话后直接回复…", "Select a conversation to reply…" }, { "回复所选 Claude 会话…", "Reply to this Claude conversation…" },
            { "该动态尚未取得会话 ID", "A session ID is not available yet" },
            { "新建文字对话    Win+Alt+P", "New conversation    Win+Alt+P" }, { "查看动态", "Activity" },
            { "切换形象", "Companions" }, { "打开 Claude Code CLI", "Open Claude Code CLI" },
            { "在 PowerShell 打开 Claude Code CLI", "Open Claude Code in PowerShell" },
            { "查看 Claude 动态", "Claude activity" }, { "Claude Code 动态", "Claude Code activity" },
            { "历史上的今天", "On this day" }, { "提醒与倒计时", "Reminders and countdowns" },
            { "提醒与倒计时…", "Reminders and countdowns…" }, { "每日提醒", "Daily reminders" },
            { "角色演绎", "Character performance" }, { "仅提示框", "Popup only" }, { "立即测试效果", "Preview effect" },
            { "添加", "Add" }, { "知道了", "Dismiss" }, { "稍后 5 分钟", "Snooze 5 minutes" },
            { "倒计时预设（分钟）", "Countdown presets (minutes)" }, { "开始自定义倒计时", "Start custom countdown" },
            { "没有进行中的倒计时", "No active countdown" }, { "示例：起来活动一下", "Example: take a movement break" },
            { "事件规划 / 门派协作", "Events and team workshop" }, { "事件规划 / 门派协作…", "Events and team workshop…" },
            { "门派大堂", "Team hall" }, { "成员与任务配置", "Members and tasks" }, { "事件规划", "Event planning" },
            { "交接记录", "Handoffs" }, { "打开角色资源目录", "Open character pack folder" },
            { "刷新外置角色", "Reload character packs" }, { "刷新外置角色 / 查看校验结果", "Reload character packs / validation" },
            { "显示宠物", "Show companion" }, { "隐藏宠物", "Hide companion" }, { "退出仙伴", "Quit XianPal" }, { "退出", "Quit" },
            { "跑一会儿", "Roam for a while" }, { "暂停 Claude 联动", "Pause Claude integration" },
            { "关于桌面宠物", "About XianPal" }, { "Claude Code：已连接", "Claude Code: connected" },
            { "任务输出在「运行情况」查看", "Task output is available in Run history" },
            { "你", "You" }, { "角色大小", "Companion size" },
            { "仙伴 · XianPal", "XianPal" }, { "仙伴 · Claude Code", "XianPal · Claude Code" },
            { "仙伴 · AI桌面伴侣", "XianPal · AI desktop companion" },
            { "休息提醒间隔（分钟）", "Break reminder interval (minutes)" },
            { "留空时继承全局值；全局留空时使用 Claude 默认配置。", "Leave a field empty to inherit the global value. Empty global fields use Claude's defaults." },
            { "留空时使用全局程序或 PATH", "Leave empty to use the global executable or PATH" },
            { "工作目录不存在。", "Working directory does not exist." }, { "设置未保存", "Settings were not saved" },
            { "安装目录过于宽泛，请选择独立应用目录。", "Choose a dedicated application folder, not a system or user root." },
            { "安装路径不能包含目录链接。", "The installation path cannot contain directory links." },
            { "此目录不属于本应用，请选择一个空目录。", "This folder contains unrelated files. Choose an empty application folder." },
            { "仙伴安装程序", "XianPal Setup" }, { "安装目录", "Installation folder" }, { "安装", "Install" },
            { "用于本机 Claude Code 工作流的动画桌面伴侣。", "An animated desktop companion for your local Claude Code workflow." },
            { "仅为当前用户安装，使用你自己的 Claude Code 配置；不捆绑模型或凭据。", "Installs for the current user. Uses your own Claude Code configuration; no model or credentials are bundled." },
            { "已检测到 Claude Code CLI。", "Claude Code CLI detected." },
            { "未检测到 Claude Code CLI，可在安装后配置。", "Claude Code CLI was not detected. You can configure it after installation." },
            { "正在安装仙伴…", "Installing XianPal…" }, { "安装失败。", "Installation failed." },
            { "仙伴已安装。请从宠物菜单配置 Claude Code。", "XianPal is installed. Open the companion menu to configure Claude Code." },
            { "桌面陪伴", "Desktop companionship" }, { "协调步态", "Coordinated movement" }, { "文件互动", "File interactions" },
            { "四位原创道友，拥有丰富的动画和表情。", "Four original companions with expressive animations." },
            { "平滑的步伐、转向及专属角色动作。", "Smooth steps, turns, and character-specific motion." },
            { "拖入文件后，在对话中确认处理需求。", "Drop files, then confirm your request in chat." },
            { "提醒和互动采用角色特色动画。", "Distinct animations for reminders and interactions." },
            { "动态", "Activity" }, { "提醒", "Reminders" }, { "●  本机 CLI", "●  Local CLI" },
            { "询问 Claude Code", "Ask Claude Code" }, { "询问 Claude Code…", "Ask Claude Code…" },
            { "Enter 发送 · Shift+Enter 换行", "Enter to send · Shift+Enter for a new line" },
            { "选择一条动态查看 Claude 回复", "Select a conversation to view Claude's reply" },
            { "Claude Code 定时任务运行详情", "Claude Code task transcript" },
            { "当前没有倒计时", "No active countdown" }, { "到点后，当前仙伴会来到屏幕中间", "Your companion moves to the center of this screen when the timer ends." },
            { "倒计时状态异常", "Countdown unavailable" }, { "开始", "Start" }, { "自定义", "Custom" }, { "提醒我做什么…", "What should I remind you to do?" },
            { "我知道了", "Dismiss" }, { "等待 Claude 回复…", "Waiting for Claude…" },
            { "仙伴 · 事件规划与门派协作", "XianPal · Events and team workshop" },
            { "仙伴 · 事件规划与门派大堂", "XianPal · Events and team hall" },
            { "确认并执行协作", "Confirm and run" }, { "停止本轮", "Stop run" }, { "人物形象", "Character" },
            { "下表：任务与依赖", "Tasks and dependencies" },
            { "点击大堂中的成员查看实时回复与交接摘要。执行记录会保存在本机。", "Select a companion in the hall to view live output and handoffs. Run history is stored locally." },
            { "共同目标 / 授权范围 / 验收要求（每位道友都能收到，不共享完整聊天历史）", "Shared goal, scope and acceptance criteria (sent to each agent; full chats stay separate)" },
            { "到点提醒，不自动执行。关联门派仅作为执行入口；补发过期提醒一次。", "Events remind you to act. Team runs require confirmation. Overdue reminders are delivered once." },
            { "每位成员是独立 Claude CLI 会话。最多2个并发；目录相同或互相包含时串行。\n可点击工作目录单元格后使用下方按钮选目录；任务依赖填写编号，以逗号分隔。", "Each member uses a separate Claude CLI session. Up to two agents run at once; overlapping folders run sequentially.\nSelect a working-directory cell, then choose a folder below. Separate dependency IDs with commas." },
            { "创建四道友门派", "Create four-agent team" }, { "执行门派任务", "Run team" }, { "保存门派配置", "Save team configuration" },
            { "取消运行", "Cancel run" }, { "配置门派名称、共同目标、成员职责、目录和任务依赖。", "Configure the team name, shared goal, member roles, folders and task dependencies." },
            { "创建一个门派并填写共同目标。", "Create a team and set a shared goal." },
            { "事件名称", "Event title" }, { "添加事件", "Add event" }, { "删除事件", "Delete event" }, { "保存事件", "Save events" },
            { "一次", "Once" }, { "每日", "Daily" }, { "已处理", "Handled" }, { "待提醒", "Upcoming" },
            { "删除定时任务", "Delete scheduled task" }, { "删除选中的任务和运行记录？", "Delete the selected task and its run history?" },
            { "定时任务未保存", "Task was not saved" }, { "任务工作目录", "Task working directory" },
            { "Claude 默认目录", "Default Claude folder" }, { "继承全局 / CLI 自身配置", "Inherit global settings or Claude's defaults" },
            { "并行", "Parallel" }, { "跳过重叠运行", "Skip overlap" }, { "受阻", "Blocked" },
            { "任务：", "Task:" }, { "Claude 回复：", "Claude reply:" }, { "执行中", "Running" },
            { "等待依赖 / 工位", "Waiting for dependencies" }, { "待命", "Ready" }, { "超时", "Timed out" }, { "已取消", "Cancelled" },
            { "上游失败，已阻断", "Blocked by upstream failure" }, { "上次运行中断", "Previous run interrupted" },
            { "点击成员查看任务、回复和交接数据 · 光点表示已记录的传递关系，不代表实时传输速度", "Select a companion to inspect output and handoffs. Moving markers show recorded connections, not live transfer speed." },
            { "门派 / 多 Agent", "Teams / agents" }, { "运行记录", "Run records" },
            { "四道友·研发", "Development team" }, { "四道友·会诊", "Review team" }, { "保存门派", "Save team" },
            { "为选中成员选目录", "Choose member folder" }, { "套用到选中成员", "Apply role to member" },
            { "成员编号", "Member ID" }, { "姓名", "Name" }, { "职责 / 系统提示", "Role / instructions" },
            { "任务编号", "Task ID" }, { "依赖任务编号", "Dependency IDs" }, { "任务内容", "Task instructions" },
            { "事件", "Event" }, { "本地时间，如2026-11-05 09:00", "Local time, e.g. 2026-11-05 09:00" },
            { "重复：once/daily/weekly", "Repeat: once/daily/weekly" }, { "关联门派名称（可空）", "Linked team (optional)" },
            { "启动仙伴", "Launch XianPal" }, { "安装完成。", "Installation complete." }, { "原安装已备份到：", "Previous installation saved at:" }
            ,{ "全屏漫游", "Full-screen roaming" }, { "AI 路线和主动发言会使用你配置的 Claude 服务，并可能产生用量。", "AI routes and proactive messages use your configured Claude service and may incur usage." }
        };
        private static readonly Dictionary<string, string> Reverse = BuildReverse();
    }
}
