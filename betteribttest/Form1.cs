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
                //  Graphics g = Graphics.FromImage(bmp);
                //   g.Clear(Color.Black);
                //   g.Dispose();
                //  g = null;
                BitmapData bdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format1bppIndexed);
                for(int y=0;y < height;y++)
                {
                    int i = bdata.Stride * y;
                    for (int x = 0; x < (width/8 ) ; x++) Marshal.WriteByte(bdata.Scan0, i+x, r.ReadByte());
                }
                bmp.UnlockBits(bdata);
                /*
                for (int y = 0; y < height; y++)
                {
                    for(int x = 0; x< width;x+=8)
                    {
                        int b = r.ReadByte();
                        int index = y * bdata.Stride + (x * 4);
                        Marshal.WriteByte(bdata.Scan0, index, p);
                        /* for(int i=0;i<8;i++)
                         {
                             Color c = ((i << i) & 0x80) != 0 ? Color.White : Color.Black;
                             dbg += ((i << i) & 0x80) != 0 ? "X" : " ";
                             bmp.SetPixel(x+i, y, c);
                         }
                         
                    }
                }
        */
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
        class GMK_Settings
        {
            public int GAME_ID;
            public byte[] DPLAY_GUID; // 16
        }
 
        public Form1()
        {
            InitializeComponent();
            cr = new ChunkReader("Undertale\\UNDERTALE.EXE", false);
            Disam dism = new Disam(cr);
         //   dism.DissasembleEveything();
       //     dism.writeFile("frog");
       //     dism.writeFile("SCR_GAMESTART");
 
            cr.SaveTexturePacker("D:\\cocos2d-x\\tests\\cpp-empty-test\\Resources\\test.png", "D:\\cocos2d-x\\tests\\cpp-empty-test\\Resources\\test.plist", 14);
            GMK_Font fnt = cr.resFonts[1];
            return;
                Bitmap bmp_chars = cr.filesImage[fnt.bitmap.texture_id].image; // don't know why or how the fonts know to look at this texture
           // GMK_Font fnt = cr.resFonts[0];
            //Bitmap bmp_chars = cr.filesImage[0].image; // don't know why or how the fonts know to look at this texture
            Bitmap target = new Bitmap(320, 200);
            string message = "TesIj $\nHaul ,$";
           
            int offset_x = 0;
            int offset_y = 0;
            int texture_x = fnt.bitmap.x;
            int texture_y = fnt.bitmap.y;
            System.Diagnostics.Debug.WriteLine(fnt);
            using (Graphics g = Graphics.FromImage(target))
            {
                g.Clear(Color.Black);
                for (int i = 0; i < 6; i++)
                {
                    g.DrawLine(Pens.Aqua, 0, i * fnt.font_size + 10, 9, i * fnt.font_size + 10);
                }
                foreach (char c in message)
                {
                    if (c == '\n')
                    {
                        offset_x = 0;
                        offset_y += fnt.font_size;
                    }
                    else
                    {
                        GMK_FontGlyph gly = fnt.map[c];

                        Rectangle src = new Rectangle(gly.x + texture_x, gly.y + texture_y, gly.width, gly.height);
                        System.Diagnostics.Debug.WriteLine(gly + " : " + src);
                        g.DrawImage(bmp_chars, offset_x + 10, offset_y + 10, src, GraphicsUnit.Pixel);
                        offset_x += gly.char_offset;
                    }

                }

            }


            string font_dir = "D:\\cocos2d-x\\tests\\cpp-empty-test\\Resources\\fonts\\";
            string font_bmp_filename = "font_chars.png";
            this.image = target;
            Stream s = File.Open(font_dir + fnt.Name + ".fnt", FileMode.Create);
            System.IO.TextWriter tw = new StreamWriter(s);
            bmp_chars.Save(font_dir + font_bmp_filename);
            tw.Write("info face=\"" + fnt.Name + "\"");
            tw.Write(" size=" + fnt.font_size);
            tw.Write(" bold=" + "0");//  fnt.maybe_Bold ? "1": "0");
            tw.Write(" italic=" + "0");//  fnt.maybe_Bold ? "1": "0");
            tw.Write(" charset=\"\"");
            tw.Write(" unicode=0");
            tw.Write(" stretchH=100");
            tw.Write(" smooth=1"); 
            tw.Write(" aa=0");
            tw.Write(" padding=0,0,0,0"); // might be wrong
            tw.Write(" spacing=1,1"); // might be wrong
            tw.Write(" outline=0"); // might be wrong
            tw.WriteLine();
            tw.Write("common lineHeight=" + fnt.font_size);
            tw.Write(" base=" + (fnt.font_size-2)); // humm unsure on base
            tw.Write(" scaleW=1024 scaleH=1024");
            tw.Write(" pages=1 packed=0"); // alphaChnl=1 redChnl=0 greenChnl=0 blueChnl=0 hummmmm
            tw.WriteLine();
            tw.Write("page id=0 file=\"" + font_bmp_filename+"\"");
            tw.WriteLine();
            tw.Write("chars count=" + fnt.glyphs.Count());
            tw.WriteLine();
            foreach(var g in fnt.glyphs)
            {
                int x = g.x + texture_x;
                int y = g.y + texture_y;
                string line = String.Format("char id={0,-5} x={1,-5} y={2,-5} width={3,-5} height={4,-5} xoffset=0    yoffset=0    xadvance={5,-5} xpage=0     chnl=15", (ushort)g.c, x, y, g.width, g.height, g.char_offset);
                tw.WriteLine(line);
            }

            tw.Close();
            tw = null;
            // getSprSprite("Undertale\\SPRT\\spr_heart");
            //getSprSprite("Undertale\\SPRT\\spr_snowdrake_head");
            //itsAllInts("Undertale\\SPRT\\spr_fallleaf");
            // disam("Undertale\\CODE\\gml_Script_SCR_TEXT");
            //disam("Undertale\\CODE\\gml_Script_attention_hackerz_no_2");
            System.Diagnostics.Debug.Write("Woot");


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
