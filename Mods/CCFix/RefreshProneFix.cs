using Patchwork.Attributes;

namespace CCFix
{
    // when we refresh affliction duration (say, cast Slicken while old one is still active), old code modifies effective duration of the existing status effect by adding to TemporaryDurationAdjustment
    // this has a side effect of not propagating updated effective duration to the AI states (e.g. KnockedBack); because of that state "ends" when initial status effect ends
    // to resolve that, I force execute Duration setter, which does execute proper updates
    [ModifiesType]
    public class mod_CharacterStats : CharacterStats
    {
        [NewMember]
        [DuplicatesBody("AdjustStatusEffectDuration")]
        public float ori_AdjustStatusEffectDuration(StatusEffect effect, float DurationAdj, bool skipOverride)
        {
            return 0.0f;
        }

        [ModifiesMember("AdjustStatusEffectDuration")]
        public float mod_AdjustStatusEffectDuration(StatusEffect effect, float DurationAdj, bool skipOverride)
        {
            float ret = ori_AdjustStatusEffectDuration(effect, DurationAdj, skipOverride);
            effect.Duration = effect.m_duration; // call setter with unadjusted field value - this doesn't modify any properties, but triggers AI state updates
            return ret;
        }
    }
}
