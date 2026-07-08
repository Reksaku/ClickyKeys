using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClickyKeys
{
    /// <summary>
    /// Reader/writer for the inbox state file
    /// <c>%AppData%\ClickyKeys\messages.dat</c>.
    ///
    /// <para>
    /// Deliberately NOT stored in <c>config.json</c>: that file is intentionally
    /// plain and user-editable (settings, language, shortcuts) and we want to
    /// keep it that way. The inbox state — the delivery cursor, the read set,
    /// and the delivered-message cache — has a different threat model: it should
    /// not be trivially editable, and a corrupt/tampered store must never break
    /// the main config.
    /// </para>
    ///
    /// <para>
    /// Protection is two-layered. The strongest layer is the <b>cursor</b>,
    /// which is an opaque token signed by the server (see MessagesService /
    /// MESSAGES_PLAN.md) — a user can't forge a valid one without the server's
    /// secret. On top of that the whole file is <b>DPAPI</b>-encrypted
    /// (<see cref="DataProtectionScope.CurrentUser"/>), so it isn't readable
    /// plain text and is bound to the Windows account. This is not
    /// cryptographically binding against the user themselves (they could call
    /// DPAPI as themselves), but it defeats casual JSON editing, which is the
    /// stated goal.
    /// </para>
    ///
    /// <para>
    /// Any failure — file missing, decrypt failure (moved from another account,
    /// hand-edited), or unparseable JSON — self-heals to a fresh empty
    /// <see cref="MessagesState"/>, mirroring <see cref="ConfigStore"/>'s
    /// "corrupt config self-heals to defaults" behaviour. On the next fetch the
    /// server simply issues a new cursor.
    /// </para>
    ///
    /// Writes are serialized behind <see cref="_gate"/> and go through
    /// <see cref="AtomicFile"/> (base64 of the ciphertext), so a crash or a
    /// concurrent write can't leave a half-written file.
    /// </summary>
    internal static class MessagesStore
    {
        private static readonly object _gate = new();

        // Optional extra entropy mixed into DPAPI. Not a secret (it ships in the
        // binary) — it just ensures our blob can't be round-tripped by an
        // unrelated app that happens to DPAPI-protect the same bytes.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ClickyKeys.messages.v1");

        private static readonly JsonSerializerOptions ReadOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private static readonly JsonSerializerOptions WriteOpts = new()
        {
            WriteIndented = false,
        };

        public static string StorePath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ClickyKeys");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "messages.dat");
            }
        }

        /// <summary>
        /// Returns the persisted inbox state, or a fresh empty
        /// <see cref="MessagesState"/> when the file is missing, can't be
        /// decrypted, or can't be parsed. Never throws.
        /// </summary>
        public static MessagesState Load()
        {
            lock (_gate)
            {
                return LoadNoLock();
            }
        }

        /// <summary>Persists <paramref name="state"/> wholesale.</summary>
        public static void Save(MessagesState state)
        {
            lock (_gate)
            {
                SaveNoLock(state);
            }
        }

        /// <summary>
        /// Load-mutate-save under the lock, so concurrent writers don't
        /// overwrite each other's unrelated fields.
        /// </summary>
        public static void Update(Action<MessagesState> mutate)
        {
            lock (_gate)
            {
                var state = LoadNoLock();
                mutate(state);
                SaveNoLock(state);
            }
        }

        private static MessagesState LoadNoLock()
        {
            try
            {
                var path = StorePath;
                if (!File.Exists(path))
                    return new MessagesState();

                var base64 = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(base64))
                    return new MessagesState();

                var protectedBytes = Convert.FromBase64String(base64);
                var plainBytes = ProtectedData.Unprotect(
                    protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(plainBytes);

                return JsonSerializer.Deserialize<MessagesState>(json, ReadOpts)
                       ?? new MessagesState();
            }
            catch (Exception ex)
            {
                // Missing/tampered/foreign-account/corrupt → self-heal.
                Debug.WriteLine($"MessagesStore.Load failed (self-healing to empty): {ex}");
                return new MessagesState();
            }
        }

        private static void SaveNoLock(MessagesState state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state, WriteOpts);
                var plainBytes = Encoding.UTF8.GetBytes(json);
                var protectedBytes = ProtectedData.Protect(
                    plainBytes, Entropy, DataProtectionScope.CurrentUser);
                AtomicFile.WriteAllText(StorePath, Convert.ToBase64String(protectedBytes));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MessagesStore.Save failed: {ex}");
            }
        }
    }
}
