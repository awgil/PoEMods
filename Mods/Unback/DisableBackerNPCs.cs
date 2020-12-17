using Patchwork.Attributes;

namespace Unback
{
	[ModifiesType]
	public class mod_BackerContent : BackerContent
	{
		[NewMember]
		private void Awake()
		{
			gameObject.SetActive(false);
		}
	}
}
