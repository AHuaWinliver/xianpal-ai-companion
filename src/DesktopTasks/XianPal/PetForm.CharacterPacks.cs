// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed partial class PetForm
    {
        private CharacterPackCatalog characterPackCatalog;
        private FileSystemWatcher characterPackWatcher;
        private readonly System.Windows.Forms.Timer characterPackTimer = new System.Windows.Forms.Timer();
        private CharacterPackScan pendingCharacterScan;
        private bool pendingCharacterReport, characterManualRefresh;
        private int characterPackDirty, characterScanBusy;
        private long characterPackChangedAt;
        private long characterPackGeneration;
        private string[] characterPackErrors = new string[0];
        private readonly object characterPackDeliveryGate = new object();
        private CharacterPackScan deliveringCharacterScan;
        private volatile bool characterPackWatchingDisposed;

        internal static string CharacterPackDirectory
        {
            get
            {
                // Existing isolated tests must never discover or write the real user's packs.
                if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") == "1") return Path.Combine(Program.StateDirectory, "characters");
                string configured = Environment.GetEnvironmentVariable("XIANPAL_CHARACTER_DIR");
                return String.IsNullOrWhiteSpace(configured) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XianPal", "Characters") : Path.GetFullPath(configured);
            }
        }

        private void InitializeCharacterPacks()
        {
            Disposed += delegate { DisposeCharacterPackWatching(); };
            PetCharacter.SetExternalProfiles(null);
            try
            {
                characterPackCatalog = new CharacterPackCatalog(CharacterPackDirectory);
                using (CharacterPackScan scan = characterPackCatalog.Scan(null, null))
                { PetCharacter.SetExternalProfiles(scan.Characters); characterPackErrors = scan.Errors; }
            }
            catch (Exception ex) { characterPackErrors = new[] { ex.Message }; }
        }

        private void StartCharacterPackWatching()
        {
            characterPackTimer.Interval = 1000;
            characterPackTimer.Tick += CharacterPackTimerTick;
            try { CreateCharacterWatcher(); }
            catch (Exception ex) { characterPackErrors = new[] { ex.Message }; }
            characterPackTimer.Start();
        }

        private void CreateCharacterWatcher()
        {
            if (characterPackWatcher != null) { characterPackWatcher.Dispose(); characterPackWatcher = null; }
            if (characterPackCatalog == null) characterPackCatalog = new CharacterPackCatalog(CharacterPackDirectory);
            Directory.CreateDirectory(characterPackCatalog.Root);
            characterPackWatcher = new FileSystemWatcher(characterPackCatalog.Root);
            characterPackWatcher.IncludeSubdirectories = true;
            characterPackWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size;
            characterPackWatcher.Changed += CharacterPackChanged;
            characterPackWatcher.Created += CharacterPackChanged;
            characterPackWatcher.Deleted += CharacterPackChanged;
            characterPackWatcher.Renamed += CharacterPackChanged;
            characterPackWatcher.Error += delegate { RequestCharacterPackRefresh(false); };
            characterPackWatcher.EnableRaisingEvents = true;
        }

        private void CharacterPackChanged(object sender, FileSystemEventArgs e)
        {
            Interlocked.Exchange(ref characterPackChangedAt, DateTime.UtcNow.Ticks);
            Interlocked.Increment(ref characterPackGeneration);
            Interlocked.Exchange(ref characterPackDirty, 1);
        }

        private void RequestCharacterPackRefresh(bool report)
        {
            if (InvokeRequired) { RunOnUi(delegate { RequestCharacterPackRefresh(report); }); return; }
            characterManualRefresh |= report;
            Interlocked.Exchange(ref characterPackChangedAt, 0);
            Interlocked.Increment(ref characterPackGeneration);
            Interlocked.Exchange(ref characterPackDirty, 1);
        }

        private bool CanApplyCharacterPackUpdate()
        {
            return !dragging && reminderOverlay == null && fileEatOverlay == null && !fileWipeTimer.Enabled &&
                (ContextMenuStrip == null || !ContextMenuStrip.Visible) && !clickActionsMenu.Visible;
        }

        private void CharacterPackTimerTick(object sender, EventArgs e)
        {
            if (closingPet || IsDisposed) return;
            if (pendingCharacterScan != null)
            {
                if (!CanApplyCharacterPackUpdate()) return;
                CharacterPackScan ready = pendingCharacterScan; bool report = pendingCharacterReport;
                pendingCharacterScan = null; pendingCharacterReport = false;
                try { ApplyCharacterPackScan(ready, report); } finally { ready.Dispose(); }
            }
            if (closingPet || IsDisposed || characterPackWatchingDisposed) return;
            if (Interlocked.CompareExchange(ref characterPackDirty, 0, 0) == 0 || characterScanBusy != 0 || !IsHandleCreated) return;
            long generation = Interlocked.Read(ref characterPackGeneration);
            long changed = Interlocked.Read(ref characterPackChangedAt);
            if (changed != 0 && DateTime.UtcNow.Ticks - changed < TimeSpan.FromMilliseconds(1500).Ticks) return;
            try { if (characterPackCatalog == null || characterPackWatcher == null || !Directory.Exists(characterPackCatalog.Root)) CreateCharacterWatcher(); }
            catch (Exception ex)
            {
                characterPackErrors = new[] { ex.Message }; Interlocked.Exchange(ref characterPackDirty, 0);
                if (characterManualRefresh) { characterManualRefresh = false; ShowCharacterPackReport(); } return;
            }
            Interlocked.Exchange(ref characterPackDirty, 0);
            if (Interlocked.Read(ref characterPackGeneration) != generation) { Interlocked.Exchange(ref characterPackDirty, 1); return; }
            bool showReport = characterManualRefresh; characterManualRefresh = false;
            Interlocked.Exchange(ref characterScanBusy, 1);
            string activeId = currentCharacterId, revision = loadedCharacterRevision;
            ThreadPool.QueueUserWorkItem(delegate
            {
                CharacterPackScan scan = null;
                try
                {
                    scan = characterPackCatalog.Scan(activeId, revision);
                    CharacterPackScan completed = scan;
                    lock (characterPackDeliveryGate)
                    {
                        if (characterPackWatchingDisposed || closingPet || IsDisposed || !IsHandleCreated) { scan.Dispose(); Interlocked.Exchange(ref characterScanBusy, 0); return; }
                        deliveringCharacterScan = scan;
                        BeginInvoke((MethodInvoker)delegate
                        {
                            lock (characterPackDeliveryGate)
                            {
                                if (!Object.ReferenceEquals(deliveringCharacterScan, completed)) return;
                                deliveringCharacterScan = null;
                            }
                            Interlocked.Exchange(ref characterScanBusy, 0);
                            if (closingPet || IsDisposed) { completed.Dispose(); return; }
                            if (pendingCharacterScan != null) pendingCharacterScan.Dispose();
                            pendingCharacterScan = completed; pendingCharacterReport = showReport;
                        });
                    }
                }
                catch (Exception)
                {
                    lock (characterPackDeliveryGate) { if (Object.ReferenceEquals(deliveringCharacterScan, scan)) deliveringCharacterScan = null; }
                    if (scan != null) scan.Dispose(); Interlocked.Exchange(ref characterScanBusy, 0);
                }
            });
        }

        private void ApplyCharacterPackScan(CharacterPackScan scan, bool report)
        {
            PetCharacter old = PetCharacter.Find(currentCharacterId);
            PetCharacter.SetExternalProfiles(scan.Characters);
            var errors = new List<string>(scan.Errors);
            PetCharacter current = PetCharacter.Find(currentCharacterId);
            if (old.IsExternal && current.Id != currentCharacterId) SetCharacter("orbit", true);
            else if (current.IsExternal && loadedCharacterRevision != current.ResourceRevision)
            {
                try
                {
                    CharacterAnimationSet prepared = scan.PreparedId == current.Id ? scan.PreparedAssets : null;
                    if (prepared != null) scan.PreparedAssets = null;
                    string previousState = state; int previousFrame = frameIndex;
                    LoadCharacterAnimations(current.Id, prepared);
                    // Resource refresh is not a new user interaction: keep the
                    // current route, task reaction, gesture deadline and gait phase.
                    state = animations.ContainsKey(previousState) ? previousState : "idle";
                    frameIndex = previousFrame % animations[state].Count;
                    Text = current.Name + " · " + Program.ProductName;
                    RefreshTrayIcon(); RefreshPetVisual();
                }
                catch (Exception ex)
                {
                    var restored = new List<PetCharacter>(scan.Characters);
                    for (int i = 0; i < restored.Count; i++) if (restored[i].Id == old.Id) restored[i] = old;
                    PetCharacter.SetExternalProfiles(restored.ToArray());
                    errors.Add(current.Id + "：更新未应用，保留当前已加载形象。" + ex.Message);
                }
            }
            characterPackErrors = errors.ToArray(); UpdateCharacterChecks();
            if (report) ShowCharacterPackReport();
        }

        private void ShowCharacterPackReport()
        {
            int count = PetCharacter.Available.Length - PetCharacter.All.Length;
            string message = "已发现 " + count + " 个外置角色。\n目录：" + CharacterPackDirectory;
            if (characterPackErrors.Length == 0) message += "\n校验通过。请在“切换形象”中选择新角色。";
            else message += "\n\n以下资源未能应用（当前已加载形象会保留）：\n" + String.Join("\n", characterPackErrors);
            MessageBox.Show(message, "外置角色资源", MessageBoxButtons.OK, characterPackErrors.Length == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void OpenCharacterPackDirectory()
        {
            try
            {
                Directory.CreateDirectory(CharacterPackDirectory);
                Process.Start(new ProcessStartInfo(CharacterPackDirectory) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void DisposeCharacterPackWatching()
        {
            if (characterPackWatchingDisposed) return;
            characterPackWatchingDisposed = true;
            characterPackTimer.Stop(); characterPackTimer.Dispose();
            if (characterPackWatcher != null) characterPackWatcher.Dispose();
            lock (characterPackDeliveryGate) { if (deliveringCharacterScan != null) { deliveringCharacterScan.Dispose(); deliveringCharacterScan = null; } }
            if (pendingCharacterScan != null) { pendingCharacterScan.Dispose(); pendingCharacterScan = null; }
        }
    }
}
