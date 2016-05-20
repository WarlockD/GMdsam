using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
namespace GameMaker.Writers.JavaScript
{
    public class ScriptWriter : IScriptWriter
    {
        public void WriteScript(File.Script code, BlockToCode output)
        {
            ILBlock block = Context.DecompileBlock( code.Data);
            if (block == null) return; // error
            int arguments = 0;
            foreach (var v in block.GetSelfAndChildrenRecursive<ILVariable>())
            {
                Match match = Context.ScriptArgRegex.Match(v.Name);
                if (match != null && match.Success)
                {
                    int arg = int.Parse(match.Groups[1].Value) + 1; // we want the count
                    if (arg > arguments) arguments = arg;
                    v.isLocal = true; // arguments are 100% local
                    v.Instance = null;
                    v.InstanceName = null; // clear all this out
                }
            }
            output.WriteLine("_scripts = _scripts or {}");
            output.WriteLine("-- CodeName: {0} ", code.Name);
            string cleanName = code.Name.Replace("gml_Script_", "");

            output.Write("function {0}(self", cleanName);
            for (int i = 0; i < arguments; i++) output.Write(",argument{0}", i);
            output.WriteLine(") {");
            output.WriteMethod(cleanName, block);
            output.WriteLine("}");
            output.WriteLine(); // extra next line
            output.WriteLine("_scripts[{0}] = {1};", code.Index, cleanName);
            output.WriteLine("_scripts[\"{0}\"] = {1};", cleanName, cleanName);
        }
    }
}



