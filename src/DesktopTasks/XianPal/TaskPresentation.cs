// SPDX-License-Identifier: Apache-2.0
using System;
using System.Globalization;

namespace XianPal
{
    internal static class TaskPresentation
    {
        internal static string Owner(string id)
        {
            foreach (PetCharacter profile in PetCharacter.All)
                if (String.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase)) return UiLanguage.Text(profile.Name);
            return PetCharacter.OwnerName(id);
        }
        internal static string Desktop(int number) { return number == 0 ? UiLanguage.Text("当前桌面") : UiLanguage.Format("桌面 {0}", number); }
        internal static string Mode(string mode)
        {
            if (!UiLanguage.IsEnglish) return ClaudeTaskService.RunModeLabel(mode);
            switch (ClaudeTaskService.NormalizeRunMode(mode))
            {
                case "interactive-powershell": return "CLI · PowerShell";
                case "interactive-terminal": return "CLI · Terminal";
                case "powershell": return "PowerShell";
                case "terminal": return "Terminal";
                case "claude-background": return "Claude BG";
                default: return "Background";
            }
        }
        internal static string Schedule(ScheduledClaudeTask task)
        {
            if (!UiLanguage.IsEnglish) return task.ScheduleText;
            string time = (task.MinutesAfterMidnight / 60).ToString("00") + ":" + (task.MinutesAfterMidnight % 60).ToString("00");
            if (task.Recurrence == "daily") return "Daily " + time;
            if (task.Recurrence == "weekly") return "Every " + CultureInfo.GetCultureInfo("en").DateTimeFormat.DayNames[Math.Max(0, Math.Min(6, task.Weekday))] + " " + time;
            int minutes = Math.Max(1, task.IntervalMinutes);
            if (minutes % 1440 == 0) return "Every " + minutes / 1440 + (minutes == 1440 ? " day" : " days");
            if (minutes % 60 == 0) return "Every " + minutes / 60 + (minutes == 60 ? " hour" : " hours");
            return "Every " + minutes + (minutes == 1 ? " minute" : " minutes");
        }
    }
}
