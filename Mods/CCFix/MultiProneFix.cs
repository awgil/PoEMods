using Patchwork.Attributes;

namespace CCFix
{
    // original code failed if two knockdown effects were applied (e.g. wizard's Slicken + Call to Slumber):
    // - first effect would apply normally and push KnockedDown state onto AI state stack
    // - second effect would apply on top of existing one, find the KnockedDown state on the stack, and update its duration
    // - then one of the effects would be suppressed and start Standup() sequence, despite long duration remaining
    // - after standup ends, AI would start performing actions despite active CC
    // with this fix, we maintain a counter of active knockdown events; Standup() is now called only if counter reaches zero
    [ModifiesType]
    public abstract class mod_AIController : AIController
    {
        [NewMember]
        private int m_numKnockdownStacks; // note that it doesn't really matter where we put this field, we never have multiple KnockedDown states on stack anyway

        // workaround to ensure NewMembers are processed before ModifiesMembers
        [NewMember]
        private void dummy() { }

        [ModifiesMember]
        private new void HandleKnockedDownEvent(GameEventArgs args)
        {
            if (args.IntData[0] != 1)
            {
                //Console.AddMessage($"-- KnockedDown: stack count={m_numKnockdownStacks}");
                if (--m_numKnockdownStacks > 0)
                {
                    return; // don't do anything, since we have more active knockdown "stacks"
                }
                m_numKnockdownStacks = 0; // don't ever go negative

                AI.Achievement.KnockedDown knockedDown = this.StateManager.FindState(typeof(AI.Achievement.KnockedDown)) as AI.Achievement.KnockedDown;
                if (knockedDown != null)
                {
                    //Console.AddMessage(" => standing up...");
                    knockedDown.Standup();
                }
            }
            else
            {
                //Console.AddMessage($"++ KnockedDown: stack count={m_numKnockdownStacks}");
                m_numKnockdownStacks++;

                this.CancelCurrentAttack();
                AI.Achievement.KnockedDown knockedDown = this.StateManager.FindState(typeof(AI.Achievement.KnockedDown)) as AI.Achievement.KnockedDown;
                if (knockedDown == null)
                {
                    // TODO: look for deferred state in the PushedBack one; with current logic newer knockdown can override stored one's duration, even if it is longer...
                    //Console.AddMessage(" => no knockdowns active, allocating new state...");
                    knockedDown = AIStateManager.StatePool.Allocate<AI.Achievement.KnockedDown>();
                    if (!(this.StateManager.CurrentState is AI.Achievement.PushedBack))
                    {
                        //Console.AddMessage(" => pushing it...");
                        this.StateManager.CurrentState.OnCancel();
                        this.StateManager.PushState(knockedDown);
                    }
                    else
                    {
                        //Console.AddMessage(" => deferring it...");
                        (this.StateManager.CurrentState as AI.Achievement.PushedBack).SetKnockedDownState(knockedDown);
                    }
                    knockedDown.SetKnockdownTime(args.FloatData[0]);
                }
                else
                {
                    //Console.AddMessage(" => updating time left...");
                    knockedDown.ResetKnockedDown(args.FloatData[0]);
                }

                // this is standard status effect handling piece of code
                AIController baseThis = this;
                if (baseThis is PartyMemberAI && args.GameObjectData[0] != null)
                {
                    PartyMemberAI component = args.GameObjectData[0].GetComponent<PartyMemberAI>();
                    if (component != null && component.gameObject.activeInHierarchy)
                    {
                        knockedDown.InCombatOverride = false;
                    }
                }
            }
        }
    }
}
