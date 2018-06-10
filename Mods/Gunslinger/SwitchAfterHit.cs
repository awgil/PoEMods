using System;
using Patchwork.Attributes;
using UnityEngine;

// whenever projectile from a PC's firearm hits, look for opportunity to switch weapons (to a different ranged weapon in set with lower id)
namespace Gunslinger
{
    [ModifiesType]
    public class mod_AttackFirearm : AttackFirearm
    {
        [NewMember]
        private void add_TrySwitchingToOtherWeapon()
        {
            try
            {
                var ownerPartyMemberAI = this.Owner?.GetComponent<PartyMemberAI>();
                if (!ownerPartyMemberAI)
                    return; // attacker isn't a party member, ignore

                var ownerEquipment = this.Owner.GetComponent<Equipment>();
                var ownerCurItems = ownerEquipment?.CurrentItems;
                if (ownerCurItems == null || ownerCurItems.PrimaryWeapon == null || ownerCurItems.SelectedWeaponSet <= 0)
                    return; // owner doesn't have primary weapon OR currently equipped set is first one, ignore

                var ownerPrimaryAttack = ownerCurItems.PrimaryWeapon.GetComponent<AttackBase>();
                if (ownerPrimaryAttack != this)
                    return; // owner's current weapon is not what caused the attack, ignore

                // find potential candidate for weapon switch (should be another ranged weapon that is either non-reloadable or already loaded)
                for (int i = ownerCurItems.SelectedWeaponSet - 1; i >= 0; --i)
                {
                    var candidateWeapon = ownerCurItems.AlternateWeaponSets[i]?.PrimaryWeapon;
                    var candidateAttack = candidateWeapon?.GetComponent<AttackRanged>();
                    if (!candidateAttack)
                        continue;

                    var candidateFirearm = candidateAttack as AttackFirearm;
                    if (candidateFirearm != null && candidateFirearm.RequiresReload)
                        continue; // skip firearms that need reloading

                    // found one!
                    Console.AddMessage($"{CharacterStats.NameColored(this.Owner)} switches to {candidateWeapon.Name} (weapon set {i + 1})!");
                    ownerEquipment.SelectWeaponSet(i, true);
                    break;
                }
            }
            catch (Exception e)
            {
                Console.AddMessage($"Gunslinger::TrySwitchingToOtherWeapon: exception {e.Message}", e.StackTrace);
            }
        }

        [NewMember]
        public override void OnImpact(GameObject projectile, GameObject enemy)
        {
            base.OnImpact(projectile, enemy); // this is ok, we're introducing new override
            add_TrySwitchingToOtherWeapon();
        }
    }
}
