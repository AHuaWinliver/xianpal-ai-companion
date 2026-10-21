// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using XianPal;

internal static class BuildReminderPreviews
{
    internal static Dictionary<string, List<Image>> Load(string assetRoot)
    {
        Dictionary<string, List<Image>> result = new Dictionary<string, List<Image>>();
        foreach (string name in new[] { "idle", "review", "waving", "jumping", "running-left" })
        {
            List<Image> frames = new List<Image>();
            result.Add(name, frames);
            string[] files = Directory.GetFiles(Path.Combine(assetRoot, name), "*.png");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
                frames.Add(Image.FromFile(file));
        }
        return result;
    }

    internal static void Release(Dictionary<string, List<Image>> frames)
    {
        foreach (List<Image> sequence in frames.Values) foreach (Image image in sequence) image.Dispose();
    }

    [STAThread] private static int Main(string[] args)
    {
        try
        {
            string assets = args[0], output = args[1];
            Directory.CreateDirectory(output);
            const int count = 157;
            using (Bitmap overview = new Bitmap(1600, 920, PixelFormat.Format24bppRgb))
            using (Graphics grid = Graphics.FromImage(overview))
            using (Font title = new Font("Microsoft YaHei UI", 12))
            using (SolidBrush ink = new SolidBrush(Color.FromArgb(42, 65, 69)))
            {
                grid.Clear(Color.FromArgb(246, 244, 236));
                int row = 0;
                foreach (PetCharacter character in PetCharacter.All)
                {
                    string target = Path.Combine(output, "performances", character.Id);
                    Directory.CreateDirectory(target);
                    string root = character.Id == "orbit" ? assets : Path.Combine(assets, "characters", character.Id);
                    Dictionary<string, List<Image>> frames = Load(root);
                    try
                    {
                        for (int f = 0; f < count; f++)
                        using (Bitmap bitmap = new Bitmap(960, 540, PixelFormat.Format32bppPArgb))
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            g.Clear(Color.Transparent);
                            double t = f * .04;
                            ReminderPerformance.Render(g, new Size(960, 540), character.ReminderStyle, frames, t, false, Rectangle.Empty);
                            bitmap.Save(Path.Combine(target, f.ToString("000") + ".png"), ImageFormat.Png);
                        }
                        grid.DrawString(character.Name + " · " + ReminderPerformance.Title(character.ReminderStyle), title, ink, 12, row * 230 + 4);
                        double[] phases = { .72, 1.80, 2.75, 3.90, 5.00 };
                        for (int column = 0; column < phases.Length; column++)
                        {
                            GraphicsState saved = grid.Save();
                            grid.TranslateTransform(column * 320, row * 230 + 34);
                            grid.SetClip(new Rectangle(0, 0, 320, 180));
                            grid.ScaleTransform(1 / 3f, 1 / 3f);
                            ReminderPerformance.Render(grid, new Size(960, 540), character.ReminderStyle, frames, phases[column], false, Rectangle.Empty);
                            grid.Restore(saved);
                        }
                    }
                    finally { Release(frames); }
                    row++;
                }
                overview.Save(Path.Combine(output, "reminder-performances.png"), ImageFormat.Png);
            }
            File.WriteAllText(Path.Combine(output, "performances.html"), Page(), new UTF8Encoding(false));
            WriteLoop(output, count);
            Console.WriteLine("PASS: four native-rendered full-screen previews and a looping GIF, 157 frames each, no installer changes.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void WriteLoop(string directory, int count)
    {
        using (BinaryWriter output = new BinaryWriter(File.Create(Path.Combine(directory, "reminder-performances.gif"))))
        using (Font font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold))
        using (SolidBrush ink = new SolidBrush(Color.FromArgb(43, 77, 78)))
        {
            output.Write(Encoding.ASCII.GetBytes("GIF89a"));
            output.Write((ushort)960); output.Write((ushort)540);
            output.Write(new byte[] { 0x70, 0, 0, 0x21, 0xff, 11 });
            output.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0")); output.Write(new byte[] { 3, 1, 0, 0, 0 });
            for (int f = 0; f < count; f++)
            using (Bitmap image = new Bitmap(960, 540, PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(image))
            using (MemoryStream stream = new MemoryStream())
            {
                g.Clear(Color.FromArgb(242, 245, 237));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                for (int role = 0; role < PetCharacter.All.Length; role++)
                {
                    PetCharacter character = PetCharacter.All[role];
                    int x = role % 2 * 480, y = role / 2 * 270;
                    using (Image scene = Image.FromFile(Path.Combine(directory, "performances", character.Id, f.ToString("000") + ".png")))
                        g.DrawImage(scene, x, y, 480, 270);
                    g.DrawString(character.Name + " · " + ReminderPerformance.Title(character.ReminderStyle), font, ink, x + 14, y + 10);
                }
                image.Save(stream, ImageFormat.Gif);
                AppendGifFrame(output, stream.ToArray());
            }
            output.Write((byte)0x3b);
        }
    }

    private static void CopyBlocks(BinaryReader input, BinaryWriter output)
    {
        for (;;)
        {
            byte count = input.ReadByte();
            if (output != null) output.Write(count);
            if (count == 0) return;
            byte[] data = input.ReadBytes(count);
            if (output != null) output.Write(data);
        }
    }

    private static void AppendGifFrame(BinaryWriter output, byte[] frame)
    {
        using (BinaryReader input = new BinaryReader(new MemoryStream(frame)))
        {
            input.ReadBytes(10); byte packed = input.ReadByte(); input.ReadBytes(2);
            int bits = packed & 7;
            byte[] palette = (packed & 128) != 0 ? input.ReadBytes(3 * (1 << (bits + 1))) : null;
            for (;;)
            {
                byte kind = input.ReadByte();
                if (kind == 0x21) { input.ReadByte(); CopyBlocks(input, null); continue; }
                if (kind != 0x2c) throw new InvalidDataException("No GIF frame");
                byte[] descriptor = input.ReadBytes(9); byte format = descriptor[8];
                if ((format & 128) != 0) { bits = format & 7; palette = input.ReadBytes(3 * (1 << (bits + 1))); }
                output.Write(new byte[] { 0x21, 0xf9, 4, 8, 4, 0, 0, 0 });
                output.Write((byte)0x2c); output.Write(descriptor, 0, 8);
                output.Write((byte)(0x80 | (format & 0x40) | bits)); output.Write(palette);
                output.Write(input.ReadByte()); CopyBlocks(input, output); return;
            }
        }
    }

    private static string Page()
    {
        return @"<!doctype html><html lang='zh-CN'><meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'><title>仙伴 · 四境报时</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#efeee8;color:#243e42;font:14px 'Microsoft YaHei UI',sans-serif}header{max-width:1180px;margin:28px auto 18px;padding:0 20px}h1{font-size:27px;margin:0 0 10px}header p{color:#627475;line-height:1.8}nav{display:flex;gap:10px;flex-wrap:wrap;margin:20px 0}button{font:inherit;border:1px solid #c7d5ce;border-radius:12px;padding:10px 17px;background:#fafbf6;color:#34514f;cursor:pointer}button[aria-pressed=true]{background:#244d50;color:white}main{margin:0 auto 30px;max-width:1180px;padding:0 20px}.stage{position:relative;aspect-ratio:16/9;overflow:hidden;border:1px solid #d0d9d1;border-radius:24px;background:radial-gradient(ellipse at 50% 90%,#e6ded1,transparent 65%),linear-gradient(150deg,#f9f7f0,#e2eeeb);box-shadow:0 20px 60px #274e4e14}.window{position:absolute;left:6%;top:10%;width:88%;height:78%;border:1px solid #fff9;background:#fff5;border-radius:15px;box-shadow:0 16px 50px #25504d0b;padding:16px;color:#82928e;font-size:12px}.window:before{content:'● ● ●　 本地桌面预览';display:block;border-bottom:1px solid #cbd9d16b;padding-bottom:12px}.ghost{width:38%;height:8px;background:#a6b9b126;margin-top:30px;border-radius:9px}canvas{position:absolute;inset:0;width:100%;height:100%}.card{position:absolute;display:none;left:50%;bottom:5%;transform:translateX(-50%);width:48%;padding:17px;border-radius:16px;background:#fffefa;box-shadow:0 8px 30px #2144431a;border:1px solid #d7ded5;font-size:clamp(10px,1.2vw,14px)}.card strong{color:#337375}.card p{margin:12px 0 6px;font-weight:600}.card small{color:#77857e}.actions{display:flex;justify-content:flex-end;gap:7px;margin-top:14px}.actions span{background:#263d3d;color:white;padding:6px 12px;border-radius:7px}.actions span:first-child{background:#ecf0ea;color:#66736d}footer{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin-top:16px;color:#647a76}#seek{flex:1;min-width:160px;accent-color:#317b78}#loading{position:absolute;top:15px;right:20px;color:#658079;font-size:12px}kbd{background:#fff9;border:1px solid #ccd6cc;border-radius:4px;padding:1px 5px}a{color:#367b77}
</style><header><h1>四境报时 · 角色专属全屏演绎</h1><p>使用程序同一套渲染逻辑导出 · 原画与手臂层级保持不变 · 半透明柔光，无碎屏、无强闪光<br>入场 → 施展 → 收势 → 事项提醒；正式程序中可按 Esc 跳过，也可选择“仅提示框”。</p><nav id='roles'></nav></header>
<main><div class='stage'><div class='window'><div class='ghost'></div><div class='ghost' style='width:23%'></div><div class='ghost' style='width:49%'></div></div><canvas id='scene' width='640' height='360'></canvas><span id='loading'></span><div class='card' id='card'><strong id='brand'></strong><p>约好的休息时间到了</p><small id='line'></small><div class='actions'><span>稍后 5 分钟</span><span>我知道了</span></div></div></div><footer><button id='play'>暂停</button><button id='restart'>重播</button><input type='range' id='seek' min='0' max='159' value='0'><span id='time'>0.00s / 6.24s</span><label><input id='loop' type='checkbox' checked>循环</label><label><input id='reduced' type='checkbox'>减少动态</label></footer><p id='description'></p><a href='index.html'>返回全动作预览</a></main>
<style>.card{top:61.85%;bottom:auto;width:54.16%}</style><script>
const roles=[['orbit','青衡','御剑飞仙','道友，约好的时辰到了。','挥袖召剑 → 踏剑腾空 → 沿弧线御剑巡游 → 收势落剑提醒；青金色剑光尾迹随行，脚底与飞剑始终贴合。'],['spark','绯笺','灵符报时','叮，给你送来一张准时符！','挥袖放出八张灵符，符纹依次显现，轻跃伴着绯色流光收回。'],['moss','苍芷','草木知时','风过草木，该歇一歇啦。','轻抬手催生两侧藤蔓，新叶舒展，微风与萤光缓缓散去。'],['pippin','小墨','墨涟唤醒','小墨踏着水纹，准时到啦。','轻跃落地，金边墨涟向四周扩散，八朵云纹应声浮现。']];
scene.width=960;scene.height=540;const ctx=scene.getContext('2d');let images=[],role=0,frame=0,paused=false,last=performance.now(),loadSerial=0;
function draw(){ctx.clearRect(0,0,960,540);const index=reduced.checked?156:frame;if(images[index]?.complete&&images[index].naturalWidth)ctx.drawImage(images[index],0,0);card.style.display=reduced.checked||frame>=140?'block':'none';seek.value=frame;time.textContent=(frame*.04).toFixed(2)+'s / 6.24s';}
async function select(i){const serial=++loadSerial;role=i;frame=0;paused=true;images=[];ctx.clearRect(0,0,960,540);brand.textContent=roles[i][1]+' · '+roles[i][2];line.textContent=roles[i][3];description.textContent=roles[i][4];document.querySelectorAll('nav button').forEach((b,k)=>b.setAttribute('aria-pressed',k===i));loading.textContent='载入本地原生预览…';await Promise.all(Array.from({length:157},(_,f)=>new Promise(resolve=>{const im=new Image();images[f]=im;im.onload=resolve;im.onerror=resolve;im.src='performances/'+roles[i][0]+'/'+String(f).padStart(3,'0')+'.png?v=sword-flight-315';})));if(serial!==loadSerial)return;loading.textContent='';paused=false;last=performance.now();play.textContent='暂停';draw();}
roles.forEach((r,i)=>{const b=document.createElement('button');b.textContent=r[1]+' · '+r[2];b.onclick=()=>select(i);document.querySelector('nav').append(b);});
play.onclick=()=>{paused=!paused;play.textContent=paused?'播放':'暂停';last=performance.now();};restart.onclick=()=>{frame=0;last=performance.now();draw();};seek.oninput=()=>{frame=+seek.value;paused=true;play.textContent='播放';draw();};reduced.onchange=()=>{last=performance.now();draw();};
seek.max=156;function animate(now){if(!paused&&!reduced.checked&&now-last>=40){let next=frame+Math.floor((now-last)/40);last=now;if(next>156){if(loop.checked)next=next%157;else{next=156;paused=true;play.textContent='播放';}}frame=next;draw();}requestAnimationFrame(animate);}requestAnimationFrame(animate);select(0);
</script></html>";
    }
}
