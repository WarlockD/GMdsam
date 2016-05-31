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
            WriteObjectUse();
            output.WriteLine(output.BlockCommentEnd);
        }
        protected virtual void WriteObjectUse()
        {
            if (spritesUsed.Count > 0) {
                output.WriteLine("Sprites Used:");
                output.Indent++;
                foreach (var kv in spritesUsed) output.WriteLine("Index={0} Name={1}", kv.Key, kv.Value);
                output.Indent--;
            }
            if (objectsUsed.Count > 0)
            {
                output.WriteLine("Objects Used:");
                output.Indent++;
                foreach (var kv in objectsUsed) output.WriteLine("Index={0} Name={1}", kv.Key, kv.Value);
                output.Indent--;
            }
        }
        public void WriteCode(File.Code code, ILBlock block)
        {
            if (code == null) throw new ArgumentNullException("code");
            if (block == null) throw new ArgumentNullException("block");
            output.Write(block);
        }
        public void WriteCode(File.Code code)
        {
            if (code == null) throw new ArgumentNullException("code");
            WriteCode(code, DecompileCode(code));
        }
        ConcurrentDictionary<string, ObjectInfo.VarType> locals = new ConcurrentDictionary<string, ObjectInfo.VarType>();
        ConcurrentDictionary<string, bool> wierdVars = new ConcurrentDictionary<string, bool>();// used to suppress errors on vars
        ConcurrentDictionary<int, string> spritesUsed = new ConcurrentDictionary<int, string>();
        ConcurrentDictionary<int, string> objectsUsed = new ConcurrentDictionary<int, string>();
        ILBlock DecompileCode(File.Code codeData)
        {
            ILBlock block = Context.DecompileBlock(codeData);
            foreach (var v in block.GetSelfAndChildrenRecursive<ILVariable>(x => !x.isGlobal))
            {
                var type = GetVarType(v);
                locals.AddOrUpdate(v.Name, type,
                    (key, existingVal) =>
                    {
                        if (existingVal != type && !wierdVars.ContainsKey(v.Name))
                        {
                            output.Info("Variable '{0}' changes from normal to array", v.Name);
                            wierdVars.TryAdd(v.Name, true);
                        }
                        return existingVal | type;

                    });
            }
            foreach (var e in block.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Call))
            {
                ILCall c = e.Operand as ILCall;
                if(c.Name == "instance_create")
                {
                    if(c.Arguments[2].Code == GMCode.Constant)
                    {
                        ILValue v = c.Arguments[2].Operand as ILValue;
                        if (!objectsUsed.ContainsKey((int)v))
                            objectsUsed.TryAdd((int)v, File.Objects[(int)v].Name);
                    }
                }
            }
            return block;
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
            if (obj.SpriteIndex > -1) spritesUsed.TryAdd(obj.SpriteIndex, File.Sprites[obj.SpriteIndex].Name);
            if (obj.Parent > -1) objectsUsed.TryAdd(obj.Parent, File.Objects[obj.Parent].Name);
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
                        ILBlock block = DecompileCode(codeData);
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
