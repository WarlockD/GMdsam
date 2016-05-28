using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GameMaker.Ast;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace GameMaker.Writers
{
    public abstract class CodeWriter
    {
        protected BlockToCode output { get; private set; }
        public CodeWriter(BlockToCode output)
        {
            this.output = output;
        }
        public class ObjectInfo
        {
            public enum VarType
            {
                BuiltIn = 0,
                Normal =1,
                Array=2,
                Array2D=4
            }
            public List<EventInfo> Events;
            public File.GObject Object;
            public Dictionary<string, VarType> Locals;
        }
        public static ObjectInfo.VarType GetVarType(ILVariable v)
        {
            ObjectInfo.VarType type;
            if (v.isArray)
                type = v.Index.Code == GMCode.Array2D ?  ObjectInfo.VarType.Array2D : ObjectInfo.VarType.Array;
            else
                type = Constants.IsDefined(v.Name) ? ObjectInfo.VarType.BuiltIn :  ObjectInfo.VarType.Normal;
            return type;
        }
        public class ActionInfo
        {
            public ILBlock Method;
            public string Name;
            public int SubType;
            public int Type;
        }
        public class EventInfo
        {
            public int Type;
            public List<ActionInfo> Actions = new List<ActionInfo>();
        }
        protected abstract void WriteScript(File.Script script, ILBlock block, int arg_count);
        protected virtual void WriteLocals(string name, List<string> strings)
        {
            if (strings.Count > 0)
            {
                output.WriteLine("{0}: {1}", name, strings.Count);
                output.Indent++;
                foreach (var s in strings)
                {
                    if(output.Column > 0) output.Write(", ");
                    output.Write(s);
                    if (output.Column > 70) output.WriteLine();
                }
                if (output.Column > 0) output.WriteLine();
                output.Indent--;
            }
        }
        protected virtual void WriteLocals(ObjectInfo info)
        {
            output.WriteLine(output.BlockCommentStart);
            if (info.Locals.Count > 0)
            {

                output.Indent++;
                WriteLocals("Locals", info.Locals.Where(x => x.Value == ObjectInfo.VarType.Normal).Select(x => x.Key).OrderBy(x => x).ToList());
                WriteLocals("Local Arrays", info.Locals.Where(x => x.Value == ObjectInfo.VarType.Array).Select(x => x.Key).OrderBy(x => x).ToList());
                WriteLocals("Local 2D Arrays", info.Locals.Where(x => x.Value == ObjectInfo.VarType.Array2D).Select(x => x.Key).OrderBy(x => x).ToList());
                WriteLocals("BuiltIn", info.Locals.Where(x => x.Value == ObjectInfo.VarType.BuiltIn).Select(x => x.Key).OrderBy(x => x).ToList());

                WriteLocals("Both Array AND Normal", info.Locals.Where(x => x.Value.HasFlag(ObjectInfo.VarType.Normal) && x.Value.HasFlag(ObjectInfo.VarType.Array)).Select(x => x.Key).OrderBy(x => x).ToList());
                output.Indent--;
            }
            output.WriteLine(output.BlockCommentEnd);
        }
        public void WriteCode(File.Code code, ILBlock block)
        {
            output.Write(output.LineComment);
            output.Write(" Code Name: ");
            output.WriteLine(code.Name);
            output.Write(block);
        }
        public void Write(File.Script script, ILBlock block=null)
        {
            if(block == null) block = Context.DecompileBlock(script.Code);
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
                }
            }
            WriteScript(script, block, arguments);
        }
        protected abstract void WriteObject(ObjectInfo info);

        public ObjectInfo BuildEventInfo(File.GObject obj)
        {
            List<EventInfo> infos = new List<EventInfo>();
            ConcurrentDictionary<string, ObjectInfo.VarType> locals = new ConcurrentDictionary<string, ObjectInfo.VarType>();

            // seperating the compiling time for all the tasks didn't make it faster humm
            for (int i = 0; i < obj.Events.Length; i++)
            {
                if (obj.Events[i] == null) continue;
                EventInfo einfo = new EventInfo();
                ConcurrentBag<ActionInfo> actions = new ConcurrentBag<ActionInfo>();
              //  var actions = einfo.Actions;
                infos.Add(einfo);
                einfo.Type = i;
                Parallel.ForEach(obj.Events[i], e => // This too much?:P
                {
                    Parallel.ForEach(e.Actions, a =>
                    {
                        File.Code codeData = File.Codes[a.CodeOffset];
                        ILBlock block = Context.DecompileBlock(codeData);
                        HashSet<string> wierdVars = new HashSet<string>(); // used to suppress errors
                        foreach (var v in block.GetSelfAndChildrenRecursive<ILVariable>(x => !x.isGlobal))
                        {
                            var type = GetVarType(v);
                            locals.AddOrUpdate(v.Name, type,
                                (key, existingVal) =>
                                {
                                    if (existingVal != type && !wierdVars.Contains(v.Name) )
                                    {
                                        output.Warning("Variable '{0}' changes from normal to array", v.Name);
                                        wierdVars.Add(v.Name);
                                    }
                                    return existingVal | type;

                                });
                        }
                        if (block == null) Context.Error("Missing block data for {0}", codeData.Name);
                        ActionInfo info = new ActionInfo() { Method = block, Name = Context.EventToString(i, e.SubType), SubType = e.SubType, Type = i };
                        actions.Add(info);
                    });
                });
                einfo.Actions = actions.OrderBy(x => x.Type).ToList();
            }
            ObjectInfo oi = new ObjectInfo();
            oi.Events = infos;
            oi.Object = obj;
            oi.Locals = locals.ToDictionary(x=> x.Key, z=> z.Value);

            return oi;
        }
        public void Write(File.GObject obj, ObjectInfo info =null)
        {
            if (info == null) info = BuildEventInfo(obj);

            WriteObject(info);
        }
    }
}
