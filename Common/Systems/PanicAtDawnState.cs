using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;
using PanicAtDawn.Common.Players;

namespace PanicAtDawn.Common.Systems;

public sealed class PanicAtDawnState : ModSystem
{
    private bool _dawnHandledThisNight;

    public override void OnWorldLoad()
    {
        _dawnHandledThisNight = false;
    }

    public override void PostUpdateWorld()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();

        if (cfg.EnableDawnShelterRule)
        {
            bool atDawnTime = Main.dayTime && Main.time < 1.0;

            if (!Main.dayTime)
                _dawnHandledThisNight = false;

            if (atDawnTime && !_dawnHandledThisNight)
            {
                _dawnHandledThisNight = true;
                ApplyDawnRule(cfg);
            }
        }
    }

    private void ApplyDawnRule(PanicAtDawnConfig cfg)
    {
        if (Main.netMode == NetmodeID.Server)
        {
            // Multiplayer: tell all clients to check dawn safety locally
            // (clients know their own spawn point, server may not)
            PanicAtDawn.SendCheckDawn(Mod);
        }
        else if (Main.netMode == NetmodeID.SinglePlayer)
        {
            // Singleplayer: check directly
            Player p = Main.LocalPlayer;
            if (p is null || !p.active || p.dead)
                return;

            if (p.GetModPlayer<PanicAtDawnPlayer>().DawnGraceTicks > 0)
                return;

            bool safe = Shelter.IsNearSpawnPoint(p, cfg.SpawnSafeRadiusTiles);
            if (!safe)
            {
                p.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
                    NetworkText.FromLiteral($"{p.name} was caught outside at dawn.")),
                    9999.0, 0);
            }
        }
    }
}
