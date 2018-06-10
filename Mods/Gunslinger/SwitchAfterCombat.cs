using System;
using Patchwork.Attributes;
using UnityEngine;

// when combat ends for a PC that has a ranged weapon equipped, look for following weapons and switch to next firearm
namespace Gunslinger
{
    [ModifiesType]
    public abstract class mod_PartyMemberAI : PartyMemberAI
    {
        [NewMember]
        private void add_TrySwitchingToFirearm(GameObject owner)
        {
            try
            {
                if (owner == null)
                    return;

                var ownerEquipment = owner.GetComponent<Equipment>();
                var ownerCurItems = ownerEquipment?.CurrentItems;
                var ownerCurAttack = ownerCurItems?.PrimaryWeapon?.GetComponent<AttackRanged>();
                if (ownerCurAttack == null)
                    return; // current weapon is not a ranged one, ignore

                // find potential candidate for weapon switch (last weapon in "firearm chain" starting from next weapon)
                int switchTo = ownerCurItems.SelectedWeaponSet;
                for (int i = ownerCurItems.SelectedWeaponSet + 1; i < ownerCurItems.AlternateWeaponSets.Length; ++i)
                {
                    if (!ownerCurItems.AlternateWeaponSets[i]?.PrimaryWeapon?.GetComponent<AttackFirearm>())
                        break; // chain is broken, stop
                    switchTo = i;
                }

                if (switchTo != ownerCurItems.SelectedWeaponSet)
                {
                    // found something to switch to!
                    var betterWeapon = ownerCurItems.AlternateWeaponSets[switchTo].PrimaryWeapon;
                    Console.AddMessage($"{CharacterStats.NameColored(owner)} switches to {betterWeapon.Name} (weapon set {switchTo + 1})!");
                    ownerEquipment.SelectWeaponSet(switchTo, true);
                }
            }
            catch (Exception e)
            {
                Console.AddMessage($"Gunslinger::TrySwitchingToFirearm: exception {e.Message}", e.StackTrace);
            }
        }

        [ModifiesMember]
        public override void HandleCombatEnd(object sender, EventArgs e)
        {
            if (this.m_ai == null)
                return;

            var currentState = this.m_ai.CurrentState as AI.Achievement.Attack;
            if (currentState != null && currentState.CanUserInterrupt())
            {
                currentState.OnCancel();
                this.m_ai.PopState(currentState);
            }

            // if we currently have a ranged weapon equipped, look for firearm in next slots
            add_TrySwitchingToFirearm(this.m_ai.Owner);

            // we can't really call base.HandleCombatEnd here (since base class method is what we're overriding, we'll get infinite recursion)
            // so just duplicate it here, it's very simple
            ReloadFirearmsInAlternateWeaponSets(this.m_ai.Owner);
        }
    }
}
