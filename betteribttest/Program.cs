using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using betteribttest.Dissasembler;
using System.IO;

namespace betteribttest
{
    static class Program
    {
        static ChunkReader cr;
        static List<string> InstanceList;
        static List<string> scriptList;
        static void spriteArgument(ILExpression expr)
        {
            if (expr.Code == GMCode.Constant)
            {
                ILValue arg = expr.Operand as ILValue;
                int instance;
                if (arg.TryParse(out instance) && (instance > 0 && instance < cr.spriteList.Count))
                {
                    arg.ValueText = "\"" + cr.spriteList[instance].Name + "\"";
                }
            }
        }
        static void soundArgument(ILExpression expr)
        {
            if (expr.Code == GMCode.Constant)
            {
                ILValue arg = expr.Operand as ILValue;
                int instance;
                if (arg.TryParse(out instance) && (instance > 0 && instance < cr.audioList.Count))
                {
                    arg.ValueText = "\"" + cr.audioList[instance].Name + "\"";
                }
            }
        }
        static void instanceArgument(ILExpression expr)
        {
            if (expr.Code == GMCode.Constant)
            {
                ILValue arg = expr.Operand as ILValue;
                int instance;
                if (arg.TryParse(out instance) && (instance > 0 && instance < InstanceList.Count))
                {
                    arg.ValueText = "\"" + InstanceList[instance] + "\"";
                }
            }
        }
        static void fontArgument(ILExpression expr)
        {
            if (expr.Code == GMCode.Constant)
            {
                ILValue arg = expr.Operand as ILValue;
                int instance;
                if (arg.TryParse(out instance) && (instance > 0 && instance < cr.resFonts.Count))
                {
                    arg.ValueText = "\"" + cr.resFonts[instance].Name + "\"";
                }
            }
        }
        // This just makes color look easyer to read
        static void colorArgument(ILExpression expr)
        {
            if (expr.Code == GMCode.Constant)
            {
                ILValue arg = expr.Operand as ILValue;
                int color;
                if (arg.TryParse(out color))
                {
                    byte red = (byte)(color & 0xFF);
                    byte green = (byte)(color >> 8 & 0xFF);
                    byte blue = (byte)(color >> 16 & 0xFF);
                    arg.ValueText = "Red=" + red + " ,Green=" + green + " ,Blue=" + blue;
                }
            }
        }
        static void scriptArgument(ILExpression expr)
        {
            if (expr.Code == GMCode.Constant)
            {
                ILValue arg = expr.Operand as ILValue;
                int instance;
                if (arg.TryParse(out instance) && (instance > 0 && instance < cr.scriptIndex.Count))
                {
                    arg.ValueText = "\"" + cr.scriptIndex[instance].script_name + "\"";

                }
            }
        }
        static void scriptExecuteFunction(string n, List<ILExpression> l)
        {
            Debug.Assert(l.Count > 0);
            scriptArgument(l[0]);
        }
        static void instanceCreateFunction(string n, List<ILExpression> l)
        {
            Debug.Assert(l.Count == 3);
            instanceArgument(l[2]);
        }
        static void draw_spriteExisits(string n, List<ILExpression> l)
        {
            Debug.Assert(l.Count > 1);
            spriteArgument(l[0]);
        }

        static void instanceExisits(string n, List<ILExpression> l)
        {
            Debug.Assert(l.Count == 1);
            instanceArgument(l[0]);
        }
        static void instanceCollision_line(string n, List<ILExpression> l)
        {
            Debug.Assert(l.Count > 4);
            instanceArgument(l[3]);
        }
        static void soundPlayStop(string n, List<ILExpression> l)
        {
            Debug.Assert(l.Count > 4);
            instanceArgument(l[0]);
        }
        public class CallFunctionLookup
        {
            public delegate void FunctionToText(string funcname, List<ILExpression> arguments);
            Dictionary<string, FunctionToText> _lookup = new Dictionary<string, FunctionToText>();
            public void Add(string funcname, FunctionToText func) { _lookup.Add(funcname, func); }
            public void FixCalls(ILBlock block)
            {
                foreach (var call in block.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Call))
                {
                    string funcName = call.Operand.ToString();
                    FunctionToText func;
                    if (_lookup.TryGetValue(funcName, out func)) func(funcName, call.Arguments);
                }
            }
        }
        public class AssignRightValueLookup
        {
            public delegate void ArgumentToText(ILExpression argument);
            Dictionary<string, ArgumentToText> _lookup = new Dictionary<string, ArgumentToText>();
            public void Add(string varName, ArgumentToText func) { _lookup.Add(varName, func); }
            public void FixCalls(ILBlock block)
            {
                // Check for assigns
                foreach (var push in block.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Assign))
                {
                    ArgumentToText func;
                    if (_lookup.TryGetValue(push.Arguments[0].ToString(), out func)) func(push.Arguments[0]);
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
            FunctionFix.Add("snd_stop", (string name, List<ILExpression> l) =>
            {
                Debug.Assert(l.Count > 0);
                soundArgument(l[0]);
            });
            FunctionFix.Add("snd_play", (string name, List<ILExpression> l) =>
            {
                Debug.Assert(l.Count > 0);
                soundArgument(l[0]);
            });


            FunctionFix.Add("draw_set_font", (string funcname, List<ILExpression> l) =>
            {
                Debug.Assert(l.Count == 1);
                fontArgument(l[0]);
            });
            FunctionFix.Add("draw_set_color", (string funcname, List<ILExpression> l) =>
            {
                Debug.Assert(l.Count == 1);
                colorArgument(l[0]);
            });
            PushFix.Add("self.sym_s", spriteArgument);
            PushFix.Add("self.mycolor", colorArgument);
            PushFix.Add("self.myfont", fontArgument);
            PushFix.Add("self.txtsound", soundArgument);
        }
        static void DebugMain()
        {
            // before I properly set up Main
            cr = new ChunkReader("D:\\Old Undertale\\files\\data.win", false); // main pc
            List<string> stringList = cr.stringList.Select(x => x.str).ToList();
            InstanceList = cr.objList.Select(x => x.Name).ToList();
            scriptList = cr.scriptIndex.Select(x => x.script_name).ToList();
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
        [STAThread]
        static void DecompileBlock(ILBlock block, string filename)
        {

        }
        static void Main(string[] args)
        {
            string dataWinFileName = args.ElementAtOrDefault(0);
            if (string.IsNullOrWhiteSpace(dataWinFileName))
            {
                Console.WriteLine("Missing data.win file");
                BadExit(1);
            }
            try
            {
                cr = new ChunkReader(dataWinFileName, false); // main pc
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not open data.win file '" + dataWinFileName + "'");
                Console.WriteLine("Exception: " + e.Message);
                BadExit(1);
            }

            List<string> stringList = cr.stringList.Select(x => x.str).ToList();
            InstanceList = cr.objList.Select(x => x.Name).ToList();
            scriptList = cr.scriptIndex.Select(x => x.script_name).ToList();
            FunctionReplacement();
            GMContext context = new GMContext() { cr = cr, InstanceList = InstanceList, scriptList = scriptList, Debug = false };
            bool doAsm = false;
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
                    case "-asm":
                        doAsm = true;
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
            if (all)
            {
                switch (toSearch)
                {
                    case "objects":
                        {
                            var errorinfo = Directory.CreateDirectory("error");
                            StreamWriter errorWriter = null;
                            foreach (var a in cr.GetAllObjectCode())
                            {
                                var info = Directory.CreateDirectory(a.ObjectName);
                                foreach (var files in a.Streams)
                                {
                                    var instructionsNew = betteribttest.Dissasembler.Instruction.Dissasemble(files.stream.BaseStream, stringList, InstanceList);
                                    if (doAsm)
                                    {
                                        string asm_filename = Path.Combine(info.FullName, files.ScriptName + ".asm");
                                        betteribttest.Dissasembler.InstructionHelper.DebugSaveList(instructionsNew.Values, asm_filename);
                                    }
                                    string code_name = Path.Combine(info.FullName, files.ScriptName + ".cpp");
                                    try
                                    {
                                        ILBlock block = new betteribttest.Dissasembler.ILAstBuilder().Build(instructionsNew, false, context);
                                        FunctionFix.FixCalls(block);
                                        PushFix.FixCalls(block);

                                        FilesFound.Add(code_name);
                                        block.DebugSave(code_name, "// ScriptName: " + files.ScriptName);
                                        Console.WriteLine("Written: " + files.ScriptName + ".cpp");
                                    }
                                    catch (Exception e)
                                    {
                                        if (errorWriter == null) errorWriter = new StreamWriter("error_objects.txt");
                                        string message = string.Format("Object: {0}  Error: {1}", files.ScriptName, e.Message);
                                        errorWriter.WriteLine(message);
                                        Console.WriteLine("Error: " + message);
                                    }
                                }
                            }
                        }
                        break;
                    case "scripts":
                        {
                            var errorinfo = Directory.CreateDirectory("error");
                            var info = Directory.CreateDirectory("scripts");
                            StreamWriter errorWriter = null;
                            foreach (var files in cr.GetAllScripts())
                            {
                                string code_name = Path.Combine(info.FullName, files.ScriptName + ".cpp");
                                context.CurrentScript = files.ScriptName;
                                var instructionsNew = betteribttest.Dissasembler.Instruction.Dissasemble(files.stream.BaseStream, stringList, InstanceList);
                                if (doAsm)
                                {
                                    string asm_filename = Path.Combine(info.FullName, files.ScriptName + ".asm");
                                    betteribttest.Dissasembler.InstructionHelper.DebugSaveList(instructionsNew.Values, asm_filename);
                                }
#if !DEBUG
                                try
                                {
#endif
                                Console.WriteLine("Writing: " + files.ScriptName + ".cpp");
                                    ILBlock block = new betteribttest.Dissasembler.ILAstBuilder().Build(instructionsNew, false, context);
                                    FunctionFix.FixCalls(block);
                                    PushFix.FixCalls(block);

                                    FilesFound.Add(code_name);
                                    block.DebugSave(code_name, "// ScriptName: " + files.ScriptName);
                                
#if !DEBUG
                                }
                                catch (Exception e)
                                {
                                    if (errorWriter == null) errorWriter = new StreamWriter("error_scripts.txt");
                                    string message = string.Format("Script: {0}  Error: {1}", files.ScriptName, e.Message);
                                    errorWriter.WriteLine(message);
                                    Console.WriteLine("Error: " + message);
                                }
#endif
                            }
                        }
                        break;
                    default:
                        Console.WriteLine("Unkonwn -all specifiyer");
                        BadExit(1);
                        break;
                }
            } else
            {
                
                foreach (var files in cr.GetCodeStreams(toSearch))
                {
                    //  Instruction.Instructions instructions = null;// Instruction.Create(files.stream, stringList, InstanceList);

                    var instructionsNew = betteribttest.Dissasembler.Instruction.Dissasemble(files.stream.BaseStream, stringList, InstanceList);
                    if (doAsm) betteribttest.Dissasembler.InstructionHelper.DebugSaveList(instructionsNew.Values, files.ScriptName + ".asm");
                    ILBlock block = new betteribttest.Dissasembler.ILAstBuilder().Build(instructionsNew, false, context);
                    //   block.Body.WriteNodes
                    // block.DebugSave("bytecode_test.cpp", "// ScriptName: " + files.ScriptName);
                    FunctionFix.FixCalls(block);
                    PushFix.FixCalls(block);
                    FilesFound.Add(files.ScriptName);
                    // block.DebugSave("bytecode_test.cpp", "// ScriptName: " + files.ScriptName);
#if DEBUG
                    betteribttest.Dissasembler.InstructionHelper.DebugSaveList(instructionsNew.Values, "debug.asm");
                    block.DebugSave("debug.cpp", "// ScriptName: " + files.ScriptName);
                    block.DebugSave(files.ScriptName + ".cpp", "// ScriptName: " + files.ScriptName);
#endif
                    Console.WriteLine("Written: " + files.ScriptName + ".cpp");
                }
            }

            if(FilesFound.Count==0)
            {
                Console.WriteLine("No scripts or objects found with '" + toSearch + "' in the name");
            } 
            System.Diagnostics.Debug.WriteLine("Done");
        }
    }
}
