using Patchwork.Attributes;

namespace CCFix
{
    // allow queueing Paralyzed state if higher-priority state is current
    // the only things higher-priority are PushedBack and KnockedDown, and it seems reasonable to allow queuing paralysis after these states end
    // there are also Dead and Unconscious, but they don't matter here, since they themselves disallow anything to be queued below them
    // ALTERNATIVE FIX: change AIController.HandleParalyzedEvent to work like AIController.HandleStunnedEffect: manually queue instead of pushing if higher-prio state is on top
    [ModifiesType]
    public class mod_Paralyzed : AI.Achievement.Paralyzed
    {
        public bool mod_CanBeQueuedIfLowerPriority
        {
            [ModifiesMember("get_CanBeQueuedIfLowerPriority")]
            get { return true; }
        }
    }
}
