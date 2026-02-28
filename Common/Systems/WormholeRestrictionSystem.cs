using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;

namespace PanicAtDawn.Common.Systems;

/// <summary>
/// Prevents wormhole potion teleportation unless the player is at full health.
/// Detours HasUnityPotion so the game never starts the consume+teleport flow.
/// Also detours UnityTeleport as a safety net (e.g. other mods calling it directly).
/// </summary>
public sealed class WormholeRestrictionSystem : ModSystem
{
    private static int _denyTextCooldown;

    public override void Load()
    {
        Terraria.On_Player.HasUnityPotion += OnHasUnityPotion;
        Terraria.On_Player.UnityTeleport += OnUnityTeleport;
    }

    public override void PostUpdateEverything()
    {
        if (_denyTextCooldown > 0)
            _denyTextCooldown--;
    }

    private static bool ShouldBlock(Player self)
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        return cfg.RequireFullHealthForWormhole
            && self.whoAmI == Main.myPlayer
            && self.statLife < self.statLifeMax2;
    }

    private static bool OnHasUnityPotion(Terraria.On_Player.orig_HasUnityPotion orig, Player self)
    {
        if (ShouldBlock(self))
        {
            if (_denyTextCooldown <= 0)
            {
                Main.NewText("You must be at full health to use a Wormhole Potion.", Color.Yellow);
                _denyTextCooldown = 90;
            }
            return false;
        }

        return orig(self);
    }

    private static void OnUnityTeleport(Terraria.On_Player.orig_UnityTeleport orig, Player self, Vector2 telePos)
    {
        if (ShouldBlock(self))
            return;

        orig(self, telePos);
    }
}
