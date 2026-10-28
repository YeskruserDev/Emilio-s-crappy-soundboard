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
                Log.Debug("discord", "rich presence connected");
                Update(false);
            }
            catch (Exception ex)
            {
                // Discord being closed is normal; keep it at debug/warn so the log stays useful.
                Log.Warn("discord", "rich presence unavailable (is the Discord desktop app running?)", ex);
                try { client?.Dispose(); } catch { }
            }
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
            catch (Exception ex)
            {
                // Discord may be closed or Rich Presence may be disabled in its settings.
                Log.Debug("discord", "presence update failed: " + ex.Message);
            }
        }
        public void Dispose()
        {
            try { client?.ClearPresence(); } catch { }
            try { client?.Dispose(); } catch { }
        }
    }
}
