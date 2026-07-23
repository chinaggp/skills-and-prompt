using System.Runtime.InteropServices;
using WindowsBlackHole.Core;

namespace WindowsBlackHole.Shell;

public sealed class RecycleBinService
{
    private const uint FofSilent = 0x0004;
    private const uint FofNoConfirmation = 0x0010;
    private const uint FofAllowUndo = 0x0040;
    private const uint FofNoErrorUi = 0x0400;
    private const uint FofxRecycleOnDelete = 0x00080000;

    public void MoveToRecycleBin(IEnumerable<string> candidates, nint ownerWindow = default)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("回收站操作必须在 STA 线程执行。");
        }

        var validation = DropValidator.Validate(candidates);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var operationType = Type.GetTypeFromCLSID(
            new Guid("3AD05575-8857-4850-9277-11B85BDB8E09"),
            throwOnError: true)!;
        var operation = (IFileOperation)Activator.CreateInstance(operationType)!;

        try
        {
            ThrowIfFailed(operation.SetOwnerWindow(ownerWindow));
            ThrowIfFailed(operation.SetOperationFlags(
                FofSilent |
                FofNoConfirmation |
                FofAllowUndo |
                FofNoErrorUi |
                FofxRecycleOnDelete));

            foreach (var path in validation.Paths)
            {
                var item = CreateShellItem(path);
                try
                {
                    ThrowIfFailed(operation.DeleteItem(item, nint.Zero));
                }
                finally
                {
                    Marshal.FinalReleaseComObject(item);
                }
            }

            ThrowIfFailed(operation.PerformOperations());
            ThrowIfFailed(operation.GetAnyOperationsAborted(out var aborted));
            if (aborted)
            {
                throw new OperationCanceledException("回收站操作已取消。");
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(operation);
        }
    }

    private static IShellItem CreateShellItem(string path)
    {
        var interfaceId = typeof(IShellItem).GUID;
        ThrowIfFailed(SHCreateItemFromParsingName(
            path,
            nint.Zero,
            ref interfaceId,
            out var item));
        return item;
    }

    private static void ThrowIfFailed(int hResult)
    {
        if (hResult < 0)
        {
            Marshal.ThrowExceptionForHR(hResult);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        nint bindingContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig]
        int BindToHandler(nint bindingContext, ref Guid handlerId, ref Guid interfaceId, out nint result);

        [PreserveSig]
        int GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem parent);

        [PreserveSig]
        int GetDisplayName(uint displayNameType, out nint name);

        [PreserveSig]
        int GetAttributes(uint mask, out uint attributes);

        [PreserveSig]
        int Compare(
            [MarshalAs(UnmanagedType.Interface)] IShellItem other,
            uint hint,
            out int order);
    }

    [ComImport]
    [Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        [PreserveSig] int Advise(nint progressSink, out uint cookie);
        [PreserveSig] int Unadvise(uint cookie);
        [PreserveSig] int SetOperationFlags(uint operationFlags);
        [PreserveSig] int SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        [PreserveSig] int SetProgressDialog(nint progressDialog);
        [PreserveSig] int SetProperties(nint propertyChangeArray);
        [PreserveSig] int SetOwnerWindow(nint ownerWindow);
        [PreserveSig] int ApplyPropertiesToItem([MarshalAs(UnmanagedType.Interface)] IShellItem item);
        [PreserveSig] int ApplyPropertiesToItems(nint items);
        [PreserveSig] int RenameItem(
            [MarshalAs(UnmanagedType.Interface)] IShellItem item,
            [MarshalAs(UnmanagedType.LPWStr)] string newName,
            nint progressSink);
        [PreserveSig] int RenameItems(nint items, [MarshalAs(UnmanagedType.LPWStr)] string newName);
        [PreserveSig] int MoveItem(
            [MarshalAs(UnmanagedType.Interface)] IShellItem item,
            [MarshalAs(UnmanagedType.Interface)] IShellItem destinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string? newName,
            nint progressSink);
        [PreserveSig] int MoveItems(nint items, [MarshalAs(UnmanagedType.Interface)] IShellItem destinationFolder);
        [PreserveSig] int CopyItem(
            [MarshalAs(UnmanagedType.Interface)] IShellItem item,
            [MarshalAs(UnmanagedType.Interface)] IShellItem destinationFolder,
            [MarshalAs(UnmanagedType.LPWStr)] string? copyName,
            nint progressSink);
        [PreserveSig] int CopyItems(nint items, [MarshalAs(UnmanagedType.Interface)] IShellItem destinationFolder);
        [PreserveSig] int DeleteItem(
            [MarshalAs(UnmanagedType.Interface)] IShellItem item,
            nint progressSink);
        [PreserveSig] int DeleteItems(nint items);
        [PreserveSig] int NewItem(
            [MarshalAs(UnmanagedType.Interface)] IShellItem destinationFolder,
            uint fileAttributes,
            [MarshalAs(UnmanagedType.LPWStr)] string name,
            [MarshalAs(UnmanagedType.LPWStr)] string? templateName,
            nint progressSink);
        [PreserveSig] int PerformOperations();
        [PreserveSig] int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
}
