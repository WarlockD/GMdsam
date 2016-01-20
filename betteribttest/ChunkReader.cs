using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.IO.Compression;
using Microsoft.Deployment.Compression;
using Microsoft.Deployment.Compression.Cab;

namespace betteribttest
{
    public class GMK_Data
    {
        public readonly int index;
        public readonly int offset;
        public string name;
        public GMK_Data(int index, int offset) { this.index = index;  this.offset = offset;this.name = null; }
        public override string ToString()
        {
            return name == null ? String.Format("{{ index: {0,-5} offset: {1,-8:X} }}",index,offset) : String.Format("{{ name: {0,-25}, index: {1,-5} offset: {2,-8:X} }}", name, index, offset);
        }
    }
    public class GMK_Code : GMK_Data
    {
        public byte[] code;
        public GMK_Code(int index, int offset) : base(index, offset) { }
    }
    class GMK_Value : GMK_Data
    {
        public int size;
        public byte[] data;
        public GMK_Value(int index, int offset) : base(index, offset) { }
    }
    class GMK_Image : GMK_Data
    {
        public string filename {  get { return name; } set { name = value; } }
        public Bitmap image;
        public GMK_Image(int index, int offset) : base(index, offset) { }
     
    }
    class GMK_String : GMK_Data
    {
        public string str;
        public string escapedString
        {
            get
            {
                using (var writer = new StringWriter())
                {
                    using (var provider = System.CodeDom.Compiler.CodeDomProvider.CreateProvider("CSharp"))
                    {
                        provider.GenerateCodeFromExpression(new System.CodeDom.CodePrimitiveExpression(str), writer, null);
                        return writer.ToString();
                    }
                }
            }
        }
        public GMK_String(int index, int offset) : base(index, offset) { }
        public override string ToString()
        {
            return base.ToString() + " String: " + escapedString;
        }
    }
    class GMK_Object: GMK_Data
    { // HUMM We have 12 allarms!  EACH ALLARM IS A 1 DIMENIONAL ARRAY! WOO!
      //  public int[] header; // first 20 bytes, last byte seems to be a size
      //  public byte[] data;
        public int[] data;
        public int obj_id = 0; // instance varable
        public bool isSolid = false;
        public bool isVisiable = false;
        public bool isPersistant = false;
        public int depth = 0; // negitive numbers are close to the player
        public int[] alarm_offsets = null; // hummmm!
        public int obj_index = 0; // readonly, mabye its index?

        public List<GMK_Value> values = new List<GMK_Value>();
        public GMK_Object(int index, int offset) : base(index, offset) { }
        public override string ToString()
        {
            return base.ToString() + " : " + String.Format("{{  obj_id : {0}, object_index: {1} }}", obj_id, obj_index);
        }

    }
    class GMK_Sprite : GMK_Data // mabye this is from object?
    {
        public GMK_Object obj;
        public int sprite_index;
        public int sprite_width;
        public int sprite_height;
        public int sprite_xoffset;
        public int sprite_yoffset;

        public int iamge_alpha;
        public int iamge_angle;
        public int iamge_blend;
        public int image_index;
        public int image_number;
        public int image_speed;
        public int image_xscale;
        public int image_yscale;

        public GMK_Sprite(int index, int offset) : base(index, offset) { }
    }
    class GMK_BackgroundPos : GMK_Data
    {
        public short texture_x; // pritty short these are offsets to the texture
        public short texture_y;
        public short texture_width;     // same about the width and height
        public short texture_height;
        // This might be more internal object stuff?
        // offset x and y are usally zero and offset width and hight usally equal the texture width hieght
        public short offset_x; // humm
        public short offset_y; // don't know
        public short offset_width; // humm
        public short offset_height; // don't know
        public GMK_BackgroundPos(int index, int offset) : base(index, offset) { }

    }
    class GMK_Background : GMK_Data
    {
        public int[] stuff; // 3 ints of stuff
        // after this there is an offset to this data
        public GMK_BackgroundPos pos;
        public GMK_Background(int index, int offset) : base(index, offset) { }
    }
    class GMK_FontGlyph : GMK_Data
    {
        public char c; // int16
        public short x;
        public short y;
        public short width;
        public short height;
        public short char_offset;
        public GMK_FontGlyph(int index, int offset) : base(index, offset) { }
        public override string ToString()
        {
            return "{ char= '" + c + "' , x=" + x + ", y=" + y + ", widht=" + width + ", height=" + height+ ", char_offset=" + char_offset + "}";
        }
    }
    class GMK_Font : GMK_Data
    {
        public List<GMK_FontGlyph> glyphs= new List<GMK_FontGlyph>(); // note sure what ANY of this stuff is except for font_size
        public Dictionary<char, GMK_FontGlyph> map = new Dictionary<char, GMK_FontGlyph>();
        public string description;
        public int font_size;
        public bool maybe_Bold; // I think these two are for bold and italix? not sure
        public bool maybe_Italii;
        public short texture_number;// = r.ReadInt16();
        public short meh;// = r.ReadInt16();
        public short meh2;// = r.ReadInt16();
        public short meh3;// = r.ReadInt16();
        public  int ref_maybe;// = r.ReadInt32();
        public List<int> header;
        public void Add(GMK_FontGlyph g)
        {
            glyphs.Add(g);
            map[g.c] = g;
        }
        public GMK_Font(int index, int offset) : base(index, offset) { }
        public override string ToString()
        {
            return base.ToString() + " { font_size = " + font_size + ", texture_number = " + texture_number + ", meh = " + meh + " , meh2 = " + meh2 + ", meh 3 = " + meh3 + " , ref_maybe = " + ref_maybe + " } ";
        }
        // System.Diagnostics.Debug.WriteLine(String.Format("'{0}': {1,-4}{2,-4}{3,-4}{4,-4}{5,-4}", c,cb, x1, y1, x2, y2));
    }
    class ChunkReader
    {

        BinaryReader r = null;
        BinaryWriter w = null;
        Dictionary<string, byte[]> wholeChunks = new Dictionary<string, byte[]>();
        //  public Dictionary<string, List<GMKFile>> fileChunks = new Dictionary<string, List<GMKFile>>();
        //  public List<GMKFile> filesCode;
        //  public List<GMKFile> filesObj;
        public Dictionary<long, GMK_Data> offsetMap = new Dictionary<long, GMK_Data>();
        public Dictionary<string, GMK_Data> nameMap = new Dictionary<string, GMK_Data>();
        void AddData(GMK_Data d)
        {
            if (offsetMap.ContainsKey(d.offset))
                WriteDebug(String.Format("Offset: 0x{0,-8:X8} Exists", d.offset));
            else offsetMap[d.offset] = d;
            if (d.name != null) {
                if (nameMap.ContainsKey(d.name)) WriteDebug(String.Format("Offset: 0x{0,-8:X8}  Name: {1} Exists", d.offset,d.name));
                else nameMap[d.name] = d;
            }
        }
      
        public List<GMK_Object> objList = new List<GMK_Object>();
        public Dictionary<int, GMK_Object> objMap = new Dictionary<int, GMK_Object>();
        public Dictionary<int, GMK_Object> objMapId = new Dictionary<int, GMK_Object>();
        public Dictionary<int, GMK_Object> objMapIndex = new Dictionary<int, GMK_Object>();

        public List<GMK_Image> filesImage;
        public List<GMK_Code> codeList = new List<GMK_Code>();
        public List<GMK_Background> backgroundList = new List<GMK_Background>();
        public List<GMK_Sprite> spriteList = new List<GMK_Sprite>();
        public List<GMK_String> stringList = new List<GMK_String>();

        static readonly int[] offset_debug_patern = new int[] { 0, -4, -8, 4, 8 };

        public GMK_Data OffsetDebugLookup(long offset)
        {
            GMK_Data d = null;
            foreach (int i in offset_debug_patern)
            {
                long lookup = offset + i;
                
                if (offsetMap.TryGetValue(lookup,out d))
                {
                    if (i != 0) WriteDebug("Offset: 0x{0,-8:X8} off by {1} with {2}", offset, i, d.ToString());
                    else return d;
                }
            }
            return d;
        }
        // sanity checks on objects
        void AddObject(GMK_Object o)
        {
            AddData(o); // remember addData is used on objects here
            GMK_Object lookup;
            if (o.obj_id > -1) if (objMapId.TryGetValue(o.obj_id, out lookup))
                {
                    WriteDebug("obj_id DUP: " + o + " -> " + lookup);
                }
                else objMapId.Add(o.obj_id, o);
            if (o.obj_index > -1) if (objMapIndex.TryGetValue(o.obj_index, out lookup))
                {
                    WriteDebug("obj_index DUP: " + o + " -> " + lookup);
                }
                else objMapIndex.Add(o.obj_index, o);
        }

        Stack<long> savedOffsets = new Stack<long>();


        void CheckAndSetOffset(long offset)
        {
            if (offsetMap.ContainsKey((int)offset))
                WriteDebug("CheckOffset: 0x{0,-8:X8} Exists", offset, offsetMap[(int)offset].GetType().Name);
            r.BaseStream.Position = offset;
        }
        void PushOffset(long offset)
        {
            PushOffset();
            CheckAndSetOffset(offset);
        }

        void PushOffset()
        {
            savedOffsets.Push(r.BaseStream.Position);
        }
        void PopOffset()
        {
            r.BaseStream.Position = savedOffsets.Pop();
        }
        void readPropertiy()
        {
            PushOffset();
            int value = r.ReadInt32();
            switch (value)
            {
                case 0:
                    return;
                case 1: // offset from something
                    PushOffset(r.ReadInt32());
                    readPropertiy();
                    PopOffset();
                    break;
                case 10: // array? mabye?
                    PushOffset();
                    readPropertiy();
                    PopOffset();
                    break;
            }
        }
        /// <summary>
        /// Tests to see IF this is a ref.  not 100% sure but its 4 byte aligned and below the file size
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        bool testIfRef(int value)
        {
            return testIfRef(value, r.BaseStream.Length);
        }
        bool testIfRef(int value, long chumkLimit)
        {
            if ((value % 4) == 0 && value < chumkLimit) return true;
            else return false;
        }
        string debugint(byte[] value, bool lookup = true,int index=0)
        {
            int intdata = BitConverter.ToInt32(value, index);
            short shortdata1 = BitConverter.ToInt16(value, index);
            short shortdata2 = BitConverter.ToInt16(value, index +2);
            string msg = String.Format("{0,12}[0x{0,-8:X8}]   {1,8}[0x{1,-4:X4}]   {2,5}[0x{2,-4:X4}]   ", intdata, shortdata1, shortdata2);
            if (lookup && intdata !=0 && intdata < r.BaseStream.Length)
            {
                string stringref = readVarString(intdata);
                if (stringref == null || stringref == "" || stringref.Length == 0 ||  stringref.Length > 40) // no string or WAY to long to be an ident
                {
                    msg += "\n";
                    PushOffset(intdata);
                    byte[] about4Ints = r.ReadBytes(16);
                    for (int i = 0; i < 16; i+=4) msg += i.ToString() + "     " + debugint(about4Ints, false, i) + "\n";
                    PopOffset();
                    msg = msg.Substring(0, msg.Length - 1);
                }
                else
                    msg += "String : " + EscapeString(stringref);
            }

            return msg;
        }
        void debugintOut(byte[] value,int index=0,bool lookup = true)
        {
            string msg = debugint(value, lookup, index);
            WriteDebug(msg);
        }
        string debugInit(int value)
        {
            string msg;
            if (value < 0x10000)
            {
                msg = String.Format("[{1,-4:X4}]:{0,-7} ", value, value & 0xFFFF);
            }
            else
            {
                int shortdata1 = (short)(value & 0xFFFF);
                int shortdata2 = (short)(value >> 16);
                msg = String.Format("[{0,-8:X8}]:{0,-12} ", value);
                msg += String.Format("[{1,-4:X4}]:{0,-7} ", shortdata2, shortdata2 & 0xFFFF);
                msg += String.Format("[{1,-4:X4}]:{0,-7} ", shortdata1, shortdata1 &0xFFFF);
            }
            return msg;
        }
        void debugInits(List<int> values,  bool tryFilter = true, bool lookup = false)
        {
            string msg = "";
            for (int i = 0; i < values.Count; i++)
            {
                int value = values[i];
                msg += String.Format("{0,-2} : ", i);
                msg += debugInit(value);
                msg += '\n';
            }
            WriteDebug(msg);
        }

        string readFixedString(int len)
        {
            byte[] bytes = r.ReadBytes(len);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        byte[] readBlock(long from, long to)
        {
            PushOffset(from);
            byte[] bytes = r.ReadBytes((int)(to - from));
            System.Diagnostics.Debug.Assert(to != r.BaseStream.Position);
            PopOffset();
            return bytes;
        }
        string readVarString(long offset) {
            // Humm, it looks like strings don't HAVE to bee int aligned.  Humm
            //  if (!testIfRef(offset)) throw new Exception("offset not int alligned or out of bounds");
            if (offset < 9999) throw new Exception("offset is null");
            GMK_Data d;
            if (offsetMap.TryGetValue(offset, out d)){
                GMK_String s = d as GMK_String;
                if (s == null) throw new Exception(d.ToString() + " NOT A STRING");
                return s.str;
            }
            throw new Exception("STRING NOT FOUND");
         //   return s;

        }
        string readVarString() // We shouldn't throw here
        {
            throw new Exception("Error out for now");
            List<byte> bytes = new List<byte>();
            for(;;)
            {
                if(r.BaseStream.Position >= r.BaseStream.Length) return null; // end of string before a 0
                byte b = r.ReadByte();
                if (b == 0) break;
                bytes.Add(b);
            }
            if (bytes.Count == 0) return null; // null if we just read a 0
            return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        }
        byte[] dumpBytes(int offset, int chunkSize)
        {
            PushOffset(offset);
            byte[] data = r.ReadBytes(chunkSize);
            PopOffset();
            return data;
        }
        List<int> CollectEntries(long offset, long chunkLimit)
        {
            PushOffset(offset);
            List<int> offsets = CollectEntries(chunkLimit);
            PopOffset();
            return offsets;
        }
        List<int> CollectEntries(long chunkLimit)
        {
            int j = 0;
            List<int> entries = new List<int>();
            int fileCount = r.ReadInt32();
            for (int i = 0; i < fileCount; i++)
            {
                int offset = r.ReadInt32();
                if (offset > 0 && (chunkLimit == 0 || offset < chunkLimit )) entries.Add(offset); // used to use chunkLimit here but some entries might not need
            }
            return entries;
        }
        void DoCode(long roomStart, long chunkLimit)
        {
            PushOffset(roomStart);
            List<int> offsets = CollectEntries(chunkLimit);
            for (int i = 0; i < offsets.Count; i++)
            {
                int offset = offsets[i]; GMK_Code code = new GMK_Code(i, offset); 
                r.BaseStream.Position = offset;
                code.name = readVarString(r.ReadInt32());
                int size = r.ReadInt32();
                code.code = r.ReadBytes(size);
                codeList.Add(code);
                AddData(code);
            }
            PopOffset();
        }
        void DoBackground(long chunkStart,long chunkLimit)
        {
            PushOffset(chunkStart); 
            // backgrounds are fixed entries?  like fonts I gather math SIZE = 0x14
            // BMS script has the wrong data for this, they are 0x20 (32 bytes) big
            List<int> offsets = CollectEntries(chunkLimit);

            for(int i=0;i< offsets.Count;i++)
            {
                int offset = offsets[i]; GMK_Background back = new GMK_Background(i,offset); 


                r.BaseStream.Position = offset;
                int next_offset = (i + 1) < offsets.Count ? offsets[i + 1] : (int)chunkLimit;
                int size = next_offset - offset;
                back.name = readVarString(r.ReadInt32());
                back.stuff = readInts(3).ToArray();
                int last = r.ReadInt32();
                GMK_BackgroundPos back_pos = new GMK_BackgroundPos(0, last);
                r.BaseStream.Position = last;
                back_pos.texture_x = r.ReadInt16();
                back_pos.texture_x = r.ReadInt16();
                back_pos.texture_width = r.ReadInt16();
                back_pos.texture_height = r.ReadInt16();
                back_pos.offset_x = r.ReadInt16();
                back_pos.offset_x = r.ReadInt16();
                back_pos.offset_width = r.ReadInt16();
                back_pos.offset_height = r.ReadInt16();
                back.pos = back_pos;
                AddData(back);
                backgroundList.Add(back);
            }
            PopOffset();
        }
        void DoRoom(long roomStart, long chunkLimit)
        {
            /*
            PushOffset(roomStart);
            List<int> offsets = CollectEntries(chunkLimit);
            int i = 0;
            do
            {
                long startOffset = r.BaseStream.Position;
                GMKFile f = new GMKFile();
                f.index = i;
                long offset = f.offset = offsets[i++];
                long next_offset = i < offsets.Count ? offsets[i] : chunkLimit;
                f.name = readVarString(r.ReadInt32());
                f.name2 = readVarString(r.ReadInt32());
                List<byte> data = new List<byte>();
                while (r.BaseStream.Position < next_offset) data.Add((byte)r.ReadByte());
                f.data = data.ToArray();
                files.Add(f);
            } while (i < offsets.Count);
            PopOffset();
            fileChunks.Add("ROOM", files);
            */
        }
        List<int> readInts(int count)
        {
            List<int> values = new List<int>();
            for (int i = 0; i < count; i++) values.Add(r.ReadInt32());
            return values;
        }
        List<short> readShorts(int count)
        {
            List<short> values = new List<short>();
            for (int i = 0; i < count; i += 2)
            {
                values.Add(r.ReadInt16());
                values.Add(r.ReadInt16());
            }
            return values;
        }
     
        public List<GMK_Font> resFonts;
        void DoFont(long chunkStart, long chunkLimit)
        {
            r.BaseStream.Position = chunkStart;
            List<int> font_offs = new List<int>();
            List<GMK_Font> res = new List<GMK_Font>();
            int numOfFonts = r.ReadInt32();
            font_offs = readInts(numOfFonts);
            for (int i = 0; i < numOfFonts; i++)
            {
                int offset = font_offs[i]; 
                GMK_Font fnt = new GMK_Font(i, offset);
                r.BaseStream.Position = offset;
                // int debug_offset = (int)(offset - chunkStart);


               // string dbg_msg = checkForRefs(r.BaseStream.Position, chunkLimit);
               // WriteDebug(dbg_msg);
                
                fnt.name = readVarString(r.ReadInt32());
                fnt.description = readVarString(r.ReadInt32());
                fnt.font_size = r.ReadInt32();
                fnt.maybe_Italii = r.ReadInt32() == 1 ;
                fnt.maybe_Bold = r.ReadInt32()== 1 ;
                fnt.texture_number = r.ReadInt16();
                fnt.meh = r.ReadInt16();
                fnt.meh2 = r.ReadInt16();
                fnt.meh3 = r.ReadInt16();
                fnt.ref_maybe = r.ReadInt32();
                fnt.header = readInts(2);
                int charCount = r.ReadInt32();
                List<int> char_offsets = readInts(charCount);
                for (int j = 0; j < charCount; j++)
                {
                    offset = char_offsets[j];
                    int debug_fnt_offset = (int)(offset - chunkStart);
                    r.BaseStream.Position = offset;
                    GMK_FontGlyph g = new GMK_FontGlyph(j, offset);
                    g.c = (char)r.ReadInt16();
                    g.x = r.ReadInt16();
                    g.y = r.ReadInt16();
                    g.width = r.ReadInt16();
                    g.height = r.ReadInt16();
                    g.char_offset = r.ReadInt16();
                    fnt.Add(g);
                }
                res.Add(fnt);
                AddData(fnt);
                int a = fnt.ref_maybe & 0xFFFF;
                int b = fnt.ref_maybe >> 16;
                    
            }
            resFonts = res;
        }
        public bool debugOn = true;
        void WriteDebug(string line)
        {
            if (!debugOn) return;
            if (line.Last() == '\n') System.Diagnostics.Debug.Write(line);
            else System.Diagnostics.Debug.WriteLine(line);
        }
        void WriteDebug(string fmt,params object[] objs)
        {
            if (!debugOn) return;
            string line = String.Format(fmt,  objs);
            if(line.Last() == '\n') System.Diagnostics.Debug.Write(line);
            else System.Diagnostics.Debug.WriteLine(line);
        }
        void objectFlags(int a)
        {
            int flag_type = a >> 24;
            int operand = a & 0x00FFFFFF;
            switch(flag_type)
            {
                case 0x3F: // its zero
                    if (operand != 0) throw new Exception("Not Zero?");
                    break;
                case 0x3C:
                case 0x3D:
                    // alot of c in here
                    if(operand != 0xCCCCCD)
                        WriteDebug("Flag Decode({0,-2:X2}:{1,-2:X2}:{2,-2:X2})", flag_type, operand >> 16, operand & 0xFFFF);
                    break;
                default:
                    throw new Exception("Unknown?");
                    break;
            }
        }
        string DebugRefLookup(int ident=0)
        {
            int value = r.ReadInt32();
            if (value == 0) return "<NULL>\n"; // nothiing?
            string msg = new string('-', ident);
            switch(value)
            {
                case 1: // link, so we do a ref lookup?
                    value = r.ReadInt32();
                    msg += " [" + value.ToString("X8") + "] ";
                    PushOffset(value);
                    DebugRefLookup(ident+1);
                    PopOffset();
                break;
                default:
                    msg += debugInit(value) + "\n";
                    break;
            }
            return msg;
        }
        string checkForRefs(long chunkStart, long chunkLimit)
        {
            System.Diagnostics.Debug.Assert((chunkStart % 4) == 0 && (chunkLimit % 4) == 0); // these are all int allinged right?
            PushOffset(chunkStart);
            long size = chunkLimit - chunkStart;
            List<int> data = readInts((int)size);
            string msg = "";
            for(int i=0;i< data.Count; i++)
            {
                int value = data[i];
                
                if (value < 100) continue; // this should quickly get rid of the ones we don't think exist
                GMK_Data d = OffsetDebugLookup(value);
                if (d != null) msg += "FOUND(" + i + "): " + d.ToString() + "\n";
            }
            PopOffset();
            if (string.IsNullOrWhiteSpace(msg)) return null; else return msg;
        }
        void DoObject(long chunkStart, long chunkLimit)
        {
            PushOffset(chunkStart);
            List<int> offsets = CollectEntries(chunkLimit);
            for (int i = 0; i < offsets.Count; i++)
            {
                int obj_offset = offsets[i];
                GMK_Object obj = new GMK_Object(i, obj_offset);
                CheckAndSetOffset(obj_offset);

                int obj_limit = (i + 1) < offsets.Count ? offsets[i + 1] : (int)chunkLimit;
                int obj_size = obj_limit - obj_offset;
                int str_offset = r.ReadInt32();

                obj.name = readVarString(str_offset); // oo my god.
               
                
                List<int> head = readInts(19);
                obj.data = head.ToArray();

                obj.obj_id = head[0];
                obj.isPersistant = head[1] != 0; // mabye 7, 7 is always negitive when this is 1?
                obj.depth = head[5]; // fairly sure
                obj.obj_index = head[6];
                objectFlags(head[10]);
                
                List<int> alarms = CollectEntries(obj_limit);
              ///  string debug_msg = checkForRefs(r.BaseStream.Position, obj_limit);
              //  if (debug_msg != null)
           //     {
               //     WriteDebug("Object: " + obj.name + "--------------------------");
               //     WriteDebug(debug_msg);
                //    WriteDebug("------------------");
              //  }
             //   continue;
                System.Diagnostics.Debug.Assert(alarms.Count == 12); // these are all int allinged right, no object more than 12?
                for (int j=0;j<12;j++) // there are 12 refrences here.  I assume they go with the alarms?
                {
                    int offset = alarms[j];
                    CheckAndSetOffset(offset);
                    int next_offset = (j+1) < alarms.Count ? alarms[j+1] : (int)obj_limit;
                    int size = next_offset - offset;
                    System.Diagnostics.Debug.Assert((size % 4) ==0); // these are all int allinged right?
                    if ((size / 4) > 1)
                    {
                        List<int> stuff = readInts(size / 4);
                        //string test = readVarString(stuff[12]);
                        // anything over the chunklimit has a good chance of being a string
                        for(int b=0;b<stuff.Count;b++)
                        {
                            if(stuff[b] >= chunkLimit)
                            {
                                 string stemp = readVarString(stuff[b]); // oo my god.
                                if(stemp != null)
                                {
                               //     WriteDebug(obj.name + " String: " + stemp + " Pos : " + b + " index : " + j);
                                }
                            }
                        }
                        /*
                        int value = r.ReadInt32();
                        string msg = obj.name + "  Index: " + j + " Size: " + size + " Current offset: " + offset.ToString("X8");
                        switch (value)
                        {
                            case 0: break; // nothing
                            case 1: // refrence?
                                {
                                    offset = r.ReadInt32();
                                    PushOffset(offset);
                                    WriteDebug(msg + " Value: " + value + "\n" + DebugRefLookup());
                                    PopOffset();
                                    //  List<int> stuff = readInts(18);
                                    //    debugInits(stuff);
                                    //  byte[] d = r.ReadBytes(size - 4);
                                    //   WriteDebug(msg + " Value: " + value);
                                    //   debugintOut(d);

                                }
                                break;
                            default:
                                {
                                    byte[] d = r.ReadBytes(size - 4);
                                    WriteDebug(msg + " Value: " + value);
                                    debugintOut(d);

                                }
                                break;
                        }
                        */
                    }
                    

                }
                if(obj.name.IndexOf("obj_froggit")>-1)
                {
                    WriteDebug("We have FROG: " + obj.name);
                }
              //  System.Diagnostics.Debug.Assert(obj.name != "obj_froggit");
                objList.Add(obj);
                AddObject(obj);
            }
            PopOffset();
        }
      
      
        void doFORM()
        {

        }
        void doTXRT(long chunkStart, long chunkLimit)
        {
            List<GMK_Image> res = new List<GMK_Image>();
            PushOffset(chunkStart);
            List<int> offsets = CollectEntries(chunkLimit);
            for(int i=0;i<offsets.Count;i++)
            {
                // so its a double offset list? humm
                int offset = offsets[i];
                r.BaseStream.Position = offset;
                int dummy = r.ReadInt32(); // 1 means and ofset hummmmmmmm.  mabye to raw data?
                int new_offset = r.ReadInt32();
                offsets[i] = new_offset;
            }
            for (int i = 0; i < offsets.Count; i++)
            {
                int offset = offsets[i];
                GMK_Image gi = new GMK_Image(i,offset);
                r.BaseStream.Position = offset;
                int next_offset = (i+1) < offsets.Count ? offsets[i+1] : (int)chunkLimit;
                int size = next_offset - offset;
                byte[] data = r.ReadBytes(size);
                MemoryStream ms = new MemoryStream(data);
                gi.image = new Bitmap(ms); // this works right?
                res.Add(gi);
                AddData(gi);
              //  gi.image.Save(i.ToString() + "_test_.png");
            }
            filesImage = res;
        }
        void DoSprite(long chunkStart,long chunkLimit)
        {
            PushOffset(chunkStart);
            List<int> offsets = CollectEntries(chunkLimit);
            for (int i = 0; i < offsets.Count; i++)
            {
                int offset = offsets[i++];
                GMK_Sprite obj = new GMK_Sprite(i, offset);
                CheckAndSetOffset(offset);

                long next_offset = i < offsets.Count ? offsets[i] : chunkLimit;
                obj.name = readVarString(r.ReadInt32());
                if (obj.name.IndexOf("frog") > -1)
                {
                    WriteDebug("We have FROG: " + obj.name);
                }
                // objList.Add(obj);
                AddData(obj);
            }
            PopOffset();
            /*
            Spr_Sprite getSprSprite(string filename)
        {
                Spr_Sprite spr = null;
                using (BinaryReader r = new BinaryReader(File.Open(filename, FileMode.Open)))
                {
                    spr = new Spr_Sprite();
                    spr.x = r.ReadUInt32(); // size?
                    spr.y = r.ReadUInt32(); // size?
                    spr.flags = r.ReadUInt32(); // size?
                    spr.width = r.ReadUInt32() + 1; // size?
                    spr.height = r.ReadUInt32() + 1; // size?
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
            */
        }
        string EscapeString(string s)
        {
            // http://stackoverflow.com/questions/323640/can-i-convert-a-c-sharp-string-value-to-an-escaped-string-literal
                using (var writer = new StringWriter())
                {
                    using (var provider = System.CodeDom.Compiler.CodeDomProvider.CreateProvider("CSharp"))
                    {
                        provider.GenerateCodeFromExpression(new System.CodeDom.CodePrimitiveExpression(s), writer, null);
                        return writer.ToString();
                    }
            }
        }
        void doStrings(long chunkStart, long chunkLimit)
        {
            PushOffset(chunkStart);
            List<int> offsets = CollectEntries(chunkLimit);
            for (int i = 0; i < offsets.Count; i++)
            {
                int offset = offsets[i];
                GMK_String str = new GMK_String(i, offset+4); // We are storing the offset to the zro trminatd string
                CheckAndSetOffset(offset);
                int str_len = r.ReadInt32() + 1; //size
                byte[] bstr = r.ReadBytes(str_len);
                str.str = System.Text.Encoding.UTF8.GetString(bstr,0, (bstr.Last() == 0 ? (bstr.Length - 1)  : bstr.Length));
                stringList.Add(str);
                AddData(str);
            }

            PopOffset();
        }
        class Chunk
        {
            public readonly long start;
            public readonly long end;
            public readonly long size;
            public readonly string name;
            public Chunk(string name, long start, long size) { this.name = name; this.start = start; this.end = start + size; this.size = size; }
        }
        Dictionary<string, Chunk> chunks = new Dictionary<string, Chunk>();

        public void runChunkReader()
        {
            Chunk chunk = null;
            long full_size = r.BaseStream.Length;
            while (r.BaseStream.Position < full_size)
            {
                string chunkName = readFixedString(4);
                int chunkSize = r.ReadInt32();
                long chuckStart = r.BaseStream.Position;
                chunk = new Chunk(chunkName, chuckStart, chunkSize);
                chunks[chunkName] = chunk;
                if (chunkName == "FORM") full_size = chunkSize; // special case for form
                else r.BaseStream.Position = chuckStart + chunkSize; // make sure we are always starting at the next chunk               
            } // we want to get strings out first so we can easily check if refrences equal them
            if (chunks.TryGetValue("STRG", out chunk))
                doStrings(chunk.start, chunk.end);
            if (chunks.TryGetValue("TXTR", out chunk))
                doTXRT(chunk.start, chunk.end); // doing objects right now

            if (chunks.TryGetValue("BGND", out chunk))
                DoBackground(chunk.start, chunk.end); // doing objects right now
            if (chunks.TryGetValue("SPRT", out chunk))
                DoSprite(chunk.start, chunk.end); // doing objects right now
            if (chunks.TryGetValue("OBJT", out chunk))
                DoObject(chunk.start, chunk.end); // doing objects right now


            if (chunks.TryGetValue("ROOM", out chunk))
                DoRoom(chunk.start, chunk.end); // doing objects right now

            if (chunks.TryGetValue("FONT", out chunk))
                DoFont(chunk.start, chunk.end); // doing objects right now
            if (chunks.TryGetValue("CODE", out chunk))
                DoCode(chunk.start, chunk.end); // doing objects right now

            //    case "OBJT": DoObject(chuckStart, chunkLimit); break;
            //     case "TXTR": doTXRT(chuckStart, chunkLimit); break;
            //    case "FONT": DoFont(chuckStart, chunkLimit); break;
            //    case "CODE": DoCode(chuckStart, chunkLimit); break;
            //     case "ROOM": DoRoom(chuckStart, chunkLimit); break;
            //    case "BGND": DoBackground(chuckStart, chunkLimit); break;
            //     case "SPRT": DoSprite(chuckStart, chunkLimit); break;
            //     case "STRG": doStrings(chuckStart, chunkLimit); break;
        }

        public ChunkReader(string filename, bool debugOn)
        {
            // this.debugOn = debugOn;
            this.debugOn = true;
            Stream s = null;

            if (filename.ToLower().IndexOf(".exe") !=-1){
                CabInfo cab = new CabInfo(filename);
                if (File.Exists("data.win")) File.Delete("data.win");
                cab.UnpackFile("data.win","data.win");  // cab.OpenRead("data.win"); doesn't work for some reason
                filename = "data.win";
            } 
            s = System.IO.File.Open(filename, FileMode.Open, FileAccess.Read);
            r = new BinaryReader(s);

            runChunkReader();
        }
    }
}