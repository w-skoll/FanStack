using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace FanStack.App.Interop;

/// <summary>Contenuto di un collegamento .lnk.</summary>
internal sealed record ShortcutInfo(string TargetPath, string Arguments, string WorkingDirectory, string IconLocation, string? AppUserModelId);

/// <summary>
/// Lettura e scrittura di collegamenti .lnk via COM (IShellLinkW + IPropertyStore),
/// senza librerie esterne. L'AppUserModelID si scrive come proprietà System.AppUserModel.ID.
/// </summary>
internal static class ShellLink
{
    private const int STGM_READ = 0x0;
    private const uint SLGP_RAWPATH = 0x4;
    private const ushort VT_LPWSTR = 31;

    /// <summary>PKEY_AppUserModel_ID = {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, 5.</summary>
    private static PROPERTYKEY PKEY_AppUserModel_ID = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    public static void Create(string lnkPath, string targetPath, string arguments, string workingDirectory,
        string iconPath, string description, string appUserModelId)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            Check(link.SetPath(targetPath));
            Check(link.SetArguments(arguments));
            Check(link.SetWorkingDirectory(workingDirectory));
            Check(link.SetIconLocation(iconPath, 0));
            Check(link.SetDescription(description));

            // L'oggetto ShellLink implementa anche IPropertyStore: lì va l'AppUserModelID.
            var store = (IPropertyStore)link;
            var pv = PROPVARIANT.FromString(appUserModelId);
            try
            {
                Check(store.SetValue(ref PKEY_AppUserModel_ID, ref pv));
                Check(store.Commit());
            }
            finally
            {
                pv.Clear();
            }

            ((IPersistFile)link).Save(lnkPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    /// <summary>Legge un .lnk senza risolverlo (nessuna interfaccia utente, nessuna ricerca del bersaglio).</summary>
    public static ShortcutInfo Read(string lnkPath)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            ((IPersistFile)link).Load(lnkPath, STGM_READ);
            var sb = new StringBuilder(1024);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, SLGP_RAWPATH);
            var target = Environment.ExpandEnvironmentVariables(sb.ToString());
            sb.Clear();
            link.GetArguments(sb, sb.Capacity);
            var args = sb.ToString();
            sb.Clear();
            link.GetWorkingDirectory(sb, sb.Capacity);
            var wd = sb.ToString();
            sb.Clear();
            link.GetIconLocation(sb, sb.Capacity, out _);
            var icon = sb.ToString();

            string? appId = null;
            var store = (IPropertyStore)link;
            if (store.GetValue(ref PKEY_AppUserModel_ID, out var pv) == 0)
            {
                try
                {
                    appId = pv.AsString();
                }
                finally
                {
                    pv.Clear();
                }
            }
            return new ShortcutInfo(target, args, wd, icon, appId);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    private static void Check(int hr)
    {
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink;

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        [PreserveSig] int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        [PreserveSig] int GetIDList(out IntPtr ppidl);
        [PreserveSig] int SetIDList(IntPtr pidl);
        [PreserveSig] int GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        [PreserveSig] int GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        [PreserveSig] int GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        [PreserveSig] int GetHotkey(out short pwHotkey);
        [PreserveSig] int SetHotkey(short wHotkey);
        [PreserveSig] int GetShowCmd(out int piShowCmd);
        [PreserveSig] int SetShowCmd(int iShowCmd);
        [PreserveSig] int GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        [PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        [PreserveSig] int Resolve(IntPtr hwnd, uint fFlags);
        [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY(Guid fmtid, uint pid)
    {
        public Guid fmtid = fmtid;
        public uint pid = pid;
    }

    /// <summary>PROPVARIANT ridotto: gestiamo solo stringhe (VT_LPWSTR).</summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;

        public static PROPVARIANT FromString(string value) =>
            new() { vt = VT_LPWSTR, pointerValue = Marshal.StringToCoTaskMemUni(value) };

        public readonly string? AsString() => vt == VT_LPWSTR ? Marshal.PtrToStringUni(pointerValue) : null;

        public void Clear()
        {
            _ = PropVariantClear(ref this);
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PROPVARIANT pvar);
    }
}
