using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using betteribttest.GMAst;
using betteribttest.FlowAnalysis;

namespace betteribttest
{
    static class Program
    {
        static ChunkReader cr;
        static List<string> InstanceList;
        static List<string> scriptList;
        static IEnumerable<string> scriptExecuteFunction(string n, IReadOnlyList<Ast> l)
        {
            for (int i = 0; i < l.Count; i++)
            {
                Ast arg = l[i];
                string ret = null;
                if (i == 0)
                {
                    int instance;
                    if (arg.TryParse(out instance) && (instance > 0 && instance < scriptList.Count))
                    {
                        //     var script = cr.scriptIndex.Find(x => x.script_index == instance);
                        ret = "\"" + cr.scriptIndex[instance].script_name + "\"";
                    }
                }
                if (ret == null) ret = arg.ToString();
                yield return ret;
            }
        }
        static IEnumerable<string> instanceCreateFunction(string n, IReadOnlyList<Ast> l)
        {
            for (int i = 0; i < l.Count; i++)
            {
                Ast arg = l[i];
                string ret = null;
                if (i == 2)
                {
                    int instance;
                    if (arg.TryParse(out instance) && (instance > 0 && instance < InstanceList.Count))
                    {
                        ret = "\"" + InstanceList[instance] + "\"";
                    }
                }
                if (ret == null) ret = arg.ToString();
                yield return ret;
            }
        }
        static IEnumerable<string> draw_spriteExisits(string n, IReadOnlyList<Ast> l)
        {
            for (int i = 0; i < l.Count; i++)
            {
                Ast arg = l[i];
                string ret = null;
                if (i == 0)
                {
                    int instance;
                    if (arg.TryParse(out instance) && (instance > 0 && instance < cr.spriteList.Count))
                    {
                        ret = "\"" + cr.spriteList[instance].Name + "\"";
                    }
                }
                if (ret == null) ret = arg.ToString();
                yield return ret;
            }
        }

        static IEnumerable<string> instanceExisits(string n, IReadOnlyList<Ast> l)
        {
            for (int i = 0; i < l.Count; i++)
            {
                Ast arg = l[i];
                string ret = null;
                if (i == 0)
                {
                    int instance;
                    if (arg.TryParse(out instance) && (instance > 0 && instance < InstanceList.Count))
                    {
                        ret = "\"" + InstanceList[instance] + "\"";
                    }
                }
                if (ret == null) ret = arg.ToString();
                yield return ret;
            }
        }
        static IEnumerable<string> instanceCollision_line(string n, IReadOnlyList<Ast> l)
        {
            for (int i = 0; i < l.Count; i++)
            {
                Ast arg = l[i];
                string ret = null;
                if (i == 3)
                {
                    int instance;
                    if (arg.TryParse(out instance) && (instance > 0 && instance < InstanceList.Count))
                    {
                        ret = "\"" + InstanceList[instance] + "\"";
                    }
                }
                if (ret == null) ret = arg.ToString();
                yield return ret;
            }
        }
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

           //  cr = new ChunkReader("D:\\Old Undertale\\files\\data.win", false); // main pc
            //  cr.DumpAllObjects("objects.txt");
            // cr = new ChunkReader("Undertale\\UNDERTALE.EXE", false);
            cr = new ChunkReader("C:\\Undertale\\UndertaleOld\\data.win", false); // alienware laptop
            //Decompiler dism = new Decompiler(cr);


            List<string> stringList = cr.stringList.Select(x => x.str).ToList();
            InstanceList = cr.objList.Select(x => x.Name).ToList();
            scriptList = cr.scriptIndex.Select(x => x.script_name).ToList();
            betteribttest.GMAst.ILDecompile ild = new GMAst.ILDecompile(stringList, InstanceList);
            



            AstCall.AddFunctionLookup("instance_create", instanceCreateFunction);
            AstCall.AddFunctionLookup("collision_line", instanceCollision_line);
            AstCall.AddFunctionLookup("instance_exists", instanceExisits);
            AstCall.AddFunctionLookup("script_execute", scriptExecuteFunction);
            AstCall.AddFunctionLookup("draw_sprite", draw_spriteExisits);
            AstCall.AddFunctionLookup("draw_sprite_ext", draw_spriteExisits);


            //  string filename_to_test = "undyne";
            //    string filename_to_test = "gasterblaster"; // lots of stuff  loops though THIS WORKS THIS WORKS!
            // string filename_to_test = "sansbullet"; //  other is a nice if not long if statements
            // we assume all the patches were done to calls and pushes

               string filename_to_test = "gml_Object_OBJ_WRITER_Draw_0";// reall loop test as we got a break in it
            // string filename_to_test = "obj_face_alphys_Step"; // this one is good but no shorts
            // string filename_to_test = "SCR_TEXTTYPE"; // start with something even simpler
            //   string filename_to_test = "SCR_TEXT"; // start with something even simpler
            //  string filename_to_test = "gml_Object_obj_dmgwriter_old_Draw_0"; // intrsting code, a bt?
            // string filename_to_test = "write"; // lots of stuff
            //  string filename_to_test = "OBJ_WRITER";



            // string filename_to_test = "Script_scr_asgface"; // this decompiles perfectly
          //  string filename_to_test = "gml_Object_obj_emptyborder_s_Step_0"; // slighty harder now WE GOT IT WOOOOOOO 


             //       string filename_to_test = "SCR_DIRECT"; // simple loop works!
            //  string filename_to_test = "gml_Script_SCR_TEXT";// case statement woo! way to long
            //   string filename_to_test = "gml_Object_obj_battlebomb_Alarm_3"; // hard, has pushenv with a break



            foreach (var files in cr.GetCodeStreams(filename_to_test))
            {
                var instructions = Instruction.Create(files.stream, stringList, InstanceList);
                instructions.SaveInstructions(files.ScriptName + ".asm");
                if (instructions.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No instructions on script '" + files.ScriptName + "'");
                    continue;
                }
                //System.Diagnostics.Debug.Assert(files.ScriptName != "gml_Object_obj_undyneboss_Destroy_0");
                MethodBody mb = new MethodBody();
                mb.Instructions = instructions.ToList();
                var graph = betteribttest.FlowAnalysis.ControlFlowGraphBuilder.Build(mb);
                graph.ComputeDominance();
                graph.ComputeDominanceFrontier();
                var export = graph.ExportGraph();
                export.Save(files.ScriptName + "_raw.dot");




                var list = ild.DecompileInternal(instructions);
                ILBlock method = new ILBlock();
                method.Body = list;
                using (PlainTextOutput sw = new PlainTextOutput(new StreamWriter(files.ScriptName + "_preil.txt"))) method.WriteTo(sw);
                ILAstOptimizer bodyGraph = new ILAstOptimizer();
                bodyGraph.Optimize(method);
                using (PlainTextOutput sw = new PlainTextOutput(new StreamWriter(files.ScriptName + "_optimize.txt"))) method.WriteTo(sw);


               


                var loops = betteribttest.FlowAnalysis.ControlStructureDetector.DetectStructure(graph, new System.Threading.CancellationToken());
                
              //  block2.SaveToFile(files.ScriptName + "_pre.cpp");
               


                //graph.ExportGraph(files.ScriptName + "_codegraph.txt");
                // graph.BuildAllAst(new Decompile(stringList, InstanceList), stackMap);
                //  graph.ExportGraph(files.ScriptName + "_graph.txt");
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

            foreach (var files in cr.GetCodeStreams())
            {
                newDecompiler.Disasemble(files.ScriptName, files.stream, stringList, InstanceList);
            }
            return;
            //       cr.DumpAllStrings("STRINGS.TXT");
            //   dism.DissasembleEveything();
            //     dism.writeFile("frog");
            //dism.TestStreamOutput("frog");
    
            //    dism.TestStreamOutput("obj_shaker_Alarm");
            //   dism.TestStreamOutput("gasterblaster_Draw");
            //  dism.TestStreamOutput("sansbullet");
            //    dism.TestStreamOutput("SCR_BORDERSETUP");
            //    dism.TestStreamOutput("obj_face_alphys_Step");
            //  dism.TestStreamOutput("SCR_GAMESTART");
            //     dism.TestStreamOutput("scr_facechoice");
            //   dism.TestStreamOutput("obj_dialoguer");
            //    cr.SaveTexturePacker("D:\\cocos2d-x\\tests\\cpp-empty-test\\Resources\\test.png", "D:\\cocos2d-x\\tests\\cpp-empty-test\\Resources\\test.plist", 14);
            //   cr.SaveNewTextureFormat("D:\\cocos2d-x\\tests\\cpp-empty-test\\Resources\\");
#endif
            //   ChunkReader cr = new ChunkReader("Undertale\\UNDERTALE.EXE", false);
            //   Disam dism = new Disam(cr);
            //  dism.writeFile("frog");
         //   Application.EnableVisualStyles();
         //   Application.SetCompatibleTextRenderingDefault(false);
         //   Application.Run(new Form1());
        }
    }
}
