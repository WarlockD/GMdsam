using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.DebuggerVisualizers;
using System.Windows.Forms;

[assembly: System.Diagnostics.DebuggerVisualizer(
typeof(DFSListVisualizer.DebuggerSide),
typeof(VisualizerObjectSource),
Target = typeof(System.String),
Description = "Simple debugger for DFS")]
namespace DFSListVisualizer
{
    public class DebuggerSide : DialogDebuggerVisualizer
    {
        override protected void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            MessageBox.Show(objectProvider.GetObject().ToString());
        }
        public static void TestShowVisualizer(object objectToVisualize)
        {
            VisualizerDevelopmentHost visualizerHost = new VisualizerDevelopmentHost(objectToVisualize, typeof(DebuggerSide));
            visualizerHost.ShowVisualizer();
        }
    }

}
