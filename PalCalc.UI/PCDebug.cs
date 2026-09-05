using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI
{
    internal static class PCDebug
    {
        public static readonly LogEventLevel DefaultFileLogLevel;
        public static readonly LoggingLevelSwitch FileLogLevel;

        static PCDebug()
        {
            DefaultFileLogLevel = LogEventLevel.Information;
            FileLogLevel = new(DefaultFileLogLevel);
        }

        [StackTraceHidden]
        public static T HandleErrors<T>(Func<T> action, Func<Exception, T> handleErr)
        {
#if HANDLE_ERRORS
            try { return action(); }
            catch (Exception ex) { return handleErr(ex); }
#else
            return action();
#endif
        }
    }
}
