using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betteribttest
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

         //   ChunkReader cr = new ChunkReader("Undertale\\UNDERTALE.EXE", false);
         //   Disam dism = new Disam(cr);
          //  dism.writeFile("frog");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
