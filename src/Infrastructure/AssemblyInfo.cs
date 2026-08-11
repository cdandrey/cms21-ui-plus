using System.Reflection;
using System.Runtime.InteropServices;
using MelonLoader;

[assembly: AssemblyTitle(Cms21UiPlus.BuildInfo.Name)]
[assembly: AssemblyDescription(Cms21UiPlus.BuildInfo.Description)]
[assembly: AssemblyCompany(Cms21UiPlus.BuildInfo.Company)]
[assembly: AssemblyProduct(Cms21UiPlus.BuildInfo.Name)]
[assembly: AssemblyCopyright("CMS21 UI+ contributors; based in part on QoLmod by Meitzi")]
[assembly: AssemblyVersion(Cms21UiPlus.BuildInfo.Version)]
[assembly: AssemblyFileVersion(Cms21UiPlus.BuildInfo.Version)]
[assembly: AssemblyCulture("")]
[assembly: MelonInfo(typeof(Cms21UiPlus.Main), Cms21UiPlus.BuildInfo.ShortName,
    Cms21UiPlus.BuildInfo.Version, Cms21UiPlus.BuildInfo.Author, Cms21UiPlus.BuildInfo.DownloadLink)]
#if NET6_0_OR_GREATER
[assembly: MelonColor(255, 4, 163, 204)]
#else
[assembly: MelonColor()]
#endif
[assembly: MelonGame(Cms21UiPlus.BuildInfo.MelonGameCompany, Cms21UiPlus.BuildInfo.MelonGameName)]
[assembly: HarmonyDontPatchAll]
[assembly: ComVisible(false)]
[assembly: Guid("531ABC8D-F2B1-4DF7-8A8A-AE755D7F8538")]
