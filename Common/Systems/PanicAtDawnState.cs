using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;
using PanicAtDawn.Common.Players;

namespace PanicAtDawn.Common.Systems;

public sealed class PanicAtDawnState : ModSystem
{
    private bool _wasDayTime;
    private bool _dawnHandledThisNight;

    public override void OnWorldLoad()
    {
        _wasDayTime = Main.dayTime;
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

        _wasDayTime = Main.dayTime;
    }

    private static void ApplyDawnRule(PanicAtDawnConfig cfg)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            var p = Main.player[i];
            if (p is null || !p.active || p.dead)
                continue;

            if (p.GetModPlayer<PanicAtDawnPlayer>().DawnGraceTicks > 0)
                continue;

            bool safe = Shelter.IsNearSpawnPoint(p, cfg.SpawnSafeRadiusTiles);

            if (safe)
                continue;

            if (cfg.DropInventoryOnDawnDeath)
                Shelter.DropInventory(p);

            p.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
                NetworkText.FromLiteral($"{p.name} was caught outside at dawn.")), 9999.0, 0);
        }
    }
}
