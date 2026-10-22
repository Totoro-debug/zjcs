using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Zjcs.Offline.State
{
    public enum SaveReadStatus
    {
        Missing,
        Primary,
        Recovered,
        Corrupt
    }

    public sealed class SaveReadResult
    {
        public SaveReadStatus Status { get; }
        public string Json { get; }

        internal SaveReadResult(SaveReadStatus status, string json)
        {
            Status = status;
            Json = json;
        }
    }

    public static class LocalJsonSaveFile
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static SaveReadResult Read(string path, Func<string, bool> isValid)
        {
            RequireArguments(path, isValid);

            bool hasPrimary = File.Exists(path);
            string primary = hasPrimary ? TryReadText(path) : null;
            if (primary != null && isValid(primary))
                return new SaveReadResult(SaveReadStatus.Primary, primary);

            string backupPath = path + ".bak";
            string backup = File.Exists(backupPath) ? TryReadText(backupPath) : null;
            if (backup != null && isValid(backup))
                return new SaveReadResult(SaveReadStatus.Recovered, backup);

            return new SaveReadResult(hasPrimary || File.Exists(backupPath)
                ? SaveReadStatus.Corrupt
                : SaveReadStatus.Missing, null);
        }

        public static void Write(string path, string json, Func<string, bool> isValid)
        {
            RequireArguments(path, isValid);
            if (json == null || !isValid(json))
                throw new InvalidDataException("The save payload does not match the current profile format.");

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);

            string temporaryPath = fullPath + ".tmp";
            string backupPath = fullPath + ".bak";
            string unreadablePath = fullPath + ".unreadable";
            try
            {
                WriteNewContents(temporaryPath, json);
                if (File.Exists(fullPath))
                {
                    byte[] previousBytes = File.ReadAllBytes(fullPath);
                    string previous = TryDecode(previousBytes);
                    if (previous != null && isValid(previous))
                        WriteAtomically(backupPath, previousBytes);
                    else
                        WriteAtomically(unreadablePath, previousBytes);

                    ReplaceAtomically(temporaryPath, fullPath);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static void WriteAtomically(string path, byte[] content)
        {
            string temporaryPath = path + ".tmp";
            try
            {
                WriteNewContents(temporaryPath, content);
                if (File.Exists(path))
                    ReplaceAtomically(temporaryPath, path);
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static void WriteNewContents(string path, string content)
        {
            WriteNewContents(path, Utf8.GetBytes(content));
        }

        private static void ReplaceAtomically(string source, string destination)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.Replace(source, destination, null);
                    return;
                }
                catch (IOException error) when (attempt < 3 && File.Exists(source)
                    && ((error.HResult & 0xffff) == 32 || (error.HResult & 0xffff) == 33
                        || (error.HResult & 0xffff) == 1175 || (error.HResult & 0xffff) == 1176))
                {
                    // Windows indexers may briefly hold the previous primary/backup.
                    // Retry the atomic operation; never delete a valid destination.
                    Thread.Sleep(10 << attempt);
                }
            }
        }

        private static void WriteNewContents(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static string TryReadText(string path)
        {
            return TryDecode(File.ReadAllBytes(path));
        }

        private static string TryDecode(byte[] bytes)
        {
            try
            {
                return Utf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return null;
            }
        }

        private static void RequireArguments(string path, Func<string, bool> isValid)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A save path is required.", nameof(path));
            if (isValid == null)
                throw new ArgumentNullException(nameof(isValid));
        }
    }
}
