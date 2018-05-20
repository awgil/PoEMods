﻿using System;
using System.Diagnostics;
using System.IO;
using Patchwork.Attributes;

namespace PoEGameInfo
{
	[AppInfoFactory]
	internal class PoEAppInfoFactory : AppInfoFactory
	{
		public override AppInfo CreateInfo(DirectoryInfo folderInfo)
		{
			string exeFileName;
			string iconFile;
			string appVersion;
			if (Environment.OSVersion.Platform == PlatformID.Unix)
			{
				exeFileName = "PillarsOfEternity";
				iconFile = "PillarsOfEternity.png";
				appVersion = null;
			}
			else
			{
				exeFileName = iconFile = "PillarsOfEternity.exe";
				appVersion = FileVersionInfo.GetVersionInfo(Path.Combine(folderInfo.FullName, exeFileName)).FileVersion;
			}

			var fileInfos = folderInfo.GetFiles(exeFileName);
			if (fileInfos.Length == 0)
			{
				throw new FileNotFoundException($"The Pillars of Eternity executable file '{exeFileName}' was not found in this directory.", exeFileName);
			}
			var exeFile = fileInfos[0];

			return new AppInfo()
			{
				BaseDirectory = folderInfo,
				Executable = exeFile,
				AppVersion = appVersion,
				AppName = "Pillars of Eternity",
				IconLocation = new FileInfo(Path.Combine(folderInfo.FullName, iconFile)),
				IgnorePEVerifyErrors = new[] {
					//Expected an ObjRef on the stack.(Error: 0x8013185E). 
					//-you can ignore the following. They are present in the original DLL. I'm not sure if they are actually errors.
					0x8013185EL,
					//The 'this' parameter to the call must be the calling method's 'this' parameter.(Error: 0x801318E1)
					//-this isn't really an issue. PEV is just confused.
					0x801318E1,
					//Call to .ctor only allowed to initialize this pointer from within a .ctor. Try newobj.(Error: 0x801318BF)
					//-this is a *verificaiton* issue is caused by copying the code from an existing constructor to a non-constructor method 
					//-it contains a call to .ctor(), which is illegal from a non-constructor method.
					//-There will be an option to fix this at some point, but it's not really an error.
					0x801318BF,
				}
			};
		}
	}
}
