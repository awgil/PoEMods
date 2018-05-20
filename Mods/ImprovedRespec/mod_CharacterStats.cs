using Patchwork.Attributes;

namespace ImprovedRespec
{
    // remove the logic that overrides base attributes from prefab
    // I guess this was done when devs wanted to patch companion attributes
    // TODO: remove the code block completely instead of undoing its effects...
	[ModifiesType]
	public class mod_CharacterStats : CharacterStats
	{
		[NewMember]
		[DuplicatesBody("Restored")]
		public void ori_Restored() { }

		[ModifiesMember("Restored")]
		public void mod_Restored()
		{
			int oriMig = BaseMight;
			int oriCon = BaseConstitution;
			int oriDex = BaseDexterity;
			int oriRes = BaseResolve;
			int oriInt = BaseIntellect;
			int oriPer = BasePerception;

			// original Restored() will override base stats from prefab
			ori_Restored();

			if (oriMig != BaseMight || oriCon != BaseConstitution || oriDex != BaseDexterity ||
				oriRes != BaseResolve || oriInt != BaseIntellect || oriPer != BasePerception)
			{
				//Console.AddMessage($"Restoring stats overridden by prefab for {DisplayName.GetText()}: MIG={BaseMight}->{oriMig}, CON={BaseConstitution}->{oriCon}, DEX={BaseDexterity}->{oriDex}, RES={BaseResolve}->{oriRes}, INT={BaseIntellect}->{oriInt}, PER={BasePerception}->{oriPer}");
				BaseMight = oriMig;
				BaseConstitution = oriCon;
				BaseDexterity = oriDex;
				BaseResolve = oriRes;
				BaseIntellect = oriInt;
				BasePerception = oriPer;
			}
		}
	}
}
