using System;
using Patchwork.Attributes;
using UnityEngine;

namespace CombatDetails
{
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
        // add current action info to static string builder and return whether something was added
        [NewMember]
        private bool add_InjectActionInfo()
        {
            // we only care about "big" tooltip (one appearing in top-left corner in combat)
            if (this != UIMapTooltipManager.Instance.BigTooltip)
                return false;

            int initialLen = s_stringBuilder.Length;
            try
            {
                AIController ai = this.Target?.GetComponent<AIController>();
                if (this.SelectedCharacter != null && ai?.StateManager != null)
                {
                    if (this.SelectedCharacter.RecoveryTimer > 0f)
                    {
                        s_stringBuilder.AppendLine();
                        s_stringBuilder.Append("Recovery: ");
                        s_stringBuilder.Append(this.SelectedCharacter.HasStatusEffectThatPausesRecoveryTimer() ? "[FF0000]" : "[FFFFFF]");
                        s_stringBuilder.Append(this.SelectedCharacter.RecoveryTimer.ToString("#0.0"));
                        s_stringBuilder.Append(" sec / ");
                        s_stringBuilder.Append(this.SelectedCharacter.TotalRecoveryTime.ToString("#0.0"));
                        s_stringBuilder.Append(" sec[-]");
                    }

                    AIState currentState = ai.StateManager.CurrentState;
                    if (currentState is AI.Achievement.HitReact || currentState is AI.Achievement.PathToPosition)
                        currentState = ai.StateManager.QueuedState;

                    var attackState = currentState as AI.Achievement.Attack;
                    if (attackState != null && attackState.CurrentAttack != null)
                    {
                        string color = attackState.CurrentAttack.CanCancel ? "[FFFFFF]" : "[FF0000]";
                        var ability = attackState.CurrentAttack.AbilityOrigin;
                        if (ability != null)
                        {
                            s_stringBuilder.AppendLine();
                            s_stringBuilder.Append("Casting: ");
                            s_stringBuilder.Append(color);
                            s_stringBuilder.Append(ability.DisplayName.GetText());
                            s_stringBuilder.Append("[-]");
                        }
                        else if (attackState.m_animDuration <= 0f)
                        {
                            s_stringBuilder.AppendLine();
                            s_stringBuilder.Append("Attacking: <idle>");
                        }
                        else
                        {
                            s_stringBuilder.AppendLine();
                            s_stringBuilder.Append("Attacking: ");
                            s_stringBuilder.Append(color);
                            s_stringBuilder.Append((attackState.m_animDuration - attackState.m_totalTime).ToString("#0.0"));
                            s_stringBuilder.Append(" sec / ");
                            s_stringBuilder.Append(attackState.m_animDuration.ToString("#0.0"));
                            s_stringBuilder.Append(" sec[-]");
                        }
                    }

                    var reloadState = currentState as AI.Achievement.ReloadWeapon;
                    if (reloadState != null)
                    {
                        float max = reloadState.m_firearm.ReloadTime / reloadState.m_speedMultiplier;
                        float rem = (reloadState.m_firearm.ReloadTime - reloadState.m_reloadTime) / reloadState.m_speedMultiplier;
                        s_stringBuilder.AppendLine();
                        s_stringBuilder.Append("Reloading: ");
                        s_stringBuilder.Append(rem.ToString("#0.0"));
                        s_stringBuilder.Append(" sec / ");
                        s_stringBuilder.Append(max.ToString("#0.0"));
                        s_stringBuilder.Append(" sec");
                    }

                    if (currentState is AI.Player.Attack || currentState is AI.Player.TargetedAttack || currentState is AI.Plan.ApproachTarget)
                    {
                        var plannedAbility = currentState.CurrentAbility;
                        if (plannedAbility == null)
                        {
                            var partyAI = ai as PartyMemberAI;
                            plannedAbility = partyAI?.QueuedAbility;
                        }
                        s_stringBuilder.AppendLine();
                        s_stringBuilder.Append("Planning: ");
                        s_stringBuilder.Append(plannedAbility ? plannedAbility.DisplayName.GetText() : "attack");
                    }
                }
            }
            catch (Exception e)
            {
                Console.AddMessage($"BetterTooltip: exception {e.Message}", e.StackTrace);
            }
            return s_stringBuilder.Length != initialLen;
        }

        // fix up table drift (this is a hack)
        [NewMember]
        private void add_FixUpTableDrift()
        {
            // we only care about "big" tooltip (one appearing in top-left corner in combat)
            if (this != UIMapTooltipManager.Instance.BigTooltip || this.HealthLabel.alpha <= 0f)
                return;

            try
            {
                // table doesn't position elements correctly if health text is the widest element
                // for some reason (didn't find the root cause yet), all elements will move by +0.25 every other frame, and HealthLabel (which is child of the first table entry) will move by -0.25
                // this causes ugly drift
                // in normal cases (health is not the widest thing), all left-aligned elements will be offset by 20
                // for now, fix it via an ugly hack
                var elements = this.Table.children;

                // first element is an intermediate object that contains HealthLabel
                // when it is the widest element, it will "drift", like the rest; however, this is compensated by the HealthLabel's drift
                // we still want to stop it, since it causes ugly jittering of the text (due to floating-point inaccuracies, I think)
                var hpPos = elements[0].localPosition;
                var hcPos = HealthLabel.cachedTransform.localPosition;
                hpPos.x += hcPos.x;
                hcPos.x = 0f;
                elements[0].localPosition = hpPos;
                HealthLabel.cachedTransform.localPosition = hcPos;

                // force reposition the remaining elements to the left border
                for (int i = 1; i < elements.Count; ++i)
                {
                    var localPos = elements[i].localPosition;
                    localPos.x = 20f;
                    elements[i].localPosition = localPos;
                }
            }
            catch (Exception e)
            {
                Console.AddMessage($"BetterTooltip: fixup exception {e.Message}", e.StackTrace);
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
                        s_stringBuilder.Append(GUIUtils.GetText(1498, CharacterStats.GetGender(this.SelectedCharacter)));
                        s_stringBuilder.Append(": ");
                        s_stringBuilder.Append(InGameHUD.GetHealthColorString(this.m_Health.CurrentStamina, this.m_Health.MaxStamina));
                        s_stringBuilder.Append(this.m_Health.CurrentStaminaString());
                        s_stringBuilder.Append("[-]/");
                        s_stringBuilder.Append(Mathf.CeilToInt(this.m_Health.MaxStamina).ToString("#0"));
                        if (this.ShowHealth)
                        {
                            s_stringBuilder.AppendLine();
                            s_stringBuilder.Append(GUIUtils.GetText(1469, CharacterStats.GetGender(this.SelectedCharacter)));
                            s_stringBuilder.Append(": ");
                            s_stringBuilder.Append(InGameHUD.GetHealthColorString(this.m_Health.CurrentHealth, this.m_Health.MaxHealth));
                            s_stringBuilder.Append(this.m_Health.CurrentHealthString());
                            s_stringBuilder.Append("[-]/");
                            s_stringBuilder.Append(Mathf.CeilToInt(this.m_Health.MaxHealth).ToString("#0"));
                        }
                    }
                    else if (!this.m_Health.HealthVisible)
                    {
                        s_stringBuilder.Append("[888888]");
                        s_stringBuilder.Append(GUIUtils.GetText(1980));
                        s_stringBuilder.Append("[-]");
                    }
                    else
                    {
                        s_stringBuilder.Append(InGameHUD.GetHealthString(this.m_Health.CurrentStamina, this.m_Health.BaseMaxStamina, CharacterStats.GetGender(this.SelectedCharacter)));
                        s_stringBuilder.Append("[-]");
                    }

                    // injection point
                    if (add_InjectActionInfo())
                    {
                        this.HealthLabel.alpha = 1f;
                    }

                    this.HealthLabel.text = s_stringBuilder.ToString();
                    s_stringBuilder.Remove(0, s_stringBuilder.Length);
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
                s_stringBuilder.Append("[888888]");
                s_stringBuilder.Append(GUIUtils.GetText(262));
                s_stringBuilder.Append("[-]");
                this.HealthLabel.text = s_stringBuilder.ToString();
                s_stringBuilder.Remove(0, s_stringBuilder.Length);
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
                add_FixUpTableDrift();
            }
            this.Panel.Refresh();
        }
    }
}
