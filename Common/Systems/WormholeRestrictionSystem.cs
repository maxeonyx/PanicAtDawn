using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;

namespace PanicAtDawn.Common.Systems;

/// <summary>
/// Prevents wormhole potion teleportation unless the player is at full health.
/// Uses a MonoMod detour on Player.UnityTeleport since no tModLoader hook covers
/// wormhole map-click teleportation.
/// </summary>
public sealed class WormholeRestrictionSystem : ModSystem
{
    private static int _denyTextCooldown;

    public override void Load()
    {
        Terraria.On_Player.UnityTeleport += OnUnityTeleport;
    }

    public override void PostUpdateEverything()
    {
        if (_denyTextCooldown > 0)
            _denyTextCooldown--;
    }

    private static void OnUnityTeleport(Terraria.On_Player.orig_UnityTeleport orig, Player self, Vector2 telePos)
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();

        if (cfg.RequireFullHealthForWormhole
            && self.whoAmI == Main.myPlayer
            && self.statLife < self.statLifeMax2)
        {
            if (_denyTextCooldown <= 0)
            {
                Main.NewText("You must be at full health to use a Wormhole Potion.", Color.Yellow);
                _denyTextCooldown = 90;
            }
            return;
        }

        orig(self, telePos);
    }
}
