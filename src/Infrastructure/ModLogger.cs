using System.Runtime.CompilerServices;
using MelonLoader;

namespace Cms21UiPlus
{
    /// <summary>Central mod logging.</summary>
    public static class ModLogger
    {
        public static void Log(string msg, Types.LoggingLevels loggingLevel,
            [CallerMemberName] string callerName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            msg = (msg ?? string.Empty).Replace("\r\n", "\n");

            switch (loggingLevel) {
                case Types.LoggingLevels.NormalClean:
#if NET6_0_OR_GREATER
                    System.Drawing.Color modColor =
                        System.Drawing.Color.FromArgb(4, 163, 204);
                    MelonLogger.Msg(modColor, msg);
#else
                    MelonLogger.Msg(msg);
#endif
                    break;
                case Types.LoggingLevels.Warning:
                    MelonLogger.Warning(string.Format("[{0}():{1}] {2}",
                        callerName, lineNumber, msg));
                    break;
                case Types.LoggingLevels.Error:
                    MelonLogger.Error(string.Format("[{0}():{1}] {2}",
                        callerName, lineNumber, msg));
                    break;
            }
        }
    }
}
