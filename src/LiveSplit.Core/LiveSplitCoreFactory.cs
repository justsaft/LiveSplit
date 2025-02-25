using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LiveSplit;

public class LiveSplitCoreFactory
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private unsafe static extern IntPtr LoadLibrary(string dllname);

    [DllImport("kernel32.dll", SetLastError = true)]
    private unsafe static extern bool FreeLibrary(IntPtr handle);

    [DllImport("libdl.so.2")]
    private unsafe static extern IntPtr dlopen(string filename, int flags);

    /* [DllImport("libdl.so.2")]
    private unsafe static extern void dlsym(IntPtr handle, string symbol); */

    [DllImport("libdl.so.2")]
    private unsafe static extern void dlclose(IntPtr handle);

    private sealed unsafe class LibraryUnloader
    {
        internal LibraryUnloader(IntPtr handle)
        {
            _handle = handle;
        }

        ~LibraryUnloader()
        {
            if (_handle != null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    FreeLibrary(_handle);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    dlclose(_handle);
                }
                else
                {
                    throw new PlatformNotSupportedException("This platform is not supported.");
                }
            }
        }

        private readonly IntPtr _handle;
    }

    public static void LoadLiveSplitCore()
    {
        string path;

        if (Environment.Is64BitOperatingSystem)
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), @"x64/livesplit_core.dll");
        }
        else
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), @"x86/livesplit_core.dll");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            unsafe
            {
                IntPtr handle = LoadLibrary(path);

                if (handle == null)
                {
                    throw new DllNotFoundException("Unable to load the native livesplit-core library: " + path);
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            unsafe
            {
                IntPtr handle = dlopen(path, 0);

                if (handle == null)
                {
                    throw new DllNotFoundException("Unable to load the native livesplit-core library: " + path);
                }
            }
        }
        else
        {
            throw new PlatformNotSupportedException("This platform is not supported.");
        }
    }
}
