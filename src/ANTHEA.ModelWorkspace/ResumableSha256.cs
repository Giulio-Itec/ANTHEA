using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ANTHEA.ModelWorkspace;

/// <summary>
/// Rilievo R1 (16/11/2026): a SHA-256 whose running state can be duplicated, so that the fingerprint of a copy whose result sets are those of
/// the previous copy plus new ones is computed from the state reached by the previous one (<see cref="ModelDocumentEntry"/>), instead of
/// inflating and hashing every block again at each publication of a series. The hash of Windows CNG (BCryptCreateHash, BCryptDuplicateHash
/// with the pseudo-handle of SHA-256, Windows 10 and later): the same algorithm System.Security.Cryptography uses on Windows, so the same
/// digest as SHA256.HashData of the same bytes. .NET 8 has no IncrementalHash.Clone; elsewhere <see cref="Supported"/> is false and the
/// fingerprint is computed from all the bytes, as before.
/// </summary>
internal sealed class ResumableSha256 : IDisposable
{
    private const nint Sha256Algorithm = 0x41; // BCRYPT_SHA256_ALG_HANDLE
    private readonly HashHandle handle;

    private ResumableSha256(HashHandle handle) => this.handle = handle;

    /// <summary>True when the state can be duplicated on this system (Windows 10 or later).</summary>
    internal static bool Supported => OperatingSystem.IsWindowsVersionAtLeast(10);

    internal static ResumableSha256 Create()
    {
        Check(BCryptCreateHash(Sha256Algorithm, out var created, IntPtr.Zero, 0, IntPtr.Zero, 0, 0), "BCryptCreateHash");
        return new(created);
    }

    internal void Append(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return;
        Check(BCryptHashData(handle, ref MemoryMarshal.GetReference(data), data.Length, 0), "BCryptHashData");
    }

    /// <summary>A new hash with the same state; this one is unchanged.</summary>
    internal ResumableSha256 Duplicate()
    {
        Check(BCryptDuplicateHash(handle, out var copy, IntPtr.Zero, 0, 0), "BCryptDuplicateHash");
        return new(copy);
    }

    /// <summary>The digest of the bytes appended; the hash cannot be used afterwards.</summary>
    internal byte[] Finish()
    {
        var digest = new byte[32];
        Check(BCryptFinishHash(handle, digest, digest.Length, 0), "BCryptFinishHash");
        Dispose();
        return digest;
    }

    public void Dispose() => handle.Dispose();

    private static void Check(int status, string call)
    {
        if (status != 0) throw new InvalidOperationException($"{call} non riuscito (0x{status:X8}).");
    }

    private sealed class HashHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public HashHandle() : base(true) { }
        protected override bool ReleaseHandle() => BCryptDestroyHash(handle) == 0;
    }

    [DllImport("bcrypt.dll")]
    private static extern int BCryptCreateHash(nint algorithm, out HashHandle hash, IntPtr hashObject, int hashObjectLength, IntPtr secret, int secretLength, int flags);

    [DllImport("bcrypt.dll")]
    private static extern int BCryptHashData(HashHandle hash, ref byte input, int inputLength, int flags);

    [DllImport("bcrypt.dll")]
    private static extern int BCryptDuplicateHash(HashHandle hash, out HashHandle copy, IntPtr hashObject, int hashObjectLength, int flags);

    [DllImport("bcrypt.dll")]
    private static extern int BCryptFinishHash(HashHandle hash, byte[] output, int outputLength, int flags);

    [DllImport("bcrypt.dll")]
    private static extern int BCryptDestroyHash(IntPtr hash);
}
