using System;
using System.Text;
using Patchwork.Attributes;
using UnityEngine;

// Overview of attack calculations in the game:
//> AttackBase::OnImpact(self, enemy, isMainTarget) - only one of the entry points!
// > NotifyHitFrame -> OnAttackHitFrame -> GenericAbility::HandleStatsOnAttackHitFrame
//  - PowderBurns: - launch new attack, don't care
// > AbilityOrigin.Activate(enemy)
//  - TODO ???
// > CalcDamage
//  > create DamageInfo(Target= enemy, Damage= 0, Attack= this, Self= self) (NB: has all info!)
//  > adjust base damage range
//  > damage multiplier: vs CCD
//  > DamagePacket::RollDamage - sets DamageBase
//  > damage multiplier: inherent?, bounce factor
//  > adjust DamageBase for traps
//  > subscribe for OnAttackRollCalculated and call attackerStats.AdjustDamageDealt
//   > damage multiplier: might mod
//   > OnPreDamageDealt:
//    > GenericAbilityWithPrimaryAttackOverride: change DefendedBy (nothing to do)
//    > BackStabAbility: apply status effects if attacking from stealth/invis
//    > BruteForce: change DefendedBy sometimes
//    > Deathblows: apply status effects if two flanking abilities are active
//    > DefensiveShooting: apply status effects if some conditions are met
//    > FlankingAbility: apply status effects if some conditions are met
//    > HeartOfFury: apply status effects unless recursive
//    > MarkedPreyTrait: apply hardcoded status effect(BonusDamage) if some conditions are met
//    > StalkersLink: apply hardcoded status effects(Accuracy & BonusDamageMult) if some conditions are met
//   > OnAddDamage: -
//   > attack roll(can be overridden)
//   > attackerStats.CalculateAccuracy()
//   > defenderStats.CalculateIsImmune()
//    > OnCheckImmunity -> StatusEffects immunity vs keywords
//   > defenderStats.CalculateDefense()
//   > if defended:
//    > ComputeHitAdjustment - a bunch of rolls to convert hit type
//    > OnAttackRollCalculated: ItemModComponent - on hit or crit[TODO]
//     > ItemModComponent::HandleAttackOnScoring[Critical] Hit
//      > activate ability or launch attack
//    > damage multiplier: crit/graze
//   > damage multiplier: weapon specialization
//   > store damage AccuracyRating, DefenseRating, Immune, RawRoll
//   > OnAdjustCritGrazeMiss: -
//   > if not miss:
//    > damage add/multiplier: active status effects
//    > damage procs: bonus damage
//    > damage multiplier: bonus for main dmg type
//    > damage multiplier: bonus for race
//    > damage multiplier: ranged weapon in close combat effects
//    > damage procs: item mods
//   > ComputeInterrupt
//   > reveal DT, autopause
//   > OnPostDamageDealt: (have to do before...)
//    > GenericSpellWithExtraAttackOnMissAndEnd: launch attack
//    > Blast: launch & impact new AttackAOE
//    > BorrowedInstinct: apply status effects(! add new acc.mods etc.)
//    > Carnage: store attack data and reapply after delay
//    > CoordinatedPositioning: teleport(! fucks up distances)
//    > CripplingGuard: launch extra attack
//    > DeepWounds: apply status effects
//    > DefensiveShooting: -
//    > DrainingTouch: heal based on damage amount
//    > EnervatingBlows: compute secondary attack & apply status effects
//    > HeartOfFury: launch attacks
//    > MinorBlights: ??? (one callback when effect applied, another when expired)
//    > PowderBurns: apply status effects
//    > StunningShots: launch attack
//    > TacticalMeld: apply status effects
//  > [CalcDamage after AdjustDamageDealt]
//  > OnHit:
//   > AIPossessableController: regain control maybe
// > [OnImpact after CalcDamage]
// > calculate DT bypass
// > if not miss:
//  > attackerStats.TriggerWhenHits
//   > OnAttackHits: -
//   > status effects WhenHits
//  > defenderStats.TriggerWhenHit
//   > status effects WhenHit(incl.reflects)
//  > apply push
//  > apply status effects & afflictions
//  > ApplyItemModAttackEffects
//  > vfx
// > if miss:
//  > attackerStats.TriggerWhenMisses
//  > vfx
// > enemyHealth.DoDamage()
//  > calculate max(0, DamageAmount)
//  > if enemyStats, override by enemyStats.CalculateDamageTaken()
//   > OnPreDamageApplied: handle damage shields
//   > if fully absorbed, return 0
//   > damage multiplier: defender aoe mod
//   > pre-DT calculation
//   > AdjustDamageByDTDR
//    > select best type
//    > AdjustDamageByDTDR_Helper
//     > if raw, return unmodified
//     > CalcDT
//     > CalcDR
//     > PreBypassDT = calculated DT * mult(1 for normal attack)
//     > bypass = dmg.bypass + attack.bypass
//     > postBypassDT = (PreBypassDT - bypass) * mult
//     > DR *= mult
//     > adjusted = dmg - postBypassDT
//     > adjusted *= (1 - clampedDR / 100)
//     > CalcMinDamage(special case for crushing)
//     > OnApplyDamageThreshhold: -
//     > adjust for min and infinite DT(immunity)
//   > dmg *= difficulty dmg mod
//   > procs damage & final adjusted damage
//   > OnApplyProcs: -
//   > OnDamageFinal:
//    > ApplyOnDamageThreshold: launch attacks
//    > EquipmentSoulbound: unlocks progress
//    > PartyMemberAI: stats tracker update
//   > TriggerWhenTakesDamage: status effects WhenTakesDamage(transfer, etc)
//   > OnPostDamageApplied:
//    > ChanterTrait: delay chant
//    > EquipmentSoulbound: unlocks progress
//    > PsychicBacklashTrait: launch attack
//  > [DoDamage after CalculateDamageTaken]
namespace CombatDetails
{
    // utility to build stat value breakdown strings
    [NewType]
    public class BonusCalculator
    {
        [NewMember] private StringBuilder m_str = new StringBuilder();
        [NewMember] private float m_curTotal = 0f;

        [NewMember]
        public void Add(float val, string reason)
        {
            if (val == 0)
                return;
            m_curTotal += val;
            if (m_str.Length == 0)
            {
                m_str.Append(val);
            }
            else
            {
                m_str.Append(val < 0 ? " - " : " + ");
                m_str.Append(Math.Abs(val));
            }
            m_str.Append(" [808080](");
            m_str.Append(reason);
            m_str.Append(")[-]");
        }

        [NewMember]
        public void Add(StatusEffect eff, float multiplier = 1f, float addend = 0f)
        {
            if (eff.Applied)
                Add(eff.CurrentAppliedValue * multiplier + addend, eff.GetDisplayName());
        }

        [NewMember]
        public void AddFromStat(CharacterStats stats, Func<StatusEffect, bool> filter, float fullValue, string suffix = "", float multiplier = 1f)
        {
            if (suffix.Length > 0)
                suffix = ": " + suffix;

            foreach (var eff in stats.ActiveStatusEffects)
            {
                if (eff.Applied && filter(eff))
                {
                    Add(eff.CurrentAppliedValue * multiplier, $"{eff.GetDisplayName()}{suffix}");
                    fullValue -= eff.CurrentAppliedValue;
                }
            }
            Add(fullValue * multiplier, $"Other{suffix}");
        }

        [NewMember]
        public void AddFromStat(CharacterStats stats, StatusEffect.ModifiedStat modStat, float fullValue, string suffix = "", float multiplier = 1f)
        {
            AddFromStat(stats, eff => eff.Params.AffectsStat == modStat, fullValue, suffix, multiplier);
        }

        [NewMember]
        public float CurTotal()
        {
            return m_curTotal;
        }

        [NewMember]
        public void Expect(float expected)
        {
            Add(expected - m_curTotal, "UNEXPECTED!");
        }

        [NewMember]
        public string Finish(string statName, bool emptyIfNothing = false)
        {
            if (emptyIfNothing && m_str.Length == 0)
                return "";

            string report = string.IsNullOrEmpty(statName) ? "" : statName + ": ";
            report += $"{m_curTotal}[A0A0A0] = {m_str.ToString()}[-]\n";
            return report;
        }
    }

    // utilities to dump reasons for specific stats
    [NewType]
    public static class Utils
    {
        [NewMember]
        public static bool IsAccuracyEffect(StatusEffect eff, AttackBase attack, CharacterStats attackerStats, CharacterStats defenderStats)
        {
            // see StatusEffect.AdjustAccuracy
            switch (eff.Params.AffectsStat)
            {
                case StatusEffect.ModifiedStat.UnarmedAccuracy:
                    var attackMelee = attack as AttackMelee;
                    return attackMelee && attackMelee.Unarmed;
                case StatusEffect.ModifiedStat.AccuracyByWeaponType:
                    var effPrefab = eff.Params.EquippablePrefab as Weapon;
                    Weapon atkWeapon = attack?.gameObject.GetComponent<Weapon>();
                    return effPrefab && atkWeapon && atkWeapon.WeaponType == effPrefab.WeaponType;
                case StatusEffect.ModifiedStat.MeleeAccuracy:
                    return attack is AttackMelee;
                case StatusEffect.ModifiedStat.RangedAccuracy:
                    return attack is AttackRanged || attack is AttackBeam;
                case StatusEffect.ModifiedStat.DistantEnemyBonus:
                    return attackerStats && defenderStats && attackerStats.IsEnemyDistant(defenderStats.gameObject);
                case StatusEffect.ModifiedStat.BonusAccuracyAtLowStamina:
                    return eff.m_generalCounter == 1;
                case StatusEffect.ModifiedStat.Accuracy:
                    return true;
                case StatusEffect.ModifiedStat.DistantEnemyWeaponAccuracyBonus:
                    return attack is AttackRanged && !attack.AbilityOrigin && attackerStats && defenderStats && attackerStats.IsEnemyDistant(defenderStats.gameObject);
                case StatusEffect.ModifiedStat.AccuracyByClass:
                    return defenderStats && defenderStats.CharacterClass == eff.Params.ClassType;
                case StatusEffect.ModifiedStat.MeleeWeaponAccuracy:
                    return attack is AttackMelee && attack.IsAutoAttack();
                case StatusEffect.ModifiedStat.AccuracyBonusForAttackersWithAffliction:
                    return attackerStats && attackerStats.HasStatusEffectFromAffliction(eff.Params.AfflictionPrefab); // see StatusEffect.AdjustAttackerAccuracy
            }
            return false;
        }

        [NewMember]
        public static bool IsDefenseEffect(StatusEffect eff, CharacterStats.DefenseType defenseType, CharacterStats attackerStats, CharacterStats defenderStats)
        {
            // see StatusEffect.AdjustDefense
            switch (eff.Params.AffectsStat)
            {
                case StatusEffect.ModifiedStat.Deflection:
                    return defenseType == CharacterStats.DefenseType.Deflect;
                case StatusEffect.ModifiedStat.Fortitude:
                    return defenseType == CharacterStats.DefenseType.Fortitude;
                case StatusEffect.ModifiedStat.Reflex:
                    return defenseType == CharacterStats.DefenseType.Reflex;
                case StatusEffect.ModifiedStat.Will:
                    return defenseType == CharacterStats.DefenseType.Will;
                case StatusEffect.ModifiedStat.DistantEnemyBonus:
                    return (defenseType == CharacterStats.DefenseType.Deflect || defenseType == CharacterStats.DefenseType.Reflex) && defenderStats && attackerStats && defenderStats.IsEnemyDistant(attackerStats.gameObject);
                case StatusEffect.ModifiedStat.AllDefense:
                    return true;
                case StatusEffect.ModifiedStat.AllDefensesExceptDeflection:
                    return defenseType == CharacterStats.DefenseType.Fortitude || defenseType == CharacterStats.DefenseType.Reflex || defenseType == CharacterStats.DefenseType.Will;
            }
            return false;
        }

        [NewMember]
        public static bool IsDamageAddEffect(StatusEffect eff, AttackBase attack, CharacterStats attackerStats, CharacterStats defenderStats)
        {
            // see StatusEffect.AdjustDamage
            switch (eff.Params.AffectsStat)
            {
                case StatusEffect.ModifiedStat.DisengagementDamage:
                    return attack.IsDisengagementAttack;
                case StatusEffect.ModifiedStat.BonusUnarmedDamage:
                    var attackMelee = attack as AttackMelee;
                    return attackMelee && attackMelee.Unarmed;
                case StatusEffect.ModifiedStat.BonusMeleeDamage:
                    return attack is AttackMelee;
            }
            return false;
        }

        [NewMember]
        public static bool IsDamageMulEffect(StatusEffect eff, AttackBase attack, CharacterStats attackerStats, CharacterStats defenderStats)
        {
            // see StatusEffect.AdjustDamageMultiplier
            switch (eff.Params.AffectsStat)
            {
                case StatusEffect.ModifiedStat.BonusMeleeWeaponDamageMult:
                    return attack is AttackMelee && attack.IsAutoAttack();
                case StatusEffect.ModifiedStat.BonusRangedWeaponDamageMult:
                    return attack is AttackRanged && attack.IsAutoAttack();
                case StatusEffect.ModifiedStat.BonusTwoHandedMeleeWeaponDamageMult:
                    var weapon = attack?.GetComponent<Weapon>();
                    return attack is AttackMelee && weapon && weapon.BothPrimaryAndSecondarySlot;
                case StatusEffect.ModifiedStat.BonusMeleeDamageMult:
                    return attack is AttackMelee && attack.IsAutoAttack();
                case StatusEffect.ModifiedStat.DamageMultByClass:
                    return defenderStats && defenderStats.CharacterClass == eff.Params.ClassType;
            }
            return false;
        }

        [NewMember]
        public static bool IsItemDTEffect(StatusEffect eff, CharacterStats wearerStats)
        {
            // see Armor.CalculateDT
            switch (eff.Params.AffectsStat)
            {
                case StatusEffect.ModifiedStat.BonusArmorDtMult:
                    return true;
                case StatusEffect.ModifiedStat.BonusArmorDtMultAtLowHealth:
                    var health = wearerStats.GetComponent<Health>();
                    return health != null && health.HealthPercentage < 0.5f;
            }
            return false;
        }

        [NewMember]
        public static float CalcLevelAdjustmentAdditive(LevelScaling scaling, float adjustmentValue, int level)
        {
            float levelAdjustment = 0f;
            if (adjustmentValue != 0)
            {
                int clampedLevel = Mathf.Clamp(level, 0, scaling.MaxLevel != 0 ? scaling.MaxLevel : Int32.MaxValue);
                int increments = (clampedLevel - scaling.BaseLevel) / scaling.LevelIncrement;
                if (increments >= 1)
                    levelAdjustment = adjustmentValue * increments;
            }
            return levelAdjustment;
        }

        [NewMember]
        public static float CalcLevelAdjustmentMultiplicative(LevelScaling scaling, float adjustmentValue, int level)
        {
            float levelMultiplier = 1f;
            if (adjustmentValue != 1f)
            {
                int clampedLevel = Mathf.Clamp(level, 0, scaling.MaxLevel != 0 ? scaling.MaxLevel : Int32.MaxValue);
                int increments = (clampedLevel - scaling.BaseLevel) / scaling.LevelIncrement;
                if (increments >= 1)
                    levelMultiplier = Mathf.Pow(adjustmentValue, increments);
            }
            return levelMultiplier;
        }

        // invariants: attackerStats != null, defenderStats != null, attackerStats.gameObject == damage.Attack.Owner, defenderStats.gameObject = damage.Target
        [NewMember]
        public static string DumpAccuracy(DamageInfo damage, CharacterStats attackerStats, CharacterStats defenderStats)
        {
            BonusCalculator calc = new BonusCalculator();
            if (damage.Attack is AttackRanged)
                calc.Add(attackerStats.RangedAccuracyBonus, "Base ranged");
            else
                calc.Add(attackerStats.MeleeAccuracyBonus, "Base melee");
            calc.Add(attackerStats.AccuracyBonusFromLevel, $"Level {attackerStats.ScaledLevel}");
            calc.Add(attackerStats.StatBonusAccuracy, $"Perception {attackerStats.Perception}");

            bool extraAccPerLevel = attackerStats.CharacterClass != CharacterStats.Class.PlayerTrap;
            if (damage.Attack != null)
                extraAccPerLevel &= damage.Attack.AbilityOrigin || damage.Attack.TriggeringAbility;
            else
                extraAccPerLevel &= attackerStats.CharacterClass == CharacterStats.Class.Chanter;
            if (extraAccPerLevel)
                calc.Add(attackerStats.ScaledLevel, "Level extra"); // spells & abilities have extra +1 acc/level bonus

            // attack.AccuracyBonusTotal
            if (damage.Attack)
            {
                calc.Add(damage.Attack.AccuracyBonus, "Inherent"); // TODO: it's adjusted by traps and some status effects, investigate more

                var levelScaling = damage.Attack.LevelScaling;
                float levelAdjustment = CalcLevelAdjustmentAdditive(levelScaling, levelScaling.AccuracyAdjustment, attackerStats.ScaledLevel);
                calc.Add(levelAdjustment, $"Scaling: {levelScaling.AccuracyAdjustment} per {levelScaling.LevelIncrement} levels after {levelScaling.BaseLevel}");

                calc.Add(damage.Attack.TemporaryAccuracyBonus, "Reflected attack"); // only set up for attacks reflected by RangedReflect status effects
                if (damage.Attack.AbilityOrigin)
                {
                    var mods = damage.Attack.AbilityOrigin.AbilityMods;
                    for (int i = 0; i < mods.Count; i++)
                        if (mods[i].Type == AbilityMod.AbilityModType.AttackAccuracyBonus)
                            calc.Add(mods[i].Value, "WTF ability mod?");
                }
            }

            // attackerStats.GetAccuracyBonus
            foreach (var eff in attackerStats.ActiveStatusEffects)
                if (IsAccuracyEffect(eff, damage.Attack, attackerStats, defenderStats))
                    calc.Add(eff);

            // vs. race (note that these don't seem to be used and don't cover all races; things like enchants use AccuracyByRace, which is converted to Accuracy when attack is launched)
            switch (defenderStats.CharacterRace)
            {
                case CharacterStats.Race.Vessel:
                    calc.AddFromStat(attackerStats, StatusEffect.ModifiedStat.VesselAccuracy, attackerStats.VesselAccuracyBonus, $"vs. race");
                    break;
                case CharacterStats.Race.Beast:
                    calc.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BeastAccuracy, attackerStats.BeastAccuracyBonus, $"vs. race");
                    break;
                case CharacterStats.Race.Wilder:
                    calc.AddFromStat(attackerStats, StatusEffect.ModifiedStat.WilderAccuracy, attackerStats.WilderAccuracyBonus, $"vs. race");
                    break;
                case CharacterStats.Race.Primordial:
                    calc.AddFromStat(attackerStats, StatusEffect.ModifiedStat.PrimordialAccuracy, attackerStats.PrimordialAccuracyBonus, $"vs. race");
                    break;
            }

            if (damage.Attack && damage.Attack.IsDisengagementAttack)
            {
                calc.AddFromStat(attackerStats, StatusEffect.ModifiedStat.DisengagementAccuracy, attackerStats.DisengagementAccuracyBonus, "disengagement");
                calc.Add(attackerStats.DifficultyDisengagementAccuracyBonus, "Disengagement difficulty");
            }

            var enemyAI = defenderStats.GetComponent<AIController>();
            if (enemyAI)
            {
                Vector3 toEnemy = defenderStats.transform.position - attackerStats.transform.position;
                float cosAngle = Vector3.Dot(defenderStats.transform.forward, toEnemy.normalized);
                if (enemyAI.CurrentTarget == null && cosAngle > 0f)
                    calc.Add(attackerStats.FlankedAccuracyBonus, "WTF flanking?"); // applied only if enemy is not targetting anyone - paralyzed + behind bonus? not used anywhere? TODO verify
            }

            var equipment = attackerStats.gameObject.GetComponent<Equipment>();
            if (equipment != null)
            {
                if (!attackerStats.GetComponent<BestiaryReference>())
                {
                    Shield equippedShield = equipment.EquippedShield;
                    if (equippedShield != null)
                        calc.Add(equippedShield.AccuracyBonus, "Shield");
                    else if (!damage.Attack?.AbilityOrigin && !equipment.TwoHandedWeapon && !equipment.DualWielding)
                        calc.Add(AttackData.Instance.Single1HWeapNoShieldAccuracyBonus, "One-handed");
                }
                else
                {
                    Shield shield = equipment.DefaultEquippedItems.Shield;
                    if (shield != null)
                        calc.Add(shield.AccuracyBonus, "Shield");
                    else if (!damage.Attack?.AbilityOrigin && !equipment.DefaultEquippedItems.TwoHandedWeapon && !equipment.DefaultEquippedItems.DualWielding)
                        calc.Add(AttackData.Instance.Single1HWeapNoShieldAccuracyBonus, "One-handed");
                }
            }

            calc.Add(attackerStats.NearestAllyWithSharedTarget(defenderStats.gameObject), "Nearest ally bonus");
            calc.Add(attackerStats.DifficultyStatBonus, "Difficulty");

            calc.Expect(damage.AccuracyRating);
            return calc.Finish("Accuracy");
        }

        [NewMember]
        public static string DumpDefense(DamageInfo damage, CharacterStats attackerStats, CharacterStats defenderStats)
        {
            // mindweb (TODO)
            if (defenderStats.HasStatusEffectOfType(StatusEffect.ModifiedStat.MindwebEffect))
            {
                return $"{damage.DefendedBy}: {damage.DefenseRating} (MINDWEB)\n";
            }

            BonusCalculator calc = new BonusCalculator();

            // defenderStats.GetDefenseScore
            bool baseOverridden = false;
            foreach (var eff in defenderStats.FindStatusEffectsOfType(StatusEffect.ModifiedStat.SetBaseDefense))
            {
                if (eff.Params.DefenseType != damage.DefendedBy)
                    continue;
                calc.Add(eff.CurrentAppliedValue, $"Base overridden by ${eff.GetDisplayName()}");
                baseOverridden = true;
                break;
            }
            if (!baseOverridden)
                calc.Add(defenderStats.GetBaseDefense(damage.DefendedBy), "Base");

            calc.Add(defenderStats.DefenseBonusFromLevel, $"Level {defenderStats.ScaledLevel}");

            // defenderStats.GetDefenseBonus vs enemy
            foreach (var eff in defenderStats.ActiveStatusEffects)
                if (IsDefenseEffect(eff, damage.DefendedBy, attackerStats, defenderStats))
                    calc.Add(eff);

            switch (damage.DefendedBy)
            {
                case CharacterStats.DefenseType.Deflect:
                    calc.Add(CharacterStats.GetStatBonusDeflection(defenderStats.Resolve), $"Resolve {defenderStats.Resolve}");
                    calc.Add(defenderStats.GetShieldDeflectBonus(defenderStats.gameObject.GetComponent<Equipment>()), "Shield"); // base + bonus (TODO elaborate?)
                    var attackRanged = damage.Attack as AttackRanged;
                    if (!attackRanged || !attackRanged.VeilPiercing)
                        calc.Add(defenderStats.VeilDeflectionBonus, "Veil");
                    break;
                case CharacterStats.DefenseType.Fortitude:
                    calc.Add(CharacterStats.GetStatDefenseTypeBonus(defenderStats.Might), $"Might {defenderStats.Might}");
                    calc.Add(CharacterStats.GetStatDefenseTypeBonus(defenderStats.Constitution), $"Constitution {defenderStats.Constitution}");
                    break;
                case CharacterStats.DefenseType.Reflex:
                    calc.Add(CharacterStats.GetStatDefenseTypeBonus(defenderStats.Dexterity), $"Dexterity {defenderStats.Dexterity}");
                    calc.Add(CharacterStats.GetStatDefenseTypeBonus(defenderStats.Perception), $"Perception {defenderStats.Perception}");
                    calc.Add(defenderStats.GetShieldReflexBonus(defenderStats.gameObject.GetComponent<Equipment>()), "Shield"); // base + maybe deflection (TODO elaborate?)
                    break;
                case CharacterStats.DefenseType.Will:
                    calc.Add(CharacterStats.GetStatDefenseTypeBonus(defenderStats.Intellect), $"Intellect {defenderStats.Intellect}");
                    calc.Add(CharacterStats.GetStatDefenseTypeBonus(defenderStats.Resolve), $"Resolve {defenderStats.Resolve}");
                    break;
                case CharacterStats.DefenseType.None:
                    break;
                default:
                    calc.Add(50, "WTF hardcoded");
                    break;
            }

            // defenderStats.GetDefenseBonus vs attack
            if (damage.Attack)
            {
                if (damage.Attack.HasStatusEffect(StatusEffect.ModifiedStat.Stunned) || damage.Attack.HasStatusEffect(StatusEffect.ModifiedStat.CanStun))
                    calc.AddFromStat(defenderStats, StatusEffect.ModifiedStat.StunDefense, defenderStats.StunDefenseBonus, "vs stun");
                if (damage.Attack.HasStatusEffect(StatusEffect.ModifiedStat.KnockedDown))
                    calc.AddFromStat(defenderStats, StatusEffect.ModifiedStat.KnockdownDefense, defenderStats.KnockdownDefenseBonus, "vs knockdown");
                if (damage.Attack.HasKeyword("poison")) // OR isSecondary && attack.HasAfflictionWithKeyword("poison")
                    calc.AddFromStat(defenderStats, StatusEffect.ModifiedStat.PoisonDefense, defenderStats.PoisonDefenseBonus, "vs poison");
                if (damage.Attack.HasKeyword("disease")) // OR similar to above
                    calc.AddFromStat(defenderStats, StatusEffect.ModifiedStat.DiseaseDefense, defenderStats.DiseaseDefenseBonus, "vs disease");
                if (damage.Attack.PushDistance != 0f || damage.Attack.HasStatusEffect(StatusEffect.ModifiedStat.Push))
                    calc.AddFromStat(defenderStats, StatusEffect.ModifiedStat.PushDefense, defenderStats.PushDefenseBonus, "vs push");
                if (damage.Attack.IsDisengagementAttack)
                    calc.AddFromStat(defenderStats, StatusEffect.ModifiedStat.DisengagementDefense, defenderStats.DisengagementDefenseBonus, "vs disengagement");
                if (damage.Attack.AbilityOrigin != null && damage.Attack.AbilityOrigin is GenericSpell)
                    calc.AddFromStat(defenderStats, StatusEffect.ModifiedStat.SpellDefense, defenderStats.SpellDefenseBonus, "vs spells");
                if (damage.Attack is AttackRanged && damage.DefendedBy == CharacterStats.DefenseType.Deflect)
                    calc.AddFromStat(defenderStats, StatusEffect.ModifiedStat.RangedDeflection, defenderStats.RangedDeflectionBonus, "vs ranged deflect");
                if (defenderStats.GetComponent<Equipment>()?.TwoHandedWeapon == true)
                    calc.AddFromStat(defenderStats, StatusEffect.ModifiedStat.TwoHandedDeflectionBonus, defenderStats.TwoHandedDeflectionBonus, "two-handed deflect"); // ! vs all attacks
                if (damage.Attack is AttackAOE)
                    calc.Add(defenderStats.DefensiveBondBonus, "Defensive Bond vs AOE");
            }

            var defenderAI = defenderStats.gameObject.GetComponent<AnimationController>();
            if (defenderAI != null)
            {
                if (defenderAI.CurrentReaction == AnimationController.ReactionType.Knockdown)
                    calc.AddFromStat(defenderStats, StatusEffect.ModifiedStat.WhileKnockeddownDefense, defenderStats.WhileKnockeddownDefenseBonus, "while knocked down");
                if (defenderAI.CurrentReaction == AnimationController.ReactionType.Stun)
                    calc.AddFromStat(defenderStats, StatusEffect.ModifiedStat.WhileStunnedDefense, defenderStats.WhileStunnedDefenseBonus, "while stunned");
            }

            calc.Add((defenderStats as mod_CharacterStats).add_GetMiscDefenseAdjustment(damage.DefendedBy, damage.Attack, attackerStats.gameObject, false), "Misc"); // TODO: this is various defenses against keyword/affliction, they are obtained by custom callbacks :(
            calc.Add(defenderStats.DifficultyStatBonus, "Difficulty");

            calc.Expect(damage.DefenseRating);
            return calc.Finish($"{damage.DefendedBy}");
        }

        [NewMember]
        public static string DumpHitAdjustment(DamageInfo damage, CharacterStats attackerStats, CharacterStats defenderStats)
        {
            if (damage.DefendedBy == CharacterStats.DefenseType.None)
                return "";

            if (defenderStats.EvadeEverything)
                return "Guaranteed miss!\n";

            if (damage.Immune)
                return "Immune!\n";

            int hitValue = damage.RawRoll + damage.AccuracyRating - damage.DefenseRating;
            bool isPhysicalAttack = damage.Attack != null && (damage.Attack.AbilityOrigin == null || damage.Attack.IsAutoAttack());
            var equipment = attackerStats.GetComponent<Equipment>();
            bool isOneHanded = equipment != null && damage.Attack == equipment.PrimaryAttack && !equipment.TwoHandedWeapon && !equipment.DualWielding && equipment.PrimaryAttack is AttackMelee && equipment.EquippedShield == null;

            StringBuilder str = new StringBuilder();
            str.Append($"Roll: [A0A0A0]{damage.RawRoll} + {damage.AccuracyRating} - {damage.DefenseRating} =[-] {hitValue} => {damage.OriginalHitType}");
            switch (damage.OriginalHitType)
            {
                case HitType.MISS:
                    str.Append($" [A0A0A0](<{attackerStats.MinimumRollToGraze})[-]\n");
                    if (isPhysicalAttack)
                    {
                        var calc = new BonusCalculator();
                        calc.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BonusMissToGrazePercent, attackerStats.BonusMissToGrazePercent, "", 100f);
                        str.Append(calc.Finish("Attacker M->G %", true));
                    }
                    break;
                case HitType.GRAZE:
                    str.Append($" [A0A0A0]({attackerStats.MinimumRollToGraze}-{CharacterStats.GrazeThreshhold})[-]\n");
                    if (isPhysicalAttack)
                    {
                        var calcGH = new BonusCalculator();
                        calcGH.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BonusGrazeToHitPercent, attackerStats.BonusGrazeToHitPercent, "", 100f);
                        if (isOneHanded)
                            calcGH.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BonusGrazeToHitRatioMeleeOneHand, attackerStats.BonusGrazeToHitPercentMeleeOneHanded, "1h", 100f);
                        str.Append(calcGH.Finish("Attacker G->H %", true));

                        var calcGM = new BonusCalculator();
                        calcGM.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BonusGrazeToMissPercent, attackerStats.BonusGrazeToMissPercent, "", 100f);
                        str.Append(calcGM.Finish("Attacker G->M %", true));
                    }
                    break;
                case HitType.HIT:
                    str.Append($" [A0A0A0]({CharacterStats.GrazeThreshhold + 1}-{attackerStats.CritThreshhold - 1})[-]\n");
                    if (isPhysicalAttack)
                    {
                        var calcHC = new BonusCalculator();
                        calcHC.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BonusHitToCritPercent, attackerStats.BonusHitToCritPercent, "", 100f);
                        if (isOneHanded)
                            calcHC.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BonusHitToCritRatioMeleeOneHand, attackerStats.BonusHitToCritPercentMeleeOneHanded, "1h", 100f);
                        var defenderHealth = defenderStats.GetComponent<Health>();
                        if (defenderHealth && defenderHealth.StaminaPercentage < 0.1f)
                            calcHC.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BonusHitToCritPercentEnemyBelow10Percent, attackerStats.BonusHitToCritPercentEnemyBelow10Percent, "near death", 100f);
                        calcHC.Add(defenderStats.DifficultyHitToCritBonusChance * 100f, "Difficulty");
                        str.Append(calcHC.Finish("Attacker H->C %", true));
                    }
                    var calcHCAll = new BonusCalculator();
                    calcHCAll.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BonusHitToCritPercentAll, attackerStats.BonusHitToCritPercentAll, "", 100f);
                    str.Append(calcHCAll.Finish("Attacker H->C all %", true));
                    if (isPhysicalAttack)
                    {
                        var calcHG = new BonusCalculator();
                        calcHG.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BonusHitToGrazePercent, attackerStats.BonusHitToGrazePercent, "", 100f);
                        str.Append(calcHG.Finish("Attacker H->G %", true));
                    }
                    break;
                case HitType.CRIT:
                    str.Append($" [A0A0A0]({attackerStats.CritThreshhold}+)[-]\n");
                    if (isPhysicalAttack)
                    {
                        var calcCH = new BonusCalculator();
                        calcCH.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BonusCritToHitPercent, attackerStats.BonusCritToHitPercent, "", 100f);
                        str.Append(calcCH.Finish("Attacker C->H %", true));
                    }
                    break;
            }
            if (damage.AttackerChangedToHitType != HitType.NONE)
            {
                str.Append($" => converted to {damage.AttackerChangedToHitType}!\n");
            }

            // defensive conversions
            var hitType = damage.AttackerChangedToHitType != HitType.NONE ? damage.AttackerChangedToHitType : damage.OriginalHitType;
            switch (hitType)
            {
                case HitType.GRAZE:
                    var calcGM = new BonusCalculator();
                    calcGM.AddFromStat(defenderStats, StatusEffect.ModifiedStat.EnemyGrazeToMissPercent, defenderStats.EnemyGrazeToMissPercent, "", 100f);
                    if (damage.DefendedBy == CharacterStats.DefenseType.Fortitude || damage.DefendedBy == CharacterStats.DefenseType.Will)
                        calcGM.AddFromStat(defenderStats, StatusEffect.ModifiedStat.EnemyFortitudeWillHitToGrazePercent, defenderStats.EnemyFortitudeWillHitToGrazePercent, "extra", 100f); // !! despite its name
                    str.Append(calcGM.Finish("Defender G->M %", true));

                    if (damage.DefendedBy == CharacterStats.DefenseType.Reflex)
                    {
                        var calcRefl = new BonusCalculator();
                        calcRefl.AddFromStat(defenderStats, StatusEffect.ModifiedStat.EnemyReflexGrazeToMissPercent, defenderStats.EnemyReflexGrazeToMissPercent, "", 100f);
                        str.Append(calcRefl.Finish("Defender G->M refl %", true));
                    }
                    break;
                case HitType.HIT:
                    var calcHG = new BonusCalculator();
                    calcHG.AddFromStat(defenderStats, StatusEffect.ModifiedStat.EnemyHitToGrazePercent, defenderStats.EnemyHitToGrazePercent, "", 100f);
                    if (damage.DefendedBy == CharacterStats.DefenseType.Deflect || damage.DefendedBy == CharacterStats.DefenseType.Reflex)
                        calcHG.AddFromStat(defenderStats, StatusEffect.ModifiedStat.EnemyDeflectReflexHitToGrazePercent, defenderStats.EnemyDeflectReflexHitToGrazePercent, "extra", 100f);
                    str.Append(calcHG.Finish("Defender H->G %", true));

                    if (damage.DefendedBy == CharacterStats.DefenseType.Reflex)
                    {
                        var calcRefl = new BonusCalculator();
                        calcRefl.AddFromStat(defenderStats, StatusEffect.ModifiedStat.EnemyReflexHitToGrazePercent, defenderStats.EnemyReflexHitToGrazePercent, "", 100f);
                        str.Append(calcRefl.Finish("Defender H->G refl %", true));
                    }
                    break;
                case HitType.CRIT:
                    var calcCH = new BonusCalculator();
                    calcCH.AddFromStat(defenderStats, StatusEffect.ModifiedStat.EnemyCritToHitPercent, defenderStats.EnemyCritToHitPercent, "", 100f);
                    str.Append(calcCH.Finish("Defender C->H %", true));
                    break;
            }
            if (hitType != damage.HitType)
            {
                str.Append($" => converted to {damage.HitType}!\n");
            }

            return str.ToString();
        }

        [NewMember]
        public static string DumpBaseDamage(DamageInfo damage, CharacterStats attackerStats, CharacterStats defenderStats)
        {
            float expectedMin = damage.Attack.DamageData.Minimum;
            float expectedMax = damage.Attack.DamageData.Maximum;

            StringBuilder str = new StringBuilder();
            str.Append($"Base damage: {expectedMin}-{expectedMax} {damage.Attack.DamageData.Type}");
            foreach (var proc in damage.Attack.DamageData.DamageProc)
                str.Append($" + {proc.PercentOfBaseDamage}% {proc.Type}");

            // min damage multiplier
            if (damage.Attack?.GetComponent<Weapon>() != null && attackerStats.WeaponDamageMinMult != 1f)
            {
                str.Append($" (x{attackerStats.WeaponDamageMinMult} min)"); // TODO: dump multiplicative effects
                expectedMin *= attackerStats.WeaponDamageMinMult;
            }

            float pctRangeReduction = damage.Attack is AttackMelee ? attackerStats.MeleeDamageRangePctIncreaseToMin : 0f; // TODO: dump effects
            if (pctRangeReduction != 0f)
            {
                str.Append($" (+{pctRangeReduction}% range to min)");
            }

            var minCCDEffect = attackerStats.FindFirstStatusEffectOfType(StatusEffect.ModifiedStat.DamageAlwaysMinimumAgainstCCD);
            if (minCCDEffect != null && (defenderStats.HasStatusEffectOfType(StatusEffect.ModifiedStat.SwapFaction) || defenderStats.HasStatusEffectOfType(StatusEffect.ModifiedStat.Confused)))
            {
                Faction enemyFaction = defenderStats.GetComponent<Faction>();
                Faction ownerFaction = attackerStats.GetComponent<Faction>();
                if (enemyFaction && ownerFaction && ownerFaction.CurrentTeam.GetRelationship(enemyFaction.OriginalTeamInstance) == Faction.Relationship.Friendly)
                {
                    str.Append($" (always min)");
                    expectedMax = expectedMin;
                }
            }

            var levelScaling = damage.Damage.LevelScaling;
            float levelMultiplier = CalcLevelAdjustmentMultiplicative(levelScaling, levelScaling.BaseDamageRatioAdjustment, attackerStats.ScaledLevel);
            if (levelMultiplier != 1f)
            {
                str.Append($" (x{levelMultiplier} scaling: {levelScaling.BaseDamageRatioAdjustment} per {levelScaling.LevelIncrement} levels after {levelScaling.BaseLevel})");
                expectedMin *= levelMultiplier;
                expectedMax *= levelMultiplier;
            }

            expectedMin += pctRangeReduction / 100f * (expectedMax - expectedMin);
            str.Append($"\nDamage roll: [A0A0A0][{expectedMin}-{expectedMax}] =[-] {damage.DamageBase}\n");
            // TODO: adjustment for traps - see AttackBase::OnImpact
            return str.ToString();
        }

        [NewMember]
        public static BonusCalculator CalculateDT(CharacterStats defenderStats, DamagePacket.DamageType dmgType)
        {
            var calcDT = new BonusCalculator();
            //var attackRanged = damage.Attack as AttackRanged;
            //bool veilPiercing = attackRanged ? attackRanged.VeilPiercing : false;
            if (dmgType < DamagePacket.DamageType.Count || dmgType == DamagePacket.DamageType.All)
            {
                // note: this is really similar to AddFromStat, except that TransferDT decrements value on target
                float baseDT = defenderStats.DamageThreshhold[(int)dmgType];
                foreach (var eff in defenderStats.ActiveStatusEffects)
                {
                    if (eff.Applied && eff.Params.AffectsStat == StatusEffect.ModifiedStat.TransferDT && (eff.Params.DmgType == dmgType || eff.Params.DmgType == DamagePacket.DamageType.All))
                    {
                        calcDT.Add(eff, -1f);
                        baseDT += eff.CurrentAppliedValue;
                    }
                }
                calcDT.Add(baseDT, "UNACCOUNTED Base");
            }

            foreach (var eff in defenderStats.ActiveStatusEffects)
            {
                if (!eff.Applied)
                    continue;

                // see StatusEffect.AdjustDamageThreshold
                switch (eff.Params.AffectsStat)
                {
                    case StatusEffect.ModifiedStat.DamageThreshhold:
                        if (eff.Params.DmgType == DamagePacket.DamageType.All || eff.Params.DmgType == dmgType)
                        {
                            float value = eff.CurrentAppliedValue;
                            Armor originArmor = (eff.Slot != Equippable.EquipmentSlot.None && eff.EquipmentOrigin) ? eff.EquipmentOrigin.GetComponent<Armor>() : null;
                            if (originArmor)
                                value = originArmor.AdjustForDamageType(value, dmgType);
                            calcDT.Add(value, eff.GetDisplayName());
                        }
                        break;
                    case StatusEffect.ModifiedStat.BonusDTFromWounds:
                        var woundsTrait = defenderStats.FindWoundsTrait();
                        int numWounds = woundsTrait ? defenderStats.CountStatusEffects(woundsTrait.StatusEffects[0].Tag) : 0;
                        if (numWounds > 0)
                            calcDT.Add(eff.CurrentAppliedValue * numWounds, eff.GetDisplayName());
                        break;
                    case StatusEffect.ModifiedStat.AddDamageTypeImmunity:
                        if (eff.Params.DmgType == DamagePacket.DamageType.All || eff.Params.DmgType == dmgType)
                            calcDT.Add(Single.PositiveInfinity, eff.GetDisplayName());
                        break;
                }
            }

            // Equipment.CalculateDT
            var defenderItems = defenderStats.GetComponent<Equipment>()?.CurrentItems;
            if (defenderItems != null)
            {
                var bestSlot = Equippable.EquipmentSlot.Count;
                BonusCalculator bestItemDT = null;
                float bestDT = 0f; // multiplied by adjustment, but not by defender effects!
                for (int i = 0; i < (int)Equippable.EquipmentSlot.Count; ++i)
                {
                    var iSlot = (Equippable.EquipmentSlot)i;
                    var item = defenderItems.GetItemInSlot(iSlot)?.GetComponent<Armor>();
                    if (item == null)
                        continue;

                    //float itemDT = item.CalculateDT(dmgType, defenderStats.BonusDTFromArmor, defenderStats.gameObject);
                    var calcItemDT = new BonusCalculator();
                    calcItemDT.Add(item.DamageThreshhold, "Base");
                    calcItemDT.AddFromStat(defenderStats, StatusEffect.ModifiedStat.BonusDTFromArmor, defenderStats.BonusDTFromArmor);
                    float levelAdjustment = CalcLevelAdjustmentAdditive(item.LevelScaling, item.LevelScaling.DtAdjustment, defenderStats.ScaledLevel);
                    calcItemDT.Add(levelAdjustment, $"Scaling: {item.LevelScaling.DtAdjustment} per {item.LevelScaling.LevelIncrement} levels after {item.LevelScaling.BaseLevel}");

                    float itemDT = item.AdjustForDamageType(calcItemDT.CurTotal(), dmgType);
                    if (itemDT > bestDT)
                    {
                        bestSlot = iSlot;
                        bestItemDT = calcItemDT;
                        bestDT = itemDT;
                    }
                }

                if (bestDT != 0f)
                {
                    var calcEff = new BonusCalculator();
                    calcEff.Add(1f, "Base");
                    foreach (var eff in defenderStats.ActiveStatusEffects)
                        if (IsItemDTEffect(eff, defenderStats))
                            calcEff.Add(eff, 1f, -1f);

                    float finalDT = bestDT * calcEff.CurTotal();
                    string desc = bestItemDT.Finish("").TrimEnd(null);
                    if (finalDT != bestItemDT.CurTotal())
                    {
                        desc = $"({desc})";
                        if (bestDT != bestItemDT.CurTotal())
                            desc += $" * {bestDT / bestItemDT.CurTotal() * 100f}%";
                        if (bestDT != finalDT)
                            desc += $" * ({calcEff.Finish("Effects").TrimEnd(null)})";
                    }

                    calcDT.Add(finalDT, $"{bestSlot}: {desc}");
                }
            }
            return calcDT;
        }

        [NewMember]
        public static BonusCalculator CalculateDTBypass(DamageInfo damage, CharacterStats attackerStats, DamagePacket.DamageType dmgType)
        {
            var calc = new BonusCalculator();
            calc.AddFromStat(attackerStats, StatusEffect.ModifiedStat.DTBypass, attackerStats.DTBypass);
            if (damage.Attack is AttackRanged)
                calc.AddFromStat(attackerStats, StatusEffect.ModifiedStat.RangedDTBYpass, attackerStats.RangedDTBypass, "ranged");
            if (damage.Attack is AttackMelee)
                calc.AddFromStat(attackerStats, StatusEffect.ModifiedStat.MeleeDTBypass, attackerStats.MeleeDTBypass, "melee");
            // add Attack.DTBypassTotal
            calc.Add(damage.Attack.DTBypass, "Inherent");
            if (damage.Attack.AbilityOrigin != null)
            {
                var mods = damage.Attack.AbilityOrigin.AbilityMods;
                for (int i = 0; i < mods.Count; i++)
                    if (mods[i].Type == AbilityMod.AbilityModType.AttackDTBypass)
                        calc.Add(mods[i].Value, "Ability mod"); // e.g.: Blast with Penetrating Blast talent
            }
            return calc;
        }

        [NewMember]
        public static string DumpDTDR(DamageInfo damage, CharacterStats attackerStats, CharacterStats defenderStats, float amount, DamagePacket.DamageType dmgType, out float adjusted, float multiplier = 1f)
        {
            if (dmgType == DamagePacket.DamageType.Raw)
            {
                adjusted = amount;
                return ""; // raw damage is never decreased
            }

            var calcDT = CalculateDT(defenderStats, dmgType);
            if (float.IsPositiveInfinity(calcDT.CurTotal()))
            {
                adjusted = 0;
                return $"{dmgType}: immune!\n";
            }
            var calcDTBypass = CalculateDTBypass(damage, attackerStats, dmgType);

            // calculate DR
            var defenderEquipment = defenderStats.GetComponent<Equipment>();
            float dr = defenderEquipment ? defenderEquipment.CalculateDR(dmgType) : 0f; // TODO: elaborate! (but it doesn't seem to be used anywhere...)

            string report = calcDT.Finish($"{dmgType} DR", true); // note that game itself uses term "DR" for additive term, which is called DT in sources
            report += calcDTBypass.Finish($"{dmgType} Bypass", true);
            if (dr != 0f)
                report += $"{dmgType} DR percent: {dr}%\n";

            var remainingDT = Mathf.Max(0f, calcDT.CurTotal() - calcDTBypass.CurTotal());
            remainingDT *= multiplier;
            dr *= multiplier;
            dr = Mathf.Clamp(dr / 100f, 0f, 1f);

            var postDT = amount - remainingDT;
            postDT *= 1f - dr;

            // calculate minimal dmg
            float minDmgPct = dmgType == DamagePacket.DamageType.Crush ? AttackData.Instance.MinCrushingDamagePercent : AttackData.Instance.MinDamagePercent;
            float minDmg = amount * minDmgPct / 100f;
            minDmg += defenderStats.DamageMinBonus; // TODO: elaborate!
            minDmg = Mathf.Max(0f, minDmg);

            report += $"{dmgType} final dmg";
            if (multiplier != 1f)
                report += $" (x{multiplier} DR)";
            report += $": [A0A0A0]({amount} - {remainingDT})";
            if (dr != 0f)
                report += $" * (1 - {dr})";
            report += $" =[-] {postDT}";
            if (postDT < minDmg)
            {
                postDT = minDmg;
                report += $" [A0A0A0](clamped to min {minDmg} = {minDmgPct}%";
                if (defenderStats.DamageMinBonus != 0f)
                    report += $" + {defenderStats.DamageMinBonus}";
                report += ")[-]";
            }

            adjusted = Mathf.Max(0f, postDT);
            report += "\n";
            return report;
        }

        [NewMember]
        public static string DumpDamage(DamageInfo damage, CharacterStats attackerStats, CharacterStats defenderStats)
        {
            if (damage.IsMiss)
                return "";

            var calcMul = new BonusCalculator();
            calcMul.Add(1f, "Base");
            var calcAdd = new BonusCalculator(); // these require might mod application
            var strProcs = new StringBuilder();
            float finishingAdd = 0f;

            var defenderHealth = defenderStats.GetComponent<Health>();

            var minCCDEffect = attackerStats.FindFirstStatusEffectOfType(StatusEffect.ModifiedStat.DamageAlwaysMinimumAgainstCCD);
            if (minCCDEffect != null && (defenderStats.HasStatusEffectOfType(StatusEffect.ModifiedStat.SwapFaction) || defenderStats.HasStatusEffectOfType(StatusEffect.ModifiedStat.Confused)))
            {
                Faction enemyFaction = defenderStats.GetComponent<Faction>();
                Faction ownerFaction = attackerStats.GetComponent<Faction>();
                if (enemyFaction && ownerFaction && ownerFaction.CurrentTeam.GetRelationship(enemyFaction.OriginalTeamInstance) == Faction.Relationship.Friendly)
                {
                    calcMul.Add(minCCDEffect.ParamsValue() - 1f, minCCDEffect.GetDisplayName());
                }
            }

            calcMul.Add(damage.Attack.DamageMultiplier - 1f, "WTF inherent?"); // used for traps?
            calcMul.Add(damage.Attack.BounceFactor(damage.Self) - 1f, $"Bounce #{damage.Attack.GetBounceCount(damage.Self)}");

            float mightMod = damage.Attack && damage.Attack.IgnoreCharacterStats ? 1f : attackerStats.StatDamageHealMultiplier;
            calcMul.Add(mightMod - 1f, $"Might {attackerStats.Might}");

            switch (damage.HitType)
            {
                case HitType.CRIT:
                    calcMul.Add(CharacterStats.CritMultiplier - 1f, "Crit");
                    calcMul.AddFromStat(attackerStats, StatusEffect.ModifiedStat.CritHitMultiplierBonus, attackerStats.CritHitDamageMultiplierBonus, "crit");
                    if (defenderHealth && defenderHealth.StaminaPercentage < 0.1f)
                        calcMul.AddFromStat(attackerStats, StatusEffect.ModifiedStat.BonusCritHitMultiplierEnemyBelow10Percent, attackerStats.CritHitDamageMultiplierBonusEnemyBelow10Percent, "crit near death");
                    break;
                case HitType.GRAZE:
                    calcMul.Add(CharacterStats.GrazeMultiplier - 1f, "Graze");
                    break;
            }

            // weapon specialization
            Weapon attackWeapon = damage.Attack.GetComponent<Weapon>();
            if (attackWeapon)
            {
                if (!attackWeapon.UniversalType)
                {
                    foreach (var ability in attackerStats.ActiveAbilities)
                    {
                        var weaponSpec = ability as WeaponSpecialization;
                        if (weaponSpec && WeaponSpecialization.WeaponSpecializationApplies(damage.Attack, weaponSpec.SpecializationCategory))
                            calcMul.Add(weaponSpec.BonusDamageMult - 1f, ability.DisplayName.GetText());
                    }
                }
                else
                {
                    float[] perCategoryBonuses = new float[(int)WeaponSpecializationData.Category.Count];
                    foreach (var ability in attackerStats.ActiveAbilities)
                    {
                        var weaponSpec = ability as WeaponSpecialization;
                        if (weaponSpec)
                            perCategoryBonuses[(int)weaponSpec.SpecializationCategory] += weaponSpec.BonusDamageMult - 1f;
                    }
                    float best = perCategoryBonuses[0];
                    int bestIdx = 0;
                    for (int i = 1; i < perCategoryBonuses.Length; ++i)
                    {
                        if (perCategoryBonuses[i] > best)
                        {
                            best = perCategoryBonuses[i];
                            bestIdx = i;
                        }
                    }
                    calcMul.Add(best, $"Weapon spec {(WeaponSpecializationData.Category)bestIdx}");
                }
            }

            // << everything below isn't accounted into damage when function is called >>

            // status effects (excluding on-hit)
            foreach (var eff in attackerStats.ActiveStatusEffects)
            {
                if (IsDamageAddEffect(eff, damage.Attack, attackerStats, defenderStats))
                    calcAdd.Add(eff); // we apply might mod later!
                if (IsDamageMulEffect(eff, damage.Attack, attackerStats, defenderStats))
                    calcMul.Add(eff, 1f, -1f);
            }

            // bonus damage
            for (int i = 0; i < attackerStats.BonusDamage.Length; ++i)
            {
                if (attackerStats.BonusDamage[i] != 0f)
                {
                    strProcs.Append($" + {attackerStats.BonusDamage[i]}% {(DamagePacket.DamageType)i} (bonus)"); // TODO: dump effects
                }
            }

            // bonus damage per type (TODO: dump effects!)
            float bonusPerTypePct = ((int)damage.Damage.Type < attackerStats.BonusDamagePerType.Length) ? attackerStats.BonusDamagePerType[(int)damage.Damage.Type] : 0f;
            if (damage.Damage.Type == DamagePacket.DamageType.All)
            {
                foreach (float bonus in attackerStats.BonusDamagePerType)
                    bonusPerTypePct += bonus;
            }
            calcMul.Add(bonusPerTypePct / 100f, $"Bonus {damage.Damage.Type} damage");

            // bonus vs race (TODO: dump effects!)
            float bonusVsRacePct = ((int)defenderStats.CharacterRace < attackerStats.BonusDamagePerRace.Length) ? attackerStats.BonusDamagePerRace[(int)defenderStats.CharacterRace] : 0f;
            calcMul.Add(bonusVsRacePct / 100f, $"Bonus against {defenderStats.CharacterRace}");

            // bonus for ranged weapon in close combat
            var equippable = damage.Attack?.GetComponent<Equippable>();
            if (equippable && equippable is Weapon && !(damage.Attack is AttackMelee) && !attackerStats.IsEnemyDistant(defenderStats.gameObject))
            {
                calcMul.Add(attackerStats.BonusRangedWeaponCloseEnemyDamageMult - 1f, "Ranged weapon in close range"); // TODO: dump multiplicative effects
            }

            // weapon damage procs
            if (equippable)
            {
                foreach (var itemMod in equippable.AttachedItemMods)
                {
                    foreach (var proc in itemMod.Mod.DamageProcs)
                    {
                        if (proc == null)
                            continue;
                        strProcs.Append($" + {proc.PercentOfBaseDamage}% {proc.Type} ({itemMod.Mod.DisplayName.GetText()})");
                    }
                }
            }

            if (attackerStats.gameObject != defenderStats.gameObject)
            {
                // on-hit status effects (NOTE: these have to be calculated here and in correct order, if we want to calculate finishing blows correctly)
                var attackerStatusEffects = attackerStats.ActiveStatusEffects;
                for (int i = attackerStatusEffects.Count - 1; i >= 0; i--)
                {
                    var eff = attackerStatusEffects[i];
                    if (!eff.Applied)
                        continue;

                    // see StatusEffect.WhenHits
                    switch (eff.Params.AffectsStat)
                    {
                        case StatusEffect.ModifiedStat.BonusMeleeDamageFromWounds:
                            var woundsTrait = attackerStats.FindWoundsTrait();
                            int numWounds = woundsTrait ? attackerStats.CountStatusEffects(woundsTrait.StatusEffects[0].Tag) : 0;
                            if (damage.Attack is AttackMelee && numWounds > 0)
                                strProcs.Append($" + {eff.CurrentAppliedValue * numWounds}% {eff.Params.DmgType} ({eff.GetDisplayName()})");
                            break;
                        case StatusEffect.ModifiedStat.BonusDamageMultAtLowStamina:
                            if (attackerStats.GetComponent<Health>().StaminaPercentage <= eff.ParamsExtraValue())
                                calcMul.Add(eff, 1f, -1f);
                            break;
                        case StatusEffect.ModifiedStat.BonusDamageMultOnKDSFTarget:
                            if (defenderStats.HasStatusEffectOfType(StatusEffect.ModifiedStat.KnockedDown) || defenderStats.HasStatusEffectOfType(StatusEffect.ModifiedStat.Stunned) || defenderStats.HasStatusEffectFromAffliction(AfflictionData.Flanked))
                                calcMul.Add(eff, 1f, -1f);
                            break;
                        case StatusEffect.ModifiedStat.BonusDamageMult:
                            if (eff.Params.DmgType == DamagePacket.DamageType.All || eff.Params.DmgType == damage.Damage.Type) // interestingly, it won't be applied if we later find out secondary damage type is better
                                calcMul.Add(eff, 1f, -1f);
                            break;
                        case StatusEffect.ModifiedStat.BeamDamageMult:
                            if (damage.Attack is AttackBeam)
                                calcMul.Add((eff.CurrentAppliedValue - 1f) * eff.m_intervalCount, eff.GetDisplayName());
                            break;
                        case StatusEffect.ModifiedStat.DamageToDOT:
                            return $"Converted to DoT: ({damage.DamageBase} * {calcMul.CurTotal()} + {calcAdd.CurTotal() * mightMod + finishingAdd}) * {eff.CurrentAppliedValue} = {(damage.DamageBase * calcMul.CurTotal() + calcAdd.CurTotal() * mightMod + finishingAdd) * eff.CurrentAppliedValue}\n";
                        case StatusEffect.ModifiedStat.BonusDamageMultIfTargetHasDOT:
                            if (defenderStats.CountDOTs() > 0)
                                calcMul.Add(eff, 1f, -1f);
                            break;
                        case StatusEffect.ModifiedStat.BonusDamageMultOnLowStaminaTarget:
                            if (defenderHealth && defenderHealth.StaminaPercentage < 0.25f)
                                calcMul.Add(eff, 1f, -1f);
                            break;
                        case StatusEffect.ModifiedStat.BonusDamageProc:
                            if (damage.Attack && damage.Attack.IsAutoAttack())
                            {
                                strProcs.Append($" + {eff.CurrentAppliedValue}% {eff.Params.DmgType} ({eff.GetDisplayName()})");
                                if (eff.AbilityOrigin && eff.AbilityOrigin is Wildstrike)
                                    strProcs.Append($" + {(attackerStats.WildstrikeDamageMult - 1f) * 100f}% {eff.Params.DmgType} (Wildstrike extra)"); // TODO: elaborate? it seems to be quite straightforward (note that this thing double-dips on improved wildstrike)
                            }
                            break;
                        case StatusEffect.ModifiedStat.DamageMultByRace:
                            if (defenderStats.CharacterRace == eff.Params.RaceType)
                                calcMul.Add(eff, 1f, -1f);
                            break;
                        case StatusEffect.ModifiedStat.SpellDamageMult:
                            if (damage.Attack != null && damage.Attack.AbilityOrigin is GenericSpell && (eff.Params.DmgType == DamagePacket.DamageType.All || eff.Params.DmgType == damage.Damage.Type))
                                calcMul.Add(eff, 1f, -1f);
                            break;
                        case StatusEffect.ModifiedStat.BonusDamageMultOnFlankedTarget:
                            if (defenderStats.HasStatusEffectFromAffliction(AfflictionData.Flanked))
                                calcMul.Add(eff, 1f, -1f);
                            break;
                        case StatusEffect.ModifiedStat.ApplyFinishingBlowDamage:
                            FinishingBlow finishing = null;
                            foreach (var ab in attackerStats.ActiveAbilities)
                                if ((finishing = ab as FinishingBlow) != null)
                                    break;
                            if (!finishing)
                                finishing = eff.AbilityOrigin as FinishingBlow;
                            if (finishing)
                            {
                                calcMul.Add(finishing.BaseHitDamageMult - 1f, eff.GetDisplayName());
                                float curRatio = defenderHealth.StaminaPercentage;
                                float thrRatio = finishing.BonusDamageHealthPctThreshhold / 100f;
                                if (curRatio < thrRatio)
                                {
                                    // NOTE: this is really ugly, it's very order dependent =/
                                    float overThreshold = thrRatio - curRatio;
                                    float multPerPercent = finishing.BonusDamagePctPerHP + finishing.GatherAbilityModSum(AbilityMod.AbilityModType.FinishingBlowDamagePercentAdjustment);
                                    float damageSoFar = damage.DamageBase * calcMul.CurTotal() + calcAdd.CurTotal() * mightMod + finishingAdd;
                                    finishingAdd += multPerPercent * damageSoFar * overThreshold * 100f;
                                }
                                var finishingOwnerStats = finishing.Owner.GetComponent<CharacterStats>();
                                if (finishingOwnerStats)
                                    calcMul.Add(finishingOwnerStats.FinishingBlowDamageMult - 1f, "Finishing Blow mult"); // TODO: dump effects
                            }
                            break;
                        case StatusEffect.ModifiedStat.BonusDamageMultWithImplements:
                            var weapon = damage.Attack?.GetComponent<Weapon>();
                            if (weapon && weapon.IsImplement)
                                calcMul.Add(eff, 1f, -1f);
                            break;
                    }
                }

                // on-hit status effects for defender (don't care about order) (TODO: reflects)
                foreach (var eff in defenderStats.ActiveStatusEffects)
                {
                    if (!eff.Applied)
                        continue;

                    switch (eff.Params.AffectsStat)
                    {
                        case StatusEffect.ModifiedStat.IncomingCritDamageMult:
                            if (damage.IsCriticalHit && (eff.Params.DmgType == DamagePacket.DamageType.All || eff.Params.DmgType == damage.Damage.Type))
                                calcMul.Add(eff, 1f, -1f);
                            break;
                        case StatusEffect.ModifiedStat.IncomingDamageMult:
                            if (eff.Params.DmgType == DamagePacket.DamageType.All || eff.Params.DmgType == damage.Damage.Type)
                                calcMul.Add(eff, 1f, -1f);
                            break;
                    }
                }
            }

            if (defenderHealth)
            {
                // absorption check goes here
                if (damage.Attack is AttackAOE)
                    calcMul.Add(defenderStats.HostileAOEDamageMultiplier - 1f, "Hostile AOE"); // TODO: dump multiplicative effects
            }

            string report = "";
            if (damage.DamageBase != 0f)
                report += calcMul.Finish("Damage multiplier");
            report += calcAdd.Finish("Extra damage", true);
            var damagePreDRDT = damage.DamageBase * calcMul.CurTotal() + calcAdd.CurTotal() * mightMod + finishingAdd;
            if (damagePreDRDT != 0f)
            {
                report += $"Damage before DR = [A0A0A0]{damage.DamageBase} * {calcMul.CurTotal()}";
                if (calcAdd.CurTotal() != 0f)
                    report += $" + {calcAdd.CurTotal()} * {mightMod}";
                if (finishingAdd != 0f)
                    report += $" + {finishingAdd} finishing";
                report += $" =[-] {damagePreDRDT}\n";

                if (strProcs.Length > 0)
                {
                    report += strProcs.ToString();
                    report += "\n";
                }
            }

            if (damage.Damage.BestOfType == DamagePacket.DamageType.None || damage.Damage.BestOfType == damage.Damage.Type)
            {
                // simple DR/DT adjustment
                float damagePostDT;
                report += DumpDTDR(damage, attackerStats, defenderStats, damagePreDRDT, damage.Damage.Type, out damagePostDT);
            }
            else
            {
                // find best of two
                float damagePostDT, damagePostDTAlt;
                report += DumpDTDR(damage, attackerStats, defenderStats, damagePreDRDT, damage.Damage.Type, out damagePostDT);
                report += DumpDTDR(damage, attackerStats, defenderStats, damagePreDRDT, damage.Damage.BestOfType, out damagePostDTAlt);
            }

            // TODO: difficulty damage modifier (story time only, who cares)
            // TODO: dump damage calculation for procs: 1/4 DT/DR, no bypass, base = (pre-DT main damage * per-type bonus)

            return report;
        }
    }

    // store detailed attack resolution string and add it to the hovered text
    [ModifiesType]
    public class mod_DamageInfo : DamageInfo
    {
        [NewMember]
        public string m_detailedInfo;

        [NewMember]
        [DuplicatesBody("GetToHitReport")]
        public void ori_GetToHitReport(GameObject attacker, GameObject defender, StringBuilder stringBuilder) { }

        [ModifiesMember("GetToHitReport")]
        public void mod_GetToHitReport(GameObject attacker, GameObject defender, StringBuilder stringBuilder)
        {
            stringBuilder.Append(string.IsNullOrEmpty(m_detailedInfo) ? "No detailed info gathered!\n" : m_detailedInfo);
            ori_GetToHitReport(attacker, defender, stringBuilder);
        }

        [NewMember]
        public void add_BuildDamageReport(CharacterStats attackerStats, CharacterStats defenderStats)
        {
            try
            {
                m_detailedInfo = Utils.DumpAccuracy(this, attackerStats, defenderStats);
                m_detailedInfo += Utils.DumpDefense(this, attackerStats, defenderStats);
                m_detailedInfo += Utils.DumpHitAdjustment(this, attackerStats, defenderStats);
                if (!IsMiss && Attack?.DamageData?.Maximum > 0f)
                {
                    m_detailedInfo += Utils.DumpBaseDamage(this, attackerStats, defenderStats);
                    m_detailedInfo += Utils.DumpDamage(this, attackerStats, defenderStats);
                }
            }
            catch (Exception e)
            {
                Console.AddMessage($"BetterCombatLog: exception {e.Message}", e.StackTrace);
            }
        }
    }

    // inject generation of detailed attack resolution strings
    [ModifiesType]
    public class mod_CharacterStats : CharacterStats
    {
        [ModifiesAccessibility]
        public new int NearestAllyWithSharedTarget(GameObject enemy)
        {
            return -1;
        }

        [NewMember]
        public int add_GetMiscDefenseAdjustment(DefenseType defenseType, AttackBase attack, GameObject enemy, bool isSecondary)
        {
            // TODO: this is a bit scary...
            int rv = 0;
            if (this.OnDefenseAdjustment != null)
                this.OnDefenseAdjustment(defenseType, attack, enemy, isSecondary, ref rv);
            return rv;
        }

        [NewMember]
        [DuplicatesBody("ComputeSecondaryAttack")]
        public DamageInfo ori_ComputeSecondaryAttack(AttackBase attack, GameObject enemy, CharacterStats.DefenseType defendedBy)
        {
            return null;
        }

        [ModifiesMember("ComputeSecondaryAttack")]
        public DamageInfo mod_ComputeSecondaryAttack(AttackBase attack, GameObject enemy, CharacterStats.DefenseType defendedBy)
        {
            var rv = ori_ComputeSecondaryAttack(attack, enemy, defendedBy);
            var enemyStats = enemy?.GetComponent<CharacterStats>();
            if (enemyStats != null)
                (rv as mod_DamageInfo).add_BuildDamageReport(this, enemyStats);
            return rv;
        }

        [ModifiesMember]
        public new void AdjustDamageDealt(GameObject enemy, DamageInfo damage, bool testing)
        {
            float mightMod = (damage.Attack && damage.Attack.IgnoreCharacterStats) ? 1f : this.StatDamageHealMultiplier;
            damage.DamageMult(mightMod);

            if (!testing && this.OnPreDamageDealt != null)
            {
                this.OnPreDamageDealt(base.gameObject, new CombatEventArgs(damage, base.gameObject, enemy));
            }
            if (!testing && this.OnAddDamage != null)
            {
                this.OnAddDamage(base.gameObject, new CombatEventArgs(damage, base.gameObject, enemy));
            }

            CharacterStats enemyStats = enemy.GetComponent<CharacterStats>();
            if (enemyStats == null)
                return;

            int attackRoll = enemyStats.GetAttackerToHitRollOverride(OEIRandom.DieRoll(100));
            int accuracy = this.CalculateAccuracy(damage.Attack, enemy);
            bool immune = enemyStats.CalculateIsImmune(damage.DefendedBy, damage.Attack, base.gameObject);
            int defense = enemyStats.CalculateDefense(damage.DefendedBy, damage.Attack, base.gameObject);

            if (damage.DefendedBy != CharacterStats.DefenseType.None)
            {
                this.ComputeHitAdjustment(attackRoll + accuracy - defense, enemyStats, damage);
                if (!testing && this.OnAttackRollCalculated != null)
                {
                    this.OnAttackRollCalculated(base.gameObject, new CombatEventArgs(damage, base.gameObject, enemy));
                }

                if (damage.IsCriticalHit)
                {
                    float critMod = this.CriticalHitMultiplier;
                    Health enemyHealth = enemy.GetComponent<Health>();
                    if (enemyHealth != null && enemyHealth.StaminaPercentage < 0.1f)
                        critMod += this.CritHitDamageMultiplierBonusEnemyBelow10Percent;
                    damage.DamageMult(critMod);
                }
                else if (damage.IsGraze)
                {
                    damage.DamageMult(CharacterStats.GrazeMultiplier);
                }
                else if (damage.IsMiss)
                {
                    damage.DamageMult(0f);
                }
            }

            WeaponSpecializationData.AddWeaponSpecialization(this, damage);
            damage.AccuracyRating = accuracy;
            damage.DefenseRating = defense;
            damage.Immune = immune;
            damage.RawRoll = attackRoll;

            // injection point (TODO: just add to OnAdjustCritGrazeMiss?)
            (damage as mod_DamageInfo).add_BuildDamageReport(this, enemyStats);

            if (!testing && damage.Immune)
            {
                UIHealthstringManager.Instance.ShowNotice(GUIUtils.GetText(2188), enemy, 1f);
                if (this.IsPartyMember)
                {
                    SoundSet.TryPlayVoiceEffectWithLocalCooldown(base.gameObject, SoundSet.SoundAction.TargetImmune, SoundSet.s_LongVODelay, false);
                }
            }
            if (!testing && this.OnAdjustCritGrazeMiss != null)
            {
                this.OnAdjustCritGrazeMiss(base.gameObject, new CombatEventArgs(damage, base.gameObject, enemy));
            }

            if (!damage.IsMiss)
            {
                for (int i = 0; i < this.ActiveStatusEffects.Count; i++)
                {
                    if (this.ActiveStatusEffects[i].Applied)
                    {
                        damage.DamageAdd(this.ActiveStatusEffects[i].AdjustDamage(base.gameObject, enemy, damage.Attack) * mightMod);
                        damage.DamageMult(this.ActiveStatusEffects[i].AdjustDamageMultiplier(base.gameObject, enemy, damage.Attack));
                    }
                }
                for (int j = 0; j < this.BonusDamage.Length; j++)
                {
                    if (this.BonusDamage[j] != 0f)
                    {
                        DamagePacket.DamageProcType damageProcType = new DamagePacket.DamageProcType((DamagePacket.DamageType)j, this.BonusDamage[j]);
                        damage.Damage.DamageProc.Add(damageProcType);
                    }
                }
                this.AddBonusDamagePerType(damage);
                this.AddBonusDamagePerRace(damage, enemyStats);
                if (damage.Attack != null)
                {
                    Equippable equippable = damage.Attack.GetComponent<Equippable>();
                    if (equippable)
                    {
                        if (equippable is Weapon && !(damage.Attack is AttackMelee) && enemy != null && !this.IsEnemyDistant(enemy))
                        {
                            damage.DamageMult(this.BonusRangedWeaponCloseEnemyDamageMult);
                        }
                        equippable.ApplyItemModDamageProcs(damage);
                    }
                }
            }

            this.ComputeInterrupt(enemyStats, damage);
            if (!testing && this.IsPartyMember)
            {
                if (enemyStats)
                {
                    enemyStats.RevealDefense(damage.DefendedBy);
                    enemyStats.RevealDT(damage.Damage.Type);
                    foreach (DamagePacket.DamageProcType damageProc in damage.Damage.DamageProc)
                    {
                        enemyStats.RevealDT(damageProc.Type);
                    }
                }
                if (damage.DefenseRating >= damage.AccuracyRating + 50 || damage.Immune)
                {
                    GameState.AutoPause(AutoPauseOptions.PauseEvent.ExtraordinaryDefence, base.gameObject, enemy, null);
                    TutorialManager.STriggerTutorialsOfTypeFast(TutorialManager.ExclusiveTriggerType.PARTYMEM_GETS_DEFENSE_TOO_HIGH);
                }
            }
            if (!testing && this.OnPostDamageDealt != null)
            {
                this.OnPostDamageDealt(base.gameObject, new CombatEventArgs(damage, base.gameObject, enemy));
            }
        }
    }

    // allow colorizing combat log tooltips
    [ModifiesType]
    public class mod_UIConsoleEntry : UIConsoleEntry
    {
        [ModifiesMember]
        private new void OnColliderTooltip(GameObject sender, bool over)
        {
            if (string.IsNullOrEmpty(this.m_Message.m_verbosemessage))
            {
                this.m_GlossaryEnabledLabel.OnColliderTooltip(sender, over);
            }
            else if (!over)
            {
                UICombatLogTooltip.GlobalHide();
            }
            else
            {
                // do not strip symbols and pass verbose string as-is
                UICombatLogTooltip.GlobalShow(this.Label, this.m_Message.m_verbosemessage);
            }
        }
    }

    [ModifiesType]
    public class mod_UICombatLogTooltip : UICombatLogTooltip
    {
        [ModifiesMember]
        public override void SetText(string text)
        {
            this.Label.supportEncoding = true;
            this.Label.text = text;
        }
    }
}
