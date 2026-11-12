namespace PalCalc.Model
{
    /// <summary>
    /// Palworld's two-GUID character key. Global Palbox transfers preserve both GUIDs, so the
    /// same key can appear in multiple saves.
    /// </summary>
    /// <param name="PlayerUId">
    /// The first GUID in the key. Fresh keys take it from the player context when available
    /// and use <see cref="Guid.Empty"/> otherwise. The game tracks current ownership separately.
    /// </param>
    /// <param name="InstanceId">
    /// The second GUID in the key, generated for fresh keys. Characters with different
    /// <paramref name="PlayerUId"/> values can share it.
    /// </param>
    public readonly record struct PalInstanceId(Guid PlayerUId, Guid InstanceId);
}
