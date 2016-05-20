using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GameMaker.Writers.Lua
{
    class LuaVarCheckCashe
    {
        public class VarInfo : IEquatable<VarInfo>
        {
            public string Name;
            public string Instance = null;
            public bool isGlobal { get { return Instance == "global"; } }
            public bool isArray = false;
            public bool Equals(VarInfo o)
            {
                return o.Name == Name && o.Instance == Instance;
            }
            public override bool Equals(object obj)
            {
                if (object.ReferenceEquals(obj, null)) return false;
                if (object.ReferenceEquals(obj, this)) return true;
                VarInfo v = obj as VarInfo;
                return v != null && Equals(v);
            }

            public override int GetHashCode()
            {
                return Name.GetHashCode();
            }
            public override string ToString()
            {
                if (Instance != null) return Instance + '.' + Name;
                else return Name;
            }
        }
        Dictionary<string, VarInfo> allvars = new Dictionary<string, VarInfo>();

        HashSet<VarInfo> allvarsset = new HashSet<VarInfo>();
        HashSet<VarInfo> allpinned = new HashSet<VarInfo>();

        public void AddVar(ILVariable v)
        {
            string name = v.FullName;
            if (allvars.ContainsKey(name)) return;
            VarInfo vi = new VarInfo();
            vi.Name = v.Name;
            if (!v.isLocal && !v.isGenerated) vi.Instance = v.InstanceName ?? v.Instance.ToString();

            vi.isArray = v.Index != null;
            allvars.Add(name, vi);
            allvarsset.Add(vi);
        }
        public void AddVars(ILBlock method)
        { // what we do here is make sure
            foreach (var v in method.GetSelfAndChildrenRecursive<ILVariable>()) AddVar(v);
            foreach (var a in method.GetSelfAndChildrenRecursive<ILAssign>())
            {
                string name = a.Variable.FullName;
                var v = allvars[name];
                allpinned.Add(v);
            }
        }
        public IEnumerable<VarInfo> GetAll()
        {
            return allvarsset;
        }
        public IEnumerable<VarInfo> GetAll(Func<VarInfo, bool> pred)
        {
            return GetAll().Where(pred);
        }
        public IEnumerable<VarInfo> GetAllUnpinned()
        {
            return allvarsset.Except(allpinned);
        }
        public IEnumerable<VarInfo> GetAllUnpinned(Func<VarInfo, bool> pred)
        {
            return GetAllUnpinned().Where(pred);
        }
    }
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
            output.WriteLine(") ");
            output.WriteMethod(cleanName, block);
            output.WriteLine("end");
            output.WriteLine(); // extra next line
            output.WriteLine("_scripts[{0}] = {1}", code.Index, cleanName);
            output.WriteLine("_scripts[\"{0}\"] = {1}", cleanName, cleanName);
        }
    }
}