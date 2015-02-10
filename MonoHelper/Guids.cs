// Guids.cs
// MUST match guids.h

namespace MonoHelper
{
	using System;

	static class GuidList
	{
		public const string GuidMonoHelperPkgString = "fbcafcd5-87dc-44f0-83c0-0a5be15709d8";
		public const string GuidMonoHelperCmdSetString = "66ae7e29-9859-4e84-b953-1502a786e958";

		public static readonly Guid GuidMonoHelperCmdSet = new Guid(GuidList.GuidMonoHelperCmdSetString);
	};
}