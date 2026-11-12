using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace NxDrawingPdfExporter.Core.Output;

public sealed class TargetSnapshot
{
    public static TargetSnapshot Missing { get; } = new TargetSnapshot(false, 0, 0, "");

    public TargetSnapshot(bool exists, long length, long writeTime, string sha256, string identity = "")
    {
        Exists = exists;
        Length = length;
        WriteTime = writeTime;
        Sha256 = sha256;
        Identity = identity;
    }

    public bool Exists { get; }
    public long Length { get; }
    public long WriteTime { get; }
    public string Sha256 { get; }
    public string Identity { get; }

    public bool Matches(TargetSnapshot other) => Exists == other.Exists &&
        (!Exists || (Length == other.Length && WriteTime == other.WriteTime && Sha256 == other.Sha256 && Identity == other.Identity));

    public bool MatchesContent(TargetSnapshot other) => Exists && other.Exists && Length == other.Length && Sha256 == other.Sha256;
}

public interface ITargetStateReader
{
    TargetSnapshot Capture(string path);
}

public sealed class PhysicalTargetStateReader : ITargetStateReader
{
    public TargetSnapshot Capture(string path)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (FileNotFoundException)
        {
            return TargetSnapshot.Missing;
        }

        using (stream)
        using (var sha = SHA256.Create())
        {
            if (!GetFileInformationByHandle(stream.SafeFileHandle, out var info))
                throw new IOException("Cannot obtain the PDF file identity.", new Win32Exception(Marshal.GetLastWin32Error()));
            return new TargetSnapshot(true, stream.Length, ((long)info.WriteHigh << 32) | info.WriteLow,
                Convert.ToBase64String(sha.ComputeHash(stream)), $"{info.Volume:X8}:{info.IndexHigh:X8}{info.IndexLow:X8}");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
}
