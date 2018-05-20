using System.IO;
using Patchwork.Attributes;

namespace CCFix
{
	[PatchInfo]
	public class CCFixPatchInfo : IPatchInfo
	{
		public FileInfo GetTargetFile(AppInfo app)
		{
			var file = Path.Combine(app.BaseDirectory.FullName, "PillarsOfEternity_Data/Managed/Assembly-CSharp.dll");
			return new FileInfo(file);
		}

		public string CanPatch(AppInfo app)
		{
			return null;
		}

		public string PatchVersion => "0.0";

		public string Requirements => "None";

		public string PatchName => "Crowd Control Stacking Fix";
	}
}
