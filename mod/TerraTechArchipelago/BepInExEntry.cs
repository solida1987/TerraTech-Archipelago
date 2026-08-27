using BepInEx;

namespace TerraTechArchipelago
{
    // The BepInEx doorway. Vanilla TerraTech loads no outside code at all —
    // its QMods folder is a convention of a mod manager the player may not
    // have, and the official ManMods pipeline only loads Workshop bundles.
    // BepInEx is the one loader that needs nothing from the game, and its
    // chainloader calls Awake on Unity's main thread after every game
    // assembly is in place, which is exactly what Reflect.Resolve needs.
    //
    // BaseUnityPlugin is a MonoBehaviour, so this class is also the frame
    // pump. That matters more than it looks: the mod dials the launcher from
    // Tick() and drains its inbox in Update(), so a loader that only calls
    // Init leaves a mod that patches correctly, reports itself healthy, and
    // then sits there forever without ever connecting to anything.
    [BepInPlugin("dk.solida.terratech.archipelago", "TerraTech Archipelago", Plugin.ModVersion)]
    public sealed class BepInExEntry : BaseUnityPlugin
    {
        private void Awake() => Plugin.Init();

        private void Update() => ModEntry.Update();
    }
}
