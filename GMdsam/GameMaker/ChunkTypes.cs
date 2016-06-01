using GameMaker.Ast;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameMaker
{
    public  static partial class File
    {

        public interface INamedResrouce
        {
            string Name { get; }
        }
        public interface IGameMakerReader
        {
            void ReadRaw(BinaryReader r);
        }
        public interface IDataResource
        {
            Stream getStream();
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
                if (entries == null) return null;
                if (entries.Length == 0) return new T[0];
                T[] data = new T[entries.Length];
                var pos = r.BaseStream.Position;
                foreach (var e in r.ForEachEntry(entries))
                {
                    T obj = new T();
                    obj.Read(r, e.Index);
                    data[e.Index] = obj;
                }
                r.BaseStream.Position = pos;
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
        public class AudioFile : GameMakerStructure, INamedResrouce, IDataResource
        {
            public string Name { get { return name; } }
            public string name;
            public int audio_type;
            public string extension;
            public string filename;
            public int effects;
            public float volume;
            public float pan;
            public int other;
            public int sound_index;
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
            public Stream Data
            {
                get
                {
                    return getStream();
                }
            }
            public Stream getStream()
            {
                return sound_index >= 0 ? File.rawAudio[sound_index].Data : null;
            }
        }

        // This class is just a simple filler for the serilizatin I use
       public struct Color : SerializerHelper.ISerilizerHelperSimple
        {
            int raw;
            public Color(int v) { this.raw = v; }

            public object CreateHelper()
            {
                return BitConverter.GetBytes(raw);
            }
            public static implicit operator Color(int val) { return new Color(val); }
            public static implicit operator int(Color val) { return val.raw; }
        }
       
        public class Texture : GameMakerStructure, IDataResource
        {
            [NonSerialized]
            int _pngLength;
            [NonSerialized]
            int _pngOffset;
            // I could read a bitmap here like I did in my other library however
            // monogame dosn't use Bitmaps, neither does unity, so best just to make a sub stream
            static readonly byte[] pngSigBytes = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
            static readonly string pngSig = System.Text.Encoding.UTF8.GetString(pngSigBytes);

            public Stream getStream()
            {
                return new MemoryStream(File.rawData, _pngOffset, _pngLength, false, false);
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
      
       
      
    
        public class SpriteFrame : GameMakerStructure
        {
            public short X;
            public short Y;
            public short Width;
            public short Height;
            public short Offset_X;
            public short Offset_Y;
            public short Crop_Width;
            public short Crop_Height;
            public short Original_Width;
            public short Original_Height;
            public short Texture_Index;

            protected override void InternalRead(BinaryReader r) {
                X = r.ReadInt16();
                Y = r.ReadInt16();
                Width = r.ReadInt16();
                Height = r.ReadInt16();
                Offset_X = r.ReadInt16();
                Offset_Y = r.ReadInt16();
                Crop_Width = r.ReadInt16();
                Crop_Height = r.ReadInt16();
                Original_Width = r.ReadInt16();
                Original_Height = r.ReadInt16();
                Texture_Index = r.ReadInt16();
            }
        }
        public class Sprite : GameMakerStructure, INamedResrouce
        {
            public string Name { get { return name; } }
            string name;
            public int Width;
            public int Height;
            public int Left;
            public int Right;
            public int Bottom;
            public int Top;
            public bool Transparent;
            public bool Smooth;
            public bool Preload;
            public int Mode;
            public bool ColCheck;
            public int Original_X;
            public int Original_Y;
            public int Type;
            public SpriteFrame[] Frames;
            public int[] MaskOffsets; 
            // lots of data, so we just keep the offsets since we have the raw file

            static List<Image> textureCache;

            public static IReadOnlyList<Image> TextureCache
            {
                get
                {
                    if (textureCache == null) textureCache = File.Textures.Select(t => Bitmap.FromStream(t.getStream())).ToList();
                    return textureCache;
                }
            }
            static Sprite()
            {
                DirectoryInfo info = new DirectoryInfo(".");
                foreach(var f in info.GetFiles("*.png")) f.Delete();
            }
            public Bitmap FrameToBitmap(int index)
            {

                SpriteFrame frame = Frames[index];
                Bitmap sprite = new Bitmap(this.Width, this.Height); //, System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
                using (Graphics g = Graphics.FromImage(sprite))
                {
                    g.DrawImage(TextureCache[frame.Texture_Index], frame.Offset_X, frame.Offset_Y, new Rectangle(frame.X, frame.Y, frame.Width, frame.Height), GraphicsUnit.Pixel);

                }
            //    sprite.Save(this.Name + "_frame_" + index + ".png");
                return sprite;
            }
            IEnumerable<bool> GetPixelData(int index)
            {
                int offset = MaskOffsets[index];
                byte[] data = File.rawData;
                int width = (this.Width + 7) / 8;
                for (int y = 0; y < this.Height; y++)
                {
                    for (int x = 0; x < this.Width; x++)
                    {
                        byte pixel = data[y * width + x / 8 + offset];
                        int bit = (7 - (x & 7) & 31);
                        bool on = ((pixel >> bit) & 1) != 0;
                        yield return on;
                    }
                }
            }
            public Bitmap MaskToBitmap(int index)
            {
                int offset = MaskOffsets[index];
                byte[] data = File.rawData;
                Bitmap test = new Bitmap(this.Width, this.Height); //, System.Drawing.Imaging.PixelFormat.Format1bppIndexed);

                int width = (this.Width + 7) / 8;
                // numArray = new byte[width * this.Height];
                //  int max_x  = Math.Max(0, Math.Min(0, this.Width));

                for (int y = 0; y < this.Height; y++)
                {
                    for (int x = 0; x < this.Width; x++)
                    {
                        byte pixel = data[y * width + x / 8 + offset];
                        int bit = (7 - (x & 7) & 31);
                        bool on = ((pixel >> bit) & 1) != 0;

                        test.SetPixel(x, y, on ? System.Drawing.Color.White : System.Drawing.Color.Black);
                        //     g.
                        //     numArray[i * width + j / 8] = (byte)(num6 | (byte)((stream_0.ReadBool() ? 1 : 0) << (7 - (j & 7) & 31)));
                    }
                }
              //  test.Save(this.Name + "_mask_" + index + ".png");
                return test;
                // I thought of making this indexed, but png saves it quite small, smaller than indexed!
                // Not sure about internaly though
            }
            protected override void InternalRead(BinaryReader r)
            {
                name = string.Intern(r.ReadStringFromNextOffset()); // be sure to intern the name
                Width = r.ReadInt32();
                Height = r.ReadInt32();
                Left = r.ReadInt32();
                Right = r.ReadInt32();
                Bottom = r.ReadInt32();
                Top = r.ReadInt32();
                Transparent = r.ReadIntBool();
                Smooth = r.ReadIntBool();
                Preload = r.ReadIntBool();
                Mode = r.ReadInt32();
                ColCheck = r.ReadIntBool();
                Original_X = r.ReadInt32();
                Original_Y = r.ReadInt32();
                this.Type = 0;
                MaskOffsets = null;
                Frames = ArrayFromOffset<SpriteFrame>(r); // if -1
                if (Frames == null)
                {
                    Debug.Assert(r.ReadInt32() == 1); // never happened humm
                    this.Type = r.ReadInt32();
                }
                else
                { // FINALY FIGURED OUT MASKS
                    int num_masks = r.ReadInt32();
                    MaskOffsets = new int[num_masks];
                    if (num_masks != 0)
                    {
                   
                        int width = (this.Width + 7) / 8;
                        for (int i = 0; i < num_masks; i++)
                        {
                            MaskOffsets[i] = (int)r.BaseStream.Position;
                            r.BaseStream.Position += width * this.Height;
                        }
                    }
                }
            //    if(MaskOffsets != null) for (int i = 0; i < MaskOffsets.Length; i++) MaskToBitmap(i, System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
                if (Frames != null)  for (int i = 0; i < Frames.Length; i++) FrameToBitmap(i);
                int[] offset0 = r.ReadInt32(7);
            }
        }
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
        public class Room : GameMakerStructure, INamedResrouce, SerializerHelper.ISerilizerHelper
        {
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
            public class Background : GameMakerStructure
            {
                public bool Visible;
                public bool Foreground;
                public int Background_Index;
                public int X;
                public int Y;
                public int Tiled_X;
                public int Tiled_Y;
                public int Speed_X;
                public int Speed_Y;
                public bool Stretch;
                protected override void InternalRead(BinaryReader r)
                {
                    Visible = r.ReadIntBool();
                    Foreground = r.ReadIntBool();
                    Background_Index = r.ReadInt32();
                    X = r.ReadInt32();
                    Y = r.ReadInt32();
                    Tiled_X = r.ReadInt32();
                    Tiled_Y = r.ReadInt32();
                    Speed_X = r.ReadInt32();
                    Speed_Y = r.ReadInt32();
                    Stretch = r.ReadIntBool();
                }
             
            };
            public class Instance : GameMakerStructure, SerializerHelper.ISerilizerHelper
            {
                public int X;
                public int Y;
                public int Object_Index;
                public int Id;
                public int Code_Offset;
                public float Scale_X;
                public float Scale_Y;
                public Color Colour;
                public float Rotation;
                protected override void InternalRead(BinaryReader r)
                {
                    X = r.ReadInt32();
                    Y = r.ReadInt32();
                    Object_Index = r.ReadInt32();
                    Id = r.ReadInt32();
                    Code_Offset = r.ReadInt32();
                    Scale_X = r.ReadSingle();
                    Scale_Y = r.ReadSingle();
                    Colour = r.ReadInt32();
                    Rotation = r.ReadSingle();
                }
                public SerializerHelper CreateHelper()
                {
                    SerializerHelper helper = new SerializerHelper(this);
               //     helper.ReplaceFieldsWithObject(o);
                    helper.RemoveField("Code_Offset");
                    if(Code_Offset >= 0) // lets make some code
                    {
                        string code = Writers.AllWriter.QuickCodeToLine(File.Codes[Code_Offset]);
                        helper.AddField("Code_String", code);
                    }
                    return helper;
                }
            };
            public class Tile : GameMakerStructure
            {
                public int X;
                public int Y;
                public int Background_Index;
                public int Offset_X;
                public int Offset_Y;
                public int Width;
                public int Height;
                public int Depth;
                public int Id;
                public float Scale_X;
                public float Scale_Y;
                public Color Blend;
                protected override void InternalRead(BinaryReader r)
                {
                    // I think tiles will change in newer versions, so watch this
                    X = r.ReadInt32();
                    Y = r.ReadInt32();
                    Background_Index = r.ReadInt32();
                    Offset_X = r.ReadInt32();
                    Offset_Y = r.ReadInt32();
                    Width = r.ReadInt32();
                    Height = r.ReadInt32();
                    Depth = r.ReadInt32();
                    Id = r.ReadInt32();
                    Scale_X = r.ReadSingle();
                    Scale_Y = r.ReadSingle();
                    Blend = r.ReadInt32();

                }
            };
            public string Name { get;  set; }
            public string Caption { get;  set; }

            public int Width;
            public int Height;
            public int Speed;
            public bool Persistent;
            public Color Colour;
            public bool Show_colour;
            public int Code_Offset;
            public bool EnableViews;
            public bool ViewClearScreen;
            public bool ClearDisplayBuffer;
            public Background[] Backgrounds;
            public View[] Views;
            public Instance[] Objects;
            public Tile[] Tiles;
            public SerializerHelper CreateHelper()
            {
                SerializerHelper helper = new SerializerHelper(this);
                //     helper.ReplaceFieldsWithObject(o);
                helper.RemoveField("Code_Offset");
                if (Code_Offset >= 0) // lets make some code // lets make some code
                {
                    string code = Writers.AllWriter.QuickCodeToLine(File.Codes[Code_Offset]);
                    helper.AddField("Code_String", code);
                }
                return helper;
            }
            protected override void InternalRead(BinaryReader r)
            {
                Name = string.Intern(r.ReadStringFromNextOffset());
                Caption = r.ReadStringFromNextOffset();
                Width = r.ReadInt32();
                Height = r.ReadInt32();
                Speed = r.ReadInt32();
                Persistent = r.ReadIntBool();
                Colour = r.ReadInt32();
                Show_colour = r.ReadIntBool();
                Code_Offset = r.ReadInt32();
                int flags = r.ReadInt32();
                EnableViews = (flags & 1) != 0;
                ViewClearScreen = (flags & 2) != 0;
                ClearDisplayBuffer = (flags & 14) != 0;

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
        public class Font : GameMakerStructure, INamedResrouce
        {
            public class Glyph : GameMakerStructure
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

        public class RawAudio : GameMakerStructure, IDataResource
        {
            // Don't really need this but have to implment Name in IFileDataResource
            public int Size;
            public Stream Data
            {
                get
                {
                    return getStream();
                }
            }
            // public byte[] RawSound { get; private set; }
            protected override void InternalRead(BinaryReader r)
            {
                Size = r.ReadInt32();
             //   RawSound = r.ReadBytes(Size);
            }

            public Stream getStream()
            {
                return new MemoryStream(File.rawData, this.Position, Size, false, false);
            }
        }
        public class Script : GameMakerStructure, IDataResource, INamedResrouce {
            string name;
            public string Name { get { return name; } }
            public Stream Data
            {
                get
                {
                    return getStream();
                }
            }
            public Code Code {
                get
                {
                    return _scriptIndex == -1 ? null : File.Codes[_scriptIndex];
                }
            }
            int _scriptIndex;
            // public byte[] RawSound { get; private set; }
            protected override void InternalRead(BinaryReader r)
            {
                name = string.Intern(r.ReadStringFromNextOffset());
                _scriptIndex = r.ReadInt32();
            }

            public Stream getStream()
            {
                if (_scriptIndex == -1)
                { // its not in the list but it might still be in the code list
                    foreach (var o in File.Search(Name))
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

        public class Code : GameMakerStructure, IDataResource, INamedResrouce
        {
            public string Name { get; protected set; }
            public int Size { get; protected set; }
            [NonSerialized]
            public ILBlock block = null; // cached
            [NonSerialized]
            protected int codePosition;

            public Stream getStream()
            {
                return new MemoryStream(File.rawData, codePosition, Size, false, false);
            }
            public Stream Data
            {
                get
                {
                    return getStream();
                }
            }
            protected override void InternalRead(BinaryReader r)
            {
                Name = string.Intern(r.ReadStringFromNextOffset());
                Debug.Assert(!Name.Contains("gotobattle"));
                Size = r.ReadInt32();
                codePosition = this.Position + 8;
            }
        }
        public class NewCode : Code
        {
            public short LocalCount { get; private set; }
            public short ArgumentCount { get; private set; }
            [NonSerialized]
            int wierd;
            [NonSerialized]
            int offset;
            public Stream Data
            {
                get
                {
                    return getStream();
                }
            }
            protected override void InternalRead(BinaryReader r)
            {
                Name = string.Intern(r.ReadStringFromNextOffset());
                Size = r.ReadInt32();
                LocalCount = r.ReadInt16();
                ArgumentCount = r.ReadInt16();
                wierd = r.ReadInt32();
                codePosition = (int)r.BaseStream.Position+ wierd-4;
                // this kind of some silly  encryption?
                offset = r.ReadInt32();
            }
        }
    }
}