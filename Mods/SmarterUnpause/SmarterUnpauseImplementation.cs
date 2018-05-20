using Patchwork.Attributes;
using UnityEngine;

namespace SmarterUnpause
{
	// remember time of the last autopause
	[NewType]
	public class SmarterUnpauseManager
	{
		[NewMember]
		public static float AutoPauseTime = 0.0f;
	}

	[ModifiesType]
	public class mod_GameState : GameState
	{
		[NewMember]
		[DuplicatesBody("AutoPause")]
		public static void ori_AutoPause(AutoPauseOptions.PauseEvent evt, GameObject target, GameObject triggerer, GenericAbility ability = null) { }

		[ModifiesMember("AutoPause")]
		public static void mod_AutoPause(AutoPauseOptions.PauseEvent evt, GameObject target, GameObject triggerer, GenericAbility ability = null)
		{
			bool wasPaused = TimeController.Instance.Paused;
			ori_AutoPause(evt, target, triggerer, ability);
			if (!wasPaused && TimeController.Instance.Paused)
			{
				// auto-pause happened; remember real time
				SmarterUnpauseManager.AutoPauseTime = TimeController.Instance.RealtimeSinceStartupThisFrame;
				//Console.AddMessage($"Auto-pausing at: {SmarterUnpauseManager.AutoPauseTime}");
			}
		}
	}

	[ModifiesType]
	public class mod_UIActionBarOnClick : UIActionBarOnClick
	{
		[ModifiesMember]
		private new void HandlePause()
		{
			//Console.AddMessage($"Pausing: {!TimeController.Instance.Paused} at {TimeController.Instance.RealtimeSinceStartupThisFrame}");
			float timeSinceAutopause = TimeController.Instance.RealtimeSinceStartupThisFrame - SmarterUnpauseManager.AutoPauseTime;
			if (TimeController.Instance.Paused && timeSinceAutopause < 0.5f)
			{
				//Console.AddMessage($"Keeping the game paused, since autopause happened {timeSinceAutopause} seconds ago.");
                SmarterUnpauseManager.AutoPauseTime = 0.0f; // if user presses unpause again, it should not be suppressed
			}
			else
			{
				TimeController.Instance.SafePaused = !TimeController.Instance.Paused;
			}
		}
	}
}
