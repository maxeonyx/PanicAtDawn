using Terraria;
using Terraria.ID;
using Terraria.Localization;
using PanicAtDawn.Common.Players;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;

namespace PanicAtDawn.Common.Systems;

public enum BossHex
{
    None = 0,
    Darkness = 1,
    Weak = 2,
    Slow = 3,
    WingClip = 4,
    Frail = 5,
}

public sealed class PanicAtDawnState : ModSystem
{
    private bool _wasDayTime;
    private bool _wasAnyBossAlive;
    private bool _dawnHandledThisNight;

    public static BossHex CurrentBossHex { get; internal set; } = BossHex.None;

    public override void OnWorldLoad()
    {
        _wasDayTime = Main.dayTime;
        _wasAnyBossAlive = AnyBossAlive();
        _dawnHandledThisNight = false;
        CurrentBossHex = BossHex.None;
    }

    public override void PostUpdateWorld()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();

        if (cfg.EnableDawnShelterRule)
        {
            // "Dawn" in Terraria is dayTime=true and time=0. Some tools (Journey/cheat mods)
            // can set time without toggling dayTime, so we key off Main.time as well.
            bool atDawnTime = Main.dayTime && Main.time < 1.0;

            if (!Main.dayTime)
                _dawnHandledThisNight = false;

            if (atDawnTime && !_dawnHandledThisNight)
            {
                _dawnHandledThisNight = true;
                ApplyDawnRule(cfg);
            }
        }

        if (cfg.EnableBossHex)
        {
            bool anyBossAlive = AnyBossAlive();
            if (_wasAnyBossAlive && !anyBossAlive)
            {
                CurrentBossHex = BossHex.None;
            }
            _wasAnyBossAlive = anyBossAlive;
        }

        _wasDayTime = Main.dayTime;
    }

    private static bool AnyBossAlive()
    {
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            var n = Main.npc[i];
            if (n.active && n.boss)
                return true;
        }
        return false;
    }

    private static void ApplyDawnRule(PanicAtDawnConfig cfg)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return; // server (or singleplayer) applies punishments

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

            p.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral($"{p.name} was caught outside at dawn.")), 9999.0, 0);
        }
    }
}
