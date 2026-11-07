// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed partial class PetForm
    {
        private int dockOpacityPercent = 100;

        private byte PetWindowAlpha
        {
            get { return String.IsNullOrEmpty(dockedEdge) ? (byte)255 : (byte)Math.Round(255 * dockOpacityPercent / 100.0); }
        }

        private ToolStripMenuItem BuildDockOpacityMenu()
        {
            var menu = new ToolStripMenuItem("贴边不透明度");
            for (int percent = 20; percent <= 100; percent += 10)
            {
                int selected = percent;
                var item = new ToolStripMenuItem(percent == 100 ? "100%（完全可见）" : percent + "%");
                item.Tag = percent;
                item.Click += delegate
                {
                    dockOpacityPercent = selected;
                    RefreshPetVisual();
                    SavePreferences();
                };
                menu.DropDownItems.Add(item);
            }
            menu.DropDownOpening += delegate
            {
                foreach (ToolStripMenuItem item in menu.DropDownItems)
                    item.Checked = (int)item.Tag == dockOpacityPercent;
            };
            return menu;
        }

        private ToolStripMenuItem BuildClaudeSettingsMenu()
        {
            var menu = new ToolStripMenuItem("Claude 配置文件");
            var byCharacter = new ToolStripMenuItem("按角色设置");
            menu.DropDownOpening += delegate
            {
                while (byCharacter.DropDownItems.Count > 0)
                {
                    ToolStripItem item = byCharacter.DropDownItems[0];
                    byCharacter.DropDownItems.RemoveAt(0);
                    item.Dispose();
                }
                foreach (PetCharacter character in PetCharacter.Available)
                    byCharacter.DropDownItems.Add(BuildClaudeSettingsScope(character.Name.Replace("&", "&&"), character.Id));
            };
            menu.DropDownItems.Add(BuildClaudeSettingsScope("全局默认", null));
            menu.DropDownItems.Add(byCharacter);
            return menu;
        }

        private ToolStripMenuItem BuildClaudeSettingsScope(string title, string characterId)
        {
            var menu = new ToolStripMenuItem(title);
            var choose = new ToolStripMenuItem("选择 JSON 配置文件…");
            var reset = new ToolStripMenuItem(characterId == null ? "使用 Claude 默认配置" : "沿用全局配置");
            choose.Click += delegate
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = "选择 Claude Code 配置文件";
                    dialog.Filter = "Claude JSON 配置 (*.json)|*.json";
                    dialog.CheckFileExists = true;
                    dialog.RestoreDirectory = true;
                    string saved = integrationPreferences.SettingsPathFor(characterId);
                    if (File.Exists(saved)) dialog.FileName = saved;
                    if (dialog.ShowDialog() == DialogResult.OK) SaveClaudeSettingsPath(characterId, dialog.FileName);
                }
            };
            reset.Click += delegate { SaveClaudeSettingsPath(characterId, ""); };
            menu.DropDownItems.Add(choose);
            menu.DropDownItems.Add(reset);
            menu.DropDownOpening += delegate
            {
                string path = integrationPreferences.SettingsPathFor(characterId);
                reset.Checked = String.IsNullOrEmpty(path);
                choose.Text = reset.Checked ? "选择 JSON 配置文件…" : "更换配置文件（" + Path.GetFileName(path).Replace("&", "&&") + "）…";
                choose.ToolTipText = path ?? "";
            };
            return menu;
        }

        private void SaveClaudeSettingsPath(string characterId, string path)
        {
            string previous = integrationPreferences.SettingsPathFor(characterId);
            try
            {
                integrationPreferences.SetSettingsPath(characterId, ClaudeSettingsFile.Validate(path));
                integrationPreferences.Save();
            }
            catch (Exception ex)
            {
                integrationPreferences.SetSettingsPath(characterId, previous);
                MessageBox.Show(ex.Message, "保存 Claude 配置失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
