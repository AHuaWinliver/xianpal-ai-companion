// SPDX-License-Identifier: Apache-2.0
namespace XianPal.Linux;

internal static class LinuxSelfTest
{
    internal static int Run()
    {
        try
        {
            var json = new CompatibleJson();
            var value = json.Deserialize<Dictionary<string, object>>("{\"event\":{\"delta\":{\"type\":\"text_delta\",\"text\":\"道友🌱\"}},\"type\":\"stream_event\"}");
            if (value["event"] is not Dictionary<string, object>) throw new Exception("Untyped nested JSON did not retain its shape.");
            var runner = new ClaudeCliRunner();
            string output = "";
            runner.OutputReceived += text => output += text;
            runner.ParseLine(json.Serialize(value));
            if (output != "道友🌱") throw new Exception("Unicode streaming failed.");
            var clock = new DragGaitClock();
            clock.RecordMotion(30, 20);
            int frame = clock.Advance(32, 0, 24);
            if (clock.Speed <= 0 || frame < 0 || frame >= 24) throw new Exception("Drag gait failed.");
            var task = new ScheduledClaudeTask { Recurrence = "weekly", Weekday = 1, MinutesAfterMidnight = 600 };
            DateTime monday = new(2026, 11, 9, 9, 0, 0);
            if (ClaudeTaskService.CalculateNextRun(task, monday) != monday.AddHours(1)) throw new Exception("Weekly schedule failed.");
            var model = CompanionRolePresets.Create("Test", Path.GetTempPath(), false);
            model.Goal = "只读检查隔离的示例目录，输出测试摘要。";
            SectExecution.Validate(model);
            string roundtrip = json.Serialize(json.Deserialize<CompanionSect>(json.Serialize(model)));
            if (!roundtrip.Contains(model.Name) || model.Members.Count != 4) throw new Exception("Workshop storage failed.");
            if (OperatingSystem.IsLinux())
            {
                if (SectExecution.DirectoriesOverlap("/tmp/Project", "/tmp/project")) throw new Exception("Case-sensitive paths overlap.");
                if (!SectExecution.DirectoriesOverlap("/tmp/project", "/tmp/project/sub")) throw new Exception("Parent directory overlap missed.");
            }
            Console.WriteLine("PASS shared JSON, Unicode stream, gait, weekly schedule and four-agent workshop validation");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
