using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameMaker.Dissasembler;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace GameMaker
{
    static class Program
    {

        static GMContext context;
        static void spriteArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg))
                arg.ValueText = "\"" + context.IndexToSpriteName((int)arg.Value) + "\"";

        }
        static void ordArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant( out arg))
            {
                char c = (char) (int) arg.Value;
                if (char.IsControl(c))
                    arg.ValueText = "\'\\x" + ((int)arg.Value).ToString("X2") + "\'";
                else
                    arg.ValueText = "\'" + c + "\'";
            }
        }

        static void soundArgument(ILNode expr)
        {

            ILValue arg;
            if (expr.MatchIntConstant(out arg) && (int) arg.Value != -1)
            {
                int instance = (int) arg.Value;
                arg.ValueText = "\"" + context.IndexToAudioName(instance) + "\"";
            }
        }
        static void instanceArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg) && (int) arg.Value != -1)
                arg.ValueText = "\"" + context.InstanceToString((int) arg.Value) + "\"";
        }
        static void fontArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg) && (int)arg.Value != -1)
                arg.ValueText = "\"" + context.IndexToFontName((int) arg.Value) + "\"";

        }
        // This just makes color look easyer to read
        static void colorArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg))
            {
                int color = (int) arg.Value;
                byte red = (byte) (color & 0xFF);
                byte green = (byte) (color >> 8 & 0xFF);
                byte blue = (byte) (color >> 16 & 0xFF);
                arg.ValueText = "{ " + red + ", " + green + ", " + blue + " }";  
            }
        }
        static void scriptArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg) && (int) arg.Value != -1)
                arg.ValueText = "\"" + context.IndexToScriptName((int) arg.Value) + "\"";
        }
        static void scriptExecuteFunction(ILCall call)
        {
            // Fancy footwork for lua as we have to send self
            int arg;
            string scriptName = null;
            if (!call.Arguments[0].MatchIntConstant(out arg) || (scriptName = context.IndexToScriptName(arg)) == null) return;
         //   call.Enviroment = "self";
            call.Name = scriptName; // change it to the script name
            call.Arguments[0] = new ILVariable() { Name = "self", isLocal = true, isResolved = true }; // change the first argument to self
            // now check for undertale scripts and make those nice
            switch (scriptName)
            {
                case "SCR_TEXTSETUP": // not sure why we have this, you run it agenst OBJ_WRITER
                    fontArgument(call.Arguments[1]);
                    colorArgument(call.Arguments[2]);
                    break;
            }
        }
        static void instanceCreateFunction(ILCall call)
        {
            Debug.Assert(call.Arguments.Count == 3);
            instanceArgument(call.Arguments[2]);
        }
        static void draw_spriteExisits(ILCall call)
        {
            Debug.Assert(call.Arguments.Count > 1);
            spriteArgument(call.Arguments[0]);
        }

        static void instanceExisits(ILCall call)
        {
            Debug.Assert(call.Arguments.Count == 1);
            instanceArgument(call.Arguments[0]);
        }
        static void instanceCollision_line(ILCall call)
        {
            Debug.Assert(call.Arguments.Count > 4);
            instanceArgument(call.Arguments[3]);
        }
        static void soundPlayStop(ILCall call)
        {
            Debug.Assert(call.Arguments.Count > 4);
            instanceArgument(call.Arguments[0]);
        }
        public class CallFunctionLookup
        {
            public delegate void FunctionToText(ILCall call);
            Dictionary<string, FunctionToText> _lookup = new Dictionary<string, FunctionToText>();
            public void Add(string funcname, FunctionToText func) { _lookup.Add(funcname, func); }
             public void FixCalls(ILBlock block)
            {
                foreach (var e in block.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Call))
                {
                    ILCall call = e.Operand as ILCall;
                    FunctionToText func;
                    if (_lookup.TryGetValue(call.Name, out func)) func(call);
                }
            }
        }
        public class AssignRightValueLookup
        {
            public delegate void ArgumentToText(ILNode argument);
            Dictionary<string, ArgumentToText> _lookup = new Dictionary<string, ArgumentToText>();
            public void Add(string varName, ArgumentToText func) { _lookup.Add(varName, func); }
            public void FixCalls(ILBlock block)
            {
                // Check for assigns
                foreach (var push in block.GetSelfAndChildrenRecursive<ILAssign>())
                {
                    ArgumentToText func; 
                    if (_lookup.TryGetValue(push.Variable.FullName, out func)) func(push.Expression);
                }
                // Check for equality
                foreach (var condition in block.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Seq || x.Code == GMCode.Sne))
                {
                    ArgumentToText func;
                    if (_lookup.TryGetValue(condition.Arguments[0].ToString(), out func)) func(condition.Arguments[1]);
                    else if (_lookup.TryGetValue(condition.Arguments[1].ToString(), out func)) func(condition.Arguments[0]);
                }
            }
        }
        static CallFunctionLookup FunctionFix = new CallFunctionLookup();
        static AssignRightValueLookup PushFix = new AssignRightValueLookup();
        static void Instructions()
        {
            Console.WriteLine("Useage <exe> data.win <-asm> [-s search_term] [-all (objects|scripts)");
            Console.WriteLine("search_term will search all scripts or object names for the text and save that file as a *.cpp");
            Console.WriteLine("-asm will also write the bytecode dissasembly");
            Console.WriteLine("There will be some wierd gotos/labels in case statements.  Ignore them, I am still trying to find that bug");
        }
        static void FunctionReplacement()
        {
            FunctionFix.Add("instance_create", instanceCreateFunction);
            FunctionFix.Add("collision_line", instanceCollision_line);
            FunctionFix.Add("instance_exists", instanceExisits);
            FunctionFix.Add("script_execute", scriptExecuteFunction);
            FunctionFix.Add("draw_sprite", draw_spriteExisits);
            FunctionFix.Add("draw_sprite_ext", draw_spriteExisits);
            FunctionFix.Add("snd_stop", (ILCall call) =>
            {
                Debug.Assert(call.Arguments.Count > 0);
                instanceArgument(call.Arguments[0]);
            });
            FunctionFix.Add("snd_play", (ILCall call) =>
            {
                Debug.Assert(call.Arguments.Count > 0);
                instanceArgument(call.Arguments[0]);
            });
            FunctionFix.Add("string", (ILCall call) =>
            {
                Debug.Assert(call.Arguments.Count == 1);
                call.Name = "tostring"; // lua uses two string
            });
            FunctionFix.Add("real", (ILCall call) =>
            {
                Debug.Assert(call.Arguments.Count ==1);
                call.Name = "tonumber"; // lua uses two string
            });


            FunctionFix.Add("draw_set_font", (ILCall call) =>
            {
                Debug.Assert(call.Arguments.Count == 1);
                fontArgument(call.Arguments[0]);
            });
            FunctionFix.Add("draw_set_color", (ILCall call) =>
            {
                Debug.Assert(call.Arguments.Count == 1);
                colorArgument(call.Arguments[0]);

            });
            FunctionFix.Add("merge_color", (ILCall call) =>
            {
                Debug.Assert(call.Arguments.Count == 3);
                colorArgument(call.Arguments[0]);
                colorArgument(call.Arguments[1]);
            });

            PushFix.Add("self.sym_s", spriteArgument);
            PushFix.Add("self.mycolor", colorArgument);
            PushFix.Add("self.myfont", fontArgument);
            PushFix.Add("self.txtsound", soundArgument);
            PushFix.Add("self.image_blend", colorArgument);
            
        }

        static void BadExit(int i)
        {
            Instructions();
            Environment.Exit(i);
        }
        
        /// <summary>
        /// The main entry point for the application.
        /// </summary>

        static ILBlock DecompileBlock(GMContext context, Stream code, string filename=null, string header = null)
        {
            var instructionsNew = GameMaker.Dissasembler.Instruction.Dissasemble(code, context);
            if (context.doAsm || context.Debug)
            {
                string asm_filename = (filename ?? context.DebugName) + ".asm";
                var list = instructionsNew.Values.Where(x => x != null).ToList();
                GameMaker.Dissasembler.InstructionHelper.DebugSaveList(list, asm_filename);
                var graph = FlowAnalysis.ControlFlowGraphBuilder.Build(list);
                graph.ExportGraph().Save((filename ?? context.DebugName) + ".dot");

            }
      
            ILBlock block = new GameMaker.Dissasembler.ILAstBuilder().Build(instructionsNew, false, context);
            if (block == null) return null;
            FunctionFix.FixCalls(block);
            PushFix.FixCalls(block);
            if(filename != null)
            {
                if (context.doLua)
                {
                    filename += ".lua";
                    block.DebugSaveLua(filename, header);
                }
                else
                {
                    filename += ".cpp";
                    block.DebugSave(filename, header);
                }
            }
            

            // Console.WriteLine("Writing: "+ filename);
            return block;
        }
        static string LuaCodeText(GMContext context, ILBlock block)
        {
            string code;
            using(Writers.LuaWriter w = new Writers.LuaWriter(context))
            {
                if (block == null)
                {
                    w.Ident++;
                    w.WriteLine("-- Look at errors.txt, bad code decompile");
                    w.Ident--;
                }
                else w.WriteMethod(context.DebugName, block);
                code = w.ToString();
            }
            return code;
        }


        static void DoFuncList(ITextOutput sb, string tableName, string partname, Dictionary<int, string> codes, bool keypresses = false)
        {
            if (codes.Count > 0)
            {
                sb.Write("-- Start "); sb.Write(partname); sb.WriteLine(" --");

                sb.Write(tableName);
                sb.WriteLine(" = {}");
                foreach (var a in codes)
                {
                    sb.Write(tableName);
                    sb.Write("[");
                    if (keypresses)
                    {
                        sb.Write("\"");
                        char c = (char) a.Key;
                        if (char.IsControl(c))
                        {
                            sb.Write("\\");
                            sb.Write(a.Key.ToString());
                        }
                        else sb.Write(c);
                        sb.Write("\"");
                    }
                    else sb.Write(a.Key.ToString());
                    sb.WriteLine("] = function()");
                    sb.Indent();
                    sb.Write(a.Value);
                    sb.Unindent();
                    sb.WriteLine("end");
                }
                sb.Write("-- End "); sb.Write(partname); sb.WriteLine(" --");
            }
            else sb.WriteLine("-- No " + partname);
        }
       
        static void InsertIntoTable(ITextOutput output, string table, int index, string func,string eventName=null)
        {
            output.WriteLine("self.{0}[{1}] = {2}", table, index, func);
            if (eventName != null) output.WriteLine("self.events[{0}] = {1}", eventName, func);
        }
        static void InsertIntoTable(ITextOutput output, string table, string index, string func, string eventName = null)
        {
            output.WriteLine("self.{0}[\"{1}\"] = {2}", table, index, func);
            if (eventName != null) output.WriteLine("self.events[{0}] = {1}", eventName, func);
        }
        static void InsertIntoTable(ITextOutput output, string table, List<KeyValuePair<int, string>> actions) {
            output.WriteLine("self.{0} = {{}}", table);
            foreach (var func in actions) InsertIntoTable(output,table, func.Key, func.Value);
        }
        static void objectHeadder(ITextOutput output, File.GObject obj)
        {
            output.WriteLine("function new_{0}(self)", obj.Name);
            output.Indent();
            output.WriteLine("function event_user(v) self.UserEvent[v]() end");
            output.WriteLine();

        }
        static void objectFooter(ITextOutput output, File.GObject obj)
        {
            output.Unindent();
            output.WriteLine("if  self.CreateEvent then  self.CreateEvent() end");
            output.WriteLine("end");
            output.WriteLine();
            output.WriteLine("_objects[\"{0}\"] = new_{0}", obj.Name);
            output.WriteLine("_objects[{1}] = new_{0}", obj.Name,obj.ObjectIndex); // put it in both to make sure we can look it up by both
        }
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
                    return  Name.GetHashCode();
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
            public IEnumerable<VarInfo> GetAllUnpinned(Func<VarInfo,bool> pred)
            {
                return GetAllUnpinned().Where(pred);
            }
        }
        public static void MakeObject(GMContext context, File.GObject obj, TextWriter output)
        {
            //   ILVariable.WriteSelfOnTextOut = false;
            HashSet<string> SawEvent = new HashSet<string>();
            LuaVarCheckCashe cache = new LuaVarCheckCashe();
            string objectcode = null;
            PlainTextOutput ptext = null;
            using (StringWriter sw = new StringWriter())
            {
                ptext = new PlainTextOutput(sw);
                for (int i = 0; i < obj.Events.Length; i++)
                {
                    if (obj.Events[i] == null) continue;
                    List<KeyValuePair<int, string>> codeFunctions = new List<KeyValuePair<int, string>>();
                    foreach (var e in obj.Events[i])
                    {

                        foreach (var a in e.Actions)
                        {
                            File.Code codeData = File.Codes[a.CodeOffset];
                            context.DebugName = obj.Name + "_" + GMContext.EventToString(i, e.SubType); // in case of debug
                            if (context.Debug)
                            {
                                Debug.WriteLine("Name: " + codeData.Name + " Event: " + GMContext.EventToString(i, e.SubType));
                           
                            }
                            ILBlock method = DecompileBlock(context, codeData.Data);
                            string code = LuaCodeText(context, method); // auto indents it
                            if(method != null) cache.AddVars(method);

                            ptext.WriteLine("function {0}()", codeData.Name);
                            // ptext.Indent();
                            // dosn't handle line endings well
                            ptext.Write(code);
                            // ptext.Unindent();
                            ptext.WriteLine();
                            ptext.WriteLine("end");
                            codeFunctions.Add(new KeyValuePair<int, string>(e.SubType, codeData.Name));
                        }
                    }
                    switch (i)
                    {
                        case 0:
                            Debug.Assert(codeFunctions.Count == 1);
                            ptext.WriteLine("self.CreateEvent = {0}", codeFunctions[0].Value);
                            break;
                        case 1:
                            Debug.Assert(codeFunctions.Count == 1);
                            ptext.WriteLine("self.DestroyEvent = {0}", codeFunctions[0].Value);
                            break;
                        case 2:
                            InsertIntoTable(ptext, "AlarmEvent", codeFunctions);
                            break;
                        case 3:
                            foreach (var e in codeFunctions)
                            {
                                switch (e.Key)
                                {
                                    case 0: ptext.WriteLine("self.StepNormalEvent = {0}", e.Value); break;
                                    case 1: ptext.WriteLine("self.StepBeginEvent = {0}", e.Value); break;
                                    case 2: ptext.WriteLine("self.StepEndEvent = {0}", e.Value); break;
                                }
                            }
                            break;
                        case 4:
                            InsertIntoTable(ptext, "CollisionEvent", codeFunctions);
                            break;
                        case 5:
                            InsertIntoTable(ptext, "Keyboard", codeFunctions);
                            break;
                        case 6: // joystick and mouse stuff here, not used much in undertale
                            ptext.WriteLine("self.{0} = {{}}", "ControlerEvents");
                            foreach (var e in codeFunctions)
                                InsertIntoTable(ptext, "ControlerEvents", GMContext.EventToString(i, e.Key), e.Value);
                            break;
                        case 7: // we only really care about user events
                            ptext.WriteLine("self.{0} = {{}}", "UserEvent");

                            foreach (var e in codeFunctions)
                            {
                                string @event = GMContext.EventToString(i, e.Key);
                                if (e.Key > 9 && e.Key < 26)
                                {
                                    InsertIntoTable(ptext, "UserEvent", e.Key - 10, e.Value, @event);
                                } else
                                {
                                    ptext.WriteLine("self.{0} = {1}", @event, e.Value);
                                    ptext.WriteLine("self.events[{0}] = {1}", @event, e.Value);
                                }
                            }
                            break;
                        case 8:
                            // special case, alot of diffrent draw events are here but undertale mainly just uses
                            // one, so we will figure out if we need a table or not
                            if (codeFunctions.Count == 1) ptext.WriteLine("self.DrawEvent = {0}", codeFunctions[0].Value);
                            else
                            {
                                ptext.WriteLine("self.{0} = {{}}", "DrawEvents");
                                foreach (var e in codeFunctions)
                                    InsertIntoTable(ptext, "DrawEvents", GMContext.EventToString(i, e.Key), e.Value);
                            }
                            break;
                        case 9:
                            InsertIntoTable(ptext, "KeyPressed", codeFunctions);
                            break;
                        case 10:
                            InsertIntoTable(ptext, "KeyReleased", codeFunctions);
                            break;
                        case 11:
                            InsertIntoTable(ptext, "Trigger", codeFunctions);
                            break;
                    }
                }
                objectcode = sw.ToString();
            }
            ptext = new PlainTextOutput(output);
            // ok try to pin some global array values first
            foreach (var v in cache.GetAll(x => x.isGlobal && x.isArray))
                ptext.WriteLine("{0} = {0} or {{}}", v.ToString()); // bunch of null correlesing
            ptext.WriteLine();
            
            objectHeadder(ptext, obj);

            obj.DebugLuaObject(ptext, false);
            ptext.WriteLine("self.events = {}");
            foreach (var v in cache.GetAll(x => !x.isGlobal && x.isArray))
                ptext.WriteLine("{0} = {0} or {{}}", v.ToString()); // bunch of null correlesing
            ptext.WriteLine(objectcode);
            objectFooter(ptext, obj);
        }

        static void MakeAllLuaObjects(GMContext context)
        {
            List<Task> tasks = new List<Task>();
            foreach (var a in File.Objects)
            {

                // var info = Directory.CreateDirectory(a.ObjectName);
                using (StreamWriter sw = new StreamWriter(a.Name + ".lua")) MakeObject(context,  a,sw);


                //  Thread t = new System.Threading.Thread(() => MakeLuaObject(context, a.Obj, files, info.FullName));
                //  Task t = Task.Factory.StartNew(() => MakeLuaObject(context, a.Obj, files, info.FullName));
                //  tasks.Add(t);
                //   tasks.Add(t);
                //    t.IsBackground = true;
                //    threads.Add(t);
                //     t.Start();
            }
           // Task.WaitAll(tasks.ToArray());

         //   threads
        }
        const string ObjectNameHeader = "gml_Object_";
        static string GetObjectName(string name)
        {
            if (!name.Contains(ObjectNameHeader)) return null;
            name = name.Remove(0, ObjectNameHeader.Length);
            name = name.Remove(0, name.LastIndexOf('_')); // number field
            name = name.Remove(0, name.LastIndexOf('_')); // object event type
            return name;
        }
        static Regex ScriptArgRegex = new Regex(@"self\.argument(\d+)", RegexOptions.Compiled);
        static void WriteVarListLua(Writers.BlockToCode output, string listName, List<string> names)
        {
            if (names.Count > 0)
            {
                string listNameLine = output.LineComment + ' ' + listName + ": ";
                string spacerLine = output.LineComment + ' ' + new string(' ',listName.Length) + ": ";
                output.Write(listNameLine);
                output.Write(names[0]);
                for (int i = 1; i < names.Count; i++)
                {
                    if (output.Column > 80)
                    {
                        output.WriteLine();
                        output.Write(spacerLine);
                    }
                    output.Write(" ,{0}", names[i]);
                }
                output.WriteLine();
            }
        }
        static void WriteScript(GMContext context, string codeName, Stream codeStream, string outFilename)
        {
            ILBlock block = DecompileBlock(context, codeStream);

            if (block == null) return; // error
            string scriptName = codeName.Contains("gml_Script_") ? codeName.Remove(0, "gml_Script_".Length) : codeName;
            context.DebugName = scriptName; // in case of debug
            int arguments = 0;
            //   Debug.Assert(scriptName != "SCR_TEXTSETUP");
            foreach (var v in block.GetSelfAndChildrenRecursive<ILVariable>())
            {
                Match match = ScriptArgRegex.Match(v.FullName);
                if (match != null && match.Success)
                {
                    int arg = int.Parse(match.Groups[1].Value) + 1; // we want the count
                    if (arg > arguments) arguments = arg;
                    v.isLocal = true; // arguments are 100% local
                }
            }
            if (context.doLua)
            {
                using (Writers.LuaWriter output = new Writers.LuaWriter(context, outFilename))
                {
                    output.WriteLine("-- FileName: {0} ", outFilename);
                    output.WriteLine("-- ScriptName: {0} ", codeName);
                    output.Write("function {0}(self", scriptName);
                    for (int i = 0; i < arguments; i++) output.Write(",argument{0}", i);
                    output.WriteLine(")");
                    output.WriteMethod(scriptName, block);
                    output.WriteLine("end");
                    output.WriteLine(); // extra next line
                    if (output.UsedVars.Count > 0)
                    {
                        List<string> names = output.UsedVars.Select(x => x.Name).Distinct().ToList();
                        WriteVarListLua(output, "Vars Used", names);
                    }
                    else output.WriteLine("-- No Vars Used...really should never print this");
                    if (output.AssignedVars.Count > 0)
                    {
                        List<string> names = output.AssignedVars.Select(x => x.Name).Distinct().ToList();
                        WriteVarListLua(output, "Vars Assigned", names);
                    }
                    else output.WriteLine("-- No Vars Assigned");
                }
            }
        }

        static void Main(string[] args)
        {
            string dataWinFileName = args.ElementAtOrDefault(0);
            if (string.IsNullOrWhiteSpace(dataWinFileName))
            {
                Console.WriteLine("Missing data.win file");
                BadExit(1);
            }
#if !DEBUG
            try
            {
#endif
            File.LoadDataWin(dataWinFileName);
            File.LoadEveything();
           //     cr = new ChunkReader(dataWinFileName, false); // main pc
#if !DEBUG
        }
            catch (Exception e)
            {
                Console.WriteLine("Could not open data.win file '" + dataWinFileName + "'");
                Console.WriteLine("Exception: " + e.Message);
                BadExit(1);
            }
#endif
            FunctionReplacement();
            context = new GMContext();
 
            bool all = false;
            string toSearch = null;
           int pos = 1;
            while(pos < args.Length)
            {
                switch (args[pos])
                {
                    case "-s":
                        pos++;
                        toSearch = args.ElementAtOrDefault(pos);
                        pos++;
                        break;
                    case "-any":
                        pos++;
                        toSearch = args.ElementAtOrDefault(pos);
                        context.doLua = true;
                        foreach(var o in File.Search(toSearch))
                        {
                            File.Code c = o as File.Code;
                            if(c!= null)
                            {
                                context.DebugName = c.Name;
                                WriteScript(context, c.Name, c.Data, c.Name + ".lua");
                            }
                        }
                        Environment.Exit(0);
                        pos++;
                        break;
                    case "-o":
                        pos++;
                        toSearch = args.ElementAtOrDefault(pos);
                        context.makeObject = true;
                        context.doLua = true;
                        // DebugLuaObject
                        {
                            File.GObject obj;
                            if(File.TryLookup(toSearch,out obj))
                            {
                                using (StreamWriter sw = new StreamWriter(obj.Name + ".lua")) MakeObject(context,  obj, sw);
                                Environment.Exit(0);
                            } else
                            {
                                Console.WriteLine("Could not find {0}", toSearch);
                                Environment.Exit(1);
                            }

                        }
                        pos++;
                        break;
                    case "-debug":
                        pos++;
                        context.Debug = true;
                        break;
                    case "-multiThread":
                        pos++;
                        context.doThreads = true;
                        break;
                    case "-all":
                        all = true;
                        pos++;
                        toSearch = args.ElementAtOrDefault(pos);
                        pos++;
                        break;
                    case "-lua":
                        context.doLua = true;
                        pos++;
                        break;
                    case "-luaobj":
                        pos++;
                        context.doLua = true;
                        context.doLuaObject = true;
                        MakeAllLuaObjects(context);
                        return ;
                    case "-asm":
                        context.doAsm = true;
                        pos++;
                        break;
                    default:
                        Console.WriteLine("Bad argument " + args[pos]);
                        BadExit(1);
                        break;
                }
                if (toSearch != null) break;
            }
            if (toSearch == null)
            {
                Console.WriteLine("Missing search field");
                BadExit(1);
            }
            List<string> FilesFound = new List<string>();
            var errorinfo = Directory.CreateDirectory("error");
            StreamWriter errorWriter = null;
            Action<string> WriteErrorLine = (string msg) =>
            {
                if (errorWriter == null) errorWriter = new StreamWriter("error_" + toSearch + ".txt");
                StringBuilder sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("MM-dd-yy HH:mm:ss.ffff"));
                sb.Append(": ");
                sb.Append(msg);
                lock (errorWriter) errorWriter.WriteLine(sb.ToString());
                lock (Console.Out) Console.WriteLine(sb.ToString());
                //lock (Debug.) Debug.WriteLine(sb.ToString());
            };
            if (all)
            {
                switch (toSearch)
                {
                    case "objects":
                        {
                            var info = Directory.CreateDirectory("objects");
                            foreach (var obj in File.Objects)
                            {
                                string filename = Path.Combine(info.FullName, obj.Name);
                                filename = Path.ChangeExtension(filename, "lua");
                             
                                using (StreamWriter sw = new StreamWriter(filename)) MakeObject(context, obj, sw);
                            }
                        }
                        break;
                    case "scripts":
                        {
                            List<Task> tasks = new List<Task>();
                            var info = Directory.CreateDirectory("scripts");
                            Regex argMatch = new Regex(@"self\.argument(\d+)", RegexOptions.Compiled);
                            foreach (var s in File.Scripts)
                            {
                                string filename = Path.Combine(info.FullName, s.Name);
                                if (context.doThreads)
                                {
                                    var ctx = context.Clone();
                                    ctx.DebugName = s.Name;
                                    Task task = Task.Run(() =>
                                    {
                                        if (s.Data != null)
                                        {
                                                WriteScript(ctx, s.Name, s.Data, filename + ".lua");
                                            
                                        }
                                        else
                                        {
                                            ctx.Error("Script {0} index is -1", s.Name);
                                        }
                                        ctx.CheckAsync();
                                       
                                    });
                                    tasks.Add(task);
                                } else
                                {
                                    context.DebugName = s.Name;
                                    if (s.Data!= null)
                                    {
                                        WriteScript(context, s.Name, s.Data, filename + ".lua");
                                    } else
                                    {
                                        Console.WriteLine("Script {0} index is -1", s.Name);
                                    }
                                    
                                }
                                
                            }
                            Task.WaitAll(tasks.ToArray());
                        }
                        break;
                    default:
                        Console.WriteLine("Unkonwn -all specifiyer");
                        BadExit(1);
                        break;
                }
            }
            else
            {
                List<Task> tasks = new List<Task>();
                HashSet<string> objects_done = new HashSet<string>();
                foreach (var s in File.Search(toSearch))
                {
                    File.GObject o = s as File.GObject;
                    if (o != null)
                    {

                        context.DebugName = o.Name; // in case of debug
                        string filename = Path.ChangeExtension(o.Name, context.doLua ? ".lua" : ".js");
                        if (context.Debug)
                        {
                            using (StreamWriter sw = new StreamWriter(filename)) MakeObject(context, o, sw);
                        }
                        else
                        {
                            var ctx = context.Clone();
                            ctx.DebugName = o.Name;
                            Task task = Task.Run(() =>
                            {
                                using (StreamWriter  sw = new StreamWriter(filename)) MakeObject(ctx, o, sw);
                            });
                            tasks.Add(task);
                            ctx.CheckAsync();
                        }
                        continue;
                    }
                    File.Script os = s as File.Script;
                    if(os != null)
                    {
                        context.DebugName = os.Name; // in case of debug
                        string codeName = os.Name.Replace("gml_Script_", "");
                        string filename = Path.ChangeExtension(codeName, context.doLua ? ".lua" : ".js");
                        if (context.Debug)
                        {
                             WriteScript(context, codeName, os.Data, filename);
                        }
                        else
                        {
                            var ctx = context.Clone();
                            ctx.DebugName = os.Name;
                            Task task = Task.Run(() => {
                                WriteScript(ctx, codeName, os.Data, filename);
                            },ctx.ct);
                            tasks.Add(task);
                            ctx.CheckAsync();
                        }
                        continue;
                    }
                }
                Task.WaitAny(tasks.ToArray());
            }

            if(FilesFound.Count==0)
            {
                Console.WriteLine("No scripts or objects found with '" + toSearch + "' in the name");
            }
            System.Diagnostics.Debug.WriteLine("Done");
        }
    }
}
