using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.CodeDom.Compiler;

namespace betteribttest
{
    public partial class Form1 : Form
    {
        ChunkReader cr;
 
       
    
        class Spr_Sprite
        {
            
            public uint x;
            public uint y;
            public uint flags;
            public uint width;
            public uint height;
            public uint[] extra;
            public Bitmap mask;

        }
        Spr_Sprite getSprSprite(string filename)
        {
            Spr_Sprite spr = null;
            using (BinaryReader r = new BinaryReader(File.Open(filename, FileMode.Open)))
            {
                spr = new Spr_Sprite();
                spr.x = r.ReadUInt32(); // size?
                spr.y =  r.ReadUInt32(); // size?
                spr.flags = r.ReadUInt32(); // size?
                spr.width = r.ReadUInt32()+1; // size?
                spr.height = r.ReadUInt32()+1; // size?
                List<uint> extra = new List<uint>();
                for (int i = 0; i < 12; i++) extra.Add(r.ReadUInt32());
                spr.extra = extra.ToArray(); //PixelFormat.Format1bppIndexed
                spr.mask = new Bitmap((int)spr.x, (int)spr.height, PixelFormat.Format1bppIndexed);
                BitmapData bdata = spr.mask.LockBits(new Rectangle(0, 0, spr.mask.Width, spr.mask.Height), ImageLockMode.ReadWrite, PixelFormat.Format1bppIndexed);
                for (int y = 0; y < spr.height; y++)
                {
                    int i = bdata.Stride * y;
                    for (int x = 0; x < ((int)spr.width / 8); x++) Marshal.WriteByte(bdata.Scan0, i + x, r.ReadByte());
                }
                spr.mask.UnlockBits(bdata);
            }
            image = spr.mask;
            this.Update();
            return spr;
            //  SaveAllStrings();
        }
        class BGND_Record
        {
            public ushort A;
            public ushort B;
            public uint C;
            public uint D;
            public uint E;
            public ushort F;
            public ushort G;
        }
        List<uint> BGND_lookup = new List<uint>();
        Dictionary<uint, BGND_Record> BGND_Records = new Dictionary<uint, BGND_Record>();
        void getBGND()
        {

            using (BinaryReader r = new BinaryReader(File.Open("Undertale\\BGND\\000009ad.dat", FileMode.Open)))
            {
                uint len = r.ReadUInt32(); // size?
                for (int i = 0; i < len; i++) BGND_lookup.Add(r.ReadUInt32());
                for (int i = 0; i < len; i++)
                {
                    BGND_Record rec = new BGND_Record();
                    rec.A = r.ReadUInt16();
                    rec.B = r.ReadUInt16();

                    rec.C = r.ReadUInt32();
                    rec.D = r.ReadUInt32();
                    rec.E = r.ReadUInt32();

                    rec.F = r.ReadUInt16();
                    rec.G = r.ReadUInt16();
                   // BGND_Records.Add(str[i], rec);
                }
            }
            //  SaveAllStrings();
        }

        Bitmap image = null;
        public void displayImage(string filename)
        {
            List<int> data = new List<int>();
            Bitmap bmp = null;
            using (BinaryReader r = new BinaryReader(File.Open(filename, FileMode.Open)))
            {
                for (int i = 0; i < 16; i++) data.Add(r.ReadInt32());

                int width = 20;// data[0];
                int height = data[1];
                bmp = new Bitmap(width, height, PixelFormat.Format1bppIndexed);
                BitmapData bdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format1bppIndexed);
                for(int y=0;y < height;y++)
                {
                    int i = bdata.Stride * y;
                    for (int x = 0; x < (width/8 ) ; x++) Marshal.WriteByte(bdata.Scan0, i+x, r.ReadByte());
                }
                bmp.UnlockBits(bdata);
                bmp.UnlockBits(bdata);

            }
          
           // System.Diagnostics.Debug.Write(v);
            this.Width = bmp.Width * 4;
            this.Height = bmp.Height * 4;

            image = bmp;
        }
        List<string> InstanceList;
        List<string> scriptList;
        IEnumerable<string> scriptExecuteFunction(string n, IReadOnlyList<Ast> l)
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
        IEnumerable<string> instanceCreateFunction(string n, IReadOnlyList<Ast> l)
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
        IEnumerable<string> draw_spriteExisits(string n, IReadOnlyList<Ast> l)
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
        
        IEnumerable<string> instanceExisits(string n, IReadOnlyList<Ast> l)
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
        IEnumerable<string> instanceCollision_line(string n, IReadOnlyList<Ast> l)
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
                        ret = "\""+ InstanceList[instance] + "\"" ;
                    }
                }
                if (ret == null) ret = arg.ToString();
                yield return ret;
            }
        }
        public Form1()
        {
            
               InitializeComponent();
           // cr = new ChunkReader("D:\\Old Undertale\\files\\data.win", false);
            //  cr.DumpAllObjects("objects.txt");
            // cr = new ChunkReader("Undertale\\UNDERTALE.EXE", false);
            cr = new ChunkReader("C:\\Undertale\\UndertaleOld\\data.win",false);
            //Decompiler dism = new Decompiler(cr);
            
  
            List<string> stringList = cr.stringList.Select(x => x.str).ToList();
            Decompile newDecompiler = new Decompile(stringList, InstanceList);
            InstanceList =newDecompiler.InstanceList = cr.objList.Select(x => x.Name).ToList();
            scriptList = cr.scriptIndex.Select(x => x.script_name).ToList(); 



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

         //   string filename_to_test = "gml_Object_OBJ_WRITER_Draw_0";// reall loop test as we got a break in it
            // string filename_to_test = "obj_face_alphys_Step"; // this one is good but no shorts
            // string filename_to_test = "SCR_TEXTTYPE"; // start with something even simpler
            //   string filename_to_test = "SCR_TEXT"; // start with something even simpler
            //  string filename_to_test = "gml_Object_obj_dmgwriter_old_Draw_0"; // intrsting code, a bt?
            // string filename_to_test = "write"; // lots of stuff
          //  string filename_to_test = "OBJ_WRITER";



            // string filename_to_test = "Script_scr_asgface"; // this decompiles perfectly
                  string filename_to_test = "gml_Object_obj_emptyborder_s_Step_0"; // slighty harder now WE GOT IT WOOOOOOO 
            //         string filename_to_test = "SCR_DIRECT"; // simple loop works!
            //  string filename_to_test = "gml_Script_SCR_TEXT";// case statement woo! way to long
            //   string filename_to_test = "gml_Object_obj_battlebomb_Alarm_3"; // hard, has pushenv with a break

            Dictionary<ControlFlowNodeOld, Stack<Ast>> stackMap = new Dictionary<ControlFlowNodeOld, Stack<Ast>>();

            foreach (var files in cr.GetCodeStreams(filename_to_test))
            {
                var instructions = Instruction.Create(files.stream, stringList, InstanceList);
                instructions.SaveInstructions(files.ScriptName + ".asm");
                if (instructions.Count ==0)
                {
                    System.Diagnostics.Debug.WriteLine("No instructions on script '" + files.ScriptName+"'");
                    continue;
                }
                //System.Diagnostics.Debug.Assert(files.ScriptName != "gml_Object_obj_undyneboss_Destroy_0");
                MethodBody mb = new MethodBody();
                mb.Instructions = instructions.ToList();
                var graph = betteribttest.FlowAnalysis.ControlFlowGraphBuilder.Build(mb);
                graph.ComputeDominance();
                graph.ComputeDominanceFrontier();
                var loops = betteribttest.FlowAnalysis.ControlStructureDetector.DetectStructure(graph, new System.Threading.CancellationToken());
                var block2 = betteribttest.FlowAnalysis.AstGraphBuilder.BuildAst(graph, newDecompiler);
                var export = graph.ExportGraph();
                block2.SaveToFile(files.ScriptName + "_pre.cpp");
                export.Save(files.ScriptName + ".dot");
              

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
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
           if(image != null)
            {
                Graphics g = e.Graphics;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(image, new Rectangle(0, 0, this.Width, this.Height));

             //   image = bmp;
            }
        }
    }
}
