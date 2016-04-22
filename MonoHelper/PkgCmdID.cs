// PkgCmdID.cs
// MUST match PkgCmdID.h

namespace MonoHelper
{
	static class PkgCmdIDList
	{
		public const uint XBuildCommandID = 0x100;
		public const uint XRebuildCommandID = 0x101;
		public const uint StartNetCommandID = 0x102;
		public const uint DebugNetCommandID = 0x103;
		public const uint StartMonoCommandID = 0x104;
		public const uint RebuildAndStartMonoCommandID = 0x105;
	};
}