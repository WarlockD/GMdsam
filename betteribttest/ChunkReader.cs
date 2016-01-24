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
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
/*
Side note to the Disam potion.
To map variables and function names to the asembley REQUIRES some kind of emulation.  The dissassembler needs context to traslate
a var name to an string name.  So it requies what "self" object its referring too as well as whats on the stack 
This means we have to dissasemble the pop statment, and figure out the last few pop statements to get what instance, what self is, and then the var offset
THEN we can figure out the name of it
meh  I am going to work on sprites and rooms.  I got those almost done

*/
namespace betteribttest
{
    public class GMK_Data : IComparable<GMK_Data>, IEquatable<GMK_Data>
    { //create comparer
        public ChunkEntry FilePosition { get; private set; }
        public string Name { get; set; }
        public GMK_Data(ChunkEntry e) { FilePosition = e; Name = null; }
        public override string ToString()
        {
            return Name == null ? String.Format("{{ Type = GMK_Data, FilePosition: {0,-8:X} }}", FilePosition.Position) : String.Format("{{ Name = \"{1}\"  Type = GMK_Data, FilePosition: {0,-8:X} }}", FilePosition.Position, Name);
        }
        public int CompareTo(GMK_Data other)
        {
            return FilePosition.Position.CompareTo(other.FilePosition.Position);
        }
        public override int GetHashCode()
        {
            return FilePosition.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            GMK_Data d = obj as GMK_Data;
            if (d != null) return Equals(d);
            return false;
        }
        public bool Equals(GMK_Data other)
        {
            return other.FilePosition == FilePosition;
        }
    }
    public class GMK_Point
    {
        public short x;
        public short y;
        public GMK_Point(short x, short y) { this.x = x; this.y = y; }
        public GMK_Point(BinaryReader r) { this.x = r.ReadInt16(); this.y = r.ReadInt16(); }
        public override string ToString()
        {
            return "(" + x + "," + y + ")";
        }
    }
    public class GMK_Code : GMK_Data
    {
        public byte[] code=null;
        public GMK_Code(ChunkEntry e) : base(e) { }
    }
    class GMK_Value : GMK_Data
    {
        public int size;
        public byte[] data;
        public GMK_Value(ChunkEntry e) : base(e) { }
    }
    class GMK_Image : GMK_Data
    {
        public string filename {  get { return Name; } set { Name = value; } }
        public Bitmap image;
        public GMK_Image(ChunkEntry e) : base(e) { }
     
    }
    class GMK_String : GMK_Data
    {
        public int index;
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
        public GMK_String(ChunkEntry e) : base(e) { }
        public override string ToString()
        {
            return base.ToString() + " String: " + escapedString;
        }
    }
    class GMK_ScriptIndex : GMK_Data
    {
        public string script_name="";
        public int script_index=-1;
        public GMK_ScriptIndex(ChunkEntry e) : base(e) { }
        public override string ToString()
        {
            return "{ script_name = " + script_name + ", scrpit_index = " + script_index + " }" + base.ToString() ;
        }
    }
    class GMK_FuncOffset : GMK_Data
    {
        public string func_name = "";
        public int func_offset = -1;
        public int code_offset = -1;
        public GMK_FuncOffset(ChunkEntry e) : base(e) { }
        public override string ToString()
        {
            return "{ func_name = " + func_name + ", func_offset = " + func_offset + ", code_other = " + code_offset + " }" + base.ToString();
        }
    }
    class GMK_Var : GMK_Data
    {
        public string var_name = "";
        public int var_offset = -1;
        public int code_offset = -1;
        public GMK_Var(ChunkEntry e) : base(e) { }
        public override string ToString()
        {
            return "{ func_name = " + var_name + ", func_offset = " + var_offset + ", code_other = " + code_offset + " }" + base.ToString();
        }
    }
    class GMK_Object: GMK_Data
    { // HUMM We have 12 allarms!  EACH ALLARM IS A 1 DIMENIONAL ARRAY! WOO!
      //  public int[] header; // first 20 bytes, last byte seems to be a size
      //  public byte[] data;
        public int[] data; // header data is 19

        // MMabye this is the order
        public int obj_id = 0; // instance varable
        public int sprite_index;
        public bool Visible = false;
        public bool Solid = false;
        public int depth = 0; // negitive numbers are close to the player
        public bool isPersistant = false;
        public int Parent = -1;
        public int Mask = -1;
        public int PhysicsObject = -1;
        public int PhysicsObjectSensor = -1;
        public int PhysicsObjectShape = -1;
        public int PhysicsObjectDensity = -1;
        // Pulled from the compile code, not sure if its needed
        public int PhysicsObjectRestitution = -1;
        public int PhysicsObjectGroup = -1;
        public int PhysicsObjectLinearDamping = -1;
        public int PhysicsObjectAngularDamping = -1;
        public int PhysicsShapeVerticesCount = -1;
        public int PhysicsObjectFriction = -1;
        public int PhysicsObjectAwake = -1;
        public int PhysicsObjectKinematic = -1;
        //public List<int> PhysicsShapeVertices  // humm
        // irght after this it does arlarms
        public int[] alarm_offsets = null; // hummmm!

        public List<GMK_Value> values = new List<GMK_Value>();
        public GMK_Object(ChunkEntry e) : base(e) { }
        public override string ToString()
        {
            return base.ToString() + " : " + String.Format("{{  obj_id : {0}, object_index: {1} }}", obj_id, Parent);
        }

    }
    class GMK_Sprite : GMK_Data // mabye this is from object?
    {
        public GMK_Object obj;
        public int width;
        public int height;
        public int widht0;
        public int height0;
        public GMK_SpritePosition[] frames;


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
        public Bitmap mask;
        public GMK_Sprite(ChunkEntry e) : base(e) { }
    }
    class GMK_SpritePosition : GMK_Data
    {
        public short x; // this is the size of the record
        public short y;
        public short width;
        public short height;
        public short renderX;
        public short renderY;
        public short width0;
        public short height0;
        public short width1;
        public short height1;
        public short texture_id;
        public GMK_SpritePosition(ChunkEntry e) : base(e) { }
        public override string ToString()
        {
            return String.Format("{{ x = {0}, y = {1}, width = {2}, height = {3}, texture_id = {4} }}",x,y,width,height,texture_id) + base.ToString() ;
        }
    }
  
    class GMK_Background : GMK_Data
    {
        public int[] stuff; // 3 ints of stuff
        // after this there is an offset to this data
        public GMK_SpritePosition pos;
        public GMK_Background(ChunkEntry e) : base(e) { }
    }
    class GMK_FontGlyph : GMK_Data
    {
        public char c; // int16
        public short x;
        public short y;
        public short width;
        public short height;
        public short char_offset;
        public GMK_FontGlyph(ChunkEntry e) : base(e) { }
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
        public GMK_SpritePosition bitmap;
        public float scaleW;
        public float scaleH;
        public void Add(GMK_FontGlyph g)
        {
            glyphs.Add(g);
            map[g.c] = g;
        }
        public GMK_Font(ChunkEntry e) : base(e) { }
        public override string ToString()
        {
            return base.ToString() + " { font_size = " + font_size + " } ";
        }
        // System.Diagnostics.Debug.WriteLine(String.Format("'{0}': {1,-4}{2,-4}{3,-4}{4,-4}{5,-4}", c,cb, x1, y1, x2, y2));
    }
    class ChunkReader
    {
        ChunkStream r = null;
        public bool debugOn = true;
        void WriteDebug(string line)
        {
            if (!debugOn) return;
            if (line.Last() == '\n') System.Diagnostics.Debug.Write(line);
            else System.Diagnostics.Debug.WriteLine(line);
        }
        void WriteDebug(string fmt, params object[] objs)
        {
            if (!debugOn) return;
            string line = String.Format(fmt, objs);
            if (line.Last() == '\n') System.Diagnostics.Debug.Write(line);
            else System.Diagnostics.Debug.WriteLine(line);
        }


        Dictionary<string, byte[]> wholeChunks = new Dictionary<string, byte[]>();
        //  public Dictionary<string, List<GMKFile>> fileChunks = new Dictionary<string, List<GMKFile>>();
        //  public List<GMKFile> filesCode;
        //  public List<GMKFile> filesObj;
        public SortedDictionary<int, GMK_Data> offsetMap = new SortedDictionary<int, GMK_Data>();
        public Dictionary<string, GMK_Data> nameMap = new Dictionary<string, GMK_Data>();

        void AddData(GMK_Data d)
        {
            if (offsetMap.ContainsKey(d.FilePosition.Position)) WriteDebug(String.Format("Offset: 0x{0,-8:X8} Exists", d.FilePosition.Position));
            else offsetMap[d.FilePosition.Position] = d;
            if (d.Name != null) {
                if (nameMap.ContainsKey(d.Name)) WriteDebug(String.Format("Offset: 0x{0,-8:X8}  Name: {1} Exists", d.FilePosition.Position,d.Name));
                else nameMap[d.Name] = d;
            }
        }
        void debugLocateOffsetInChunk(long offset)
        {
            foreach (var kv in chunks)
            {
                Chunk c = kv.Value;
                if (c.name == "FORM") continue; // skip this one
                if (offset > c.start && offset < c.end)
                {
                    WriteDebug("Offset: {0} is in Chunk {1}", offset, c.name);
                    return;
                }
            }
            WriteDebug("Offset: {0} not in chunk", offset);
        }
        void DebugFindBetweenOFfsets(int offset)
        {
            debugLocateOffsetInChunk(offset);
            var e = offsetMap.GetEnumerator();
            e.MoveNext();
            var last = e.Current;
            while (e.MoveNext())
            {
                if (offset > last.Key && offset < e.Current.Key)
                {
                    WriteDebug("Not Found but between: {0,-8}", offset);
                    WriteDebug("this({0,-8}): {1}", last.Value.FilePosition.Position, last.ToString());
                    WriteDebug("that({0,-8}): {1}", e.Current.Value.FilePosition.Position, last.ToString());
                    return;
                }
                last = e.Current;
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
            if (o.Parent > -1) if (objMapIndex.TryGetValue(o.Parent, out lookup))
                {
                    WriteDebug("obj_index DUP: " + o + " -> " + lookup);
                }
                else objMapIndex.Add(o.Parent, o);
        }

        Stack<long> savedOffsets = new Stack<long>();


        void CheckAndSetOffset(long offset)
        {
            if (offsetMap.ContainsKey((int)offset))
                WriteDebug("CheckOffset: 0x{0,-8:X8} Exists", offset, offsetMap[(int)offset].GetType().Name);
            r.BaseStream.Position = offset;
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
        string readFixedString(int len)
        {
            byte[] bytes = r.ReadBytes(len);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        string readVarString(int offset) {
            if (offset < 9999) throw new Exception("offset is null");
            GMK_Data d;
            if (offsetMap.TryGetValue(offset, out d)){
                GMK_String s = d as GMK_String;
                if (s == null) throw new Exception(d.ToString() + " NOT A STRING");
                return s.str;
            }
            throw new Exception("STRING NOT FOUND");
        }

        void DoCode(int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach(ChunkEntry e in entries)
            {
                GMK_Code code = new GMK_Code(e);
                code.Name = readVarString(r.ReadInt32());
                int code_size = r.ReadInt32();
                code.code = r.ReadBytes(code_size);
                codeList.Add(code);
                AddData(code);
            }
        }
        void DoBackground(int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit); ; // only 14 bytes?
            foreach (ChunkEntry e in entries)
            {
                GMK_Background back = new GMK_Background(e);
                back.Name = readVarString(r.ReadInt32());
                back.stuff = r.ReadInt32(3);
                int sprite_pos = r.ReadInt32();
                back.pos = spritePos[sprite_pos];
                AddData(back);
                backgroundList.Add(back);
            }
        }
        class RoomTest : GMK_Data
        {
            public class RoomBackground
            {
                public int enabled;
            //   public int visible;
                public int foreground;
                public int index;
                public int x;
                public int y;
                public int tileX;
                public int tileY;
                public int[] more;
            }
            public class RoomTiles
            {
                public int enabled;
                //   public int visible;
                public int foreground;
                public int index;
                public int x;
                public int y;
                public int tileX;
                public int tileY;
                public int[] more;
            }
            public class GameObjects // fixed size
            {
                public int x;
                public int y;
                public int index;
                public int compiledIndex;
                public float scaleX;
                public float scaleY;
                public float tint;
                public int rotation;
                public GameObjects(BinaryReader r) // 9 ints, 36 bytes
                {
                    int[] test = new int[9];
                    for (int i = 0; i < 9; i++) test[0] = r.ReadInt32();
                    x = r.ReadInt32();
                    y = r.ReadInt32();
                    index = r.ReadInt32();
                    compiledIndex = r.ReadInt32();
                    scaleX = r.ReadSingle();
                    scaleY = r.ReadSingle();
                    tint = r.ReadSingle();
                    rotation = r.ReadInt32();
                    int extra = r.ReadInt32();
                }
            }
            public RoomTest(ChunkEntry e) : base(e) { }
            public string caption;
            public int width;
            public int height;
            public int speed;
            public int persistent;
            public int colour;
            public int showColour;
            public int compiledIndex;
            public int flag0;
            public bool enableViews {  get { return ((flag0) & 1) != 0; } }
            public bool viewClearScreen { get { return ((flag0) & 2) != 0; } }
            public bool clearDisplayBuffer { get { return ((flag0) & 4) != 0; } }
            public int[] offsetsToData;  // So this is the bugger, these are offsets to data
            // offsetsToData[0]
            public int physicsWorldId;
            public int physicsWorldTop;   // this was all from hacking the compiler so not sure how true it is
            public int physicsWorldLeft;  // the values above I have tested with game maker and mostly true
            public int physicsWorldRight;
            public int physicsWorldBottom;
            public int physicsWorldGravityX;
            public int physicsWorldGravityY;
            public int physicsWorldPixToMeters;
            public List<RoomBackground> backgrounds = new List<RoomBackground>();
            public List<RoomBackground> tiles = new List<RoomBackground>();
            public List<GameObjects> objects = new List<GameObjects>();
        }
        void DoRoomBackground(RoomTest room, int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r,  chunkStart,  chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                RoomTest.RoomBackground back = new RoomTest.RoomBackground();
                back.enabled = r.ReadInt32();
                // back.visible = r.ReadInt32(); wrong version?
                back.foreground = r.ReadInt32();
                back.index = r.ReadInt32();
                back.x = r.ReadInt32();
                back.y = r.ReadInt32();
                back.tileX = r.ReadInt32();
                back.tileY = r.ReadInt32();
                room.backgrounds.Add(back);
                room.backgrounds.Add(back);
            }
        }
        void DoRoomViews(RoomTest room, int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r,chunkStart,chunkLimit);
            foreach (ChunkEntry e in entries)
            {
           
            }
        }
        void DoRoomGameObjects(RoomTest room, int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                RoomTest.GameObjects obj = new RoomTest.GameObjects(r);
                room.objects.Add(obj);
            }
        }
        void DoRoomTiles(RoomTest room, int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
              //  RoomTest.GameObjects obj = new RoomTest.GameObjects(r);
              //  room.objects.Add(obj);
            }
        }
        void DoRoom(int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                RoomTest room = new RoomTest(e);
                room.Name = readVarString(r.ReadInt32());
                room.caption = readVarString(r.ReadInt32());
                room.width = r.ReadInt32();
                room.height = r.ReadInt32();
                room.speed = r.ReadInt32();
                room.persistent = r.ReadInt32();
                room.colour = r.ReadInt32();
                room.showColour = r.ReadInt32();
                room.compiledIndex = r.ReadInt32();
                room.flag0 = r.ReadInt32();
                room.offsetsToData = r.ReadInt32(4); // read 4 ints
                room.physicsWorldId = r.ReadInt32();
                room.physicsWorldTop = r.ReadInt32();  // this was all from hacking the compiler so not sure how true it is
                room.physicsWorldLeft = r.ReadInt32();  // the values above I have tested with game maker and mostly true
                room.physicsWorldRight = r.ReadInt32();
                room.physicsWorldBottom = r.ReadInt32();
                room.physicsWorldGravityX = r.ReadInt32(); // these three might be wrong
                room.physicsWorldGravityY = r.ReadInt32();
                room.physicsWorldPixToMeters = r.ReadInt32();
                if(room.Name == "room_ruins1")
                {
                    WriteDebug("room_ruins1 debug");
                }
                // this should be the start of the room.offsetsToData[0] offsets according to tests
                System.Diagnostics.Debug.Assert(room.offsetsToData[0] == r.BaseStream.Position);
                DoRoomBackground(room, room.offsetsToData[0], chunkLimit); 
                DoRoomViews(room, room.offsetsToData[1], chunkLimit);
                DoRoomGameObjects(room, room.offsetsToData[2], chunkLimit);
                DoRoomTiles(room, room.offsetsToData[3], chunkLimit);
                

            }
            entries = null;
        }
     
     
        public List<GMK_Font> resFonts;
        void DoFont(int chunkStart, int chunkLimit)
        {
            resFonts = new List<GMK_Font>();
            ChunkEntries.DebugOutput = false;
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                GMK_Font fnt = new GMK_Font(e);

                fnt.Name = readVarString(r.ReadInt32());
                fnt.description = readVarString(r.ReadInt32());
                fnt.font_size = r.ReadInt32();
                fnt.maybe_Bold = r.ReadInt32() == 1;
                fnt.maybe_Italii = r.ReadInt32() == 1;
                int data = r.ReadInt32();
                int first_char = data & 0xFFFF;
                int char_set = (data >> 16) & 0xFF;
                int antiAlias = (data >> 24) & 0xFF;
                int last = r.ReadInt32();
                int humm = r.ReadInt32();
                if (!spritePos.TryGetValue(humm, out fnt.bitmap)) throw new Exception( "Could not find bitmap");
                // DebugFindBetweenOFfsets(humm); // this is in the tpag area of the stream
               
                 //   public GMK_SpritePosition bitmap;

                fnt.scaleW = r.ReadSingle(); // scales?
                fnt.scaleH = r.ReadSingle(); // scales?
                ChunkEntries charGlyphs = new ChunkEntries(r, chunkLimit);
                foreach (ChunkEntry fe in charGlyphs)
                {
                    GMK_FontGlyph g = new GMK_FontGlyph(fe);
                    g.c = (char)r.ReadInt16();
                    g.x = r.ReadInt16();
                    g.y = r.ReadInt16();
                    g.width = r.ReadInt16();
                    g.height = r.ReadInt16();
                    g.char_offset = r.ReadInt16();
                    fnt.Add(g);
                }
                // OHH check here?
                resFonts.Add(fnt);
                AddData(fnt);
                    
            }
        }



        void DoObject(int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                GMK_Object obj = new GMK_Object(e);

                obj.Name = readVarString(r.ReadInt32()); // oo my god.


                int[] head = r.ReadInt32(19);
                obj.data = head.ToArray();
                //   obj.obj_id = head[0]; // instance varable ooor sprite index? hummmmmm
                obj.sprite_index = head[0];
                System.Diagnostics.Debug.Assert(!(obj.sprite_index == 0 && obj.sprite_index == 1));
                obj.Visible = head[2] != 0;
                System.Diagnostics.Debug.Assert(!(head[2] == 0 && head[2] == 1));
                obj.Solid = head[3] != 0;
                System.Diagnostics.Debug.Assert(!(head[3] == 0 && head[3] == 1));
                obj.isPersistant = head[4] != 0;
                obj.depth = head[5]; // negitive numbers are close to the player
                obj.Parent = head[6];

                obj.Mask = head[6]; // Humm I am thinking objects are varable size
                                    //   obj.PhysicsObject = head[8];
                                    //    obj.PhysicsObjectSensor = head[9];
                                    //   obj.PhysicsObjectShape = head[10];
                                    //   obj.PhysicsObjectDensity = head[11];

                foreach (ChunkEntry alarm in new ChunkEntries(r, chunkLimit, false))
                {
                }




                //  System.Diagnostics.Debug.Assert(obj.name != "obj_froggit");
                objList.Add(obj);
                AddObject(obj);
            }
        }
      
        void doFORM()
        {

        }
       
        void doTXRT(int chunkStart, int chunkLimit)
        {
            // CHECK THIS, we have the textures.  I am sure the file size is in there somewhere

            filesImage = new List<GMK_Image>();
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                GMK_Image gi = new GMK_Image(e);
                int dummy = r.ReadInt32(); // Always 1?  Does this mean its in the file?
                int new_offset = r.ReadInt32();
                gi.image = new Bitmap(r.StreamFromPosition(new_offset));
                AddData(gi);
                filesImage.Add(gi);
            }
        }
        Bitmap readBitmapfromTPNG(GMK_SpritePosition p)
        {
            return null;
        }
        void SaveSpritePng(string filename, GMK_SpritePosition p)
        {
            Bitmap texture = filesImage[p.texture_id].image;
            Bitmap sprite = new Bitmap(p.width, p.height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(sprite);
            Rectangle srcRect = new Rectangle(p.x, p.y, p.width, p.height);
            g.DrawImage(texture, 0, 0, srcRect, GraphicsUnit.Pixel);
            if (filename.IndexOf('.') == -1) filename += ".png";
            sprite.Save(filename);
        }
        // We are doing this enough freaking times that a function helps with it
        GMK_SpritePosition[] readFrames()
        {
            List<GMK_SpritePosition> frames = new List<GMK_SpritePosition>();
            foreach (ChunkEntry frame_offset in new ChunkEntries(r, chunks["TPAG"].end, false)) // don't check range since these are all in TPAG
            {
                GMK_SpritePosition spos = spritePos[frame_offset.Position]; // it should be in here already
                frames.Add(spos);
            }
            return frames.ToArray();
        }
        Bitmap readMask(int width, int height)
        {
            int stride = width * 1;// bpp;  // bits per row
            stride += 16;            // round up to next 32-bit boundary
            stride /= 16;            // DWORDs per row
            stride *= 2;             // bytes per row
            
         //   int size_of_data = stride * height;
         //   byte[] data = r.ReadBytes(size_of_data);
          //  GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
          //  IntPtr pointer = pinnedArray.AddrOfPinnedObject();
            
          //  Bitmap mask = new Bitmap(width, height, stride, PixelFormat.Format1bppIndexed, pointer);
          //  Marshal.FreeHGlobal(pointer);
            Bitmap mask= new Bitmap(width, height, PixelFormat.Format1bppIndexed); 
            BitmapData bdata = mask.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format1bppIndexed) ;
            for (int y = 0; y < height; y++)
            {
                int i = bdata.Stride * y;
                int bytes_to_read = width / 8;
                if ((width % 8) != 0) bytes_to_read++; // need another byte OHHH
                byte[] data = r.ReadBytes(bytes_to_read);
                // So we always read even bytes? we are 2 byte aligned?
                
                for (int x = 0; x < (width / 8); x++) Marshal.WriteByte(bdata.Scan0, i + x, data[x]);
            }
            /*
            for (int y = 0; y < height; y++)
            {
                int i = bdata.Stride * y;
                for (int x = 0; x < (width / 8); x++) Marshal.WriteByte(bdata.Scan0, i + x, r.ReadByte());
            }
            */
            mask.UnlockBits(bdata);
            return mask;
        }
        void DoSprite(int chunkStart,int chunkLimit)
        {
            spriteList = new List<GMK_Sprite>();
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                GMK_Sprite spr = new GMK_Sprite(e); // so 19 bytes
                spr.Name = readVarString(r.ReadInt32());
                spr.width = r.ReadInt32();
                spr.height = r.ReadInt32();
                int flags = r.ReadInt32(); // not just one
                spr.widht0 = r.ReadInt32() ; // size?
                spr.height0 = r.ReadInt32() ; // size?

                int another = r.ReadInt32();
                int[] extra = r.ReadInt32(7);
                spr.frames = readFrames();
                int have_mask = r.ReadInt32();
                if (have_mask != 0) {
                    int mask_width = spr.width;
                    int mask_height = spr.height;
                    spr.mask = readMask(mask_width, mask_height);
                } 
                /*
                if (spr.Name.IndexOf("frog") > -1) // frog  spr_froghead
                {
                    bool oldDebug = this.debugOn;
                    this.debugOn = true;
                  
                  
                    WriteDebug("We have FROG: " + spr.Name);

                    for (int i = 0; i < spr.frames.Length; i++) SaveSpritePng(spr.Name + "_" + i, spr.frames[i]);
                    pr.mask.Save(spr.Name + "_mask.png");
                    this.debugOn = oldDebug;
                }
                */
                // objList.Add(obj);
                AddData(spr);
                spriteList.Add(spr);
            }

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
        void doStrings(int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                int string_size = r.ReadInt32() ; //size 
                GMK_String str = new GMK_String(new ChunkEntry(e.Position + 4, e.Position + 4 + string_size, string_size)); // The strings are looked up by other functions by the offset
                byte[] bstr = r.ReadBytes(string_size);
                str.str = System.Text.Encoding.UTF8.GetString(bstr, 0, string_size);
                str.index = stringList.Count;
                stringList.Add(str);
                AddData(str);
            }
        }
        public List<GMK_ScriptIndex> scriptIndex = new List<GMK_ScriptIndex>();
        public Dictionary<int, GMK_ScriptIndex> scriptMap = new Dictionary<int, GMK_ScriptIndex>();
        void doSCPT(int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                GMK_ScriptIndex scpt = new GMK_ScriptIndex(e);
                System.Diagnostics.Debug.Assert(e.ChunkSize == 8);
                scpt.script_name = readVarString(r.ReadInt32());
                scpt.script_index = r.ReadInt32();
                AddData(scpt);
                scriptIndex.Add(scpt);
            }

        }
        void doGEN8(long chunkStart, long chunkLimit)
        {
//
      //      long size = chunkLimit - chunkStart;
      //      List<int> test2 = readInts((int)(size / 4)); //  lots of weird data here, does this set up the vars?
           // string test = readVarString(r.ReadInt32());
            // special?

        }
        public Dictionary<long, GMK_SpritePosition> spritePos = new Dictionary<long, GMK_SpritePosition>();
        void doTPAG(int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                GMK_SpritePosition p = new GMK_SpritePosition(e);
                System.Diagnostics.Debug.Assert(e.ChunkSize == 22);

                p.x = r.ReadInt16(); // this is the size of the record
                p.y = r.ReadInt16();
                p.width = r.ReadInt16();
                p.height = r.ReadInt16();
                p.renderX = r.ReadInt16();
                p.renderY = r.ReadInt16();
                p.width0 = r.ReadInt16();
                p.height0 = r.ReadInt16();
                p.width1 = r.ReadInt16();
                p.height1 = r.ReadInt16();
                p.texture_id = r.ReadInt16();
                AddData(p);
                spritePos[e.Position] = p;
            }
        }

        // Ok, this is stupid, but I get it
        // All the vars and function names are on a link list from one to another that tie to the name
        // to make this much easyer for the disassembler, we are going to change all these refrences
        // to be string indexes to strlist.  To DO this however, requires us to read the entire code section
        // as a bunch of ints, go though the etnire thing with the refrences, repeat then figure out the code section
        // into seperate scripts blocks.  Meh
        class CodeNameRefrence
        {
            public GMK_String name;
            public int count;
            public int start_ref;
            public int[] offsets;
        }
       void refactorCode_FindAllRefs(SortedDictionary<int,CodeNameRefrence> refs, Chunk codeChunk, Chunk refChunk)
        {
            r.Position = refChunk.start;
            int refCount = refChunk.size / 12;
            // Each ref is 3 ints long, firt is name refrence, second is number of refs, and thrid is the chain
            for (int i = 0; i < refCount; i++)
            {
                CodeNameRefrence nref = new CodeNameRefrence();
                int name_ref = r.ReadInt32();
                GMK_String str = offsetMap[name_ref] as GMK_String;
                if (str == null) throw new Exception("We MUST have a string here or all is lost");
                nref.name = str;
                nref.count = r.ReadInt32();
                nref.start_ref = r.ReadInt32() & 0x00FFFFFF;
                nref.offsets = new int[nref.count];
                r.PushSeek(nref.start_ref);
                for (int j = 0; j < nref.count; j++)
                {
                    int first = r.ReadInt32(); // skip the first pop opcode
                    int position = r.Position;
                    int offset = r.ReadInt32() & 0x00FFFFFF;
                    if (refs.ContainsKey(position)) throw new Exception("This ref was already in there?");
                    else refs[position] = nref;
                    nref.offsets[j] = offset;
                    r.Position += (offset-8);
                  //  r.BaseStream.Seek(looffsetcation - 8L, SeekOrigin.Current); // this is crazy, so its an offset to the NEXT entry?  Gezzz
                }
                r.PopPosition();
            }
        }
        void refactorCode(Chunk codeChunk, Chunk funcChunk, Chunk varChunk) 
        {
            // first the easy bit, getting all of the code start
            
            // Functions first
            int funcSize = funcChunk.size / 12;
            SortedDictionary<int, CodeNameRefrence> refs = new SortedDictionary<int, CodeNameRefrence>();
            refactorCode_FindAllRefs(refs, codeChunk,funcChunk);
            refactorCode_FindAllRefs(refs, codeChunk,varChunk);

            ChunkEntries entries = new ChunkEntries(r, codeChunk.start, codeChunk.end);
            foreach (ChunkEntry e in entries)
            {
                GMK_Code code = new GMK_Code(e);
                code.Name = readVarString(r.ReadInt32());
                int code_size = r.ReadInt32();
                int startPosition = r.Position;
                code.code = r.ReadBytes(code_size);
                for(int i=0; i< code_size;i+=4, startPosition+=4)
                {
                    CodeNameRefrence name_ref;
                    if (refs.TryGetValue(startPosition, out name_ref))
                    {
                        code.code[i] = (byte)((name_ref.name.index) & 0xFF);
                        code.code[i + 1] = (byte)((name_ref.name.index >> 8) & 0xFF);
                        code.code[i + 2] = (byte)((name_ref.name.index >> 16) & 0xFF);
                    }
                }
                codeList.Add(code);
                AddData(code);
            }
        }


        class Chunk
        {
            public readonly int start;
            public readonly int end;
            public readonly int size;
            public readonly string name;
            public Chunk(string name, int start, int size) { this.name = name; this.start = start; this.end = start + size; this.size = size; }
        }
        Dictionary<string, Chunk> chunks = new Dictionary<string, Chunk>();

        public void runChunkReader()
        {
            Chunk chunk = null;
            int full_size = r.Length;
            while (r.BaseStream.Position < full_size)
            {
                string chunkName = readFixedString(4);
                int chunkSize = r.ReadInt32();
                int chuckStart = r.Position;
                chunk = new Chunk(chunkName, chuckStart, chunkSize);
                chunks[chunkName] = chunk;
                if (chunkName == "FORM") full_size = chunkSize; // special case for form
                else r.Position = chuckStart + chunkSize; // make sure we are always starting at the next chunk               
            } // we want to get strings out first so we can easily check if refrences equal them
            if (chunks.TryGetValue("STRG", out chunk))
                doStrings(chunk.start, chunk.end);
            if (chunks.TryGetValue("TXTR", out chunk))
                doTXRT(chunk.start, chunk.end); // doing objects right now
            if (chunks.TryGetValue("TPAG", out chunk))
                doTPAG(chunk.start, chunk.end); // doing objects right now
                                                // These three need to be processed first ofr the others to work
            if (chunks.TryGetValue("BGND", out chunk))
                DoBackground(chunk.start, chunk.end); // doing objects right now
            if (chunks.TryGetValue("SPRT", out chunk))
                DoSprite(chunk.start, chunk.end); // doing objects right now

            this.debugOn = false;
            if (chunks.TryGetValue("OBJT", out chunk))
                DoObject(chunk.start, chunk.end); // doing objects right now

            this.debugOn = true;
            if (chunks.TryGetValue("ROOM", out chunk))
                DoRoom(chunk.start, chunk.end); // doing objects right now
            this.debugOn = false;

            if (chunks.TryGetValue("FONT", out chunk))
                DoFont(chunk.start, chunk.end); // doing objects right now
        
            if (chunks.TryGetValue("SCPT", out chunk))
                doSCPT(chunk.start, chunk.end); // doing objects right now
            if (chunks.TryGetValue("GEN8", out chunk))
                doGEN8(chunk.start, chunk.end); // doing objects right now


            refactorCode(chunks["CODE"], chunks["FUNC"], chunks["VARI"]);
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
            r = new ChunkStream(s);

            runChunkReader();
        }
    }
}