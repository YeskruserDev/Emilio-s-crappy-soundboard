using System;
using DiscordRPC;

namespace EmiliosSoundboard
{
    /// <summary>Best-effort Discord Rich Presence. It does not require or store a user token.</summary>
    internal sealed class DiscordPresence : IDisposable
    {
        private const string ApplicationId = "1564710814374436884";
        private readonly DiscordRpcClient client;
        public DiscordPresence()
        {
            try
            {
                client = new DiscordRpcClient(ApplicationId);
                client.Initialize();
                Update(false);
            }
            catch { try { client?.Dispose(); } catch { } }
        }
        public void Update(bool coolMode)
        {
            try
            {
                if (client == null || !client.IsInitialized) return;
                client.SetPresence(new RichPresence
                {
                    Details = coolMode ? "Exploring Rainbow Road" : "Using the soundboard",
                    State = "Emilio's Soundboard",
                    Timestamps = Timestamps.Now
                });
            }
            catch { /* Discord may be closed or Rich Presence may be disabled. */ }
        }
        public void Dispose() { try { client?.ClearPresence(); } catch { } try { client?.Dispose(); } catch { } }
    }
}
