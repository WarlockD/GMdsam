﻿//#define CAB_DECOMPRESS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.IO.Compression;
#if CAB_DECOMPRESS
using Microsoft.Deployment.Compression;
using Microsoft.Deployment.Compression.Cab;
#endif
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Xml;
using System.Diagnostics;
using System.Text.RegularExpressions;
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
        public int startPosition;
        public int size;
        public GMK_Code(ChunkEntry e) : base(e) { }
    }
    public class GMK_Image : GMK_Data
    {
        public string filename {  get { return Name; } set { Name = value; } }
        public Bitmap image;
        public GMK_Image(ChunkEntry e) : base(e) { }
     
    }
    public class GMK_String : GMK_Data
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
    public class GMK_ScriptIndex : GMK_Data, IComparable<GMK_ScriptIndex>
    {
        public string script_name="";
        public int script_index=-1;
        public GMK_ScriptIndex(ChunkEntry e) : base(e) { }
        public override string ToString()
        {
            return "{ script_name = " + script_name + ", scrpit_index = " + script_index + " }" + base.ToString() ;
        }

        public int CompareTo(GMK_ScriptIndex other)
        {
            return script_index.CompareTo(other.script_index);
        }
        public override int GetHashCode()
        {
            return script_index;
        }
    }
    public class GMK_FuncOffset : GMK_Data
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
    public class GMK_Audio : GMK_Data
    {
        public int audioType;
        public string extension;
        public string filename;
        public int effects;
        public float volume;
        public float pan;
        public int other;
        public int maybe_offset;
        public byte[] data = null; // sound data, if it exits
        public GMK_Audio(ChunkEntry e) : base(e) { }
        public void SaveAudio(string path= null)
        {
            if (data == null) return;
            if (path == null) path = filename;
            else path = Path.Combine(path, filename);
            FileStream sr = new FileStream(path, FileMode.Create, FileAccess.Write);
            sr.Write(data, 0, data.Length);
            sr.Close();
        }
        public override string ToString()
        {
            return "{ Audio Data }";
        }
    }

    public class GMK_Object : GMK_Data
    { // HUMM We have 12 allarms!  EACH ALLARM IS A 1 DIMENIONAL ARRAY! WOO!
      //  public int[] header; // first 20 bytes, last byte seems to be a size
      //  public byte[] data;
        public class Event
        {
            public int SubType;
            public string SubTypeName;
            public Action[] Actions;
        }
        public class Action
        {
            public int LibID;
            public int ID;
            public int Kind;
            public bool UseRelative;
            public bool IsQuestion;
            public bool UseApplyTo;
            public int ExeType;
            public string Name;
            public int CodeOffset;
            //    public int ArgumentCount;
            public int Who;
            public bool Relative;
            public bool IsNot;

            public Action(ChunkStream r)
            {
                LibID = r.ReadInt32();
                ID = r.ReadInt32(); // address?
                                    //   string test = r.readStringFromOffset(ID);
                Kind = r.ReadInt32();
                //  int[] test = r.ReadInt32(6);

                UseRelative = r.readIntBool();
                IsQuestion = r.readIntBool();
                UseApplyTo = r.readIntBool();
                ExeType = r.ReadInt32();
                int string_offset = r.ReadInt32();
                Name = r.readCacheString(string_offset);
                Debug.Assert(Name == "");
                CodeOffset = r.ReadInt32();
                int ArgumentCount = r.ReadInt32();

                Who = r.ReadInt32();
                Relative = r.readIntBool();

                IsNot = r.readIntBool();
                int zero_cause_its_compiled = r.ReadInt32();


                Debug.Assert(zero_cause_its_compiled == 0);
            }
        }
        public struct PhysicsVert
        {
            public float X;
            public float Y;
        }
        public int ObjectIndex = -1;
        public int SpriteIndex = 0;
        public bool Solid = false;
        public bool Visible = false;
        public int Depth = 10;
        public bool Persistent = false;
        public int Parent = -1;
        public int Mask = -1;
        public bool PhysicsObject = false;
        public bool PhysicsObjectSensor = false;
        public int PhysicsObjectShape = -1;
        public float PhysicsObjectDensity = 0.0f;
        public float PhysicsObjectRestitution = 0.0f;
        public int PhysicsObjectGroup = -1;
        public float PhysicsObjectLinearDamping = 0.0f;
        public float PhysicsObjectAngularDamping = 0.0f;
        public float PhysicsObjectFriction = 0.0f;
        public bool PhysicsObjectAwake = false;
        public bool PhysicsObjectKinematic = false;
        //  public List<PointF> PhysicsShapeVertices = null;
        public string ParentName = null;
        public string SpriteName = null;
        public PhysicsVert[] PhysicisVertexs;
        public int[] eventOffsets = new int[12]; // hummmm!
        public Event[][] Events;
        public GMK_Object(ChunkEntry e) : base(e) { }

        public GMK_Object(ChunkEntry e, ChunkStream r, int ObjectIndex) : base(e)
        {
            this.ObjectIndex = ObjectIndex;
            this.Name = r.readStringFromOffset();
            this.SpriteIndex = r.ReadInt32();
            this.Visible = r.readIntBool();
            this.Solid = r.readIntBool();
            this.Depth = r.ReadInt32();
            this.Persistent = r.readIntBool();
            this.Parent = r.ReadInt32();
            this.Mask = r.ReadInt32();
            this.PhysicsObject = r.readIntBool();
            this.PhysicsObjectSensor = r.readIntBool();
            this.PhysicsObjectShape = r.ReadInt32();
            this.PhysicsObjectDensity = r.ReadSingle();
            this.PhysicsObjectRestitution = r.ReadSingle();
            this.PhysicsObjectGroup = r.ReadInt32();
            this.PhysicsObjectLinearDamping = r.ReadSingle();
            this.PhysicsObjectAngularDamping = r.ReadSingle();
            int verts = r.ReadInt32();
            this.PhysicsObjectFriction = r.ReadSingle();
            this.PhysicsObjectAwake = r.readIntBool(); // this came out as a single with undertale, version diffrences?
            this.PhysicsObjectKinematic = r.readIntBool();
            if (verts > 0)
            {
                this.PhysicisVertexs = new PhysicsVert[verts];
                for (int i = 0; i < verts; i++) this.PhysicisVertexs[i] = new PhysicsVert() { X = r.ReadSingle(), Y = r.ReadSingle() };
            }
            else this.PhysicisVertexs = new PhysicsVert[0];
            // eventOffsets = r.ReadInt32(12);// the next 12 are the events.  Each event offset has even more information
            // do it manualy
            int count = r.ReadInt32();
            Debug.Assert(count == 12); // should have 12?
            int[] mainEvents = r.ReadInt32(count);
            Events = new Event[12][];
            for (int i = 0; i < mainEvents.Length; i++)
            {
                ChunkEntries eventList = new ChunkEntries(r, mainEvents[i], e.Limit);
                if (eventList.Count == 0) continue;
                List<Event> list = new List<Event>();
                foreach (var eo in eventList)
                {
                    Event ev = new Event();
                    ev.SubType = r.ReadInt32();
                    ev.SubTypeName = GMContext.EventToString(i, ev.SubType);
                    ChunkEntries actionList = new ChunkEntries(r, e.Limit);
                    if (actionList.Count == 0) { ev.Actions = new Action[0]; continue; } // shouldn't happen
                    List<Action> actions = new List<Action>();
                    foreach (var ao in actionList)
                    {
                        Action a = new Action(r);
                        actions.Add(a);
                    }
                    ev.Actions = actions.ToArray();
                    list.Add(ev);
                }
                Events[i] = list.ToArray();
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Name = ");
            sb.AppendFormat("Name = {0,-30}", this.Name);
            if (this.ParentName != null) sb.AppendFormat("  Parrent = {0,-30}", this.ParentName);
            if (this.SpriteName != null) sb.AppendFormat("  Sprite = {0,-30}", this.SpriteName);
            return sb.ToString();
        }
        public void DebugLuaObject(ITextOutput sw,bool outputRawEvents)
        {
            sw.WriteLine("self.index = {0}", ObjectIndex);
            sw.WriteLine("self.name = \"{0}\"", this.Name);
            if (this.Parent >= 0)
            {
                sw.WriteLine("self.parent_index = {0}", this.Parent);
                sw.WriteLine("self.parent_name = \"{0}\"", this.ParentName);
            }
            sw.WriteLine("self.sprite_index = {0}", this.SpriteIndex);
            sw.WriteLine("self.visible = {0}", this.Visible ? "true" : "false");
            sw.WriteLine("self.solid = {0}", this.Solid ? "true" : "false");
            sw.WriteLine("self.persistent = {0}", this.Persistent ? "true" : "false");
            sw.WriteLine("self.depth = {0}", this.Depth);
            if (outputRawEvents)
            {
                for (int i = 0; i < Events.Length; i++)
                {
                    Event[] eo = Events[i];
                    if (eo == null) continue;
                    foreach (var e in eo)
                    {
                        sw.WriteLine("self.Raw_{0} = {{}}", e.SubTypeName);
                        sw.Write("table.insert(self.{0},{");
                        foreach (var a in e.Actions)
                        {
                            sw.Write("LibID = {0},", a.LibID);
                            sw.Write("ID = {0},", a.ID);
                            sw.Write("Kind = {0},", a.Kind);
                            sw.Write("UseRelative = {0},", a.UseRelative ? "true" : "false");
                            sw.Write("IsQuestion = {0},", a.IsQuestion ? "true" : "false");
                            sw.Write("UseApplyTo = {0},", a.UseApplyTo ? "true" : "false");
                            sw.Write("ExeType = {0},", a.ExeType);
                            sw.Write("Name = \"{0}\",", a.Name);
                            sw.Write("CodeOffset = {0},", a.CodeOffset);
                            sw.Write("Who = {0},", a.Who);
                            sw.Write("Relative = {0},", a.Relative ? "true" : "false");
                            sw.Write("IsNot = {0},", a.IsNot ? "true" : "false");
                        }
                        sw.WriteLine("})");
                    }
                }
            }
        }
    }

    public class GMK_Sprite : GMK_Data // mabye this is from object?
    {
        public int width;
        public int height;
        public int widht0;
        public int height0;
        public GMK_SpritePosition[] frames;
        public Bitmap mask;
        public GMK_Sprite(ChunkEntry e) : base(e) { }
    }
    public class GMK_SpritePosition : GMK_Data
    {
        public Rectangle rect;
        public Point offset;
        public Size crop;
        public Size original;
        public short textureId;
        public GMK_SpritePosition(ChunkEntry e) : base(e) { }
        public GMK_SpritePosition(ChunkEntry e,ChunkStream r) : base(e) {
            rect = new Rectangle();
            offset = new Point();
            crop = new Size();
            original = new Size();
            rect.X = r.ReadInt16();
            rect.Y = r.ReadInt16();
            rect.Width = r.ReadInt16();
            rect.Height = r.ReadInt16();
            offset.X = r.ReadInt16();
            offset.Y = r.ReadInt16();
            crop.Width = r.ReadInt16();
            crop.Height = r.ReadInt16();
            original.Width = r.ReadInt16();
            original.Height = r.ReadInt16();
            textureId = r.ReadInt16();
        }
        public override string ToString()
        {
            return String.Format("SpriteData:  Rect = {0}, TextureID = {1} ", rect, textureId);
        }
    }

    public class GMK_Background : GMK_Data
    {
        public int[] stuff; // 3 ints of stuff
        // after this there is an offset to this data
        public GMK_SpritePosition pos;
        public GMK_Background(ChunkEntry e) : base(e) { }
    }
    public class GMK_FontGlyph : GMK_Data
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
    public class GMK_Font : GMK_Data
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
    public class ChunkReader
    {
        class Chunk
        {
            public readonly int start;
            public readonly int end;
            public readonly int size;
            public readonly string name;
            public Chunk(string name, int start, int size) { this.name = name; this.start = start; this.end = start + size; this.size = size; }
        }
        Dictionary<string, Chunk> chunks = new Dictionary<string, Chunk>();
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

      
        public List<GMK_Object> objList = new List<GMK_Object>();
        public Dictionary<int, GMK_Object> objMap = new Dictionary<int, GMK_Object>();
        public Dictionary<int, GMK_Object> objMapId = new Dictionary<int, GMK_Object>();
        public Dictionary<int, GMK_Object> objMapIndex = new Dictionary<int, GMK_Object>();

        public List<GMK_Image> filesImage;
        public List<GMK_Code> codeList = new List<GMK_Code>();
        public List<GMK_Background> backgroundList = new List<GMK_Background>();
        public List<GMK_Sprite> spriteList = new List<GMK_Sprite>();
        public List<GMK_String> stringList = new List<GMK_String>();


        public void DumpAllObjects(string filename)
        {
            StreamWriter sw = new StreamWriter(filename);
            foreach (var o in objList) sw.WriteLine(o.ToString());
            sw.Close();
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
             //   code.code = r.ReadBytes(code_size);
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


        [StructLayout(LayoutKind.Explicit)]
        struct FloatInt
        {
            [FieldOffset(0)]
            public float f;
            [FieldOffset(0)]
            public int i;
            public FloatInt(int i)
            {
                this.f = 0.0f;
                this.i = i;
            }
        }
        // sanity checks on objects
        void AddObject(GMK_Object o)
        {
            AddData(o); // remember addData is used on objects here
            objList.Add(o);
            GMK_Object lookup;
            if (o.Parent > -1) if (objMapIndex.TryGetValue(o.Parent, out lookup))
                {
                    WriteDebug("obj_index DUP: " + o + " -> " + lookup);
                }
                else objMapIndex.Add(o.Parent, o);
        }
        void DoObject(int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            objList = new List<GMK_Object>();
            foreach (ChunkEntry e in entries)
            {
                GMK_Object obj = new GMK_Object(e,r,objList.Count);
                AddObject(obj);
            }
            foreach (GMK_Object o in objList) { // map the names
                if (o.Parent != -100) o.ParentName = objList[o.Parent].Name;
                if(o.SpriteIndex != -1) o.SpriteName = spriteList[o.SpriteIndex].Name;
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
              //  gi.image = new Bitmap(r.StreamFromPosition(new_offset));
                AddData(gi);
                filesImage.Add(gi);
            }
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
#if false
                if (have_mask != 0) {
                    int mask_width = spr.width;
                    int mask_height = spr.height;
                    spr.mask = readMask(mask_width, mask_height);
                } 
#endif
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
        void doStrings(int chunkStart, int chunkLimit)
        {
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                
                int string_size = r.ReadInt32() ; //size 
                GMK_String str = new GMK_String(new ChunkEntry(e.Position + 4, e.Position + 4 + string_size, string_size)); // The strings are looked up by other functions by the offset
                byte[] bstr = r.ReadBytes(string_size);
                str.str = System.Text.Encoding.UTF8.GetString(bstr, 0, string_size);
                r.AddToCache(e.Position, str.str);
                r.AddToCache(e.Position+4, str.str); // in case it asks for either the postion at the string or before it
                str.index = stringList.Count;
                stringList.Add(str);
                AddData(str);
            }
        }
        public List<GMK_ScriptIndex> scriptIndex = new List<GMK_ScriptIndex>();
        public Dictionary<int, GMK_ScriptIndex> scriptMap = new Dictionary<int, GMK_ScriptIndex>();
        public enum CodeDataType
        {
            Unkonwn,
            Script,
            Create,
            Draw,
            Step,
            Alarm,
            Other,
            Collision,
            Keyboard,
            Destroy,
            Mouse,
            KeyPress,
        }

        public struct CodeData
        {
            public string Name;
            public CodeDataType Type;
            public int TypeIndex;
            public BinaryReader stream;
        }
        public IEnumerable<CodeData> GetCodeStreams()
        {
            foreach (GMK_Code c in codeList)
            {
                ChunkStream ms = getReturnStream();
                yield return new CodeData() { Name = c.Name, stream = new BinaryReader(new OffsetStream(ms.BaseStream, c.startPosition, c.size)) };
            }
        }
        public CodeData GetCodeStreamAtIndex(int index)
        {
            GMK_Code c = codeList[index];
            ChunkStream ms = getReturnStream();
            return new CodeData() { Name = c.Name, stream = new BinaryReader(new OffsetStream(ms.BaseStream, c.startPosition, c.size)) };
        }
        public IEnumerable<CodeData> GetCodeStreams(string search)
        {
            List<CodeData> list;
            if (search.IndexOf("obj_") == 0) list= GetObjectCode(search).ToList();
            else
            {
                ChunkStream ms = getReturnStream();
                list = codeList.Where(x => x.Name.Contains(search)).Select(x => new CodeData() { Name = x.Name, stream = new BinaryReader(new OffsetStream(ms.BaseStream, x.startPosition, x.size)) }).ToList();
            }
            return list;
        }
        // silly wrapper
        public IEnumerable<CodeData> GetAllScripts()
        {
            return GetCodeStreams("gml_Script");
        }
        public class ObjectCodeReturn
        {
            public List<CodeData> Streams;
            public string ObjectName;
            public GMK_Object Obj = null;
        }
     
        public bool ObjectNameMatch(string[] sname, string[] objsplit)
        {
            int j = 3;
            for (int i = 0; i < sname.Length; i++, j++)
                if (sname[i] != objsplit[j]) return false;
            // check to see if we are at the end of object split
            j += 2;
            if (objsplit.Length != j)
                return false;
            return true;
        }
        public IEnumerable<CodeData> GetObjectCode(string objectName)
        {
            int index = objectName.IndexOf("obj_");
            if (index == 0) objectName = objectName.Remove(0, "obj_".Length);
            string[] sname = objectName.Split('_');
            foreach (GMK_Code c in codeList.Where(x => x.Name.Contains(objectName)))
            {
                string[] split = c.Name.Split('_');
                if (!ObjectNameMatch(sname, split)) continue; // not a name match
                CodeData data = new CodeData() { Name = c.Name };
                switch (split[split.Length-2])
                {
                    case "Create": data.Type = CodeDataType.Create; break;
                    case "Other": data.Type = CodeDataType.Other; break;
                    case "Alarm": data.Type = CodeDataType.Alarm; break;
                    case "Step": data.Type = CodeDataType.Step; break;
                    case "Draw": data.Type = CodeDataType.Draw; break;
                    case "KeyPress":
                        data.Type = CodeDataType.KeyPress; break;
                    case "Collision": data.Type = CodeDataType.Collision; break;
                    case "Destroy": data.Type = CodeDataType.Destroy; break;
                    case "Keyboard": data.Type = CodeDataType.Keyboard; break;
                    case "Mouse": data.Type = CodeDataType.Mouse; break;
                    default:
                        data.Type = CodeDataType.Unkonwn;
                        break;
                }
                data.TypeIndex = int.Parse(split.Last());
                ChunkStream ms = getReturnStream();
                // we need to make it a binary stream
                ms.Position = c.startPosition;
                byte[] bdata = ms.ReadBytes(c.size);
              
                data.stream = new BinaryReader(new MemoryStream(bdata));
                yield return data;
            }
        }

        
        public IEnumerable<ObjectCodeReturn> GetAllObjectCode()
        {
            foreach(GMK_Object o in nameMap.Values.OfType<GMK_Object>())
            {
                string name = o.Name;
                ObjectCodeReturn ret = new ObjectCodeReturn();
                ret.Obj = o;
                ret.Streams = GetObjectCode(name).ToList();
                ret.ObjectName = name;
                yield return ret;
            }
        }
      
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
            scriptIndex.Sort();
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
                GMK_SpritePosition p = new GMK_SpritePosition(e,r);
                AddData(p);
                spritePos[e.Position] = p;
            }
        }
        public List<GMK_Audio> audioList;
        void doAudio(int chunkStart, int chunkLimit)
        {
            Chunk rawData = chunks["AUDO"]; // raw data
            ChunkEntries rawDataEntries = new ChunkEntries(r, rawData.start, rawData.end);

            audioList = new List<GMK_Audio>();
            ChunkEntries entries = new ChunkEntries(r, chunkStart, chunkLimit);
            foreach (ChunkEntry e in entries)
            {
                GMK_Audio audio = new GMK_Audio(e);
                audio.Name = r.readStringFromOffset();
                audio.audioType = r.ReadInt32();
                // Ok, 101 seems to be wave files in the win files eveything else is mabye exxternal?
                // 100 is mp3 ogg?
                // found no other types in there.  
               // Debug.Assert(audio.audioType == 101 || audio.audioType == 100);
              
                audio.extension = r.readStringFromOffset();
                audio.filename = r.readStringFromOffset();
                audio.effects = r.ReadInt32();
                audio.volume = r.ReadSingle();
                audio.pan = r.ReadSingle();
                audio.other = r.ReadInt32();
                audio.maybe_offset = r.ReadInt32();
                // Debug.WriteLineIf(audio.audioType == 101, "Audio file : " + audio.filename + " index? " + audio.maybe_offset); // I suspect this is in the audo list
                //Debug.WriteLineIf(audio.audioType == 100, "Audio file : " + audio.filename + " index? " + audio.maybe_offset); // not sure what the offset is for here?
                // this is eating a BIG chunk of memory, don't need it for the decompiler
#if false
                if (audio.audioType == 101)
                {
                    var offset = rawDataEntries[audio.maybe_offset];
                    r.PushSeek(offset.Position);
                    int size = r.ReadInt32();
                    audio.data = r.ReadBytes(size);
                    r.PopPosition();
                }
#endif
           //     audio.SaveAudio();
                AddData(audio);
                audioList.Add(audio);
            }

        }
            string MakePlistPoint(int x, int y)
        {
            return '{' + x + "," + y + '}';
        }
    
      
        // Ok, this is stupid, but I get it
        // All the vars and function names are on a link list from one to another that tie to the name
        // to make this much easyer for the disassembler, we are going to change all these refrences
        // to be string indexes to strlist.  To DO this however, requires us to read the entire code section
        // as a bunch of ints, go though the etnire thing with the refrences, repeat then figure out the code section
        // into seperate scripts blocks.  Meh
        // Also a side note is that object refrences as well as instance creation MUST fit in a 16bit value
        // If thats the case are objects manged by an index or am I reading objects wrong?
        public class CodeNameRefrence
        {
            public GMK_String name;
            public int count;
            public int start_ref;
            public int[] offsets;
        }
        void refactorCode_FindAllRefs_Old(SortedDictionary<int, CodeNameRefrence> refs, Chunk codeChunk, Chunk refChunk)
        {
            r.Position = refChunk.start;
            int refCount = refChunk.size / 12;

         //   int refCount = r.ReadInt32(); // what is this first number? refrence count?
         //   System.Diagnostics.Debug.WriteLine("Reading {0} refrences from {1}", refCount, refChunk.name);
            //  System.Diagnostics.Debug.WriteLine("Reading {0} refrences from {1}", count, refChunk.name);
            // Each ref is 3 ints long, firt is name refrence, second is number of refs, and thrid is the chain
            for (int i = 0; i < refCount; i++)
            {
                CodeNameRefrence nref = new CodeNameRefrence();
                int[] record = r.ReadInt32(3); // still 3 ints
                GMK_String str = offsetMap[record[0]] as GMK_String;
                if (str == null) throw new Exception("We MUST have a string here or all is lost");
                nref.name = str;
                nref.count = record[1];
                nref.start_ref = record[2] & 0x00FFFFFF;
                nref.offsets = new int[nref.count];

                r.PushSeek(nref.start_ref);
                int offset = 0;
                for (int j = 0; j < nref.count; j++)
                {
                    int first = r.ReadInt32(); // skip the first pop opcode
                    int position = r.Position;
                    offset = r.ReadInt32() & 0x00FFFFFF;
                    if (refs.ContainsKey(position)) throw new Exception("This ref was already in there?");
                    else refs[position] = nref;
                    nref.offsets[j] = offset;
                    r.Position += (offset - 8);
                    //  r.BaseStream.Seek(looffsetcation - 8L, SeekOrigin.Current); // this is crazy, so its an offset to the NEXT entry?  Gezzz
                }
                r.PopPosition();
            }
        //    int[] record2 = r.ReadInt32(10); // still 3 ints


        }
        class RefactorWierdCount
        {
            public int[] data;
            public string Name;
            public string ArgumentAlways;
            
        }

        void refactorCode_FindAllRefs(SortedDictionary<int,CodeNameRefrence> refs, Chunk codeChunk, Chunk refChunk)
        {
            r.Position = refChunk.start;
           int totalSize = refChunk.size / 12;
            int[] record = null;
            int refCount = r.ReadInt32(); // what is this first number? refrence count?
            List<CodeNameRefrence> debugrefs = new List<CodeNameRefrence>();
            System.Diagnostics.Debug.WriteLine("Reading {0} refrences from {1}", refCount, refChunk.name);
            //  System.Diagnostics.Debug.WriteLine("Reading {0} refrences from {1}", count, refChunk.name);
            // Each ref is 3 ints long, firt is name refrence, second is number of refs, and thrid is the chain
            for (int i = 0; i < refCount; i++)
            {
                CodeNameRefrence nref = new CodeNameRefrence();
                debugrefs.Add(nref);
                record = r.ReadInt32(3); // still 3 ints
                GMK_String str = offsetMap[record[0]] as GMK_String;
                if (str == null) throw new Exception("We MUST have a string here or all is lost");
                nref.name = str;
                nref.count = record[1];
                nref.start_ref = record[2] & 0x00FFFFFF;
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
             
            int wierd_count = r.ReadInt32();
            List<RefactorWierdCount> wierdStuff = new List<RefactorWierdCount>();
            for (int i = 0; i < wierd_count; i++)
            {
                RefactorWierdCount wierd = new RefactorWierdCount();
                wierdStuff.Add(wierd);
                wierd.data = r.ReadInt32(4); // humm 4 ints now?
                GMK_String str = offsetMap[wierd.data[1]] as GMK_String;
                wierd.Name = str.str;
                str = offsetMap[wierd.data[3]] as GMK_String;
                wierd.ArgumentAlways = str.str;
                System.Diagnostics.Debug.Assert(wierd.data[0] == 1 && wierd.data[2] == 0);
                // first and alst are bools?

            }
        }
        void refactorCode_FindAllRefsVar(SortedDictionary<int, CodeNameRefrence> refs, Chunk codeChunk, Chunk refChunk)
        {
            r.Position = refChunk.start;
            int totalSize = refChunk.size / (5 * sizeof(int));
            int[] record = null;
            int nodeCount = r.ReadInt32(); // what is this first number? refrence count?  version mabye? humm
            int next_count = r.ReadInt32();
            int a_bool = r.ReadInt32();
            List<CodeNameRefrence> debugrefs = new List<CodeNameRefrence>();
      //      System.Diagnostics.Debug.WriteLine("Reading {0} refrences from {1}", refCount, refChunk.name);
            //  System.Diagnostics.Debug.WriteLine("Reading {0} refrences from {1}", count, refChunk.name);
            // Each ref is 3 ints long, firt is name refrence, second is number of refs, and thrid is the chain
            for (int i = 0; i < totalSize; i++)
            {
                
                // FUCK they changed it, its a string index now, its still 3 ints
                record = r.ReadInt32(5);
                GMK_String str = offsetMap[record[0]] as GMK_String;
                int some_negivitve = record[1];
                int unkonwno = record[2];
                int ref_count = record[3];
                int start_offset = record[4];
                if (ref_count >0)
                {
                    CodeNameRefrence nref = new CodeNameRefrence();
                    nref.name = str;
                    nref.start_ref = start_offset;
                    nref.count = ref_count;
                    nref.offsets = new int[ref_count];
                    r.PushSeek(start_offset);
                 //   this.debugOn = true;
                    for (int j=0;j< ref_count;j++)
                    {
                     //   debugLocateOffsetInChunk(start_offset);
                        int first = r.ReadInt32(); // skip the first pop opcode
                        int position = r.Position;
                        int offset = r.ReadInt32() & 0x00FFFFFF;
                        if (refs.ContainsKey(position)) throw new Exception("This ref was already in there?");
                        else refs[position] = nref;
                        nref.offsets[j] = offset;
                        r.Position += (offset - 8);
                    }
                    r.PopPosition();
                }
               
            }
            // Start of the func chunk, so this is correct.   Meh

        }
        public SortedDictionary<int, CodeNameRefrence> codeRefs;
        public Dictionary<string, GMK_Code> codeLookup;
        void refactorCode(Chunk codeChunk, Chunk funcChunk, Chunk varChunk)
        {
            // first the easy bit, getting all of the code start

            // Functions first
            int funcSize = funcChunk.size / 12;
            codeRefs = new SortedDictionary<int, CodeNameRefrence>();
            //   refactorCode_FindAllRefs(codeRefs, codeChunk,funcChunk);
            //    refactorCode_FindAllRefsVar(codeRefs, codeChunk,varChunk);

            refactorCode_FindAllRefs_Old(codeRefs, codeChunk, funcChunk);
            refactorCode_FindAllRefs_Old(codeRefs, codeChunk, varChunk);
            // refactorCode_FindAllRefs_Old(refs, codeChunk, varChunk);

            ChunkEntries entries = new ChunkEntries(r, codeChunk.start, codeChunk.end);
            codeLookup = new Dictionary<string, GMK_Code>();
            foreach (ChunkEntry e in entries)
            {
                GMK_Code code = new GMK_Code(e);
                code.Name = readVarString(r.ReadInt32());
                code.size = r.ReadInt32();
                code.startPosition = r.Position;
                codeList.Add(code);
                AddData(code);
                codeLookup[code.Name] = code;
                
                for (int i = 0; i < code.size; i += 4)
                {
                    CodeNameRefrence name_ref;
                    if (codeRefs.TryGetValue(r.Position, out name_ref))
                    {
                        int startPos = r.Position;
                        byte[] buffer = r.ReadBytes(4);
                        ushort debug0 = BitConverter.ToUInt16(buffer, 0);
                        ushort debug1 = BitConverter.ToUInt16(buffer, 2);
                        buffer[0] = (byte)((name_ref.name.index) & 0xFF);
                        buffer[1] = (byte)((name_ref.name.index >> 8) & 0xFF);
                        buffer[2] = (byte)((name_ref.name.index >> 16) & 0xFF);
                        r.Position = startPos;
                        r.BaseStream.Write(buffer, 0, 4);
                    }
                    else r.Position += 4;
                }
            }

        }


    

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

            if (chunks.TryGetValue("SOND", out chunk))
                doAudio(chunk.start, chunk.end); // doing objects right now
            

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
        byte[] file_data;
        MemoryStream ms;
        public ChunkStream getReturnStream() { return r; }
        public byte[] getFileData() { return file_data; }
        public ChunkReader(string filename, bool debugOn)
        {
            // this.debugOn = debugOn;
            this.debugOn = true;
            Stream s = null;
#if CAB_DECOMPRESS
            if (filename != "data.win" && filename.ToLower().IndexOf(".exe") !=-1){
                CabInfo cab = new CabInfo(filename);
                if (File.Exists("data.win")) File.Delete("data.win");
                cab.UnpackFile("data.win","data.win");  // cab.OpenRead("data.win"); doesn't work for some reason
                filename = "data.win";
            } 
#endif
            s = System.IO.File.Open(filename, FileMode.Open, FileAccess.Read);
            file_data = new byte[s.Length];
            s.Read(file_data, 0, (int)s.Length);
            s.Close();
            s = null;
             ms = new MemoryStream(file_data);
            r = new ChunkStream(ms);

            runChunkReader();
        }

    }
}