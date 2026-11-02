// SPDX-License-Identifier: GPL-3.0-only
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using EtiketStudio.Core;
using Microsoft.Win32.SafeHandles;

namespace EtiketStudio.Windows.Printing;

public sealed record PrinterInfo(string Name, string Driver, string Port, uint Status)
{
    public bool IsEpl2844 => Regex.IsMatch(Driver, @"TLP\s*2844\b", RegexOptions.IgnoreCase) &&
        !Regex.IsMatch(Driver + " " + Name, @"2844\s*-?\s*Z\b|ZPL", RegexOptions.IgnoreCase);
    public string Display => $"{Name}  ·  {Port}";
}
public sealed record QueueJob(int Id, string Document, string Status, bool Owned);

public sealed class WindowsSpooler
{
    public List<PrinterInfo> Printers()
    {
        Native.EnumPrinters(6, null, 2, IntPtr.Zero, 0, out int needed, out _);
        if (needed == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error is not 0 and not 122) throw Error(error);
            return [];
        }
        IntPtr buffer = Marshal.AllocHGlobal(needed);
        try
        {
            Check(Native.EnumPrinters(6, null, 2, buffer, needed, out _, out int count));
            int size = Marshal.SizeOf<Native.Printer2>(); var result = new List<PrinterInfo>();
            for (int i = 0; i < count; i++)
            {
                var p = Marshal.PtrToStructure<Native.Printer2>(buffer + i * size);
                result.Add(new(p.Name ?? "", p.Driver ?? "", p.Port ?? "", p.Status));
            }
            return result.OrderByDescending(p => p.IsEpl2844).ThenBy(p => p.Name).ToList();
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    public PrinterInfo Require(string name, bool epl)
    {
        if (string.IsNullOrEmpty(name)) throw new DomainException("SelectPrinter");
        var printer = Printers().FirstOrDefault(p => p.Name == name) ?? throw new DomainException("NoPrinter");
        if (epl && !printer.IsEpl2844) throw new DomainException("NotEpl");
        // PAUSED | ERROR | PAPER_JAM | PAPER_OUT | PAPER_PROBLEM | OFFLINE | NOT_AVAILABLE | USER_INTERVENTION | DOOR_OPEN.
        if ((printer.Status & (1u | 2u | 8u | 16u | 64u | 128u | 4096u | 1048576u | 4194304u)) != 0) throw new DomainException("PrinterOffline");
        return printer;
    }
    public int Send(string printer, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default, Action<int>? onJobStarted = null)
    {
        Require(printer, true); cancellationToken.ThrowIfCancellationRequested();
        using var handle = Open(printer);
        var document = new Native.Document { Name = "Zebra TLP 2844 Label Designer " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), DataType = "RAW" };
        int job = Native.StartDocPrinter(handle, 1, ref document);
        if (job == 0) throw Error(Marshal.GetLastWin32Error());
        bool ended = false;
        try
        {
            onJobStarted?.Invoke(job);
            Check(Native.StartPagePrinter(handle));
            byte[] bytes = payload.ToArray(); var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                BytePump.WriteAll(bytes.Length, (offset, count) =>
                {
                    Check(Native.WritePrinter(handle, pinned.AddrOfPinnedObject() + offset, count, out int written));
                    return written;
                }, cancellationToken);
            }
            finally { pinned.Free(); }
            Check(Native.EndPagePrinter(handle)); Check(Native.EndDocPrinter(handle)); ended = true;
            return job; // Accepted by Windows; deliberately not a claim of physical printing.
        }
        finally { if (!ended) Native.AbortPrinter(handle); }
    }
    public List<QueueJob> Jobs(string printer)
    {
        using var handle = Open(printer);
        Native.EnumJobs(handle, 0, 999, 1, IntPtr.Zero, 0, out int needed, out _);
        if (needed == 0)
        {
            int error = Marshal.GetLastWin32Error(); if (error is not 0 and not 122) throw Error(error); return [];
        }
        var buffer = Marshal.AllocHGlobal(needed);
        try
        {
            Check(Native.EnumJobs(handle, 0, 999, 1, buffer, needed, out _, out int count));
            int size = Marshal.SizeOf<Native.Job1>(); var jobs = new List<QueueJob>();
            for (int i = 0; i < count; i++)
            {
                var j = Marshal.PtrToStructure<Native.Job1>(buffer + i * size);
                string state = (j.Status & (2u | 32u | 64u | 512u | 1024u)) != 0 ? "JobError" : (j.Status & 1u) != 0 ? "JobPaused" : (j.Status & 16u) != 0 ? "JobPrinting" : "JobQueued";
                jobs.Add(new(j.Id, j.Document ?? "", T.Get(state), ((j.Document ?? "").StartsWith("Zebra TLP 2844 Label Designer ", StringComparison.Ordinal) || (j.Document ?? "").StartsWith("Etiket Studio ", StringComparison.Ordinal) || (j.Document ?? "").StartsWith("TBRK Label Studio ", StringComparison.Ordinal))));
            }
            return jobs;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    public void Cancel(string printer, int jobId)
    {
        // Refresh before cancelling; never cancel an unrelated queue job by a recycled ID.
        if (!Jobs(printer).Any(j => j.Id == jobId && j.Owned)) throw new DomainException("NoJob");
        using var handle = Open(printer); Check(Native.SetJob(handle, jobId, 0, IntPtr.Zero, 3));
    }
    private static SafePrinterHandle Open(string name)
    {
        if (string.IsNullOrEmpty(name)) throw new DomainException("SelectPrinter");
        Check(Native.OpenPrinter(name, out var handle, IntPtr.Zero)); return handle;
    }
    private static void Check(bool success) { if (!success) throw Error(Marshal.GetLastWin32Error()); }
    private static DomainException Error(int code) => code switch
    {
        5 => new("AccessDenied"), 1801 => new("NoPrinter"), 1722 or 1062 => new("SpoolUnavailable"), _ => new("SpoolError", code)
    };

    private sealed class SafePrinterHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafePrinterHandle() : base(true) { }
        protected override bool ReleaseHandle() => Native.ClosePrinter(handle);
    }
    private static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct Document
        { [MarshalAs(UnmanagedType.LPWStr)] public string? Name, OutputFile, DataType; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct Printer2
        {
            public string? Server, Name, Share, Port, Driver, Comment, Location;
            public IntPtr DevMode; public string? Separator, PrintProcessor, DataType, Parameters; public IntPtr Security;
            public uint Attributes, Priority, DefaultPriority, StartTime, UntilTime, Status, Jobs, AveragePpm;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct SystemTime { public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct Job1
        {
            public int Id; public string? Printer, Machine, User, Document, DataType, StatusText;
            public uint Status, Priority, Position, TotalPages, PagesPrinted; public SystemTime Submitted;
        }
        [DllImport("winspool.drv", EntryPoint = "EnumPrintersW", SetLastError = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumPrinters(int flags, string? name, int level, IntPtr buffer, int size, out int needed, out int returned);
        [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool OpenPrinter(string name, out SafePrinterHandle handle, IntPtr defaults);
        [DllImport("winspool.drv", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ClosePrinter(IntPtr handle);
        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)] internal static extern int StartDocPrinter(SafePrinterHandle handle, int level, ref Document document);
        [DllImport("winspool.drv", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool StartPagePrinter(SafePrinterHandle handle);
        [DllImport("winspool.drv", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EndPagePrinter(SafePrinterHandle handle);
        [DllImport("winspool.drv", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EndDocPrinter(SafePrinterHandle handle);
        [DllImport("winspool.drv", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool AbortPrinter(SafePrinterHandle handle);
        [DllImport("winspool.drv", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool WritePrinter(SafePrinterHandle handle, IntPtr data, int count, out int written);
        [DllImport("winspool.drv", EntryPoint = "EnumJobsW", SetLastError = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumJobs(SafePrinterHandle handle, int first, int number, int level, IntPtr jobs, int size, out int needed, out int returned);
        [DllImport("winspool.drv", EntryPoint = "SetJobW", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetJob(SafePrinterHandle handle, int id, int level, IntPtr info, int command);
    }
}
