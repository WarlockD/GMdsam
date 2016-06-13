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
        public enum VarOwner
        {
            Self = 0,
            Global = 1,
            BuiltIn = 3,
        }
        public enum VarType 
        {
            Unkonwn=0,
            Bool, // maybe not used
            String,
            Int,
            Real,
            SpriteIndex,
            ObjectIndex,
        }
        public class LocalInfo
        {
            public string Name;
            public HashSet<GM_Type> GMTypes = new HashSet<GM_Type>();
            public List<ILVariable> Uses = new List<ILVariable>();
            public List<ILExpression> assignments = new List<ILExpression>();
            public GM_Type Type = GM_Type.NoType;
            public VarOwner Owner;
            public int ArrayDim = 0;
            public void Add(ILVariable v)
            {
                if (v.Name != Name) throw new ArgumentException("Name must be the same", "v");
                GMTypes.Add(v.Type);
                if (v.isArray)
                {
                    ArrayDim = 1;
                    if (v.Index.Code == GMCode.Array2D) ArrayDim = 2;
                } else Type = Type.ConvertType(v.Type);
                lock (Uses) Uses.Add(v);
            }
            public LocalInfo(ILVariable v)
            {
                this.Name = v.Name;
                this.Owner = v.isGlobal ? VarOwner.Global : VarOwner.Self;
                if (Constants.IsDefined(this.Name)) this.Owner |= VarOwner.BuiltIn;
                Add(v);
            }
            public override string ToString()
            {
                string str =  "(" + Type + ")" + Owner + "." + Name;
                if (ArrayDim == 1) str += "[]";
                if(ArrayDim == 2) str += "[][]";
                return str;
            }
        }
        ConcurrentDictionary<string, LocalInfo> locals = new ConcurrentDictionary<string, LocalInfo>();
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
           
            public Dictionary<string, LocalInfo> Locals;
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
                WriteLocals("Locals", info.Locals.Where(x => x.Value.ArrayDim == 0 && x.Value.Owner == VarOwner.Self).Select(x => x.Key).OrderBy(x => x).ToList());
                WriteLocals("Local Arrays", info.Locals.Where(x => x.Value.ArrayDim > 0 && x.Value.Owner == VarOwner.Self).Select(x => x.Key).OrderBy(x => x).ToList());
                WriteLocals("BuiltIn", info.Locals.Where(x => x.Value.Owner == VarOwner.BuiltIn).Select(x => x.Key).OrderBy(x => x).ToList());

                WriteLocals("Both Array AND Normal", info.Locals.Where(x => x.Value.ArrayDim > 0 && x.Value.Type != GM_Type.NoType).Select(x => x.Key).OrderBy(x => x).ToList());
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
        static void InstanceArgument(CodeWriter writer, ILCall c, int index)
        {
            int i = ResourceArgument(File.Objects, c, index);
            if (i >=0) writer.objectsUsed.TryAdd(i, File.Sprites[i].Name);
        }
        static void SpriteArgument(CodeWriter writer, ILCall c, int index)
        {
            int i = ResourceArgument(File.Sprites, c, index);
            if(i >= 0) writer.spritesUsed.TryAdd(i, File.Sprites[i].Name);
        }
        static int ResourceValue<T>(IReadOnlyList<T> list, ILValue v) where T : File.GameMakerStructure, File.INamedResrouce
        {
            int i = -1;
            if(v != null && v.IntValue != null)
            {
                i = (int)v.IntValue;
                if (i >= 0 && i < list.Count)
                    v.ValueText = list[(int)v.IntValue].Name;
                else
                    v.ValueText = "Range?=" + i.DebugHex();
            }
            return -1;
        }
        static int ResourceArgument<T>(IReadOnlyList<T> list, ILCall c, int index) where T : File.GameMakerStructure, File.INamedResrouce
        {
            if (c.Arguments[index].Code == GMCode.Constant)
                return ResourceValue(list, c.Arguments[index].Operand as ILValue);
            else
                return -1;
        }
            static CodeWriter()
        {
            calls["instance_create"] = (CodeWriter writer, ILCall c) => InstanceArgument(writer, c, 2);
            calls["instance_exists"] = (CodeWriter writer, ILCall c) => InstanceArgument(writer, c, 0);
            calls["script_execute"] = (CodeWriter writer, ILCall c) => ResourceArgument(File.Scripts, c, 0);
            calls["snd_play"] = (CodeWriter writer, ILCall c) => ResourceArgument(File.Sounds, c, 0);
            
            calls["draw_sprite"] = calls["draw_sprite_ext"] = (CodeWriter writer, ILCall c) => SpriteArgument(writer, c, 0);

            assigns["sprite_index"] = (CodeWriter writer, ILValue c) => ResourceValue(File.Sprites, c);
            calls["path_start"] = (CodeWriter writer, ILCall c) =>
            {
                ResourceArgument(File.Paths, c, 0);
                if (c.Arguments[3].Code == GMCode.Constant)
                {
                    ILValue v = c.Arguments[3].Operand as ILValue;
                    switch ((int) v)
                    {
                        case 0: v.ValueText = "path_action_stop"; break;
                        case 1: v.ValueText = "path_action_restart"; break;
                        case 2: v.ValueText = "path_action_continue"; break;
                        case 3: v.ValueText = "path_action_reverse"; break;
                        default:
                            v.ValueText = null;
                            break;
                    }
                }
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
                    locals.AddOrUpdate(v.Name,
                        (key) => {
                            var info = new LocalInfo(v);
                            v.UserData = info;
                            return info;
                            },
                         (key, existingVal) =>
                        {
                            v.UserData = existingVal;
                            existingVal.Add(v);
                            return existingVal;
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
                // Try to resolve all the v types here
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
           // block.ClearAndSetAllParents();
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
            else
            {
                block = script.Block;
                File.Code codeData = script.Code;
                if (block == null)
                {
                    output.Error("Code '{0}' empty, but used here", codeData.Name);
                }
                else if (!codeUsed.ContainsKey(codeData.Name) || !codeUsed.TryAdd(codeData.Name, true)) // check if already done
                {
                    AddBlockToLocals(block);
                }

            }
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
