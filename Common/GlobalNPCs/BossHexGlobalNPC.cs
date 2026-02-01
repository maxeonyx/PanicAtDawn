using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;
using PanicAtDawn.Common.Systems;

namespace PanicAtDawn.Common.GlobalNPCs;

/// <summary>
/// Applies hex effects to bosses during boss fights.
/// Handles spawning logic and visual/behavioral modifications.
/// </summary>
public sealed class BossHexGlobalNPC : GlobalNPC
{
    // Instance data per NPC - needed for per-NPC state
    public override bool InstancePerEntity => true;

    // Track if we've applied initial scale to this boss
    private bool _appliedInitialScale;
    private float _originalScale = 1f;

    public override void OnSpawn(NPC npc, Terraria.DataStructures.IEntitySource source)
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.EnableBossHex)
            return;

        if (!npc.boss)
            return;

        // Trigger hex rolling via the manager
        BossHexManager.OnBossSpawn(npc.type);

        // Only announce once per boss spawn (server or singleplayer)
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        var hexes = BossHexManager.Current;
        if (!hexes.HasAnyHex)
            return;

        // Announce all active hexes
        var hexNames = hexes.GetActiveHexNames();
        if (hexNames.Count == 0)
            return;

        string hexList = string.Join(", ", hexNames);
        string message = hexNames.Count == 1
            ? $"Boss Hex: {hexList}"
            : $"Boss Hexes: {hexList}";

        if (Main.netMode == NetmodeID.Server)
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(message), Color.Orange);
        else
            Main.NewText(message, Color.Orange);

        // Announce special conditions
        if (hexes.Flashy == FlashyHex.TimeLimit)
        {
            string timeMsg = "You have 3 minutes to defeat the boss!";
            if (Main.netMode == NetmodeID.Server)
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(timeMsg), Color.Red);
            else
                Main.NewText(timeMsg, Color.Red);
        }

        if (hexes.Constraint == ConstraintHex.PacifistHealer && hexes.PacifistHealerIndex >= 0)
        {
            var healer = Main.player[hexes.PacifistHealerIndex];
            if (healer?.active == true)
            {
                string healerMsg = $"{healer.name} is the Pacifist Healer! They cannot damage the boss but heal allies.";
                if (Main.netMode == NetmodeID.Server)
                    ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(healerMsg), Color.LightGreen);
                else
                    Main.NewText(healerMsg, Color.LightGreen);
            }
        }
    }

    public override void AI(NPC npc)
    {
        if (!npc.boss)
            return;

        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.EnableBossHex)
            return;

        var hexes = BossHexManager.Current;
        if (!hexes.HasAnyHex)
            return;

        // Apply boss-side effects
        ApplyBossFlashyEffects(npc, hexes);
        ApplyBossModifierEffects(npc, hexes);
    }

    public override void PostAI(NPC npc)
    {
        if (!npc.boss)
            return;

        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.EnableBossHex)
            return;

        var hexes = BossHexManager.Current;

        // Apply scale changes (only once per boss)
        if (!_appliedInitialScale && hexes.HasAnyHex)
        {
            _originalScale = npc.scale;
            _appliedInitialScale = true;

            if (hexes.Flashy == FlashyHex.TinyFastBoss)
            {
                npc.scale = _originalScale * 0.5f;
            }
            else if (hexes.Flashy == FlashyHex.HugeBoss)
            {
                npc.scale = _originalScale * 2f;
            }
        }
    }

    public override bool PreDraw(NPC npc, Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (!npc.boss)
            return true;

        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.EnableBossHex)
            return true;

        var hexes = BossHexManager.Current;

        // InvisibleBoss: don't draw the boss at all
        if (hexes.Flashy == FlashyHex.InvisibleBoss)
        {
            return false; // Skip drawing
        }

        return true;
    }

    public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
    {
        if (!npc.boss)
            return;

        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.EnableBossHex)
            return;

        var hexes = BossHexManager.Current;

        // GlassCannon: boss takes 50% more damage
        if (hexes.Modifier == ModifierHex.GlassCannon)
        {
            modifiers.FinalDamage *= 1.5f;
        }
    }

    public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
    {
        if (!npc.boss)
            return;

        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.EnableBossHex)
            return;

        var hexes = BossHexManager.Current;

        // GlassCannon: boss takes 50% more damage
        if (hexes.Modifier == ModifierHex.GlassCannon)
        {
            modifiers.FinalDamage *= 1.5f;
        }
    }

    private void ApplyBossFlashyEffects(NPC npc, ActiveHexes hexes)
    {
        // SwiftBoss is in Modifier but affects boss speed
        // TinyFastBoss: 1.5x speed (scale handled in PostAI)
        if (hexes.Flashy == FlashyHex.TinyFastBoss)
        {
            // Boost velocity slightly each frame (capped to avoid runaway)
            float speedMult = 1.5f;
            float maxSpeed = 20f; // Reasonable cap to prevent instakill speeds

            if (npc.velocity.Length() > 0.1f && npc.velocity.Length() < maxSpeed)
            {
                // Increase speed by adjusting velocity magnitude
                float currentSpeed = npc.velocity.Length();
                float targetSpeed = Math.Min(currentSpeed * speedMult, maxSpeed);
                // Only apply a small boost per frame to avoid jitter
                npc.velocity = Vector2.Normalize(npc.velocity) * 
                    MathHelper.Lerp(currentSpeed, targetSpeed, 0.02f);
            }
        }
    }

    private void ApplyBossModifierEffects(NPC npc, ActiveHexes hexes)
    {
        // SwiftBoss: boss moves/attacks 25% faster
        if (hexes.Modifier == ModifierHex.SwiftBoss)
        {
            float speedMult = 1.25f;
            float maxSpeed = 18f;

            if (npc.velocity.Length() > 0.1f && npc.velocity.Length() < maxSpeed)
            {
                float currentSpeed = npc.velocity.Length();
                float targetSpeed = Math.Min(currentSpeed * speedMult, maxSpeed);
                npc.velocity = Vector2.Normalize(npc.velocity) * 
                    MathHelper.Lerp(currentSpeed, targetSpeed, 0.02f);
            }
        }
    }
}
