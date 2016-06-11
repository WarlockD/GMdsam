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
        public enum VarType
        {
            BuiltIn = 0,
            Normal = 1,
            Array = 2,
            Array2D = 4
        }
        ConcurrentDictionary<string, VarType> locals = new ConcurrentDictionary<string, VarType>();
        Dictionary<string, List<ILExpression>> assignments = new Dictionary<string, List<ILExpression>>();
        Dictionary<string, List<ILCall>> funcCalls = new Dictionary<string, List<ILCall>>();
        ConcurrentDictionary<string, bool> wierdVars = new ConcurrentDictionary<string, bool>();// used to suppress errors on vars
        ConcurrentDictionary<string, bool> codeUsed = new ConcurrentDictionary<string, bool>();
        ConcurrentDictionary<int, string> spritesUsed = new ConcurrentDictionary<int, string>();
        ConcurrentDictionary<int, string> objectsUsed = new ConcurrentDictionary<int, string>();
        protected BlockToCode output { get; private set; }
        public CodeWriter(BlockToCode output)
        {
            this.output = output;
        }
        
        public class CodeInfo
        {
           
            public Dictionary<string, VarType> Locals;
        }
        public class ObjectInfo : CodeInfo
        {
           
            public List<EventInfo> Events;
            public File.GObject Object;
        }
        public class ScriptInfo : CodeInfo
        {
            public File.Script Script;
            public int ArgumentCount;
            public ILBlock Block;
        }
        public static VarType GetVarType(ILVariable v)
        {
            VarType type;
            if (v.isArray)
                type = v.Index.Code == GMCode.Array2D ?  VarType.Array2D : VarType.Array;
            else
                type = Constants.IsDefined(v.Name) ? VarType.BuiltIn :  VarType.Normal;
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
        protected abstract void WriteScript(ScriptInfo info);
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
        protected virtual void WriteLocals(CodeInfo info)
        {
            output.WriteLine(output.BlockCommentStart);
            if (info.Locals.Count > 0)
            {

                output.Indent++;
                WriteLocals("Locals", info.Locals.Where(x => x.Value == VarType.Normal).Select(x => x.Key).OrderBy(x => x).ToList());
                WriteLocals("Local Arrays", info.Locals.Where(x => x.Value == VarType.Array).Select(x => x.Key).OrderBy(x => x).ToList());
                WriteLocals("Local 2D Arrays", info.Locals.Where(x => x.Value == VarType.Array2D).Select(x => x.Key).OrderBy(x => x).ToList());
                WriteLocals("BuiltIn", info.Locals.Where(x => x.Value == VarType.BuiltIn).Select(x => x.Key).OrderBy(x => x).ToList());

                WriteLocals("Both Array AND Normal", info.Locals.Where(x => x.Value.HasFlag(VarType.Normal) && x.Value.HasFlag(VarType.Array)).Select(x => x.Key).OrderBy(x => x).ToList());
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
        static Dictionary<string, Action<CodeWriter, ILCall>> calls = new Dictionary<string, Action<CodeWriter, ILCall>>();
        static Dictionary<string, Action<CodeWriter, ILValue>> assigns = new Dictionary<string, Action<CodeWriter, ILValue>>();
        static CodeWriter()
        {
            calls["instance_create"] = (CodeWriter writer, ILCall c) =>
            {
                if (c.Arguments[2].Code == GMCode.Constant)
                {
                    ILValue v = c.Arguments[2].Operand as ILValue;
                    writer.objectsUsed.TryAdd((int)v, File.Objects[(int)v].Name);
                }
            };
            calls["draw_sprite"] = calls["draw_sprite_ext"] = (CodeWriter writer, ILCall c) =>
            {
                if (c.Arguments[0].Code == GMCode.Constant)
                {
                    ILValue v = c.Arguments[0].Operand as ILValue;
                    writer.spritesUsed.TryAdd((int)v, File.Sprites[(int)v].Name);
                }
            };
            assigns["sprite_index"] = (CodeWriter writer, ILValue v) =>
            {
                writer.spritesUsed.TryAdd((int)v, File.Sprites[(int)v].Name);
            };

        }
        IEnumerable<ILValue> FindAllConstantsAssigned( List<ILExpression> list, string vrname=null)
        {
            foreach (var e in list)
            {
                ILValue value = e.Arguments.Single().Operand as ILValue;
                if (value != null) yield return value;
                ILVariable vr = e.Arguments.Single().Operand as ILVariable;
                if (vr == null) continue;
                // We only go up one level
                foreach(var ee in assignments.Where(x=>x.Key == vr.Name)) {
                    foreach(var eee in ee.Value)
                    {
                        value = eee.Arguments.Single().Operand as ILValue;
                        if (value != null) yield return value;
                    }
                    
                }
            }
        }
        void AddBlockToLocals(ILBlock block)
        {
            lock (locals)
            {
                foreach (var v in block.GetSelfAndChildrenRecursive<ILVariable>(x => !x.isGlobal))
                {
                    var type = GetVarType(v);
                    locals.AddOrUpdate(v.Name, type,
                        (key, existingVal) =>
                        {
                            if (existingVal != type && !wierdVars.ContainsKey(v.Name)) // Stops the message repeating, atlesat on this object
                        {
                                output.Info("Game Bug, Variable '{0}' changes from normal to array", v.Name);
                                wierdVars.TryAdd(v.Name, true);
                            }
                            return existingVal | type;

                        });
                }
            }
            lock (assignments)
            {
                foreach (var e in block.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Assign))
                {
                    ILVariable vr = e.Operand as ILVariable;
                    List<ILExpression> vs;
                    if (!assignments.TryGetValue(vr.Name, out vs)) assignments.Add(vr.Name, vs = new List<ILExpression>());
                    vs.Add(e);
                }
            }
            lock (funcCalls)
            {
                foreach (var e in block.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Call))
                {
                    ILCall c = e.Operand as ILCall;
                    List<ILCall> vs;
                    if (!funcCalls.TryGetValue(c.Name, out vs)) funcCalls.Add(c.Name, vs = new List<ILCall>());
                    vs.Add(c);
                }
            }
        }
        void CheckAllVars()
        {
            foreach (var kv in assignments.Where(x => Constants.IsDefined(x.Key)))
            {
                Action<CodeWriter, ILValue> action;
                if (assigns.TryGetValue(kv.Key, out action))
                {
                    foreach (var v in FindAllConstantsAssigned(kv.Value, kv.Key)) action(this, v);
                }
            }
            foreach (var kv in funcCalls.Where(x => Constants.IsDefined(x.Key)))
            {
                Action<CodeWriter, ILCall> action;
                if (calls.TryGetValue(kv.Key, out action)) foreach(var c in kv.Value) action(this, c);
            }
        }
        ILBlock DecompileCode(File.Code codeData)
        {
            ILBlock block = codeData.Block;
            if(block == null)
            {
                output.Error("Code '{0}' empty, but used here", codeData.Name);
            } else if(!codeUsed.ContainsKey(codeData.Name) || !codeUsed.TryAdd(codeData.Name, true)) // check if already done
            {
                AddBlockToLocals(block);
            }
            return block;
        }
        public void Write(File.Script script)
        {
            ILBlock block;
                if (script.Code == null)
                {
                    Context.Warning("Empty code Data for script");
                    return; // error
                }
                else block = DecompileCode(script.Code);
            CheckAllVars();
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
            ScriptInfo oi = new ScriptInfo();
            oi.Locals = locals.ToDictionary(x => x.Key, z => z.Value);
            oi.Script = script;
            oi.ArgumentCount = arguments;
            oi.Block = block;
            WriteScript(oi);
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
            CheckAllVars();
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
