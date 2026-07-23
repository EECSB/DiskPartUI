using System.Runtime.Versioning;
using System.Security.Principal;

namespace DiskPartUI.Services;

///<summary>Detects whether the process is running with Administrator rights.</summary>
public static class ElevationHelper
{
    [SupportedOSPlatform("windows")]
    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
