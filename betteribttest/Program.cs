using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using betteribttest.Dissasembler;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace betteribttest
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
            if (expr.MatchIntConstant(out arg))
            {
                int instance = (int) arg.Value;
                arg.ValueText = "\"" + context.IndexToAudioName(instance) + "\"";
            }
        }
        static void instanceArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg))
                arg.ValueText = "\"" + context.InstanceToString((int) arg.Value) + "\"";
        }
        static void fontArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg))
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
            if (expr.MatchIntConstant(out arg))
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
        }
        static void DebugMain()
        {
            // before I properly set up Main
            //cr = new ChunkReader("D:\\Old Undertale\\files\\data.win", false); // main pc

            //  cr.DumpAllObjects("objects.txt");
            // cr = new ChunkReader("Undertale\\UNDERTALE.EXE", false);
            // cr = new ChunkReader("C:\\Undertale\\UndertaleOld\\data.win", false); // alienware laptop
            //Decompiler dism = new Decompiler(cr);
            FunctionReplacement();
            //  string filename_to_test = "undyne";
            //    string filename_to_test = "gasterblaster"; // lots of stuff  loops though THIS WORKS THIS WORKS!
            //   string filename_to_test = "sansbullet"; //  other is a nice if not long if statements
            // we assume all the patches were done to calls and pushes

            //  string filename_to_test = "gml_Object_OBJ_WRITER_Draw_0";// reall loop test as we got a break in it
            //  string filename_to_test = "gml_Object_OBJ_WRITER";// reall loop test as we got a break in it


            // string filename_to_test = "obj_face_alphys_Step"; // this one is good but no shorts
            // string filename_to_test = "SCR_TEXTTYPE"; // start with something even simpler
            string filename_to_test = "SCR_TEXT"; // start with something even simpler
                                                  //  string filename_to_test = "gml_Object_obj_dmgwriter_old_Draw_0"; // intrsting code, a bt?
                                                  // string filename_to_test = "write"; // lots of stuff
                                                  //string filename_to_test = "OBJ_WRITER";

            // dosn't work, still need to work on shorts too meh
            //  string filename_to_test = "gml_Object_OBJ_WRITER_Alarm_0"; // good switch test WORKS 5/15
            //  string filename_to_test = "GAMESTART";

            //   string filename_to_test = "Script_scr_asgface"; // WORKS 4/12 too simple
            //   string filename_to_test = "gml_Object_obj_emptyborder_s_Step_0"; // slighty harder now WORKS 4/12
            // Emptyboarer is a MUST test.  It has a && in it as well as simple if statments and calls.  If we can't pass this nothing else will work
            //    string filename_to_test = "SCR_DIRECT"; // simple loop works! WORKS 4/12
            // case statement woo! way to long, WORKS 4/14 my god, if this one works, they eveything works!  I hope
            // string filename_to_test = "gml_Script_SCR_TEXT";


            //     string filename_to_test = "gml_Object_obj_battlebomb_Alarm_3"; // hard, has pushenv with a break WORKS 4/14

            filename_to_test = "gml_Object_OBJ_WRITERCREATOR_Create_0";
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
            var instructionsNew = betteribttest.Dissasembler.Instruction.Dissasemble(code, context);
            if (context.doAsm)
            {
                string asm_filename = filename + ".asm";
                betteribttest.Dissasembler.InstructionHelper.DebugSaveList(instructionsNew.Values, asm_filename);
            }
            string raw_filename = Path.GetFileName(filename);
            ILBlock block = new betteribttest.Dissasembler.ILAstBuilder().Build(instructionsNew, false, context);
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
            using (StringWriter sw = new StringWriter())
            {
                PlainTextWriter ptext = new PlainTextWriter(sw);
                block.Body.WriteLuaNodes(ptext); // remember this is already idented
                code = sw.ToString();
                code = CleanLuaText(code); // god I need to fix this, maybe I have to build custom ast's afterall
            }
            return code;
        }
        static void CleanLuaText(StringBuilder sb)
        {
            // fix some bugs
            sb.Replace(" && ", " and ");
            sb.Replace(" || ", " or ");
            sb.Replace("stack.self", "self");
            sb.Replace("!=", "~=");
            sb.Replace("\r\n\r\n", "\r\n");
        }
        static string CleanLuaText(string text)
        {
            StringBuilder sb = new StringBuilder(text);
            CleanLuaText(sb);
            return sb.ToString();
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
        static void objectHeadder(ITextOutput output, GMK_Object obj)
        {
            output.WriteLine("function new_{0}(self)", obj.Name);
            output.Indent();
            output.WriteLine("function event_user(v) self.UserEvent[v]() end");
            output.WriteLine();

        }
        static void objectFooter(ITextOutput output, GMK_Object obj)
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
                public string FullText;
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
                if (!v.isLocal) vi.Instance = v.InstanceName ?? v.Instance.ToString();

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
        public static void MakeObject(GMContext context, ChunkReader cr, GMK_Object obj, TextWriter output)
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
                            GMK_Code codeData = cr.codeList[a.CodeOffset];
                            if (context.Debug)
                            {
                                Debug.WriteLine("Name: " + codeData.Name + " Event: " + GMContext.EventToString(i, e.SubType));
                            }
                            ILBlock method = DecompileBlock(context, new MemoryStream(codeData.data));
                            string code = LuaCodeText(context, method); // auto indents it
                            cache.AddVars(method);

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

        static void MakeAllLuaObjects(ChunkReader cr, GMContext context)
        {
            List<Task> tasks = new List<Task>();
            foreach (var a in cr.GetAllObjectCode())
            {

                // var info = Directory.CreateDirectory(a.ObjectName);
                using (StreamWriter sw = new StreamWriter(a.Name + ".lua")) MakeObject(context, cr, a,sw);


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
        static void WriteScript(GMContext context, string codeName, Stream codeStream, TextWriter tw, string header = null)
        {
            ILBlock block = DecompileBlock(context, codeStream);
            string scriptName = codeName.Remove(0, "gml_Script_".Length);
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
            PlainTextOutput ptext = new PlainTextOutput(tw);
            if (context.doLua)
            {
                ptext.WriteLine("-- ScriptName: {0} ", codeName);
                if (header != null) ptext.WriteLine("-- {0}", header);
                ptext.Write("function {0}(self", scriptName);
                for (int i = 0; i < arguments; i++) ptext.Write(",argument{0}", i);
                ptext.WriteLine(")");
                ptext.Indent();
                block.Body.WriteLuaNodes(ptext, true);
                ptext.Unindent();
                ptext.WriteLine("end");
            } else
            {
                throw new Exception("Not supported yet");
            }
        }

        static void Main(string[] args)
        {
            ChunkReader cr=null;
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
                cr = new ChunkReader(dataWinFileName, false); // main pc
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
            context = new GMContext(cr);
 
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
                    case "-o":
                        pos++;
                        toSearch = args.ElementAtOrDefault(pos);
                        context.makeObject = true;
                        context.doLua = true;
                        // DebugLuaObject
                        {
                            GMK_Data data;
                            if(!cr.nameMap.TryGetValue(toSearch,out data))
                            {
                                Console.WriteLine("Could not find {0}", toSearch);
                                Environment.Exit(1);
                            }
                            GMK_Object obj = data as GMK_Object;
                            if(obj == null)
                            {
                                Console.WriteLine("{0} is not an object", toSearch);
                                Environment.Exit(1);
                            }
                            using (StreamWriter sw = new StreamWriter(obj.Name + ".lua")) MakeObject(context, cr, obj, sw);
                            Environment.Exit(0);
                        }
                        pos++;
                        break;
                    case "-debug":
                        pos++;
                        context.Debug = true;
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
                        MakeAllLuaObjects(cr, context);
                        return ;
                        
                        break;
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
                            foreach (var files in cr.GetAllObjectCode())
                            {
                                string filename = Path.Combine(info.FullName, files.Name);
                                using (StreamWriter sw = new StreamWriter(filename)) MakeObject(context, cr, files, sw);
                            }
                        }
                        break;
                    case "scripts":
                        {
                            List<Task> tasks = new List<Task>();
                            var info = Directory.CreateDirectory("scripts");
                            Regex argMatch = new Regex(@"self\.argument(\d+)", RegexOptions.Compiled);
                            foreach (var files in cr.codeList.Where(x => x.Name.Contains("gml_Script")))
                            {
                                string filename = Path.Combine(info.FullName, files.Name);
                                Task task = Task.Run(() =>
                                {
                                    using (StreamWriter sw = new StreamWriter(filename + ".lua"))
                                        WriteScript(context, files.Name, new MemoryStream(files.data), sw);
                                });
                                tasks.Add(task);
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
                foreach (var s in cr.nameMap.Values.Where(x => x.Name.Contains(toSearch)))
                {
                    GMK_Object o = s as GMK_Object;
                    if (o != null)
                    {
                        string filename = Path.ChangeExtension(o.Name, context.doLua ? ".lua" : ".js");
                        if (context.Debug)
                        {
                            using (StreamWriter sw = new StreamWriter(filename)) MakeObject(context, cr, o, sw);
                        }
                        else
                        {
                            Task task = Task.Run(() =>
                            {
                                using (StreamWriter sw = new StreamWriter(filename)) MakeObject(context, cr, o, sw);
                            });
                            tasks.Add(task);
                        }
                        continue;
                    } 
                    else if (s.Name.Contains("gml_Script")) // its a script
                    {
                        GMK_Code c = s as GMK_Code;
                        string codeName = c.Name.Replace("gml_Script_", "");
                        string filename = Path.ChangeExtension(codeName, context.doLua ? ".lua" : ".js");
                        Task task = Task.Run(() =>
                        {
                            MemoryStream ms = new MemoryStream(c.data);
                            using (StreamWriter sw = new StreamWriter(filename)) WriteScript(context, codeName, ms, sw);
                        }
                        );
                        tasks.Add(task);

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
