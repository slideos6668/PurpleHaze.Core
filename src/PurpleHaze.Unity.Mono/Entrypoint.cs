using System;
using System.Threading;
using PurpleHaze.Unity.Mono;

namespace Doorstop
{
    /// <summary>
    /// Unity Doorstop front door. Doorstop calls this before the game finishes starting.
    /// No BepInEx — this is Purple Haze's own entry into the Mono domain.
    /// </summary>
    public static class Entrypoint
    {
        private static int _started;

        /// <summary>Doorstop 3/4 compatible signature: parameterless static void.</summary>
        public static void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return;

            try
            {
                // Never do heavy work on the Doorstop call stack — only mscorlib may exist yet.
                var t = new Thread(GameBootstrap.Run)
                {
                    IsBackground = true,
                    Name = "PurpleHaze.GameBootstrap"
                };
                t.Start();
            }
            catch (Exception ex)
            {
                GameBootstrap.EmergencyLog("Entrypoint.Start: " + ex);
            }
        }
    }
}
