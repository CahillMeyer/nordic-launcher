# Builds one launcher .exe per app (icon embedded) and points the Start Menu
# and pinned taskbar shortcuts at it. Re-run after changing an icon.
# Icons must stay outside AppData: writes there from packaged apps are
# redirected to a private folder the shell can't see.
# Profile must match the profiles\<name> the app's script passes to Chrome.
Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct PKEY { public Guid fmtid; public uint pid; }
[StructLayout(LayoutKind.Explicit)] public struct PV { [FieldOffset(0)] public ushort vt; [FieldOffset(8)] public IntPtr p; }
[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPropertyStore { void GetCount(out uint c); void GetAt(uint i, out PKEY k); void GetValue(ref PKEY k, out PV v); void SetValue(ref PKEY k, ref PV v); void Commit(); }
[ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPersistFile { void GetClassID(out Guid g); [PreserveSig] int IsDirty(); void Load([MarshalAs(UnmanagedType.LPWStr)] string f, uint m); void Save([MarshalAs(UnmanagedType.LPWStr)] string f, bool r); void SaveCompleted(string f); void GetCurFile(out IntPtr f); }
[ComImport, Guid("00021401-0000-0000-C000-000000000046")] public class CShellLink {}
public static class Aumid {
  public static void Set(string lnk, string id) {
    var link = new CShellLink();
    ((IPersistFile)link).Load(lnk, 2);
    var key = new PKEY { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
    var v = new PV { vt = 31, p = Marshal.StringToCoTaskMemUni(id) };
    ((IPropertyStore)link).SetValue(ref key, ref v);
    ((IPropertyStore)link).Commit();
    ((IPersistFile)link).Save(lnk, true);
    Marshal.FreeCoTaskMem(v.p);
  }
}
"@
$here = $PSScriptRoot
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$programs = [Environment]::GetFolderPath("Programs")
$pinned = "$env:APPDATA\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"
$sh = New-Object -ComObject WScript.Shell

foreach ($a in @(
  @{ Name = "Nordic Prep"; Id = "ccm.NordicPrep"; Exe = "nordic-prep.exe"; Icon = "prep.ico"; Profile = "prep"; Script = "/home/ccm/projects/nordic-prep/viewer/open-dashboard.sh" },
  @{ Name = "Nordic Overview"; Id = "ccm.NordicOverview"; Exe = "nordic-overview.exe"; Icon = "overview.ico"; Profile = "overview"; Script = "/home/ccm/projects/nordic-overview/viewer/open-viewer.sh" }
)) {
  $exe = "$here\$($a.Exe)"
  & $csc /nologo /target:winexe /out:$exe /win32icon:"$here\$($a.Icon)" /r:System.Management.dll "$here\Launcher.cs"
  if ($LASTEXITCODE) { throw "csc failed for $($a.Exe)" }

  $links = @("$programs\$($a.Name).lnk") + @(Get-ChildItem $pinned -Filter "$($a.Name)*.lnk" | % FullName)
  foreach ($path in $links) {
    $l = $sh.CreateShortcut($path)
    $l.TargetPath = $exe
    $l.Arguments = "$($a.Script) $($a.Id) $($a.Profile)"
    $l.WorkingDirectory = $here
    $l.IconLocation = "$exe,0"
    $l.WindowStyle = 7
    $l.Description = $a.Name
    $l.Save()
    [Aumid]::Set($path, $a.Id)
    "$path -> $exe $($l.Arguments)"
  }
}
ie4uinit.exe -show
