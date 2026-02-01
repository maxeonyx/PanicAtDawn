using System;
using System.Collections.Generic;
using Terraria;

namespace PanicAtDawn.Common.Systems;

public enum FlashyHex
{
    None = 0,
    InvisibleBoss,      // Boss is literally invisible
    WingClip,           // No flight
    Blackout,           // Extreme darkness
    TimeLimit,          // 3 minutes or everyone dies
    Reversal,           // Inverted controls
    TinyFastBoss,       // 0.5x size, 1.5x speed
    HugeBoss,           // 2x size
    Mirrored,           // Damaging clone spawns
    UnstableGravity,    // Gravity flips periodically
    MeteorShower,       // Falling stars damage players
}

public enum ModifierHex
{
    None = 0,
    ExtraPotionSickness,  // 3x potion sickness duration
    SlowAttack,           // Reduced attack speed
    ManaDrain,            // Mana costs +50%
    Inaccurate,           // Ranged spread increased
    SwiftBoss,            // Boss moves/attacks 25% faster
    Sluggish,             // Player movement -25%
    Frail,                // Max HP -20%
    BrokenArmor,          // Defense halved
    GlassCannon,          // +50% damage dealt and taken
    Marked,               // Boss deals +25% damage
}

public enum ConstraintHex
{
    None = 0,
    NoBuffPotions,    // Buff potions disabled (heal/mana OK)
    NoRangedDamage,   // Ranged weapons deal 0
    NoMeleeDamage,    // Melee weapons deal 0
    NoMagicDamage,    // Magic weapons deal 0
    Grounded,         // No jumping
    NoGrapple,        // Hooks disabled
    PacifistHealer,   // One player heals teammates instead of damaging
}

/// <summary>
/// Holds the active hexes for a boss fight.
/// </summary>
public class ActiveHexes
{
    public FlashyHex Flashy { get; set; } = FlashyHex.None;
    public ModifierHex Modifier { get; set; } = ModifierHex.None;
    public ConstraintHex Constraint { get; set; } = ConstraintHex.None;
    
    // For time limit hex
    public int TimeLimitTicks { get; set; } = 0;
    public int TimeLimitMaxTicks { get; set; } = 0;
    
    // For unstable gravity
    public int GravityFlipTicks { get; set; } = 0;
    
    // For meteor shower
    public int MeteorTicks { get; set; } = 0;
    
    // For pacifist healer - which player index is the healer
    public int PacifistHealerIndex { get; set; } = -1;

    public bool HasAnyHex => Flashy != FlashyHex.None || Modifier != ModifierHex.None || Constraint != ConstraintHex.None;

    public void Clear()
    {
        Flashy = FlashyHex.None;
        Modifier = ModifierHex.None;
        Constraint = ConstraintHex.None;
        TimeLimitTicks = 0;
        TimeLimitMaxTicks = 0;
        GravityFlipTicks = 0;
        MeteorTicks = 0;
        PacifistHealerIndex = -1;
    }

    public List<string> GetActiveHexNames()
    {
        var names = new List<string>();
        if (Flashy != FlashyHex.None) names.Add(FormatHexName(Flashy.ToString()));
        if (Modifier != ModifierHex.None) names.Add(FormatHexName(Modifier.ToString()));
        if (Constraint != ConstraintHex.None) names.Add(FormatHexName(Constraint.ToString()));
        return names;
    }

    private static string FormatHexName(string name)
    {
        // Convert PascalCase to "Pascal Case"
        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsUpper(c) && result.Length > 0)
                result.Append(' ');
            result.Append(c);
        }
        return result.ToString();
    }
}

/// <summary>
/// Manages hex rolling and persistence per boss type.
/// </summary>
public static class BossHexManager
{
    // Persisted hexes per boss type (by NPC type ID)
    private static readonly Dictionary<int, ActiveHexes> _persistedHexes = new();
    
    // Current active hexes for the ongoing fight
    public static ActiveHexes Current { get; private set; } = new();
    
    // Track which boss type is currently being fought
    public static int CurrentBossType { get; private set; } = -1;

    public static void OnWorldLoad()
    {
        _persistedHexes.Clear();
        Current = new ActiveHexes();
        CurrentBossType = -1;
    }

    public static void OnBossSpawn(int bossType)
    {
        // Already fighting this boss type
        if (CurrentBossType == bossType && Current.HasAnyHex)
            return;

        CurrentBossType = bossType;

        // Check for persisted hexes for this boss
        if (_persistedHexes.TryGetValue(bossType, out var persisted))
        {
            Current = persisted;
            return;
        }

        // Roll new hexes based on player count
        int playerCount = CountActivePlayers();
        Current = RollHexes(playerCount);
        
        // Set up time limit if rolled
        if (Current.Flashy == FlashyHex.TimeLimit)
        {
            Current.TimeLimitMaxTicks = 3 * 60 * 60; // 3 minutes
            Current.TimeLimitTicks = Current.TimeLimitMaxTicks;
        }
        
        // Set up pacifist healer if rolled
        if (Current.Constraint == ConstraintHex.PacifistHealer && playerCount > 1)
        {
            Current.PacifistHealerIndex = Main.rand.Next(Main.maxPlayers);
            // Find a valid player
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                int idx = (Current.PacifistHealerIndex + i) % Main.maxPlayers;
                if (Main.player[idx]?.active == true && !Main.player[idx].dead)
                {
                    Current.PacifistHealerIndex = idx;
                    break;
                }
            }
        }

        // Persist for this boss type
        _persistedHexes[bossType] = Current;
    }

    public static void OnBossDefeated(int bossType)
    {
        // Clear persistence - next time this boss is fought, re-roll
        _persistedHexes.Remove(bossType);
        
        if (CurrentBossType == bossType)
        {
            Current = new ActiveHexes();
            CurrentBossType = -1;
        }
    }

    public static void OnAllBossesDead()
    {
        // Just clear current, keep persistence
        Current = new ActiveHexes();
        CurrentBossType = -1;
    }

    private static ActiveHexes RollHexes(int playerCount)
    {
        var hexes = new ActiveHexes();
        
        // Get enum values (skip None at index 0)
        var flashyValues = Enum.GetValues(typeof(FlashyHex));
        var modifierValues = Enum.GetValues(typeof(ModifierHex));
        var constraintValues = Enum.GetValues(typeof(ConstraintHex));
        
        int flashyCount = flashyValues.Length - 1;
        int modifierCount = modifierValues.Length - 1;
        int constraintCount = constraintValues.Length - 1;

        if (playerCount == 1)
        {
            // 1 player: 1 hex from any category
            int category = Main.rand.Next(3);
            switch (category)
            {
                case 0:
                    hexes.Flashy = (FlashyHex)flashyValues.GetValue(Main.rand.Next(1, flashyCount + 1));
                    break;
                case 1:
                    hexes.Modifier = (ModifierHex)modifierValues.GetValue(Main.rand.Next(1, modifierCount + 1));
                    break;
                case 2:
                    hexes.Constraint = (ConstraintHex)constraintValues.GetValue(Main.rand.Next(1, constraintCount + 1));
                    break;
            }
        }
        else if (playerCount == 2)
        {
            // 2 players: 2 hexes from 2 different categories
            int cat1 = Main.rand.Next(3);
            int cat2 = (cat1 + Main.rand.Next(1, 3)) % 3; // Different category
            
            foreach (int cat in new[] { cat1, cat2 })
            {
                switch (cat)
                {
                    case 0:
                        hexes.Flashy = (FlashyHex)flashyValues.GetValue(Main.rand.Next(1, flashyCount + 1));
                        break;
                    case 1:
                        hexes.Modifier = (ModifierHex)modifierValues.GetValue(Main.rand.Next(1, modifierCount + 1));
                        break;
                    case 2:
                        hexes.Constraint = (ConstraintHex)constraintValues.GetValue(Main.rand.Next(1, constraintCount + 1));
                        break;
                }
            }
        }
        else
        {
            // 3+ players: 1 from each category
            hexes.Flashy = (FlashyHex)flashyValues.GetValue(Main.rand.Next(1, flashyCount + 1));
            hexes.Modifier = (ModifierHex)modifierValues.GetValue(Main.rand.Next(1, modifierCount + 1));
            hexes.Constraint = (ConstraintHex)constraintValues.GetValue(Main.rand.Next(1, constraintCount + 1));
        }

        return hexes;
    }

    private static int CountActivePlayers()
    {
        int count = 0;
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            var p = Main.player[i];
            // Check active AND that the player has a name (real player, not empty slot)
            if (p?.active == true && !string.IsNullOrEmpty(p.name))
                count++;
        }
        return Math.Max(1, count);
    }
}
