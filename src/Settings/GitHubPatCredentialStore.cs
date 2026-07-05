using System.Runtime.InteropServices;
using System.Text;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Хранение GitHub PAT в Windows Credential Manager (не в JSON-файлах).
/// </summary>
internal static class GitHubPatCredentialStore
{
    public const string TargetName = "GitHubWallpaper/PersonalAccessToken";

    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;

    /// <summary>Возвращает сохранённый PAT или <c>null</c>, если токен не задан.</summary>
    public static string? Read()
    {
        if (!CredRead(TargetName, CredentialTypeGeneric, 0, out var credentialPtr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPtr);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
            {
                return null;
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    /// <summary>Сохраняет PAT в Credential Manager.</summary>
    public static void Save(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var blob = Encoding.UTF8.GetBytes(token.Trim());
        var blobPtr = Marshal.AllocCoTaskMem(blob.Length);

        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);

            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = TargetName,
                UserName = "GitHubPersonalAccessToken",
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPtr,
                Persist = CredentialPersistLocalMachine,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException(
                    $"Не удалось сохранить PAT в Credential Manager (код {Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(blobPtr);
        }
    }

    /// <summary>Удаляет сохранённый PAT.</summary>
    public static void Delete()
    {
        if (!CredDelete(TargetName, CredentialTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            // 1168 = ERROR_NOT_FOUND — токена уже нет
            if (error != 1168)
            {
                throw new InvalidOperationException(
                    $"Не удалось удалить PAT из Credential Manager (код {error}).");
            }
        }
    }

    /// <summary>Есть ли сохранённый PAT.</summary>
    public static bool Exists() => Read() is not null;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(
        string target,
        uint type,
        uint reservedFlag,
        out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite([In] ref NativeCredential userCredential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
