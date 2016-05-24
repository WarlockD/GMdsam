using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GameMaker
{
    public partial class File
    {
        public interface INamedResrouce
        {
            string Name { get; }
        }
        public interface IGameMakerReader
        {
            void ReadRaw(BinaryReader r);
        }
        public interface IFileDataResource
        {
            Stream Data { get; }
        }
        public interface IIndexable
        {
            int Index { get; }
        }
        [Serializable]
        public abstract class GameMakerStructure : IEquatable<GameMakerStructure>, IIndexable//: ISerializable
        {
            [NonSerialized]
            int position;
            [NonSerialized]
            protected int? index;
            public int Position { get { return position; } }
            public int Index {  get { return index == null ? -100 : (int)index; } }
            public bool hasIndex {  get { return index != null;  } }
            protected abstract void InternalRead(BinaryReader r);
            public void ReadFromDataWin(BinaryReader r, int index)
            {
                this.index = index;
                position = (int) r.BaseStream.Position; // used for equality
                InternalRead(r);
            }
            public void ReadFromDataWin(BinaryReader r)
            {
                this.index = null;
                position = (int) r.BaseStream.Position; // used for equality
                InternalRead(r);
            }
            public static T ReadStructure<T>(BinaryReader r, int? index = null) where T : GameMakerStructure, new()
            {
                T o = new T();
                o.InternalRead(r);
                o.index = index;
                return o;
            }
            public static T[] ArrayFromOffset<T>(BinaryReader r, int offset) where T : GameMakerStructure, new()
            {
                var pos = r.BaseStream.Position;
                r.BaseStream.Position = offset;
                T[] ret = ArrayFromOffset<T>(r);
                r.BaseStream.Position = pos;
                return ret;
            }
            public static T[] ArrayFromOffset<T>(BinaryReader r) where T : GameMakerStructure, new()
            {
                var entries = r.ReadChunkEntries();
                if (entries.Length == 0) return new T[0];
                T[] data = new T[entries.Length];
                foreach (var e in r.ForEachEntry(entries))
                {
                    T obj = new T();
                    obj.Read(r, e.Index);
                    data[e.Index] = obj;
                }
                return data;
            }
            public void Read(BinaryReader r, int index)
            {
                this.index = index;
                this.position = (int) r.BaseStream.Position;
                InternalRead(r);
            }
            public override int GetHashCode()
            {
                return position;
            }
            public bool Equals(GameMakerStructure other)
            {
                return other.Position == Position;
            }
            public override string ToString()
            {
                INamedResrouce ns = this as INamedResrouce;
                return ns != null ? ns.Name : this.GetType().Name;
            }
            /*
            public abstract void GetObjectData(SerializationInfo info, StreamingContext context);
            // The special constructor is used to deserialize values.
            public GameMakerStructure(SerializationInfo info, StreamingContext context){ }
            public GameMakerStructure() { }
            */
        }
        [Serializable]
        public class AudioFile : GameMakerStructure, INamedResrouce, IFileDataResource
        {
            string name;
            int audio_type;
            string extension;
            string filename;
            int effects;
            float volume;
            float pan;
            int other;
            int sound_index;
  
            public string Name { get { return name; } }
            public int AudioType { get { return audio_type; } }
            public string Extension { get { return extension; } }
            public string FileName { get { return filename; } }
            public int Effects { get { return effects; } }
            public float Volume { get { return volume; } }
            public float Pan { get { return pan; } }
            public int Other { get { return other; } }
            public int SoundIndex { get { return sound_index; } }
            public Stream Data { get { return sound_index >= 0 ? File.rawAudio[SoundIndex].Data : null; } }

            protected override void InternalRead(BinaryReader r)
            {
                name = string.Intern(r.ReadStringFromNextOffset()); // be sure to intern the name
                audio_type = r.ReadInt32();
                // Ok, 101 seems to be wave files in the win files eveything else is mabye exxternal?
                // 100 is mp3 ogg?
                // found no other types in there.  
                // Debug.Assert(audio.audioType == 101 || audio.audioType == 100);
                extension = r.ReadStringFromNextOffset();
                filename = r.ReadStringFromNextOffset();
                effects = r.ReadInt32();
                volume = r.ReadSingle();
                pan = r.ReadSingle();
                other = r.ReadInt32();
                sound_index = r.ReadInt32();
            }
            /*
            public GameMakerStructure(SerializationInfo info, StreamingContext context) { }
            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("")
            }
            */
        }
       
       
        public class Texture : GameMakerStructure, IFileDataResource
        {
            int _pngLength;
            int _pngOffset;
            // I could read a bitmap here like I did in my other library however
            // monogame dosn't use Bitmaps, neither does unity, so best just to make a sub stream
            static readonly byte[] pngSigBytes = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
            static readonly string pngSig = System.Text.Encoding.UTF8.GetString(pngSigBytes);
            public Stream Data
            {
                get
                {
                    return new MemoryStream(File.rawData, _pngOffset, _pngLength, false, false);
                }
                
            }
           protected override void InternalRead(BinaryReader r)
            {
                int dummy = r.ReadInt32(); // Always 1
                _pngOffset = r.ReadInt32(); // offset to texture
                r.BaseStream.Position = _pngOffset;
                string sig = r.ReadFixedString(8);

                if (sig != pngSig) throw new Exception("Texture not a png");
                // to get the texture lengh, we have to read all the chunks and add them together
                // once this is done, we can create a proper stream
                // We are doing it this way so we can read the entire stream just once for whatever
                // api needs it or to return a memory stream with a proper length

                _pngLength = 0;
                int length;
                string chunk;
                do
                {
                    length = r.ReadBigInt32();
                    chunk = r.ReadFixedString(4);
                    if (length < 0) throw new Exception("Ugh, have to fix this");
                    r.BaseStream.Position += length + 4; // plus the CRC
                } while (chunk != "IEND");
                _pngLength = (int)r.BaseStream.Position - Position;
            }
        }
      
        public abstract class LuaObjectBuilder
        {
            public string TableName = null;
            public bool ProcessKeyLikeString = false;
            public LuaObjectBuilder Parent = null;
            public static string ValueToString(char v)
            {
                switch (v)
                {
                    case '\a': return "\\a";
                    case '\n': return "\\n";
                    case '\r': return "\\r";
                    case '\t': return "\\t";
                    case '\v': return "\\v";
                    case '\\': return "\\\\";
                    case '\"': return "\\\"";
                    case '\'': return "\\\'";
                    //  case '[': return "\\[";
                    //   case ']': return "\\]";
                    default:
                        if (char.IsControl(v)) return string.Format("\\{0}", (byte)v);
                        else return v.ToString();
                }
            }
            public static string ValueToString(string v)
            {
                StringBuilder sb = new StringBuilder();
                AppendValue(sb, v);
                return sb.ToString();
            }
            public static string ValueToString(LuaObjectBuilder v)
            {
                StringBuilder sb = new StringBuilder();
                AppendValue(sb, v);
                return sb.ToString();
            }
            public static void AppendValue(StringBuilder sb, char v)
            {
                sb.Append(ValueToString(v));
            }
            public static void AppendValue(TextWriter wr, char v)
            {
                wr.Write(ValueToString(v));
            }
            public static void AppendValue(StringBuilder sb, string v)
            {
                sb.Append('"');
                foreach (var c in v) AppendValue(sb, c);
                sb.Append('"');
            }
            public static void AppendValue(TextWriter wr, string v)
            {
                wr.Write('"');
                foreach (var c in v) AppendValue(wr, c);
                wr.Write('"');
            }
            public static void AppendValue(StringBuilder sb, LuaObjectBuilder v)
            {
                v.ToStringBuilder(sb);
            }
            public static void AppendValue(TextWriter wr, LuaObjectBuilder v)
            {
                v.ToStringBuilder(wr);
            }
            public static void AppendValue(StringBuilder sb, bool v)
            {
                sb.Append(v ? "true" : "false");
            }
            public static void AppendValue(TextWriter wr, bool v)
            {
                wr.Write(v ? "true" : "false");
            }
            public static void AppendValue(StringBuilder sb, object v)
            {
                if (v is LuaObjectBuilder) AppendValue(sb, v as LuaObjectBuilder);
                else if (v is string) AppendValue(sb, v as string);
                else if (v is char)
                {
                    sb.Append('"');
                    AppendValue(sb, (char)v);
                    sb.Append('"');
                }
                else if (v is bool) AppendValue(sb, (bool)v);
                else sb.Append(v.ToString());
            }
            public static void AppendValue(TextWriter wr, object v)
            {
                if (v is LuaObjectBuilder) AppendValue(wr, v as LuaObjectBuilder);
                else if (v is string) AppendValue(wr, v as string);
                else if (v is char)
                {
                    wr.Write('"');
                    AppendValue(wr, (char)v);
                    wr.Write('"');
                }
                else if (v is bool) AppendValue(wr, (bool)v);
                else wr.Write(v.ToString());
            }


            public void AppendSingleEntry(TextWriter wr, KeyValuePair<string, object> v)
            {
                if (ProcessKeyLikeString)
                {
                    wr.Write('[');
                    AppendValue(wr, v.Key);
                    wr.Write(']');
                }
                else wr.Write(v.Key);
                wr.Write(" = ");
                AppendValue(wr, v.Value);
            }
            public void AppendSingleEntry(StringBuilder sb, KeyValuePair<string, object> v)
            {
                if (ProcessKeyLikeString)
                {
                    sb.Append('[');
                    AppendValue(sb, v.Key);
                    sb.Append(']');
                }
                else sb.Append(v.Key);
                sb.Append(" = ");
                AppendValue(sb, v.Value);
            }
            public abstract void ToStringBuilder(StringBuilder sb);
            public abstract void ToStringBuilder(TextWriter wr);

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                ToStringBuilder(sb);
                return sb.ToString();
            }
        }
        public interface ILuaObject
        {
            LuaObjectBuilder ToLuaStructure();
        }
        public class LuaArrayBuilder : LuaObjectBuilder
        {
            List<object> list = new List<object>();
            public bool StartAtOne = false;
            public void Add<T>(T value)
            {
                ILuaObject o = value as ILuaObject;
                if (o != null)
                    list.Add(o.ToLuaStructure());
                else
                    list.Add(value);
            }
            public override void ToStringBuilder(TextWriter wr)
            {
                IndentedTextWriter iwr = wr as IndentedTextWriter;
                if (iwr == null) iwr = new IndentedTextWriter(wr);
                iwr.Write('{');
                // we know we have atleast one
                if (list.Count > 0)
                {
                    iwr.Indent++;
                    if (list[0] is LuaObjectBuilder) iwr.WriteLine();

                    AppendValue(iwr, list[0]);
                    for (int i = 1; i < list.Count; i++)
                    {
                        iwr.Write(", ");
                        if (list[i] is LuaObjectBuilder) iwr.WriteLine();
                        AppendValue(iwr, list[i]);
                    }
                    iwr.Write(' ');
                    iwr.Indent--;
                }
                iwr.Write("}");
            }
            public override void ToStringBuilder(StringBuilder sb)
            {
                sb.Append("{");
                // we know we have atleast one
                if (list.Count > 0)
                {
                    sb.Append(' ');
                    AppendValue(sb, list[0]);
                    for (int i = 1; i < list.Count; i++)
                    {
                        sb.Append(", ");
                        AppendValue(sb, list[i]);
                    }
                    sb.Append(' ');
                }
                sb.Append("}");
            }
        }
        public class LuaTableBuilder : LuaObjectBuilder
        {
            List<KeyValuePair<string, object>> Values = new List<KeyValuePair<string, object>>();

            public void Add<T>(string name, T value)
            {
                ILuaObject o = value as ILuaObject;
                if (o != null)
                    Values.Add(new KeyValuePair<string, object>(name, o.ToLuaStructure()));
                else
                    Values.Add(new KeyValuePair<string, object>(name, value));
            }
            public override void ToStringBuilder(TextWriter wr)
            {
                IndentedTextWriter iwr = wr as IndentedTextWriter;
                if (iwr == null) iwr = new IndentedTextWriter(wr);
                iwr.Write('{');
                // we know we have atleast one
                if (Values.Count > 0)
                {
                    iwr.Indent++;
                    if (Values[0].Value is LuaObjectBuilder) iwr.WriteLine();
                    AppendSingleEntry(iwr, Values[0]);
                    for (int i = 1; i < Values.Count; i++)
                    {
                        iwr.Write(", ");
                        if (Values[i].Value is LuaObjectBuilder) iwr.WriteLine();
                        AppendSingleEntry(iwr, Values[i]);
                    }
                    iwr.Indent--;
                    iwr.Write(' ');
                }
                iwr.Write("}");
            }

            public override void ToStringBuilder(StringBuilder sb)
            {
                sb.Append("{");
                if (Values.Count > 0)
                {
                    // we know we have atleast one
                    AppendSingleEntry(sb, Values[0]);
                    for (int i = 1; i < Values.Count; i++)
                    {
                        sb.Append(',');
                        AppendSingleEntry(sb, Values[i]);
                    }
                    sb.Append(' ');
                }
                sb.Append("}");
            }
        }
        [Serializable()]
        public class SpriteFrame : GameMakerStructure, ILuaObject
        {
            public short X { get; private set; }
            public short Y { get; private set; }
            public short Width { get; private set; }
            public short Height { get; private set; }
            public short OffsetX { get; private set; }
            public short OffsetY { get; private set; }
            public short CropWidth { get; private set; }
            public short CropHeight { get; private set; }
            public short OriginalWidth { get; private set; }
            public short OriginalHeight { get; private set; }
            public short TextureIndex { get; private set; }

            public LuaObjectBuilder ToLuaStructure()
            {
                LuaTableBuilder b = new LuaTableBuilder();
                b.Add("x", X);
                b.Add("y", Y);
                b.Add("width", Width);
                b.Add("height", Height);
                b.Add("offsetx", OffsetX);
                b.Add("offsety", OffsetY);
                b.Add("crop_width", CropWidth);
                b.Add("crop_height", CropHeight);
                b.Add("original_width", OriginalWidth);
                b.Add("original_height", OriginalHeight);
                b.Add("texture", TextureIndex);
                return b;
            }
            public override string ToString()
            {
                return ToLuaStructure().ToString();
            }
            protected override void InternalRead(BinaryReader r) {
                X = r.ReadInt16();
                Y = r.ReadInt16();
                Width = r.ReadInt16();
                Height = r.ReadInt16();
                OffsetX = r.ReadInt16();
                OffsetY = r.ReadInt16();
                CropWidth = r.ReadInt16();
                CropHeight = r.ReadInt16();
                OriginalWidth = r.ReadInt16();
                OriginalHeight = r.ReadInt16();
                TextureIndex = r.ReadInt16();
            }
        }
        [Serializable()]
        public class Sprite : GameMakerStructure, INamedResrouce, ILuaObject
        {
            public string Name { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public int Flags { get; private set; }
            public int Width0 { get; private set; }
            public int Height0 { get; private set; }
            public int Another { get; private set; }
            public int[] Extra { get; private set; }
            public SpriteFrame[] Frames { get; private set; }
            public LuaObjectBuilder ToLuaStructure()
            {
                LuaTableBuilder b = new LuaTableBuilder();
                b.Add("index", Index);
                b.Add("name", Name);
                b.Add("width", Width);
                b.Add("height", Height);
                LuaArrayBuilder frameArray = new LuaArrayBuilder();
                foreach (var frame in Frames) frameArray.Add(frame);
                b.Add("frames", frameArray);
                return b;
            }
            public override string ToString()
            {
                return ToLuaStructure().ToString();
            }
      
            protected override void InternalRead(BinaryReader r)
            {
                Name = string.Intern(r.ReadStringFromNextOffset()); // be sure to intern the name
                Width = r.ReadInt32();
                Height = r.ReadInt32();
                Flags = r.ReadInt32();
                Width0 = r.ReadInt32();
                Height0 = r.ReadInt32();
                Another = r.ReadInt32();
                Extra = r.ReadInt32(7);
                Frames = ArrayFromOffset<SpriteFrame>(r);
                // bitmask is here
                int haveMask = r.ReadInt32();
                if (haveMask != 0)
                { // have mask?
                    int stride = (Width % 8) != 0 ? Width + 1 : Width;
                    //	std::vector<uint8_t>* mask = new std::vector<uint8_t>();
                    //	mask->resize(stride * header.height);
                    //	r.read(mask->data(), mask->size());
                    //	_spriteMaskLookup.emplace(std::make_pair(name, mask));
                }
            }

        }
        [Serializable()]
        public class GObject : GameMakerStructure, INamedResrouce
        {
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
                public int ArgumentCount;
                public int Who;
                public bool Relative;
                public bool IsNot;

                public Action(BinaryReader r)
                {
                    LibID = r.ReadInt32();
                    ID = r.ReadInt32(); // address?
                                        //   string test = r.readStringFromOffset(ID);
                    Kind = r.ReadInt32();
                    //  int[] test = r.ReadInt32(6);

                    UseRelative = r.ReadIntBool();
                    IsQuestion = r.ReadIntBool();
                    UseApplyTo = r.ReadIntBool();
                    ExeType = r.ReadInt32();
                    Name = r.ReadStringFromNextOffset();
                    Debug.Assert(Name == "");
                    CodeOffset = r.ReadInt32();
                    ArgumentCount = r.ReadInt32();
                    Who = r.ReadInt32();
                    Relative = r.ReadIntBool();
                    IsNot = r.ReadIntBool();
                    int zero_cause_its_compiled = r.ReadInt32();
                    Debug.Assert(zero_cause_its_compiled == 0);
                }
            }
            public struct PhysicsVert
            {
                public float X;
                public float Y;
            }
            public string Name { get; set; }
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
            public void DebugLuaObject(ITextOutput sw, bool outputRawEvents)
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
            protected override void InternalRead(BinaryReader r)
            {
                Name = string.Intern(r.ReadStringFromNextOffset());
                this.SpriteIndex = r.ReadInt32();
                this.Visible = r.ReadIntBool();
                this.Solid = r.ReadIntBool();
                this.Depth = r.ReadInt32();
                this.Persistent = r.ReadIntBool();
                this.Parent = r.ReadInt32();
                this.Mask = r.ReadInt32();
                this.PhysicsObject = r.ReadIntBool();
                this.PhysicsObjectSensor = r.ReadIntBool();
                this.PhysicsObjectShape = r.ReadInt32();
                this.PhysicsObjectDensity = r.ReadSingle();
                this.PhysicsObjectRestitution = r.ReadSingle();
                this.PhysicsObjectGroup = r.ReadInt32();
                this.PhysicsObjectLinearDamping = r.ReadSingle();
                this.PhysicsObjectAngularDamping = r.ReadSingle();
                int verts = r.ReadInt32();
                this.PhysicsObjectFriction = r.ReadSingle();
                this.PhysicsObjectAwake = r.ReadIntBool(); // this came out as a single with undertale, version diffrences?
                this.PhysicsObjectKinematic = r.ReadIntBool();
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
                    var eventList = r.ReadChunkEntries();
                    if (eventList.Length == 0) continue;
                    List<Event> list = new List<Event>();
                    foreach (var eo in eventList)
                    {
                        Event ev = new Event();
                        ev.SubType = r.ReadInt32();
                        ev.SubTypeName = Context.EventToString(i, ev.SubType);
                        var actionEntries = r.ReadChunkEntries();
                        if (actionEntries.Length == 0) { ev.Actions = new Action[0]; continue; } // shouldn't happen
                        List<Action> actions = new List<Action>();
                        foreach (var ao in r.ForEachEntry(actionEntries))
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
        };
        [Serializable()]
        public class Background : GameMakerStructure, INamedResrouce
        {
            public string Name { get; private set; }
            public bool Trasparent { get; private set; }
            public bool Smooth { get; private set; }
            public bool Preload { get; private set; }
            public SpriteFrame Frame { get; private set; }
            protected override void InternalRead(BinaryReader r)
            {
                Name = string.Intern(r.ReadStringFromNextOffset());
                Trasparent = r.ReadIntBool();
                Smooth = r.ReadIntBool();
                Preload = r.ReadIntBool();
                int offset = r.ReadInt32();
                r.BaseStream.Position = offset;
                Frame = new SpriteFrame();
                Frame.Read(r, -1);
            }
        };
        [Serializable()]
        public class Room : GameMakerStructure, INamedResrouce
        {
            [Serializable()]
            public class View : GameMakerStructure
            {
                public bool Visible;
                public int X;
                public int Y;
                public int Width;
                public int Height;
                public int Port_X;
                public int Port_Y;
                public int Port_Width;
                public int Port_Height;
                public int Border_X;
                public int Border_Y;
                public int Speed_X;
                public int Speed_Y;
                public int ViewIndex;
                protected override void InternalRead(BinaryReader r)
                {
                    Visible = r.ReadIntBool();
                    X = r.ReadInt32();
                    Y = r.ReadInt32();
                    Width = r.ReadInt32();
                    Height = r.ReadInt32();
                    Port_X = r.ReadInt32();
                    Port_Y = r.ReadInt32();
                    Port_Width = r.ReadInt32();
                    Port_Height = r.ReadInt32();
                    Border_X = r.ReadInt32();
                    Border_Y = r.ReadInt32();
                    Speed_X = r.ReadInt32();
                    Speed_Y = r.ReadInt32();
                    index = r.ReadInt32();
                }
            }
            [Serializable()]
            public class Background : GameMakerStructure
            {
                public bool Visible;
                public bool Foreground;
                public int BackgroundIndex;
                public int X;
                public int Y;
                public int TiledX;
                public int TiledY;
                public int SpeedX;
                public int SpeedY;
                public bool Stretch;
                protected override void InternalRead(BinaryReader r)
                {
                    Visible = r.ReadIntBool();
                    Foreground = r.ReadIntBool();
                    BackgroundIndex = r.ReadInt32();
                    X = r.ReadInt32();
                    Y = r.ReadInt32();
                    TiledX = r.ReadInt32();
                    TiledY = r.ReadInt32();
                    SpeedX = r.ReadInt32();
                    SpeedY = r.ReadInt32();
                    Stretch = r.ReadIntBool();
                }
            };
            [Serializable()]
            public class Instance : GameMakerStructure
            {
                public int X;
                public int Y;
                public int ObjectIndex;
                public int Id;
                public int CodeOffset;
                public float Scale_X;
                public float Scale_Y;
                public int Colour;
                public float Rotation;
                protected override void InternalRead(BinaryReader r)
                {
                    X = r.ReadInt32();
                    Y = r.ReadInt32();
                    ObjectIndex = r.ReadInt32();
                    Id = r.ReadInt32();
                    CodeOffset = r.ReadInt32();
                    Scale_X = r.ReadSingle();
                    Scale_Y = r.ReadSingle();
                    Colour = r.ReadInt32();
                    Rotation = r.ReadSingle();
                }
            };
            [Serializable()]
            public class Tile : GameMakerStructure
            {
                public int X;
                public int Y;
                public int BackgroundIndex;
                public int OffsetX;
                public int OffsetY;
                public int Width;
                public int Height;
                public int Depth;
                public int Id;
                public float ScaleX;
                public float ScaleY;
                public int Blend;
                public int Ocupancy;
                protected override void InternalRead(BinaryReader r)
                {
                    X = r.ReadInt32();
                    Y = r.ReadInt32();
                    BackgroundIndex = r.ReadInt32();
                    OffsetX = r.ReadInt32();
                    OffsetY = r.ReadInt32();
                    Width = r.ReadInt32();
                    Height = r.ReadInt32();
                    Depth = r.ReadInt32();
                    Id = r.ReadInt32();
                    ScaleX = r.ReadSingle();
                    ScaleY = r.ReadSingle();
                    int mixed = r.ReadInt32();
                    Blend = mixed & 0x00FFFFFF;
                    Ocupancy = mixed >> 24;
                }
            };
            public string Name { get;  set; }
            public string Caption { get;  set; }

            public int Width;
            public int Height;
            public int Speed;
            public int Persistent;
            public int Colour;
            public int Show_colour;
            public int CodeOffset;
            public int Flags;
            public Background[] Backgrounds;
            public View[] Views;
            public Instance[] Objects;
            public Tile[] Tiles;
            protected override void InternalRead(BinaryReader r)
            {
                Name = string.Intern(r.ReadStringFromNextOffset());
                Caption = r.ReadStringFromNextOffset();
                Width = r.ReadInt32();
                Height = r.ReadInt32();
                Speed = r.ReadInt32();
                Persistent = r.ReadInt32();
                Colour = r.ReadInt32();
                Show_colour = r.ReadInt32();
                CodeOffset = r.ReadInt32();
                Flags = r.ReadInt32();
                int backgroundsOffset = r.ReadInt32();
                int viewsOffset = r.ReadInt32();
                int instancesOffset = r.ReadInt32();
                int tilesOffset = r.ReadInt32();
                Backgrounds = ArrayFromOffset<Background>(r, backgroundsOffset);
                Views = ArrayFromOffset<View>(r, viewsOffset);
                Objects = ArrayFromOffset<Instance>(r, instancesOffset);
                Tiles = ArrayFromOffset<Tile>(r, tilesOffset);
            }
        }
        [Serializable()]
        public class Font : GameMakerStructure, INamedResrouce, ILuaObject
        {
            [Serializable()]
            public class Glyph : GameMakerStructure, ILuaObject
            {
                public short ch;
                public short x;
                public short y;
                public short width;
                public short height;
                public short shift;
                public short offset;
                protected override void InternalRead(BinaryReader r)
                {
                    ch = r.ReadInt16();
                    x = r.ReadInt16();
                    y = r.ReadInt16();
                    width = r.ReadInt16();
                    height = r.ReadInt16();
                    shift = r.ReadInt16();
                    offset = r.ReadInt16();
                }
                public LuaObjectBuilder ToLuaStructure()
                {
                    LuaTableBuilder b = new LuaTableBuilder();
                    b.Add("ch", (char)ch);
                    b.Add("x", x);
                    b.Add("y", y);
                    b.Add("width", width);
                    b.Add("height", height);
                    b.Add("shift", shift);
                    b.Add("offset", offset);
                    return b;
                }
            }
            public string Name { get; private set; }
            public string Description { get; private set; }
            public int Size { get; private set; }
            public bool Bold { get; private set; }
            public bool Italic { get; private set; }
            public char FirstChar { get; private set; }
            public char LastChar { get; private set; }
            public int AntiAlias { get; private set; }
            public int CharSet { get; private set; }

            public SpriteFrame Frame { get; private set; }
            public float ScaleW { get; private set; }
            public float ScaleH { get; private set; }
            public Glyph[] Glyphs { get; private set; }
            public LuaObjectBuilder ToLuaStructure()
            {
                LuaTableBuilder b = new LuaTableBuilder();
                b.Add("name", Name);
                b.Add("description", Description);
                b.Add("size", Size);
                b.Add("bold", Bold);
                b.Add("italic", Italic);
                b.Add("first", FirstChar);
                b.Add("last", LastChar);
                b.Add("anti_alias", AntiAlias);
                b.Add("charset", CharSet);
                b.Add("scalew", ScaleW);
                b.Add("scaleh", ScaleH);
                b.Add("frame", Frame);
                LuaTableBuilder glyphs = new LuaTableBuilder() { ProcessKeyLikeString = true, TableName = "glyphs" };
                foreach (var g in Glyphs) glyphs.Add(((char)g.ch).ToString(), g);
                b.Add("glyphs", glyphs);
                return b;
            }
            protected override void InternalRead(BinaryReader r)
            {
                Name = string.Intern(r.ReadStringFromNextOffset());
                Description = r.ReadStringFromNextOffset();
                Size = r.ReadInt32();
                Bold = r.ReadIntBool();
                Italic = r.ReadIntBool(); ;
                int flag = r.ReadInt32();
                FirstChar = (char)(flag & 0xFFFF);
                CharSet = (flag >> 16) & 0xFF;
                AntiAlias = (flag >> 24) & 0xFF;
                LastChar = (char)r.ReadInt32();
                var pos = r.BaseStream.Position + 4;
                r.BaseStream.Position = r.ReadInt32();
                Frame = new SpriteFrame();
                Frame.Read(r, -1);
                r.BaseStream.Position = pos;
                ScaleW = r.ReadSingle();
                ScaleH = r.ReadSingle();
                Glyphs = ArrayFromOffset<Glyph>(r);
            }


        }

        public class RawAudio : GameMakerStructure, IFileDataResource
        {
            public int Size { get; private set; }
            public Stream Data
            {
                get{
                    return new MemoryStream(File.rawData, this.Position, Size, false, false);
                }
            }
           // public byte[] RawSound { get; private set; }
            protected override void InternalRead(BinaryReader r)
            {
                Size = r.ReadInt32();
             //   RawSound = r.ReadBytes(Size);
            }
        }
        public class Script : GameMakerStructure, IFileDataResource, INamedResrouce {
            public string Name { get; set; }
            public Code Code {
                get
                {
                    return _scriptIndex == -1 ? null : File.Codes[_scriptIndex];
                }
            }
            int _scriptIndex;
            public Stream Data
            {
                get
                {
                    if (_scriptIndex == -1) { // its not in the list but it might still be in the code list
                        foreach(var o in File.Search(Name))
                        {
                            if (o == this) continue; // skip if its this
                            Code c = o as Code;
                            if (c == null) continue; // just looking for code
                            return c.Data;
                        }
                        return null;
                    }
                    else
                        return File.Codes[_scriptIndex].Data;
                }
            }
            // public byte[] RawSound { get; private set; }
            protected override void InternalRead(BinaryReader r)
            {
                Name = string.Intern(r.ReadStringFromNextOffset());
                _scriptIndex = r.ReadInt32();
            }
        }

        public class Code : GameMakerStructure, IFileDataResource, INamedResrouce
        {
            public string Name { get; set; }
            public int Size { get; private set; }
            public Stream Data
            {
                get
                {
                    return new MemoryStream(File.rawData, this.Position + 8, Size, false, false);
                }
            }
            protected override void InternalRead(BinaryReader r)
            {
                Name = string.Intern(r.ReadStringFromNextOffset());
                Debug.Assert(!Name.Contains("gotobattle"));
                Size = r.ReadInt32();
            }
        }
    }
}