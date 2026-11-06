// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XianPal;
internal static class ChatBubbleTests
{
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static void Bodies(Control control,List<RichTextBox> result){foreach(Control child in control.Controls){var rich=child as RichTextBox;if(rich!=null)result.Add(rich);Bodies(child,result);}}
    [STAThread] private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();string root=Environment.GetEnvironmentVariable("FUFU_STATE_DIR");Directory.CreateDirectory(root);
            var a=new ActivityRecord {Title="今天一起做些什么？",CharacterId="orbit",SessionId=Guid.NewGuid().ToString(),State="就绪",Updated=DateTime.Now,Output="你：今天一起做些什么？\r\n\r\n"};
            a.StartConversation("今天一起做些什么？");a.AppendOutput("道友好。先把今天最重要的一件事告诉我，我们一起慢慢理清。🍵\n\n我可以陪你规划日程，也能帮你检查项目、整理文件。");
            a.AppendUserPrompt("先帮我把项目计划列出来，要留一点休息时间 😊");a.AppendOutput("好，我们把事情分成三步：\n\n1. 明确目标与今天可用的时间。\n2. 拆成两三个能完成的小任务。\n3. 每完成一段，留几分钟休息。\n\n你希望先做哪一项？");
            Check(a.Messages.Count==4&&a.Messages[0].Role=="user"&&a.Messages[1].Role=="assistant","explicit alternating roles without parsing transcript");
            a.AppendOutput("\n引用示例：你：这仍然是道友引用的内容。");Check(a.Messages.Count==4,"quoted user marker never creates a fake turn");
            using(var form=new ActivityTrayForm())
            {
                form.SetActivities(new[]{a});form.ShowNear(new Rectangle(600,20,192,208));Application.DoEvents();form.FlushConversationForTest();Application.DoEvents();
                Check(form.ConversationForTest.MessageCount==4&&form.ConversationForTest.HasBothSides,"visible left/right bubbles");
                Check(form.ConversationForTest.UnicodeSupportedForTest(),"actual visible bubbles use modern Unicode RichEdit");
                var bodies=new List<RichTextBox>();Bodies(form.ConversationForTest,bodies);Check(bodies.Count==4,"one selectable native text control per message");
                bodies[3].Select(0,2);string selected=bodies[3].SelectedText;
                string previousText=bodies[3].Text;a.AppendOutput("\n新的流式内容已到达。✅");form.SetActivities(new[]{a});Check(bodies[3].Text==previousText,"stream refresh is throttled instead of immediate per-token redraw");form.FlushConversationForTest();Check(bodies[3].SelectedText==selected,"stream update preserves text selection");
                Check(form.ConversationForTest.MessageCount==4,"stream delta updates existing bubble instead of adding duplicates");
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height));image.Save(Path.Combine(root,"chat-preview.png"));}
                Check(form.PinnedOpen,"conversation remains open until manual close");form.HideUnlessPinned();Check(form.Visible,"background updates do not hide pinned chat");
                var input=(TextBox)typeof(ActivityTrayForm).GetField("reply",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(form);input.Text="尚未发送的草稿";
                var legacy=new ActivityRecord {Title="外部会话",Output="你：引用文字\n旧的输出没有角色元数据",State="就绪",Updated=DateTime.Now};form.SetActivities(new[]{legacy,a});form.SelectActivity(legacy);form.FlushConversationForTest();Check(form.ConversationForTest.MessageCount==1,"external legacy output preserved without guessing roles");
                form.SelectActivity(a);form.FlushConversationForTest();Check(form.ConversationForTest.MessageCount==4,"switching conversation restores its message bubbles");Check(input.Text=="尚未发送的草稿","switching sessions preserves unsent drafts");
                a.AppendUserPrompt("长消息测试");a.AppendOutput(new String('测',18000)+"😀");form.SetActivities(new[]{a});form.FlushConversationForTest();Check(form.ConversationForTest.MessageCount==6,"long response can scroll without losing message roles");
                form.DismissForTest();Check(!form.Visible&&!form.PinnedOpen,"manual close dismisses chat");
            }
            var unicode=new ActivityRecord {Output=""};unicode.StartConversation("👨‍💻");unicode.AppendOutput(new String('字',130000)+"🚀");Check(!UiText.HasUnpairedSurrogate(unicode.Messages[1].Text),"bounded chat history keeps surrogate pairs intact");
            var waiting=new ActivityRecord {State="正在运行",CharacterId="orbit",Updated=DateTime.Now,Output="你：你好"};waiting.StartConversation("你好");
            using(var form=new ActivityTrayForm())
            {
                form.SetActivities(new[]{waiting});form.ShowNear(new Rectangle(600,20,192,208));form.FlushConversationForTest();
                var bodies=new List<RichTextBox>();Bodies(form.ConversationForTest,bodies);Check(bodies.Exists(delegate(RichTextBox box){return box.Text=="正在思考…";})&&waiting.Messages.Count==1,"thinking placeholder is visible but never logged as a model response");
                waiting.AppendOutput("你好，道友。");form.SetActivities(new[]{waiting});form.FlushConversationForTest();Check(form.ConversationForTest.MessageCount==2&&waiting.Messages[1].Text=="你好，道友。","first token replaces pending indicator without a duplicate turn");form.DismissForTest();
            }
            Console.WriteLine("Chat UI tests passed. No Claude/model calls.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
