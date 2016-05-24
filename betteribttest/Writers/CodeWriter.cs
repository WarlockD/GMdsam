using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GameMaker.Ast;

namespace GameMaker.Writers
{
    public abstract class CodeWriter
    {
        protected class LuaVarCheckCashe
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

        protected BlockToCode output { get; private set; }
        protected LuaVarCheckCashe cache;
        public CodeWriter(BlockToCode output)
        {
            this.output = output;
            this.cache = new LuaVarCheckCashe();
        }
        protected class ActionInfo
        {
            public ILBlock Method;
            public string Name;
            public int SubType;
            public int Type;
        }
        protected class EventInfo
        {
            public int Type;
            public List<ActionInfo> Actions = new List<ActionInfo>();
        }
        protected abstract void WriteScript(File.Script script, ILBlock block, int arg_count);
        public void Write(File.Script script)
        {
            ILBlock block = Context.DecompileBlock(script.Code);
            if (block == null)
            {
                Context.Error("Missing block data for {0}", script.Code.Name);
                return; // error
            }
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
            WriteScript(script, block, arguments);
        }
        protected abstract void WriteObject(File.GObject obj, List<EventInfo> infos);
        public void Write(File.GObject obj)
        {

            List<EventInfo> infos = new List<EventInfo>();
            List<Task<ActionInfo>> tasks = new List<Task<ActionInfo>>();
            // seperating the compiling time for all the tasks didn't make it faster humm

            for (int i = 0; i < obj.Events.Length; i++)
            {
                if (obj.Events[i] == null) continue;
                EventInfo einfo = new EventInfo();
                var actions = einfo.Actions;
                infos.Add(einfo);
                einfo.Type = i;
                foreach (var e in obj.Events[i])
                {
                    foreach (var a in e.Actions)
                    {
                        Task<ActionInfo> task = new Task<ActionInfo>(() =>
                        {
                            File.Code codeData = File.Codes[a.CodeOffset];
                            ILBlock block = Context.DecompileBlock(codeData);
                            if (block == null) Context.Error("Missing block data for {0}", codeData.Name);
                            ActionInfo info = new ActionInfo() { Method = block, Name = Context.EventToString(i, e.SubType), SubType = e.SubType, Type = i };
                            lock (actions) actions.Add(info);
                            return info;
                        }, TaskCreationOptions.AttachedToParent);
                        tasks.Add(task);
                        task.Start();
                    }
                }
            }
            foreach (var t in tasks)
            {
                ActionInfo info = t.Result;
                cache.AddVars(info.Method);
            }

            WriteObject(obj, infos);
        }
    }
}
