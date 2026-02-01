using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;
using PanicAtDawn.Common.Systems;

namespace PanicAtDawn.Common.Players;

public sealed class PanicAtDawnPlayer : ModPlayer
{
    public float Sanity;
    public int DawnGraceTicks;
    public bool IsInSafeZone; // Exposed for UI to show recovery color
    private int _wormholeDripTicks;
    private int _suffocateTick;
    private int _denyUseTextCooldown;

    public override void Initialize()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        Sanity = cfg.SanityMax;
        DawnGraceTicks = 0;
        _wormholeDripTicks = 0;
        _suffocateTick = 0;
        _denyUseTextCooldown = 0;
    }

    public override void OnRespawn()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        Sanity = cfg.SanityMax;
        _suffocateTick = 0;
    }

    public override void OnEnterWorld()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        DawnGraceTicks = Math.Max(0, cfg.DawnJoinGraceSeconds) * 60;
    }

    public override void PostUpdate()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();

        if (DawnGraceTicks > 0)
            DawnGraceTicks--;

        if (_denyUseTextCooldown > 0)
            _denyUseTextCooldown--;

        if (cfg.EnableLinkSanity)
            UpdateLinkSanity(cfg);

        if (cfg.EnableWormholeDrip)
            UpdateWormholeDrip(cfg);

        if (cfg.EnableBossHex)
            ApplyBossHexes();
    }

    public override bool CanUseItem(Item item)
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        
        // Check recall/mirror restrictions
        if (cfg.DisableRecallAndMirrors)
        {
            if (item.type is ItemID.RecallPotion or ItemID.MagicMirror or ItemID.IceMirror or ItemID.CellPhone or ItemID.Shellphone)
            {
                if (Main.myPlayer == Player.whoAmI && _denyUseTextCooldown <= 0)
                {
                    Main.NewText("Recall/mirrors are disabled. Use Wormhole Potions to regroup.");
                    _denyUseTextCooldown = 90;
                }
                return false;
            }
        }

        // Check boss hex restrictions
        if (cfg.EnableBossHex && AnyBossAlive())
        {
            var hexes = BossHexManager.Current;
            
            // NoGrapple constraint - block grappling hooks
            if (hexes.Constraint == ConstraintHex.NoGrapple && IsGrapplingHook(item))
            {
                if (Main.myPlayer == Player.whoAmI && _denyUseTextCooldown <= 0)
                {
                    Main.NewText("Grappling hooks are disabled by the No Grapple hex!", Color.Orange);
                    _denyUseTextCooldown = 60;
                }
                return false;
            }
        }

        return base.CanUseItem(item);
    }

    /// <summary>
    /// Check if an item is a grappling hook by checking if it shoots a grapple projectile.
    /// </summary>
    private static bool IsGrapplingHook(Item item)
    {
        if (item.shoot <= 0)
            return false;
        
        // Check if the projectile has grapple AI (aiStyle 7)
        // We need to check the projectile defaults
        try
        {
            var proj = new Projectile();
            proj.SetDefaults(item.shoot);
            return proj.aiStyle == 7; // Grapple AI
        }
        catch
        {
            // Defensive: if anything goes wrong, don't block the item
            return false;
        }
    }

    /// <summary>
    /// Modify damage dealt to NPCs based on active hexes.
    /// This handles melee weapon hits (not projectiles).
    /// </summary>
    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        if (!target.boss)
            return;

        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.EnableBossHex)
            return;

        var hexes = BossHexManager.Current;
        
        // NoMeleeDamage - melee attacks deal no damage to bosses
        if (hexes.Constraint == ConstraintHex.NoMeleeDamage)
        {
            modifiers.FinalDamage *= 0f;
        }
    }

    /// <summary>
    /// Modify projectile damage dealt to NPCs based on active hexes.
    /// </summary>
    public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
    {
        if (!target.boss)
            return;

        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.EnableBossHex)
            return;

        var hexes = BossHexManager.Current;
        
        // Determine damage class of the projectile
        bool isRanged = proj.DamageType == DamageClass.Ranged || proj.DamageType.CountsAsClass(DamageClass.Ranged);
        bool isMagic = proj.DamageType == DamageClass.Magic || proj.DamageType.CountsAsClass(DamageClass.Magic);
        bool isMelee = proj.DamageType == DamageClass.Melee || proj.DamageType.CountsAsClass(DamageClass.Melee);

        // NoRangedDamage - ranged projectiles deal no damage to bosses
        if (hexes.Constraint == ConstraintHex.NoRangedDamage && isRanged)
        {
            modifiers.FinalDamage *= 0f;
        }

        // NoMagicDamage - magic projectiles deal no damage to bosses
        if (hexes.Constraint == ConstraintHex.NoMagicDamage && isMagic)
        {
            modifiers.FinalDamage *= 0f;
        }

        // NoMeleeDamage - melee projectiles deal no damage to bosses
        if (hexes.Constraint == ConstraintHex.NoMeleeDamage && isMelee)
        {
            modifiers.FinalDamage *= 0f;
        }
    }

    private void UpdateLinkSanity(PanicAtDawnConfig cfg)
    {
        // Check if player is near their spawn point (bed).
        bool nearSpawn = Shelter.IsNearSpawnPoint(Player, cfg.SpawnSafeRadiusTiles);
        IsInSafeZone = nearSpawn;

        if (nearSpawn)
        {
            // Safe near spawn: regen sanity.
            Sanity = Math.Clamp(Sanity + (cfg.SanityRegenPerSecond / 60f), 0f, cfg.SanityMax);
            _suffocateTick = 0;
            return;
        }

        int teammate = FindClosestLinkedTeammate();

        // Calculate drain rate.
        float drainRate = cfg.SanityDrainPerSecond;
        
        // Double drain in darkness.
        if (IsPlayerInDarkness())
            drainRate *= cfg.SanityDarknessDrainMultiplier;
        
        // Quarter drain near sunflowers (Happy! buff).
        if (Player.HasBuff(BuffID.Sunflower))
            drainRate *= 0.25f;

        if (teammate < 0)
        {
            // Singleplayer / no linked teammate: sanity drains continuously.
            Sanity = Math.Clamp(Sanity - (drainRate / 60f), 0f, cfg.SanityMax);

            if (Sanity > 0f)
            {
                _suffocateTick = 0;
                return;
            }

            ApplySuffocation(cfg);
            return;
        }

        var other = Main.player[teammate];
        float dist = Vector2.Distance(Player.Center, other.Center);
        float radiusPx = cfg.LinkRadiusTiles * 16f;

        float delta = 0f;
        if (dist <= radiusPx)
            delta = cfg.SanityRegenPerSecond;
        else
            delta = -drainRate;

        Sanity = Math.Clamp(Sanity + (delta / 60f), 0f, cfg.SanityMax);

        // Shared pool for a duo: snap both to the lower sanity, so separation hurts both.
        // (This stays stable for >2 players as well: it tends toward the group's minimum.)
        var otherMod = other.GetModPlayer<PanicAtDawnPlayer>();
        float shared = Math.Min(Sanity, otherMod.Sanity);
        Sanity = shared;
        otherMod.Sanity = shared;

        if (Sanity > 0f)
        {
            _suffocateTick = 0;
            return;
        }

        ApplySuffocation(cfg);
    }

    private void ApplySuffocation(PanicAtDawnConfig cfg)
    {
        // Apply damage as a steady DOT. Server is authoritative.
        _suffocateTick++;
        if (_suffocateTick < 60)
            return;
        _suffocateTick = 0;

        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (Player.dead)
            return;

        Player.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral($"{Player.name} had a panic attack.")), cfg.SuffocationDamagePerSecond, 0);
    }

    private void UpdateWormholeDrip(PanicAtDawnConfig cfg)
    {
        // Very low, deterministic rate. If you already have enough, it pauses.
        if (cfg.WormholeDripStackCap <= 0)
            return;

        int current = CountItems(ItemID.WormholePotion);
        if (current >= cfg.WormholeDripStackCap)
            return;

        _wormholeDripTicks++;
        int intervalTicks = Math.Max(1, cfg.WormholeDripSeconds) * 60;
        if (_wormholeDripTicks < intervalTicks)
            return;
        _wormholeDripTicks = 0;

        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Player.QuickSpawnItem(Player.GetSource_Misc("PanicAtDawn:WormholeDrip"), ItemID.WormholePotion, 1);
    }

    /// <summary>
    /// Applies active boss hexes to the player. Only runs during boss fights.
    /// </summary>
    private void ApplyBossHexes()
    {
        var hexes = BossHexManager.Current;
        if (!hexes.HasAnyHex)
            return;

        // Only apply during active boss fights
        if (!AnyBossAlive())
            return;

        // Apply flashy hexes (player-side effects)
        ApplyFlashyHex(hexes.Flashy);
        
        // Apply modifier hexes
        ApplyModifierHex(hexes.Modifier);
        
        // Apply constraint hexes
        ApplyConstraintHex(hexes.Constraint);
    }

    private void ApplyFlashyHex(FlashyHex hex)
    {
        switch (hex)
        {
            case FlashyHex.WingClip:
                // Disable flight completely
                Player.wingTime = 0f;
                Player.rocketTime = 0;
                break;
                
            case FlashyHex.Blackout:
                // Extreme darkness - apply Blackout debuff
                Player.AddBuff(BuffID.Blackout, 2);
                break;
                
            // Other flashy hexes are handled elsewhere:
            // - InvisibleBoss: BossHexGlobalNPC.PreDraw
            // - TinyFastBoss/HugeBoss: BossHexGlobalNPC.PostAI
            // - TimeLimit/UnstableGravity/MeteorShower: PanicAtDawnState
            // - Reversal: TODO
            // - Mirrored: TODO
        }
    }

    private void ApplyModifierHex(ModifierHex hex)
    {
        switch (hex)
        {
            case ModifierHex.Frail:
                // -20% max HP
                Player.statLifeMax2 = (int)(Player.statLifeMax2 * 0.8f);
                break;
                
            case ModifierHex.BrokenArmor:
                // Defense halved (apply Broken Armor debuff)
                Player.AddBuff(BuffID.BrokenArmor, 2);
                break;
                
            case ModifierHex.Sluggish:
                // Slow debuff (reapplied every frame so it appears permanent)
                Player.AddBuff(BuffID.Slow, 2);
                break;
                
            case ModifierHex.SlowAttack:
                // Reduced attack speed (Slow debuff approximates this)
                Player.AddBuff(BuffID.Slow, 2);
                break;
                
            // TODO: Implement remaining modifiers:
            // - ExtraPotionSickness: needs hook in potion consumption
            // - ManaDrain: modify mana costs
            // - Inaccurate: spread projectiles
            // - GlassCannon: damage modifier (partially in BossHexGlobalNPC)
            // - Marked: boss damage boost (in BossHexGlobalNPC)
            // - SwiftBoss: handled in BossHexGlobalNPC
        }
    }

    private void ApplyConstraintHex(ConstraintHex hex)
    {
        switch (hex)
        {
            case ConstraintHex.Grounded:
                // No jumping - cancel any upward velocity from jumps
                // We detect jump attempts and zero out
                if (Player.velocity.Y < 0 && Player.controlJump)
                {
                    Player.velocity.Y = 0;
                }
                // Also disable rocket boots, cloud jumps, etc.
                Player.jumpSpeedBoost = 0;
                Player.jumpBoost = false;
                break;
                
            case ConstraintHex.NoGrapple:
                // Handled separately in CanUseItem or via hook projectile cancellation
                break;
                
            // TODO: Implement remaining constraints:
            // - NoBuffPotions: check item use
            // - NoRangedDamage/NoMeleeDamage/NoMagicDamage: damage modifiers
            // - PacifistHealer: special role assignment
        }
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

    private int FindClosestLinkedTeammate()
    {
        // Uses Terraria's team mechanic. If both players are on team 0 (no team), treat it as linked anyway.
        int best = -1;
        float bestDist = float.MaxValue;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            var other = Main.player[i];
            if (other is null || !other.active || other.dead || other.whoAmI == Player.whoAmI)
                continue;

            bool linked = (Player.team == other.team) || (Player.team == 0 && other.team == 0);
            if (!linked)
                continue;

            float d = Vector2.DistanceSquared(Player.Center, other.Center);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        return best;
    }

    private int CountItems(int itemType)
    {
        int count = 0;
        for (int i = 0; i < Player.inventory.Length; i++)
        {
            var it = Player.inventory[i];
            if (it is not null && !it.IsAir && it.type == itemType)
                count += it.stack;
        }
        return count;
    }

    private bool IsPlayerInDarkness()
    {
        // Check if player has the Darkness debuff, or if local lighting is very low.
        if (Player.HasBuff(BuffID.Darkness) || Player.HasBuff(BuffID.Blackout))
            return true;

        // Check tile lighting at player position.
        Point tile = Player.Center.ToTileCoordinates();
        if (!WorldGen.InWorld(tile.X, tile.Y, 10))
            return false;

        // Lighting.GetColor returns the light color at the tile; we check brightness.
        Color light = Lighting.GetColor(tile.X, tile.Y);
        float brightness = (light.R + light.G + light.B) / (255f * 3f);

        // Consider "darkness" if brightness is below 10%.
        return brightness < 0.10f;
    }
}
