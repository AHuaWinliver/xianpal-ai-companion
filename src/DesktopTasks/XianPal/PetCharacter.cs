// SPDX-License-Identifier: Apache-2.0
// Original cultivation companions. Stable IDs retain existing settings compatibility.
using System;
using System.Collections.Generic;
namespace XianPal
{
    internal sealed class PetCharacter
    {
        internal string Id, Name, ReminderStyle, Persona;
        internal float MouthX, MouthY;
        internal string[] Quotes;
        internal string ResourceRoot = null, ResourceRevision = null;
        internal bool IsExternal { get { return !String.IsNullOrEmpty(ResourceRoot); } }
        private static PetCharacter[] external = new PetCharacter[0];
        internal static PetCharacter[] Available
        {
            get { var result = new List<PetCharacter>(All); result.AddRange(external); return result.ToArray(); }
        }
        internal static void SetExternalProfiles(PetCharacter[] profiles)
        { external = profiles == null ? new PetCharacter[0] : (PetCharacter[])profiles.Clone(); }
        internal static readonly PetCharacter[] All =
        {
            Create("orbit", "青衡", "command", "温柔可靠的青年游修，深青衣袍、半束黑发，腰间携一枚寻路罗盘。以道友称呼用户，语气自然克制，偶用修行、行路比喻做事，但不堆砌古文。既能轻松陪聊，也能直接清楚地协助任务。",
                "道友，今日这一程，我们稳稳地开始。", "修行不必急，眼前这一小步也算数。", "我替你看着时辰，你安心做事。", "把难题拆开，便有了落脚之处。", "窗外有风，也记得歇歇眼睛。"),
            Create("spark", "绯笺", "beam", "明快细心的符箓手艺少女，杏桃色衣裙、棕色双髻，爱画小符、整理笔记。称呼用户道友，发言活泼有分寸，不撒娇索取关注，不假装真实恋人。把清晰计划比作画符前的起笔，专业任务优先给准确答案。",
                "今日计划写好啦，一笔一画慢慢来！", "开工之前，先给好心情留个位置。", "这个点子不错，我先记在小笺上。", "符纸能重画，事情也可以重新开始。", "休息一下？我的小茶盏还温着呢。"),
            Create("moss", "苍芷", "warning", "安静亲切的草木药师，银灰束发、苔绿色衣袍，随身带草药小袋。喜欢以草木四时解释节奏，提醒饮水、活动和休息，但不诊断疾病、不推荐药方，也不把奇幻修行当现实医学。回答技术问题准确直接。",
                "给思绪一点舒展的时间，也记得休息。", "草木各有时节，不必催自己太急。", "先喝口水，我们再理清这件事。", "慢慢来，清楚比匆忙更重要。", "这一页做完，起来活动片刻可好？"),
            Create("pippin", "小墨", "stone", "圆滚滚的墨黑灵龟，炭灰色皮肤、淡金云纹龟壳，琥珀色眼睛。温暖憨趣，用简短中文完整交流，偶尔说慢慢来、稳稳当当，不只发动物拟声。称呼用户道友，能理解并协助任务，不能声称拥有现实超能力。",
                "小墨到位，今天也稳稳当当。", "慢一点没关系，我陪你走完。", "别急别急，龟壳借你靠一小会儿。", "这件事，咱们一口一口慢慢消化。", "道友，该伸伸脖子、松松肩膀啦。")
        };
        private static PetCharacter Create(string id, string name, string style, string persona, params string[] quotes)
        { return new PetCharacter { Id=id, Name=name, ReminderStyle=style, Persona=persona, Quotes=quotes,
            MouthX=id=="pippin"?.26f:id=="orbit"?.56f:id=="moss"?.47f:.5f,
            MouthY=id=="pippin"?.69f:.44f }; }
        internal static PetCharacter Find(string id)
        {
            foreach (PetCharacter p in All) if (String.Equals(p.Id,id,StringComparison.OrdinalIgnoreCase)) return p;
            foreach (PetCharacter p in external) if (String.Equals(p.Id,id,StringComparison.OrdinalIgnoreCase)) return p;
            // Retired original geometric characters migrate to a matching archetype.
            if (id=="lumen" || id=="nova") return All[3];
            if (id=="ribbon" || id=="coco") return All[1];
            if (id=="dawn" || id=="pebble") return All[2];
            return All[0];
        }
        internal static bool ValidateProfiles()
        {
            HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
            foreach (PetCharacter p in All) if (!ids.Add(p.Id) || String.IsNullOrWhiteSpace(p.Persona) || p.Quotes.Length<5 || p.MouthX<=0 || p.MouthX>=1 || p.MouthY<=0 || p.MouthY>=1) return false;
            return All.Length==4 && All[0].Id=="orbit" && Find("nova").Id=="pippin";
        }
    }
}
