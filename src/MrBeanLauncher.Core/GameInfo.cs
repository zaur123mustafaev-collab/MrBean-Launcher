using System.Collections.Generic;

namespace MrBeanLauncher.Core;

public class GameInfo
{
	public string Java;

	public List<string> Classpath = new List<string>();

	public string MainClass;

	public List<string> FabricJvm = new List<string>();

	public string VersionId;

	public string AssetIndex;

	public string LoaderVersion;
}
