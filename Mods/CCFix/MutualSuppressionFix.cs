using Patchwork.Attributes;

namespace CCFix
{
    // disallow mutual suppression of status effects
    // this can happen if a mob is affected both by Paralyze and Petrify, with higher duration left on paralyze
    // with original code, Paralyze will be suppressed since Petrify is stronger affliction (overrides it), and Petrify will be suppressed since Paralyze has longer duration
    // (there is no symmetrical "weaker affliction" check)
    [ModifiesType]
    public class mod_StatusEffect : StatusEffect
    {
        [ModifiesMember]
        public new bool Suppresses(StatusEffect eff, bool suppress_if_tied)
        {
            if (eff.Params.AffectsStat == StatusEffect.ModifiedStat.GenericMarker)
            {
                // UI-only effects are never suppressed
                return false;
            }

            if (this.AfflictionOrigin != null && eff.AfflictionOrigin != null)
            {
                // both effects come from affliction; compare strengths
                if (this.AfflictionOrigin.OverridesAffliction(eff.AfflictionOrigin))
                {
                    //Console.AddMessage($"SUPPRESSION: '{GetDebuggerString()} > {eff.GetDebuggerString()} due to affliction");
                    return true;
                }
                else if (eff.AfflictionOrigin.OverridesAffliction(this.AfflictionOrigin))
                {
                    // this is the missing check in the original code
                    return false;
                }
            }

            // either one of the effects doesn't have affliction origin, or they are of equal strength
            if (!this.Stackable && !eff.Stackable && this.GetStackingKey() == eff.GetStackingKey() && this.NonstackingEffectType == eff.NonstackingEffectType)
            {
                // these are two non-stackable effects affecting the same 'thing'; see which one is stronger
                if (this.HasBiggerValueThan(eff))
                {
                    //Console.AddMessage($"SUPPRESSION: '{GetDebuggerString()} > {eff.GetDebuggerString()} due to bigger value ({CurrentAppliedValue} vs {eff.CurrentAppliedValue})");
                    return true;
                }
                else if (eff.HasBiggerValueThan(this))
                {
                    // this is handled slightly differently in original code
                    return false;
                }

                // effects have same value, see which one has more time remaining
                // TODO: why do we even need this check? even if we suppress longer one, it will become unsuppresses when other effect expires anyway...
                if (this.TimeLeft > eff.TimeLeft)
                {
                    //Console.AddMessage($"SUPPRESSION: '{GetDebuggerString()} > {eff.GetDebuggerString()} due to bigger time left ({TimeLeft} vs {eff.TimeLeft})");
                    return true;
                }
                else if (eff.TimeLeft > this.TimeLeft)
                {
                    return false;
                }

                // effects are tied, select one using tiebreaker rule (guaranteed to be reversed for reverse check)
                //if (suppress_if_tied)
                //	Console.AddMessage($"SUPPRESSION: '{GetDebuggerString()} > {eff.GetDebuggerString()} due to tie break");
                return suppress_if_tied;
            }

            // neither suppresses the other
            return false;
        }
    }
}
