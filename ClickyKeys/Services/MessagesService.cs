using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClickyKeys
{
    /// <summary>
    /// Fetches announcements/news incrementally from
    /// <c>clickykeys.fun/api/messages.php</c> and maintains the local inbox
    /// state via <see cref="MessagesStore"/>.
    ///
    /// <para>
    /// Modelled on <see cref="SponsorshipService"/> / TelemetryService: one
    /// long-lived <see cref="HttpClient"/> carrying the same User-Agent tokens
    /// as the other API clients, a fire-and-forget entry point safe to call at
    /// startup, and total exception-swallowing — the inbox must never surface
    /// an error to the user or affect app behaviour.
    /// </para>
    ///
    /// <para>
    /// Incremental protocol: the client sends the opaque, server-signed
    /// <c>cursor</c> it stored last time (or nothing on first run); the server
    /// returns only messages newer than it plus a fresh cursor. Delivery-time
    /// gating ("a new user gets no backlog") is therefore enforced server-side
    /// by the cursor, not by any client clock. See MESSAGES_PLAN.md.
    /// </para>
    ///
    /// PHASE 1: fetch + filter + persistence only. No UI — results are written
    /// to the store and logged to <see cref="Debug"/>. The Inbox window and the
    /// unread badge come in Phase 2 and will read <see cref="GetCachedMessages"/>
    /// / <see cref="UnreadCount"/> and call <see cref="MarkRead"/>.
    /// </summary>
    public sealed class MessagesService
    {
        // Endpoint is chosen by build channel, not hardcoded: dev builds talk to
        // staging (for testing), while real store/github builds hit production.
        // This mirrors how the distribution channel itself is a compile-time
        // constant in BuildInfo, so a shipped build can't accidentally point at
        // staging and the choice can't be flipped from config.json.
        private static readonly string Endpoint = ResolveEndpoint();

        private static string ResolveEndpoint()
        {
            var host = BuildInfo.Distribution == DistributionType.dev
                ? "https://staging.clickykeys.fun"
                : "https://clickykeys.fun";
            return host + "/api/messages.php";
        }

        private static readonly HttpClient _http = BuildHttpClient();

        /// <summary>
        /// Raised after each successful fetch (including ones that added
        /// nothing), with the number of NEW messages added. Fires on the
        /// fetch's background thread, so subscribers must marshal to the UI
        /// thread themselves. Lets the UI light up the unread badges the moment
        /// a message is received, without waiting for user interaction.
        /// </summary>
        public event Action<int>? Updated;

        private static HttpClient BuildHttpClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            // Same identity flags the other endpoints see. Distribution and
            // version drive server-side targeting, so they must be present and
            // must come from BuildInfo (not config.json, which the user can
            // edit) — matching RequestReleasesAPI.
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("ClickyKeysApp", BuildInfo.Version));
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Distro", BuildInfo.Distribution.ToString()));
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Type", "application"));
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Trigger", App.LaunchTrigger));

            return http;
        }

        /// <summary>
        /// Fire-and-forget fetch, safe to call unconditionally from
        /// App.OnStartup. Never awaited by the caller — startup must not wait
        /// on network I/O — and every exception is caught inside the task.
        /// </summary>
        public void FetchAtStartup()
        {
            _ = Task.Run(async () =>
            {
                try { await FetchAsync().ConfigureAwait(false); }
                catch (Exception ex) { Debug.WriteLine($"MessagesService.FetchAtStartup failed: {ex}"); }
            });
        }

        /// <summary>
        /// Performs one incremental fetch: sends the stored cursor, filters the
        /// returned messages, appends the new ones to the cache (de-duplicated),
        /// and persists the fresh cursor. Returns the number of NEW messages
        /// added, or 0 on any failure. Never throws.
        /// </summary>
        public async Task<int> FetchAsync()
        {
            try
            {
                var state = MessagesStore.Load();

                var url = Endpoint;
                if (!string.IsNullOrEmpty(state.Cursor))
                    url += "?cursor=" + Uri.EscapeDataString(state.Cursor);

                var json = await _http.GetStringAsync(url).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                    return 0;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var response = JsonSerializer.Deserialize<MessagesResponse>(json, options);
                if (response == null)
                    return 0;

                var existingIds = new HashSet<int>(state.Cache.Select(m => m.Id));
                int added = 0;

                foreach (var msg in response.Messages)
                {
                    if (!PassesClientFilter(msg)) continue;
                    if (!existingIds.Add(msg.Id)) continue; // already cached

                    state.Cache.Add(msg);
                    added++;
                }

                // Only advance the cursor when the server actually gave us a new
                // one. On error responses (empty cursor) we keep the old cursor
                // so the next launch re-fetches the same window.
                if (!string.IsNullOrEmpty(response.Cursor))
                    state.Cursor = response.Cursor;

                // Housekeeping: drop expired messages and orphaned read ids so
                // the on-disk cache doesn't grow without bound.
                PruneState(state);

                MessagesStore.Save(state);

                Debug.WriteLine(
                    $"MessagesService: +{added} new message(s), cache={state.Cache.Count}, " +
                    $"unread={UnreadCountFrom(state)}, cursor set={(!string.IsNullOrEmpty(state.Cursor))}");

                // Notify the UI (badges) that state changed. Raised on this
                // background thread; handlers marshal to the UI thread.
                Updated?.Invoke(added);

                return added;
            }
            catch (Exception ex)
            {
                // No connection, DNS failure, 5xx, timeout, bad JSON — all
                // silent. The cursor is untouched, so nothing is lost.
                Debug.WriteLine($"MessagesService.FetchAsync failed: {ex}");
                return 0;
            }
        }

        /// <summary>
        /// Client-side defensive re-check of the server's targeting, plus the
        /// expiry gate. The server already filters by channel/version from the
        /// User-Agent tokens; this guards against a mis-tagged message.
        /// </summary>
        private static bool PassesClientFilter(MessageEntry msg)
        {
            if (msg.ExpiresAt is { } exp && exp <= DateTime.UtcNow)
                return false;

            var target = msg.Target;
            if (target == null)
                return true;

            if (!MatchesDistribution(target.Distributions))
                return false;

            if (!MatchesVersion(target.Version))
                return false;

            return true;
        }

        private static bool MatchesDistribution(List<string> distributions)
        {
            // No restriction → everyone.
            if (distributions == null || distributions.Count == 0)
                return true;

            // Dev builds see everything, so a locally-built tester can preview
            // store/github-targeted messages.
            if (BuildInfo.Distribution == DistributionType.dev)
                return true;

            var mine = BuildInfo.Distribution.ToString();
            return distributions.Any(d =>
                string.Equals(d?.Trim(), mine, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Version rule matcher. "" = all; "2.4.0+" = this version or newer;
        /// "2.4.2" = exactly that version. Unparseable rules fail closed
        /// (message hidden) rather than being shown to the wrong audience.
        /// </summary>
        private static bool MatchesVersion(string rule)
        {
            if (string.IsNullOrWhiteSpace(rule))
                return true;

            if (!Version.TryParse(BuildInfo.Version, out var current))
                return false;

            rule = rule.Trim();

            if (rule.EndsWith('+'))
            {
                var minText = rule[..^1].Trim();
                return Version.TryParse(minText, out var min) && current >= min;
            }

            return Version.TryParse(rule, out var exact) && current == exact;
        }

        /// <summary>
        /// Removes messages past their <c>expires_at</c> and read ids that no
        /// longer map to a cached message, in place. Keeps the persisted cache
        /// bounded over time.
        /// </summary>
        private static void PruneState(MessagesState state)
        {
            var now = DateTime.UtcNow;

            state.Cache.RemoveAll(m => m.ExpiresAt is { } exp && exp <= now);

            var liveIds = new HashSet<int>(state.Cache.Select(m => m.Id));
            state.ReadIds.RemoveAll(id => !liveIds.Contains(id));
        }

        // ── Read/unread helpers (used by the Phase 2 UI) ───────────────────────

        /// <summary>
        /// All cached, non-expired messages, newest first. Expired entries are
        /// filtered here too (not only pruned on fetch) so a long-running
        /// session never shows a message past its expiry.
        /// </summary>
        public IReadOnlyList<MessageEntry> GetCachedMessages()
        {
            var now = DateTime.UtcNow;
            var state = MessagesStore.Load();
            return state.Cache
                .Where(m => m.ExpiresAt is not { } exp || exp > now)
                .OrderByDescending(m => m.PublishAt)
                .ToList();
        }

        /// <summary>
        /// Snapshot of the ids the user has already read. The inbox reads this
        /// once when it opens — BEFORE marking the shown messages read — so it
        /// can render previously-read messages collapsed and unread ones
        /// expanded.
        /// </summary>
        public IReadOnlyCollection<int> GetReadIds()
            => new HashSet<int>(MessagesStore.Load().ReadIds);

        /// <summary>Count of cached, non-expired messages not yet read.</summary>
        public int UnreadCount() => UnreadCountFrom(MessagesStore.Load());

        private static int UnreadCountFrom(MessagesState state)
        {
            var read = new HashSet<int>(state.ReadIds);
            var now = DateTime.UtcNow;
            return state.Cache.Count(m =>
                !read.Contains(m.Id) && (m.ExpiresAt is not { } exp || exp > now));
        }

        /// <summary>Marks a message read (idempotent).</summary>
        public void MarkRead(int id)
        {
            MessagesStore.Update(state =>
            {
                if (!state.ReadIds.Contains(id))
                    state.ReadIds.Add(id);
            });
        }

        /// <summary>
        /// Permanently removes a message from the local cache (and its read
        /// mark). It won't reappear: the incremental cursor has already advanced
        /// past it, so the server won't send it again.
        /// </summary>
        public void Delete(int id)
        {
            MessagesStore.Update(state =>
            {
                state.Cache.RemoveAll(m => m.Id == id);
                state.ReadIds.RemoveAll(x => x == id);
            });
        }
    }
}
