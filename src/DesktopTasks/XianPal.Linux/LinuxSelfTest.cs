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
            ValidateRoutes();
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
                string[] identity = LinuxProcessIdentity.CurrentMarker().Split('|');
                if (LinuxProcessIdentity.IsAlive(identity) != true) throw new Exception("Current process identity was unstable.");
                identity[1] = (ulong.Parse(identity[1]) + 1).ToString();
                if (LinuxProcessIdentity.IsAlive(identity) != false) throw new Exception("Reused process ID was accepted.");
                if (LinuxProcessIdentity.IsAlive(new[] { "partial" }) != null) throw new Exception("Partial process marker was treated as exited.");
                if (SectExecution.DirectoriesOverlap("/tmp/Project", "/tmp/project")) throw new Exception("Case-sensitive paths overlap.");
                if (!SectExecution.DirectoriesOverlap("/tmp/project", "/tmp/project/sub")) throw new Exception("Parent directory overlap missed.");
            }
            Console.WriteLine("PASS shared JSON, Unicode stream, gait, weekly schedule and four-agent workshop validation");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void ValidateRoutes()
    {
        var area = new System.Drawing.Rectangle(-1920, -100, 1920, 1080);
        var size = new System.Drawing.Size(144, 156);
        var origin = new System.Drawing.Point(-960, 300);
        for (int pattern = 0; pattern < 4; pattern++)
        {
            var route = CompanionRoutes.GeneratePlayfulPath(area, size, origin, pattern);
            if (route.Count < 14 || ArtifactTravelClock.RouteLength(origin, route) < ArtifactTravelClock.Threshold(area)) throw new Exception("Full-monitor route was too short.");
            foreach (var point in route)
                if (point.X < area.Left || point.X + size.Width > area.Right || point.Y < area.Top || point.Y + size.Height > area.Bottom)
                    throw new Exception("A route crossed the active monitor's working area.");
        }
        var controls = new[] { new System.Drawing.PointF(.05f, .05f), new System.Drawing.PointF(.95f, .95f), new System.Drawing.PointF(.05f, .95f), new System.Drawing.PointF(.95f, .05f) };
        if (CompanionRoutes.GenerateAiPath(area, size, origin, "orbit", controls).Count < 30) throw new Exception("AI control points were not interpolated.");
        controls[0] = new System.Drawing.PointF(float.NaN, .3f);
        if (CompanionRoutes.GenerateAiPath(area, size, origin, "orbit", controls).Count != 0) throw new Exception("Nonfinite AI coordinates were accepted.");
        var lines = Enumerable.Repeat("PATH|curve=wave|points=0.05,0.05;0.95,0.95;0.05,0.95;0.95,0.05", 24);
        var response = PetBrain.Parse(string.Join("\n", lines) + "\nSAY|Hello 🌱", PetCharacter.All[0]);
        if (response.PathPlans.Length != 24 || response.Speech != "Hello 🌱") throw new Exception("Batched route protocol was incomplete.");
    }
}
