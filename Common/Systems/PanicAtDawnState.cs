using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;
using PanicAtDawn.Common.Players;

namespace PanicAtDawn.Common.Systems;

public sealed class PanicAtDawnState : ModSystem
{
    private bool _wasDayTime;
    private bool _wasAnyBossAlive;
    private bool _dawnHandledThisNight;
    private int _lastBossType = -1;

    public override void OnWorldLoad()
    {
        _wasDayTime = Main.dayTime;
        _wasAnyBossAlive = AnyBossAlive();
        _dawnHandledThisNight = false;
        _lastBossType = -1;
        BossHexManager.OnWorldLoad();
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

        if (cfg.EnableBossHex)
        {
            UpdateBossHexes();
        }

        _wasDayTime = Main.dayTime;
    }

    private void UpdateBossHexes()
    {
        bool anyBossAlive = AnyBossAlive(out int bossType);
        
        if (anyBossAlive && !_wasAnyBossAlive)
        {
            // Boss just spawned - this is handled by GlobalNPC.OnSpawn
            _lastBossType = bossType;
        }
        else if (_wasAnyBossAlive && !anyBossAlive)
        {
            // All bosses gone - could be defeat (handled by OnKill) or despawn/player death
            // Don't clear hexes here - OnKill handles actual defeats
            // Just clear the current fight state so next spawn re-rolls if needed
            _lastBossType = -1;
        }

        // Update active hex effects
        if (anyBossAlive && BossHexManager.Current.HasAnyHex)
        {
            UpdateActiveHexEffects();
        }

        _wasAnyBossAlive = anyBossAlive;
    }

    private void UpdateActiveHexEffects()
    {
        var hexes = BossHexManager.Current;

        // Time Limit
        if (hexes.Flashy == FlashyHex.TimeLimit)
        {
            hexes.TimeLimitTicks--;
            
            // Announce at certain thresholds
            if (hexes.TimeLimitTicks == 60 * 60 * 2) // 2 minutes
                AnnounceTimeLeft("2 minutes remaining!");
            else if (hexes.TimeLimitTicks == 60 * 60) // 1 minute
                AnnounceTimeLeft("1 minute remaining!");
            else if (hexes.TimeLimitTicks == 60 * 30) // 30 seconds
                AnnounceTimeLeft("30 seconds remaining!", Color.Orange);
            else if (hexes.TimeLimitTicks == 60 * 10) // 10 seconds
                AnnounceTimeLeft("10 seconds!", Color.Red);
            
            if (hexes.TimeLimitTicks <= 0)
            {
                // Time's up - kill everyone
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int i = 0; i < Main.maxPlayers; i++)
                    {
                        var p = Main.player[i];
                        if (p?.active == true && !p.dead)
                        {
                            p.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
                                NetworkText.FromLiteral($"{p.name} ran out of time.")), 9999.0, 0);
                        }
                    }
                }
            }
        }

        // Unstable Gravity
        if (hexes.Flashy == FlashyHex.UnstableGravity)
        {
            hexes.GravityFlipTicks++;
            // Flip every 5-10 seconds randomly
            if (hexes.GravityFlipTicks >= 60 * 5 && Main.rand.NextBool(60 * 5))
            {
                hexes.GravityFlipTicks = 0;
                // Flip all players' gravity
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    var p = Main.player[i];
                    if (p?.active == true && !p.dead)
                    {
                        p.gravDir *= -1;
                    }
                }
                
                if (Main.netMode == NetmodeID.Server)
                    ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral("Gravity shifts!"), Color.Purple);
                else if (Main.netMode != NetmodeID.MultiplayerClient)
                    Main.NewText("Gravity shifts!", Color.Purple);
            }
        }

        // Meteor Shower
        if (hexes.Flashy == FlashyHex.MeteorShower)
        {
            hexes.MeteorTicks++;
            // Spawn falling stars periodically (every 0.5-1.5 seconds)
            if (hexes.MeteorTicks >= 30 && Main.rand.NextBool(60))
            {
                hexes.MeteorTicks = 0;
                SpawnMeteor();
            }
        }
    }

    private void SpawnMeteor()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        // Spawn near a random active player
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int playerIdx = Main.rand.Next(Main.maxPlayers);
            var p = Main.player[playerIdx];
            if (p?.active != true || p.dead)
                continue;

            // Random X offset from player
            float x = p.Center.X + Main.rand.Next(-600, 600);
            float y = p.Center.Y - 800; // Above screen

            // Spawn a falling star projectile that damages players
            int projType = ProjectileID.FallingStar;
            Vector2 velocity = new Vector2(Main.rand.Next(-2, 3), Main.rand.Next(10, 15));
            
            Projectile.NewProjectile(
                Terraria.Entity.GetSource_NaturalSpawn(),
                x, y,
                velocity.X, velocity.Y,
                projType,
                50, // Damage
                2f,
                Main.myPlayer,
                0f, 0f);
            
            break;
        }
    }

    private static void AnnounceTimeLeft(string message, Color? color = null)
    {
        Color c = color ?? Color.Yellow;
        if (Main.netMode == NetmodeID.Server)
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(message), c);
        else if (Main.netMode != NetmodeID.MultiplayerClient)
            Main.NewText(message, c);
    }

    private static bool AnyBossAlive() => AnyBossAlive(out _);

    private static bool AnyBossAlive(out int bossType)
    {
        bossType = -1;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            var n = Main.npc[i];
            if (n.active && n.boss)
            {
                bossType = n.type;
                return true;
            }
        }
        return false;
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
