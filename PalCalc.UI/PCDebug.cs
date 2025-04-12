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
