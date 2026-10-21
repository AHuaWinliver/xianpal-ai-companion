using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace XianPal
{
    internal sealed class ChineseInputTextBox : TextBox
    {
        private bool composing;
        private DateTime committedAt = DateTime.MinValue;
        internal bool IsComposing { get { return composing || (DateTime.UtcNow - committedAt).TotalMilliseconds < 100; } }

        internal ChineseInputTextBox() { ImeMode = ImeMode.On; }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x010D) composing = true; // WM_IME_STARTCOMPOSITION
            if (message.Msg == 0x010E) { composing = false; committedAt = DateTime.UtcNow; }
            base.WndProc(ref message);
        }
    }

    // WinForms may fall back to the legacy RichEdit 2.0 window class. That class
    // stores Unicode text, but it fails to shape many surrogate-pair emoji, ZWJ
    // sequences and variation selectors. RICHEDIT50W uses the Unicode msftedit
    // engine available on supported Windows versions.
    internal sealed class UnicodeRichTextBox : RichTextBox
    {
        private static readonly IntPtr MsftEditModule = LoadLibrary("msftedit.dll");

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr handle, StringBuilder className, int maximumCount);

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                if (MsftEditModule != IntPtr.Zero) parameters.ClassName = "RICHEDIT50W";
                return parameters;
            }
        }

        internal bool UsesUnicodeRichEdit
        {
            get
            {
                if (!IsHandleCreated) CreateControl();
                StringBuilder name = new StringBuilder(64);
                GetClassName(Handle, name, name.Capacity);
                return name.ToString().IndexOf("RICHEDIT50W", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
