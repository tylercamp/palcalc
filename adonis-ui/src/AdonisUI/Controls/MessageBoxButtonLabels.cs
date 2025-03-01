using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AdonisUI.Controls
{
    internal class MessageBoxButtonLabels
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr MB_GetString(uint wBtn);

        private static string GetMessageBoxText(uint buttonId, string fallback)
        {
            try
            {
                IntPtr ptr = MB_GetString(buttonId);

                // https://learn.microsoft.com/en-us/windows/win32/menurc/common-control-parameters
                // "An ampersand (&) character in the text indicates that the following character is used as a
                // mnemonic character for the control. When the control is displayed, the ampersand is not
                // shown, but the mnemonic character is underlined."
                return Marshal.PtrToStringUni(ptr)?.TrimStart('&') ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static string ok;
        public static string Ok => ok ??= GetMessageBoxText(0, "OK");

        private static string cancel;
        public static string Cancel => cancel ??= GetMessageBoxText(1, "Cancel");

        private static string yes;
        public static string Yes => yes ??= GetMessageBoxText(5, "Yes");

        private static string no;
        public static string No => no ??= GetMessageBoxText(6, "No");
    }
}
