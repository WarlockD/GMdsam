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
        public List<int> itsAllInts(string filename)
        {
            List<int> data = new List<int>();
            Stream f = File.Open(filename, FileMode.Open);
            BinaryReader r = new BinaryReader(f);
            int len = (int)f.Length / 4;
            while(f.Position != f.Length)
            {
                data.Add(r.ReadInt32());
            }
            string v = "";
            int col = 0;
            foreach (int a in data)
            {
                v += a.ToString() + " ";
                if (++col > 32)
                {
                    v += "\n";
                    col = 0;
                }

            }
            v += "\n";
            int t = data[data.Count - 4];

            v += String.Format("{0,5:X} {1,5:X}\n", (t & 0xFFFF), (t >> 16) & 0xFFFF);
            t = data[data.Count - 3];
            v += String.Format("{0,5:X} {1,5:X}\n", (t & 0xFFFF), (t >> 16) & 0xFFFF);
            t = data[data.Count - 2];
            v += String.Format("{0,5:X} {1,5:X}\n", (t & 0xFFFF), (t >> 16) & 0xFFFF);
            t = data[data.Count - 1];
            v += String.Format("{0,5:X} {1,5:X}\n", (t & 0xFFFF), (t >> 16) & 0xFFFF);
            System.Diagnostics.Debug.Write(v);
            System.Diagnostics.Debug.Write(cr.stringList[(int)((uint)(data[data.Count - 1]))].str);
            
            return data;
        }

        public Form1()
        {
            InitializeComponent();
           // cr = new ChunkReader("D:\\Old Undertale\\files\\data.win", false);
            //  cr.DumpAllObjects("objects.txt");
             cr = new ChunkReader("Undertale\\UNDERTALE.EXE", false);
            //Decompiler dism = new Decompiler(cr);
            
            DecompilerNew newDecompiler = new DecompilerNew();
            List<string> stringList = cr.stringList.Select(x => x.str).ToList();
            List<string> InstanceList=newDecompiler.InstanceList = cr.objList.Select(x => x.Name).ToList();
            // we assume all the patches were done to calls and pushes
            // string filename_to_test = "obj_face_alphys_Step";
            //  string filename_to_test = "SCR_TEXTTYPE"; // start with something even simpler
            // string filename_to_test = "Script_scr_asgface"; // this decompiles perfectly
            //   string filename_to_test = "gml_Object_obj_emptyborder_s"; // slighty harder now
            string filename_to_test = "SCR_DIRECT"; // loop
          //  string filename_to_test = "gml_Object_obj_battlebomb_Alarm_3";
            foreach (var files in cr.GetCodeStreams(filename_to_test))
            {
                newDecompiler.Disasemble(files.ScriptName, files.stream, stringList, InstanceList);
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
            // dism.TestStreamOutput("gasterblaster");
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
