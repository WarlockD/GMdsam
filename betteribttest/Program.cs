using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using betteribttest.Dissasembler;

namespace betteribttest
{
    static class Program
    {
        static ChunkReader cr;
        static List<string> InstanceList;
        static List<string> scriptList;
        static void spriteArgument(ILVariable v, ILExpression expr)
        {
            if (expr.Code == GMCode.Push)
            {
                ILValue arg = expr.Operand as ILValue;
                int instance;
                if (arg.TryParse(out instance) && (instance > 0 && instance < cr.spriteList.Count))
                {
                    arg.ValueText = "\"" + cr.spriteList[instance].Name + "\"";
                }
            }
        }
        static void instanceArgument(ILVariable v, ILExpression expr)
        {
            if (expr.Code == GMCode.Push)
            {
                ILValue arg = expr.Operand as ILValue;
                int instance;
                if (arg.TryParse(out instance) && (instance > 0 && instance < InstanceList.Count))
                {
                    arg.ValueText = "\"" + InstanceList[instance] + "\"";
                }
            }
        }
        static void fontArgument(ILVariable v, ILExpression expr)
        {
            if (expr.Code == GMCode.Push)
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
        static void colorArgument(ILVariable v, ILExpression expr)
        {
            if (expr.Code == GMCode.Push)
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
        static void scriptArgument(ILVariable v, ILExpression expr)
        {
            if (expr.Code == GMCode.Push)
            {
                ILValue arg = expr.Operand as ILValue;
                int instance;
                if (arg.TryParse(out instance) && (instance > 0 && instance < cr.scriptIndex.Count))
                {
                    arg.ValueText = "\"" + cr.scriptIndex[instance].script_name + "\"";

                }
            }
        }
        static void scriptExecuteFunction(string n, IList<ILExpression> l)
        {
            Debug.Assert(l.Count > 0);
            scriptArgument(null, l[0]);
        }
        static void instanceCreateFunction(string n, IList<ILExpression> l)
        {
            Debug.Assert(l.Count == 3);
            instanceArgument(null, l[2]);
        }
        static void draw_spriteExisits(string n, IList<ILExpression> l)
        {
            Debug.Assert(l.Count > 1);
            spriteArgument(null, l[0]);
        }

        static void instanceExisits(string n, IList<ILExpression> l)
        {
            Debug.Assert(l.Count ==1);
            instanceArgument(null, l[0]);
        }
        static void instanceCollision_line(string n, IList<ILExpression> l)
        {
            Debug.Assert(l.Count > 4);
            instanceArgument(null, l[3]);
        }
       
        public class CallFunctionLookup
        {
            public delegate void FunctionToText(string funcname, IList<ILExpression> arguments);
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
            public delegate void ArgumentToText(ILVariable v, ILExpression argument);
            Dictionary<string, ArgumentToText> _lookup = new Dictionary<string, ArgumentToText>();
            public void Add(string funcname, ArgumentToText func) { _lookup.Add(funcname, func); }
            public void FixCalls(ILBlock block)
            {
                // Check for assigns
                foreach (var push in block.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Pop))
                {
                    ArgumentToText func;
                    ILVariable v = push.Operand as ILVariable;
                    if (_lookup.TryGetValue(v.Name, out func)) func(v,  push.Arguments[0]);
                }
                // Check for equality
                foreach (var condition in block.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Seq || x.Code == GMCode.Sne))
                {
                    ArgumentToText func;
                    if(condition.Arguments[0].Code == GMCode.Push)
                    {
                        ILVariable v = condition.Arguments[0].Operand as ILVariable;
                        if (_lookup.TryGetValue(v.Name, out func)) func(v, condition.Arguments[1]);
                    }                    
                }
            }
        }
        static CallFunctionLookup FunctionFix = new CallFunctionLookup();
        static AssignRightValueLookup PushFix = new AssignRightValueLookup();
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

             cr = new ChunkReader("D:\\Old Undertale\\files\\data.win", false); // main pc
            //  cr.DumpAllObjects("objects.txt");
            // cr = new ChunkReader("Undertale\\UNDERTALE.EXE", false);
           // cr = new ChunkReader("C:\\Undertale\\UndertaleOld\\data.win", false); // alienware laptop
            //Decompiler dism = new Decompiler(cr);


            List<string> stringList = cr.stringList.Select(x => x.str).ToList();
            InstanceList = cr.objList.Select(x => x.Name).ToList();
            scriptList = cr.scriptIndex.Select(x => x.script_name).ToList();


            FunctionFix.Add("instance_create", instanceCreateFunction);
                FunctionFix.Add("collision_line", instanceCollision_line);
                FunctionFix.Add("instance_exists", instanceExisits);
                FunctionFix.Add("script_execute", scriptExecuteFunction);
                FunctionFix.Add("draw_sprite", draw_spriteExisits);
                FunctionFix.Add("draw_sprite_ext", draw_spriteExisits);

            FunctionFix.Add("draw_set_font", (string funcname, IList<ILExpression> l) =>{
                Debug.Assert(l.Count == 1);
                fontArgument(null,l[0]);
            });
            FunctionFix.Add("draw_set_color", (string funcname, IList<ILExpression> l) => {
                Debug.Assert(l.Count == 1);
                colorArgument(null, l[0]);
            });
            PushFix.Add("sym_s", spriteArgument);
            PushFix.Add("mycolor", colorArgument);
            PushFix.Add("myfont", fontArgument);
            //  string filename_to_test = "undyne";
            //    string filename_to_test = "gasterblaster"; // lots of stuff  loops though THIS WORKS THIS WORKS!
         //   string filename_to_test = "sansbullet"; //  other is a nice if not long if statements
            // we assume all the patches were done to calls and pushes

            //  string filename_to_test = "gml_Object_OBJ_WRITER_Draw_0";// reall loop test as we got a break in it
          //  string filename_to_test = "gml_Object_OBJ_WRITER";// reall loop test as we got a break in it


            // string filename_to_test = "obj_face_alphys_Step"; // this one is good but no shorts
            // string filename_to_test = "SCR_TEXTTYPE"; // start with something even simpler
            //  string filename_to_test = "SCR_TEXT"; // start with something even simpler
            //  string filename_to_test = "gml_Object_obj_dmgwriter_old_Draw_0"; // intrsting code, a bt?
            // string filename_to_test = "write"; // lots of stuff
            //  string filename_to_test = "OBJ_WRITER";



          //   string filename_to_test = "Script_scr_asgface"; // WORKS 4/12 too simple
           //   string filename_to_test = "gml_Object_obj_emptyborder_s_Step_0"; // slighty harder now WORKS 4/12
            // Emptyboarer is a MUST test.  It has a && in it as well as simple if statments and calls.  If we can't pass this nothing else will work


             //    string filename_to_test = "SCR_DIRECT"; // simple loop works! WORKS 4/12
            string filename_to_test = "gml_Script_SCR_TEXT";// case statement woo! way to long


          //     string filename_to_test = "gml_Object_obj_battlebomb_Alarm_3"; // hard, has pushenv with a break WORKS 4/14



            foreach (var files in cr.GetCodeStreams(filename_to_test))
            {
                //  Instruction.Instructions instructions = null;// Instruction.Create(files.stream, stringList, InstanceList);

                var instructionsNew = betteribttest.Dissasembler.Instruction.Dissasemble(files.stream.BaseStream, stringList, InstanceList);
                betteribttest.Dissasembler.InstructionHelper.DebugSaveList(instructionsNew.Values, files.ScriptName + "_new.asm");
                new betteribttest.Dissasembler.ILAstBuilder().Build(instructionsNew, false, stringList, InstanceList);


                //  if (instructions!= null) instructions.SaveInstructions(files.ScriptName + ".asm");
                continue;
                //System.Diagnostics.Debug.Assert(files.ScriptName != "gml_Object_obj_undyneboss_Destroy_0");
            }
            System.Diagnostics.Debug.WriteLine("Ok");
#if false
            foreach (var files in cr.GetCodeStreams(filename_to_test))
            {
                if (files.ScriptName == "gml_Script_SCR_TEXT") continue; // too big and too complcated right now
                try
                {
                    newDecompiler.Disasemble(files.ScriptName, files.stream, stringList, InstanceList);
                } catch(Exception e)
                {
                    // drop all exceptions
                    // throw new Exception(e);
                }
               
            }
#endif
        }
    }
}
