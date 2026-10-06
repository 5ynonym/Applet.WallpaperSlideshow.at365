using System.Runtime.InteropServices;

namespace Applets.WallpaperSlideshow;

internal static class ImageDeletion
{
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem {
        void BindToHandler(IntPtr context, in Guid handler, in Guid iid, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint name, out IntPtr result);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }
    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation {
        void Advise(IntPtr sink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog(IntPtr dialog);
        void SetProperties(IntPtr properties);
        void SetOwnerWindow(uint window);
        void ApplyPropertiesToItem(IntPtr item);
        void ApplyPropertiesToItems(IntPtr items);
        void RenameItem(IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink);
        void RenameItems(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string name);
        void MoveItem(IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink);
        void MoveItems(IntPtr items, IntPtr destination);
        void CopyItem(IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink);
        void CopyItems(IntPtr items, IntPtr destination);
        void DeleteItem(IShellItem item, IntPtr sink);
        void DeleteItems(IntPtr items);
        void NewItem(IntPtr destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, IntPtr sink);
        void PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr context, in Guid iid, out IShellItem item);
    public static void Recycle(string path) {
        var fullPath = Path.GetFullPath(path);
        // Only one exact history file; no wildcards, directory traversal, or recursive deletion.
        if (fullPath.Contains('\0') || fullPath.Contains('*') || fullPath.Contains('?') || Directory.Exists(fullPath))
            throw new InvalidOperationException("履歴の画像ファイルだけ削除できます。");
        Exception? failure = null;
        var thread = new Thread(() => {
            IFileOperation? operation = null;
            IShellItem? item = null;
            try {
                operation = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09"))!)!;
                SHCreateItemFromParsingName(fullPath, IntPtr.Zero, new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), out item);
                item.GetAttributes(0x20000000, out var attributes);
                if ((attributes & 0x20000000) != 0) throw new IOException("フォルダーは削除できません。");
                // RECYCLEONDELETE plus EARLYFAILURE: never silently fall back to permanent deletion.
                operation.SetOperationFlags(0x00080000 | 0x00100000 | 0x20000000 | 0x10 | 0x400 | 0x4);
                operation.DeleteItem(item, IntPtr.Zero);
                operation.PerformOperations();
                operation.GetAnyOperationsAborted(out var aborted);
                if (aborted || File.Exists(fullPath)) throw new IOException("画像をごみ箱へ移せませんでした。");
            } catch (Exception error) { failure = error; }
            finally {
                if (item is not null) Marshal.FinalReleaseComObject(item);
                if (operation is not null) Marshal.FinalReleaseComObject(operation);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (failure is not null) throw new IOException("画像をごみ箱へ移せませんでした。", failure);
    }
}
