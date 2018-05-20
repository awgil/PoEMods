using System;
using Patchwork.Attributes;
using UnityEngine;

namespace CombatDetails
{
    // utility for debugging slowdowns (I think I fixed them by processing only BigTooltip)
    [NewType]
    public static class zDebug
    {
        [NewMember] public static float LastTS;
        [NewMember] public static int LastCount;

        [NewMember]
        public static void tick()
        {
            float ts = TimeController.Instance.RealtimeSinceStartupThisFrame;
            if (LastTS != ts)
            {
                if (LastCount > 2)
                    Console.AddMessage($"{LastCount} tooltip updates in a frame");
                LastTS = ts;
                LastCount = 0;
            }
            LastCount++;
        }
    }

    // make some fields public
    [ModifiesType]
    public class mod_Attack : AI.Achievement.Attack
    {
        [ModifiesAccessibility] public new float m_animDuration;
        [ModifiesAccessibility] public new float m_totalTime;
    }

    [ModifiesType]
    public class mod_ReloadWeapon : AI.Achievement.ReloadWeapon
    {
        [ModifiesAccessibility] public new AttackFirearm m_firearm;
        [ModifiesAccessibility] public new float m_reloadTime;
        [ModifiesAccessibility] public new float m_speedMultiplier;
    }

    // add extra timing data to the tooltip
    [ModifiesType]
    public class mod_UIMapTooltip : UIMapTooltip
    {
        [NewMember]
        private void add_InjectActionInfo()
        {
            // we only care about "big" tooltip (one appearing in top-left corner in combat)
            if (this != UIMapTooltipManager.Instance.BigTooltip)
                return;

            // DEBUG slowdowns
            zDebug.tick();

            AIController ai = this.Target.GetComponent<AIController>();
            if (this.SelectedCharacter != null && ai != null && ai.StateManager != null)
            {
                if (this.SelectedCharacter.RecoveryTimer > 0f)
                {
                    string color = this.SelectedCharacter.HasStatusEffectThatPausesRecoveryTimer() ? "FF0000" : "FFFFFF";
                    this.HealthLabel.text += $"\nRecovery: [{color}]{this.SelectedCharacter.RecoveryTimer.ToString("#0.0")} sec / {this.SelectedCharacter.TotalRecoveryTime.ToString("#0.0")} sec[-]";
                    this.HealthLabel.alpha = 1f;
                }

                AIState currentState = ai.StateManager.CurrentState;
                if (currentState is AI.Achievement.HitReact || currentState is AI.Achievement.PathToPosition)
                    currentState = ai.StateManager.QueuedState;

                var attackState = currentState as AI.Achievement.Attack;
                if (attackState != null && attackState.CurrentAttack != null)
                {
                    string color = attackState.CurrentAttack.CanCancel ? "FFFFFF" : "FF0000";
                    var ability = attackState.CurrentAttack.AbilityOrigin;
                    if (ability != null)
                    {
                        this.HealthLabel.text += $"\nCasting: [{color}]{ability.DisplayName.GetText()}[-]";
                    }
                    else if (attackState.m_animDuration <= 0f)
                    {
                        this.HealthLabel.text += $"\nAttacking: <idle>";
                    }
                    else
                    {
                        float rem = attackState.m_animDuration - attackState.m_totalTime;
                        this.HealthLabel.text += $"\nAttacking: [{color}]{rem.ToString("#0.0")} sec / {attackState.m_animDuration.ToString("#0.0")} sec[-]";
                    }
                    this.HealthLabel.alpha = 1f;
                }

                var reloadState = currentState as AI.Achievement.ReloadWeapon;
                if (reloadState != null)
                {
                    float max = reloadState.m_firearm.ReloadTime / reloadState.m_speedMultiplier;
                    float rem = (reloadState.m_firearm.ReloadTime - reloadState.m_reloadTime) / reloadState.m_speedMultiplier;
                    this.HealthLabel.text += $"\nReloading: {rem.ToString("#0.0")} sec / {max.ToString("#0.0")} sec";
                    this.HealthLabel.alpha = 1f;
                }

                if (currentState is AI.Player.Attack || currentState is AI.Player.TargetedAttack || currentState is AI.Plan.ApproachTarget)
                {
                    var plannedAbility = currentState.CurrentAbility;
                    if (plannedAbility == null)
                    {
                        var partyAI = ai as PartyMemberAI;
                        plannedAbility = partyAI?.QueuedAbility;
                    }
                    string plannedString = plannedAbility ? plannedAbility.DisplayName.GetText() : "attack";
                    this.HealthLabel.text += $"\nPlanning: {plannedString}";
                    this.HealthLabel.alpha = 1f;
                }
            }
        }

        [ModifiesMember]
        private new void RefreshDynamicContent()
        {
            Container containerComponent = this.Target.GetComponent<Container>();
            this.RefreshBackgroundColor();
            if (this.m_Health != null)
            {
                this.HealthLabel.alpha = (!this.TargetIsParty && this.m_Health.Uninjured) ? 0f : 1f;
                if (this.HealthLabel.gameObject.activeInHierarchy)
                {
                    if (this.TargetIsParty)
                    {
                        UIMapTooltip.s_stringBuilder.Append(GUIUtils.GetText(1498, CharacterStats.GetGender(this.SelectedCharacter)));
                        UIMapTooltip.s_stringBuilder.Append(": ");
                        UIMapTooltip.s_stringBuilder.Append(InGameHUD.GetHealthColorString(this.m_Health.CurrentStamina, this.m_Health.MaxStamina));
                        UIMapTooltip.s_stringBuilder.Append(this.m_Health.CurrentStaminaString());
                        UIMapTooltip.s_stringBuilder.Append("[-]/");
                        UIMapTooltip.s_stringBuilder.Append(Mathf.CeilToInt(this.m_Health.MaxStamina).ToString("#0"));
                        if (this.ShowHealth)
                        {
                            UIMapTooltip.s_stringBuilder.AppendLine();
                            UIMapTooltip.s_stringBuilder.Append(GUIUtils.GetText(1469, CharacterStats.GetGender(this.SelectedCharacter)));
                            UIMapTooltip.s_stringBuilder.Append(": ");
                            UIMapTooltip.s_stringBuilder.Append(InGameHUD.GetHealthColorString(this.m_Health.CurrentHealth, this.m_Health.MaxHealth));
                            UIMapTooltip.s_stringBuilder.Append(this.m_Health.CurrentHealthString());
                            UIMapTooltip.s_stringBuilder.Append("[-]/");
                            UIMapTooltip.s_stringBuilder.Append(Mathf.CeilToInt(this.m_Health.MaxHealth).ToString("#0"));
                        }
                        this.HealthLabel.text = UIMapTooltip.s_stringBuilder.ToString();
                        UIMapTooltip.s_stringBuilder.Remove(0, UIMapTooltip.s_stringBuilder.Length);
                    }
                    else if (!this.m_Health.HealthVisible)
                    {
                        this.HealthLabel.text = string.Concat("[888888]", GUIUtils.GetText(1980), "[-]");
                    }
                    else
                    {
                        this.HealthLabel.text = InGameHUD.GetHealthString(this.m_Health.CurrentStamina, this.m_Health.BaseMaxStamina, CharacterStats.GetGender(this.SelectedCharacter)) + "[-]";
                    }

                    // injection point
                    try
                    {
                        add_InjectActionInfo();
                    }
                    catch (Exception e)
                    {
                        Console.AddMessage($"BetterTooltip: exception {e.Message}");
                    }
                }
            }
            else if (containerComponent == null || !containerComponent.IsEmpty)
            {
                this.HealthLabel.text = string.Empty;
                this.HealthLabel.alpha = 0f;
            }
            else
            {
                this.HealthLabel.alpha = 1f;
                UIMapTooltip.s_stringBuilder.Append("[888888]");
                UIMapTooltip.s_stringBuilder.Append(GUIUtils.GetText(262));
                UIMapTooltip.s_stringBuilder.Append("[-]");
                this.HealthLabel.text = UIMapTooltip.s_stringBuilder.ToString();
                UIMapTooltip.s_stringBuilder.Remove(0, UIMapTooltip.s_stringBuilder.Length);
            }

            if (this.DtParent)
            {
                this.DtParent.SetActive(this.ShowDefenses);
            }
            if (this.DefenseParent)
            {
                this.DefenseParent.SetActive(this.ShowDefenses);
            }
            if (this.ImmunitiesParent)
            {
                this.ImmunitiesParent.ExternalActivation = this.ShowDefenses;
            }
            if (this.ResistancesParent)
            {
                this.ResistancesParent.ExternalActivation = this.ShowDefenses;
            }
            if (this.SubTop)
            {
                this.SubTop.widgetContainer = (!this.RaceLabel || string.IsNullOrEmpty(this.RaceLabel.text) ? this.NameLabel : this.RaceLabel);
            }

            this.Divider.alpha = (this.HealthLabel.alpha > 0f || this.ShowDefenses) ? 1f : 0f;
            if (this.Table)
            {
                this.Table.Reposition();
            }
            this.Panel.Refresh();
        }
    }
}
