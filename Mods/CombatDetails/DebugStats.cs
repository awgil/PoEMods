using System.Collections.Generic;
using System.Text;
using Patchwork.Attributes;
using UnityEngine;

namespace CombatDetails
{
    // hook up input events, so that we can print debug info for character under cursor by Ctrl+K
    [ModifiesType]
    public class mod_GameInput : GameInput
    {
        [NewMember]
        [DuplicatesBody("Awake")]
        private void ori_Awake() { }

        [ModifiesMember("Awake")]
        private void mod_Awake()
        {
            ori_Awake();
            Instance.OnHandleInput += new HandleInput(DebugStats.HandleInput);
        }
    }

    [NewType]
    public class DebugStats
    {
        [NewMember]
        public static string DumpBasicStats(CharacterStats stats)
        {
            var str = new StringBuilder();
            str.Append($"Race / subrace: {stats.CharacterRace} / {stats.CharacterSubrace}\n");
            str.Append($"Class: {stats.CharacterClass}\n");
            str.Append($"Culture / background: {stats.CharacterCulture} / {stats.CharacterBackground}\n");
            str.Append($"Deity / order: {stats.Deity} / {stats.PaladinOrder}\n");
            str.Append($"Level: {stats.ScaledLevel} (base {stats.Level})\n");
            // TODO: elaborate stat mods
            str.Append($"MIG: {stats.Might}\n");
            str.Append($"DEX: {stats.Dexterity}\n");
            str.Append($"CON: {stats.Constitution}\n");
            str.Append($"PER: {stats.Perception}\n");
            str.Append($"INT: {stats.Intellect}\n");
            str.Append($"RES: {stats.Resolve}\n");
            // TODO: health/stamina
            return str.TrimEnd().ToString();
        }

        [NewMember]
        public static string DumpStatusEffects(CharacterStats stats)
        {
            var str = new StringBuilder();
            foreach (var eff in stats.ActiveStatusEffects)
            {
                str.Append(eff.Applied ? "+ " : "- ");
                str.Append($"{eff.GetDisplayName()} -> {eff.Params.GetDebuggerString()} = {eff.CurrentAppliedValue}");
                float remaining = eff.TimeLeft;
                if (remaining != 0)
                    str.Append($" ({remaining} sec)");
                str.Append("\n");
            }
            return str.TrimEnd().ToString();
        }

        [NewMember]
        public static string DumpAbilities(CharacterStats stats)
        {
            var str = new StringBuilder();
            foreach (var abil in stats.ActiveAbilities)
            {
                str.Append(abil.Activated ? "+ " : "- ");
                str.Append($"{abil.Name()}\n");
            }
            return str.TrimEnd().ToString();
        }

        [NewMember]
        public static string DumpAIState(AIController ai)
        {
            var str = new StringBuilder();
            ai.StateManager.BuildDebugText(str);
            return str.TrimEnd().ToString();
        }

        [NewMember]
        public static void HandleInput()
        {
            if (!GameInput.GetControlkey() || !GameInput.GetKeyUp(KeyCode.K) || !GameCursor.CharacterUnderCursor)
                return;

            var stats = GameCursor.CharacterUnderCursor.GetComponent<CharacterStats>();
            if (!stats)
                return;
            var ai = GameCursor.CharacterUnderCursor.GetComponent<AIController>();

            var message = new Console.ConsoleMessage($"Stats for {stats.Name()}, click to expand...", Console.ConsoleState.Combat);
            message.Children = new List<Console.ConsoleMessage>();
            message.Children.Add(new Console.ConsoleMessage("Basic stats", DumpBasicStats(stats), Console.ConsoleState.Combat));
            message.Children.Add(new Console.ConsoleMessage($"Status effects ({stats.ActiveStatusEffects.Count})", DumpStatusEffects(stats), Console.ConsoleState.Combat));
            message.Children.Add(new Console.ConsoleMessage($"Abilities ({stats.ActiveAbilities.Count})", DumpAbilities(stats), Console.ConsoleState.Combat));
            if (ai?.StateManager != null)
                message.Children.Add(new Console.ConsoleMessage("AI state", DumpAIState(ai), Console.ConsoleState.Combat));
            Console.Instance.AddMessage(message);
        }
    }
}
