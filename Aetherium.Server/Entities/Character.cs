using System;
using System.Linq;
using System.Drawing;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium
{
    public class Character : Entity
    {
        public Character() : base()
        {
            // Characters spawn at full health. Previously this was Health(0,0), which left every
            // character "dead" the moment combat could read it; combat (P3-7) now depends on this.
            Set(new Health(100, 100));
            // Base melee strength. Equals CombatSystem.DefaultAttackDamage so a bare
            // character hits exactly as hard as the pre-slice-2 fixed damage; a carried
            // Weapon adds on top of this (P3-7 slice 2).
            Set(new AttackPower(10));
            Set(new HasHeading());
            Set(new Perception());
            Set(new Memory());

            // Continuous action pipeline (engine gap-analysis §4.1): a Character accrues AP each world
            // tick and spends it to act. The default (Speed == MaxBudget == cost) affords one action per
            // eligible tick, matching pre-pipeline immediacy; abilities gate their cast on this budget
            // (see GameMapGrain.UseAbilityAsync, wire-abilities-live). Monster overrides this in its own
            // constructor. Resource pools are per-world data and are stamped at JoinPlayerAsync, not here,
            // so the engine never bakes in a genre-specific pool (mana/stamina/…).
            Set(new ActionSpeed(speed: 1.0, maxBudget: 1.0));

            // Characters emit high heat (visible in infrared)
            Set(new HeatSignature(0.9, TimeSpan.FromSeconds(10)));
        }
    }
}
