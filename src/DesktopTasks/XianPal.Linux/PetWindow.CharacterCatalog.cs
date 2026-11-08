// SPDX-License-Identifier: Apache-2.0
namespace XianPal.Linux;

internal sealed partial class PetWindow
{
    private Task<CharacterPackScan> characterScan;
    private CharacterPackScan pendingCharacterScan;
    private string scanningCharacter, scanningRevision;
    private string[] characterScanErrors = Array.Empty<string>();

    private void InitializeCharacterCatalog()
    {
        using var scan = catalog.Scan(preferences.Character, null);
        PetCharacter.SetExternalProfiles(scan.Characters);
        characterScanErrors = scan.Errors;
        Character = PetCharacter.Find(preferences.Character);
        if (scan.PreparedId == Character.Id && scan.PreparedAssets != null)
        {
            externalFrames = scan.PreparedAssets; scan.PreparedAssets = null;
            ResetCharacterBehavior(); RenderFrame("idle", 0);
        }
        else LoadCharacter(Character);
    }

    // The catalog validates checksums and decodes full packs. Keep this work off
    // the render thread; one worker and one prepared pack bound memory usage.
    private void Rescan()
    {
        if (IsClosing || characterScan != null || pendingCharacterScan != null) return;
        scanningCharacter = Character.Id; scanningRevision = Character.ResourceRevision;
        string id = scanningCharacter, revision = scanningRevision;
        characterScan = Task.Run(() => catalog.Scan(id, revision));
    }

    private void ConsumeCharacterScan()
    {
        if (characterScan?.IsCompleted == true)
        {
            var completed = characterScan; characterScan = null;
            try { pendingCharacterScan = completed.GetAwaiter().GetResult(); }
            catch (Exception error) { characterScanErrors = new[] { error.Message }; }
        }
        if (pendingCharacterScan == null || dragging || ContextMenu?.IsOpen == true || ChatWorking || BrainWorking ||
            urgentRoute || flight.Active || roamTarget != null || clock.ElapsedMilliseconds < gestureEnd) return;
        using var scan = pendingCharacterScan; pendingCharacterScan = null;
        characterScanErrors = scan.Errors;
        PetCharacter.SetExternalProfiles(scan.Characters);
        if (Character.IsExternal && !scan.Characters.Any(profile => profile.Id == Character.Id))
        {
            LoadCharacter(PetCharacter.All[0]); Save(); return;
        }
        // A role may have been switched while validation was running. Do not
        // replace its newer assets with a stale worker's decoded frame set.
        if (Character.Id != scanningCharacter || Character.ResourceRevision != scanningRevision ||
            scan.PreparedId != Character.Id || scan.PreparedAssets == null) return;
        sprite.Source = null; artifact.Clear();
        externalFrames?.Dispose(); externalFrames = scan.PreparedAssets; scan.PreparedAssets = null;
        Character = PetCharacter.Find(scan.PreparedId);
        ResetCharacterBehavior(); RenderFrame("idle", 0);
    }

    private void DisposeCharacterScans()
    {
        pendingCharacterScan?.Dispose(); pendingCharacterScan = null;
        var running = characterScan; characterScan = null;
        if (running == null) return;
        _ = running.ContinueWith(task =>
        {
            if (task.IsCompletedSuccessfully) task.Result.Dispose();
            else if (task.IsFaulted) _ = task.Exception;
        }, TaskScheduler.Default);
    }
}
