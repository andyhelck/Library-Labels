using System;
using System.Windows.Forms;

namespace Library_Labels_Namespace
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Library_Labels_Class());
        }
    }
}
