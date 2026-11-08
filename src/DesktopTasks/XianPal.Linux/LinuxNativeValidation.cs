// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;

namespace XianPal.Linux;

internal static class LinuxNativeValidation
{
    internal static async Task Run(PetWindow pet, string evidence)
    {
        string originalFixture = Environment.GetEnvironmentVariable("FUFU_TEST_CLAUDE_COMMAND");
        string terminal = LinuxTerminal.FindExecutable("xterm") ?? throw new Exception("xterm is required for native terminal acceptance.");
        Environment.SetEnvironmentVariable("XIANPAL_TERMINAL", terminal);
        string fixtures = Path.Combine(Program.StateDirectory, "native-fixtures");
        Directory.CreateDirectory(fixtures);
        string fixtureSource = """
#!/usr/bin/env python3
import json, os, subprocess, sys, time
# Python 3.6 on a minimal CentOS 7 image can use an ASCII filesystem codec.
# Recover the actual UTF-8 argument/path bytes instead of emitting surrogate escapes.
args = [os.fsencode(arg).decode('utf-8') for arg in sys.argv[1:]]
settings = {}
if '--settings' in args:
    index = args.index('--settings')
    with open(args[index+1], encoding='utf-8') as source:
        settings = json.load(source)
    os.environ.update(settings.get('env', {}))
    del args[index:index+2]
profile = os.environ.get('XIANPAL_FIXTURE_PROFILE', 'default')
root = os.path.join(os.environ['FUFU_STATE_DIR'], 'fixture-jobs', profile)
os.makedirs(root, exist_ok=True)
marker = os.path.basename(os.fsencode(sys.argv[0]).decode('utf-8')) + '|' + profile + '|' + os.getcwdb().decode('utf-8')
if '--help' in args:
    print('--bg --json --all')
    sys.exit(0)
if args and args[0] == 'agents':
    rows = []
    for name in os.listdir(root):
        if name.endswith('.json'):
            with open(os.path.join(root, name), encoding='utf-8') as source:
                row = json.load(source)
            row['state'] = 'done' if time.time() >= row['finish'] else 'working'
            rows.append(row)
    print(json.dumps(rows))
    sys.exit(0)
if args and args[0] == 'logs':
    print('背景任务中文🌱 ' + marker)
    sys.exit(0)
flags = args[:args.index('--')] if '--' in args else args
if '--bg' in flags:
    name = args[args.index('--name')+1]
    row = {'id':'job-' + name, 'name':name, 'kind':'background', 'state':'working', 'sessionId':'23b035ea-713c-468b-aa56-70cefad033a9', 'finish':time.time()+1.8}
    with open(os.path.join(root, name+'.json'), 'w', encoding='utf-8') as target:
        json.dump(row, target)
    print('Dispatched ' + marker)
    sys.exit(0)
if '--session-id' in flags:
    session = args[args.index('--session-id')+1]
    def hook(event, **payload):
        payload.update({'session_id':session, 'hook_event_name':event})
        for group in settings.get('hooks', {}).get(event, []):
            for handler in group.get('hooks', []):
                subprocess.run(handler['command'], shell=True, input=json.dumps(payload, ensure_ascii=False).encode('utf-8'), check=True)
    hook('SessionStart')
    hook('UserPromptSubmit', prompt=args[-1])
    hook('PreToolUse', tool_name='Read', tool_input={'path':'isolated-example.txt'})
    print('Native TTY fixture: ' + marker)
    time.sleep(.6)
    text = '中文🌱原生对话|' + marker + '|tty=' + str(sys.stdin.isatty() and sys.stdout.isatty())
    hook('MessageDisplay', delta=text)
    hook('Stop', last_assistant_message=text)
    hook('SessionEnd')
    sys.exit(0)
request = sys.stdin.read()
print(json.dumps({'type':'result','result':'角色输出|' + marker, 'is_error':False}, ensure_ascii=False))
""";
        var preferences = IntegrationPreferences.Load();
        string originalPreferences = new CompatibleJson().Serialize(preferences);
        try
        {
            foreach (PetCharacter character in PetCharacter.All)
            {
                string program = Path.Combine(fixtures, character.Id + "-claude.py");
                File.WriteAllText(program, fixtureSource);
                File.SetUnixFileMode(program, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                string configuration = Path.Combine(fixtures, character.Id + ".json");
                File.WriteAllText(configuration, new CompatibleJson().Serialize(new { env = new Dictionary<string, string> { ["XIANPAL_FIXTURE_PROFILE"] = character.Id } }));
                preferences.SetExecutablePath(character.Id, program);
                preferences.SetSettingsPath(character.Id, configuration);
            }
            preferences.Save();
            string directory = Path.Combine(fixtures, "中文 space ' $literal");
            Directory.CreateDirectory(directory);
            string before = pet.Conversation.LastReply;
            foreach (PetCharacter character in PetCharacter.All)
            {
                var task = pet.Services.Tasks.Add("Verify companion routing", directory, "interval", TimeSpan.Zero, DayOfWeek.Monday, 300,
                    DateTime.Now, "background", "block", character.Id);
                pet.Services.Tasks.RunNow(task);
                await Wait(() => task.History.Count > 0 && task.History[0].Status != "running");
                if (task.History[0].Status != "success" || !task.History[0].Transcript.Contains(character.Id + "-claude.py|" + character.Id + "|" + directory))
                    throw new Exception("Companion executable, settings or working directory were not respected: " + character.Id + "\n" + task.History[0].Transcript);
            }
            string literal = "Literal request $(touch SHOULD_NOT_EXIST) ; 中文 \" ' --bg";
            var interactive = pet.Services.Tasks.Add(literal, directory, "interval", TimeSpan.Zero, DayOfWeek.Monday, 300,
                DateTime.Now, "interactive-terminal", "block", "spark");
            pet.Services.Tasks.RunNow(interactive);
            var nativeRecord = interactive.History[0];
            pet.Services.Tasks.RunNow(interactive);
            if (interactive.History[0].Status != "skipped") throw new Exception("Blocking terminal task started twice.");
            await Wait(() => nativeRecord.Status != "running");
            if (nativeRecord.Status != "success" || !nativeRecord.Transcript.Contains("tty=True") || !nativeRecord.Transcript.Contains("spark-claude.py|spark|") || !nativeRecord.Transcript.Contains(literal))
                throw new Exception("Native CLI lost its terminal, literal prompt, hooks or companion identity: " + nativeRecord.Transcript);
            if (File.Exists(Path.Combine(directory, "SHOULD_NOT_EXIST"))) throw new Exception("A literal prompt was executed as shell code.");
            var background = pet.Services.Tasks.Add("Isolated background check", directory, "interval", TimeSpan.Zero, DayOfWeek.Monday, 300,
                DateTime.Now, "claude-background", "block", "moss");
            pet.Services.Tasks.RunNow(background);
            var backgroundRecord = background.History[0];
            await Wait(() => backgroundRecord.Status != "running");
            if (backgroundRecord.Status != "success" || !backgroundRecord.Transcript.Contains("moss-claude.py|moss|"))
                throw new Exception("Claude Background task routing or status query failed: " + backgroundRecord.Transcript);
            if (pet.Conversation.LastReply != before) throw new Exception("Native task output leaked into regular chat.");
            File.WriteAllText(Path.Combine(evidence, "native-task-results.txt"),
                "PASS four independent executables and settings; Unicode/quoted cwd; real terminal TTY; literal prompt; hooks; blocking mode; native background lifecycle; regular-chat isolation.\n");
        }
        finally
        {
            new CompatibleJson().Deserialize<IntegrationPreferences>(originalPreferences).Save();
            Environment.SetEnvironmentVariable("FUFU_TEST_CLAUDE_COMMAND", originalFixture);
        }
    }

    private static async Task Wait(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(50);
        while (!done())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Native terminal task timed out.");
            await Task.Delay(80);
        }
    }
}
