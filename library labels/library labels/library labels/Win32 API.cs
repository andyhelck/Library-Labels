using System;
using System.Runtime.InteropServices;




namespace Library_Labels_Namespace
{
    public class Win32_API
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public Int32 left;
            public Int32 top;
            public Int32 right;
            public Int32 bottom;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct CHARRANGE
        {
            public Int32 cpMin;
            public Int32 cpMax;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FORMATRANGE
        {
            public IntPtr hdc;
            public IntPtr hdcTarget;
            public RECT rc;
            public RECT rcPage;
            public CHARRANGE chrg;
        }

        [DllImport("user32.dll")]
        internal static extern Int32 SendMessage(IntPtr hWnd, Int32 msg, Int32 wParam, IntPtr lParam);

        internal const Int32 WM_USER = 0x400;
        internal const Int32 EM_FORMATRANGE = WM_USER + 57;
    }
}
