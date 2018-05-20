using Patchwork.Attributes;

namespace ImprovedRespec
{
    // allow customizing stats on level-up to level 2 for NPCs
    // TODO: allow modifying level-1 abilities for companions, but make sure to preserve things like Sagani's fox
    [ModifiesType]
    public class mod_UICharacterCreationController : UICharacterCreationController
    {
        [NewMember]
        [DuplicatesBody("ShouldSkip")]
        public bool ori_ShouldSkip()
        {
            return true;
        }

        [ModifiesMember("ShouldSkip")]
        public bool mod_ShouldSkip()
        {
            if (Type != ControllerType.ATTRIBUTES)
            {
                return ori_ShouldSkip();
            }
            else if (UICharacterCreationManager.Instance.CreationType != UICharacterCreationManager.CharacterCreationType.LevelUp)
            {
                return false;
            }
            else if (Character.CoreData.IsPlayerCharacter || Character.CoreData.IsHiredAdventurer)
            {
                return Character.CoreData.Level != 0;
            }
            else
            {
                // for premade NPCs, allow customizing stats on level-up to level 2
                return Character.CoreData.Level > 1;
            }
        }
    }
}
