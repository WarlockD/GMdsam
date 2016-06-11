using GameMaker.Ast;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

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
        public interface IImageResorce
        {
            Image Image { get; }
        }
        public interface IDataResource
        {
            Stream getStream();
        }
        [DataContract]
        public abstract class GameMakerStructure : IEquatable<GameMakerStructure> //: ISerializable
        {
            int position;
            [DataMember(EmitDefaultValue = false, Order = 0)]
            protected int? index;
            [DataMember(EmitDefaultValue = false, Order = 1)]
            protected string name;
            public int Position {  get { return position; } }
            public int Index { get { return index ?? -1; } }
            public string Name {  get { return name; } }
            protected abstract void InternalRead(BinaryReader r);

            public void ReadFromDataWin(BinaryReader r, int index)
            {
                this.index = index;
                position = (int) r.BaseStream.Position; // used for equality
                InternalRead(r);
            }
            public static T ReadStructure<T>(BinaryReader r) where T : GameMakerStructure, new()
            {
                T o = new T();
                o.InternalRead(r);
                o.index = default(int);
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
        [DataContract]
        public class AudioFile : GameMakerStructure, INamedResrouce, IDataResource
        {
            [DataMember(Order = 10)]
            public int audio_type;
            [DataMember(Order = 11)]
            public string extension;
            [DataMember(Order = 12)]
            public string filename;
            [DataMember(Order = 13)]
            public int effects;
            [DataMember(Order = 14)]
            public float volume;
            [DataMember(Order = 15)]
            public float pan;
            [DataMember(Order = 16)]
            public int other;
            [DataMember(Order = 17)]
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
        [DataContract]
        public class Texture : GameMakerStructure, IDataResource, IImageResorce
        {
            int _pngLength;
            int _pngOffset;
            // I could read a bitmap here like I did in my other library however
            // monogame dosn't use Bitmaps, neither does unity, so best just to make a sub stream
            static readonly byte[] pngSigBytes = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
            static readonly string pngSig = System.Text.Encoding.UTF8.GetString(pngSigBytes);

            Image _cache = null;
            public Image Image
            {
                get
                {
                    if(_cache == null) _cache = Bitmap.FromStream(getStream());
                    return _cache;
                }
            }

            
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



        [DataContract]
        public class SpriteFrame : GameMakerStructure, IImageResorce
        {
            [DataMember(Order = 10)]
            public short X;
            [DataMember(Order = 11)]
            public short Y;
            [DataMember(Order = 12)]
            public short Width;
            [DataMember(Order = 13)]
            public short Height;
            [DataMember(Order = 14)]
            public short Offset_X;
            [DataMember(Order = 15)]
            public short Offset_Y;
            [DataMember(Order = 16)]
            public short Crop_Width;
            [DataMember(Order = 17)]
            public short Crop_Height;
            [DataMember(Order = 18)]
            public short Original_Width;
            [DataMember(Order = 19)]
            public short Original_Height;
            [DataMember(Order = 20)]
            public short Texture_Index;

            Bitmap _cache = null;
            public Image Image
            {
                get
                {
                    if (_cache == null)
                    {
                        Image texture = File.Textures[Texture_Index].Image;
                        _cache = new Bitmap(this.Width, this.Height); //, System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
                        using (Graphics g = Graphics.FromImage(_cache))
                            g.DrawImage(texture, Offset_X, Offset_Y, new Rectangle(X, Y, Width, Height), GraphicsUnit.Pixel);
       
                    }
                    return _cache;
                }
            }
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
        [DataContract]
        public class Sprite : GameMakerStructure, INamedResrouce
        {
            [DataMember(Order = 10)]
            public int Width;
            [DataMember(Order = 11)]
            public int Height;
            [DataMember(Order = 12)]
            public int Left;
            [DataMember(Order = 13)]
            public int Right;
            [DataMember(Order = 14)]
            public int Bottom;
            [DataMember(Order = 15)]
            public int Top;
            [DataMember(Order = 16)]
            public bool Transparent;
            [DataMember(Order = 17)]
            public bool Smooth;
            [DataMember(Order = 18)]
            public bool Preload;
            [DataMember(Order = 19)]
            public int Mode;
            [DataMember(Order = 20)]
            public bool ColCheck;
            [DataMember(Order = 21)]
            public int Original_X;
            [DataMember(Order = 22)]
            public int Original_Y;
            [DataMember(Order = 23)]
            public int Type;
            [DataMember(Order = 24)]
            public bool has_masks;
            [DataMember(Order = 25)]
            public SpriteFrame[] Frames;
           
            public int[] MaskOffsets; 
            // lots of data, so we just keep the offsets since we have the raw file
            public IEnumerable<Image> FrameImages
            {
                get
                {
                    foreach (var f in Frames) yield return f.Image;
                }
            }
            List<Image> _maskCache = null;
            public IReadOnlyList<Image> Masks
            {
                get {
                    if(_maskCache == null)
                    {
                        _maskCache = new List<Image>();
                        if (has_masks)
                        {
                            for (int i = 0; i < MaskOffsets.Length; i++)
                                _maskCache.Add(MaskToBitmap(i));
                        } 
                    }
                    return _maskCache;
                }
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
                    }
                }
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
                        has_masks = true;
                        int width = (this.Width + 7) / 8;
                        for (int i = 0; i < num_masks; i++)
                        {
                            MaskOffsets[i] = (int)r.BaseStream.Position;
                            r.BaseStream.Position += width * this.Height;
                        }
                    } else
                    {
                        has_masks = false;
                    }
                }
             //    if(MaskOffsets != null) for (int i = 0; i < MaskOffsets.Length; i++) MaskToBitmap(i, System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
           //     if (Frames != null)  for (int i = 0; i < Frames.Length; i++) FrameToBitmap(i);
           //     int[] offset0 = r.ReadInt32(7);
            }

        }
        [DataContract]
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
                name = string.Intern(r.ReadStringFromNextOffset());
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
        [DataContract]
        public class Background : GameMakerStructure, INamedResrouce, IImageResorce
        {
            [DataMember(Order = 10)]
            public bool Trasparent;
            [DataMember(Order = 11)]
            public bool Smooth;
            [DataMember(Order = 12)]
            public bool Preload;
            [DataMember(Order = 13)]
            public SpriteFrame Frame;

            public Image Image
            {
                get
                {
                    return Frame.Image;
                }
            }

            
            protected override void InternalRead(BinaryReader r)
            {
                name = string.Intern(r.ReadStringFromNextOffset());
                Trasparent = r.ReadIntBool();
                Smooth = r.ReadIntBool();
                Preload = r.ReadIntBool();
                int offset = r.ReadInt32();
                r.BaseStream.Position = offset;
                Frame = new SpriteFrame();
                Frame.Read(r, -1);
            }
        };
        [DataContract]
        public class Room : GameMakerStructure, INamedResrouce
        {
            [DataContract]
            public class View : GameMakerStructure
            {
                [DataMember(Order = 1)]
                public bool Visible;
                [DataMember(Order = 2)]
                public int X;
                [DataMember(Order = 3)]
                public int Y;
                [DataMember(Order = 4)]
                public int Width;
                [DataMember(Order = 5)]
                public int Height;
                [DataMember(Order = 6)]
                public int Port_X;
                [DataMember(Order = 7)]
                public int Port_Y;
                [DataMember(Order = 8)]
                public int Port_Width;
                [DataMember(Order = 9)]
                public int Port_Height;
                [DataMember(Order = 10)]
                public int Border_X;
                [DataMember(Order = 11)]
                public int Border_Y;
                [DataMember(Order = 12)]
                public int Speed_X;
                [DataMember(Order = 13)]
                public int Speed_Y;
                [DataMember(Order = 14)]
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

            [DataContract]
            public class Background : GameMakerStructure
            {
                [DataMember(Order = 1)]
                public bool Visible;
                [DataMember(Order = 2)]
                public bool Foreground;
                [DataMember(Order = 3)]
                public int Background_Index;
                [DataMember(EmitDefaultValue =false, Order = 4)]
                public string Background_Name {  get { return Background_Index >= 0 ? File.Backgrounds[Background_Index].Name : null; } set { } }
                [DataMember(Order = 5)]
                public int X;
                [DataMember(Order = 6)]
                public int Y;
                [DataMember(Order = 7)]
                public int Tiled_X;
                [DataMember(Order = 8)]
                public int Tiled_Y;
                [DataMember(Order = 9)]
                public int Speed_X;
                [DataMember(Order = 10)]
                public int Speed_Y;
                [DataMember(Order = 11)]
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

            [DataContract]
            public class Instance : GameMakerStructure
            {
                [DataMember(Order = 1)]
                public int X;
                [DataMember(Order = 2)]
                public int Y;
                [DataMember(Order = 3)]
                public int Object_Index;
                [DataMember(EmitDefaultValue = false, Order = 4)]
                public string Object_Name { get { return Object_Index >= 0 ? File.Objects[Object_Index].Name : null; } set { } }
                [DataMember(Order = 5)]
                public int Id;
                [DataMember(Order = 6)]
                public float Scale_X;
                [DataMember(Order = 7)]
                public float Scale_Y;
                [DataMember(Order = 8)]
                public byte[] color;
                [DataMember(Order = 9)]
                public float Rotation;
                [DataMember(EmitDefaultValue = false, Order = 12)]
                public string Room_Code = null;
                public int Code_Offset;
                protected override void InternalRead(BinaryReader r)
                {
                    X = r.ReadInt32();
                    Y = r.ReadInt32();
                    Object_Index = r.ReadInt32();
                    Id = r.ReadInt32();
                    Code_Offset = r.ReadInt32();
                    Scale_X = r.ReadSingle();
                    Scale_Y = r.ReadSingle();
                    color = r.ReadBytes(4);
                    Rotation = r.ReadSingle();
                }
            };
            [DataContract]
            public class Tile : GameMakerStructure
            {
                [DataMember(Order = 1)]
                public int X;
                [DataMember(Order = 2)]
                public int Y;
                [DataMember(Order = 3)]
                public int Background_Index;
                [DataMember(Order = 4)]
                public int Offset_X;
                [DataMember(Order = 5)]
                public int Offset_Y;
                [DataMember(Order = 6)]
                public int Width;
                [DataMember(Order = 7)]
                public int Height;
                [DataMember(Order = 8)]
                public int Depth;
                [DataMember(Order = 9)]
                public int Id;
                [DataMember(Order = 10)]
                public float Scale_X;
                [DataMember(Order = 11)]
                public float Scale_Y;
                [DataMember(Order = 12)]
                public byte[] Blend;
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
                    Blend = r.ReadBytes(4);

                }
            };
            [DataMember(EmitDefaultValue = false, Order = 10)]
            public string caption;
            [DataMember( Order = 11)]
            public int width;
            [DataMember(Order = 12)]
            public int height;
            [DataMember(Order = 13)]
            public int speed;
            [DataMember(Order = 14)]
            public bool persistent;
            [DataMember(Order = 15)]
            public byte[] color;
            [DataMember(Order = 16)]
            public bool show_color;
       
           


            [DataMember(Order = 17)]
            public bool enable_views;
            [DataMember(Order = 18)]
            public bool view_clear_screen;
            [DataMember(Order = 19)]
            public bool clear_display_buffer;
            [DataMember(EmitDefaultValue = false, Order = 20)]
            public Background[] Backgrounds;
            [DataMember(EmitDefaultValue = false, Order = 21)]
            public View[] Views;
            [DataMember(EmitDefaultValue = false, Order = 22)]
            public Instance[] Objects;
            [DataMember(EmitDefaultValue = false, Order = 23)]
            public Tile[] Tiles;
            [DataMember(EmitDefaultValue = false, Order = 24)]
            public string Room_Code = null; // filled in latter when this class is serilized
            public int code_offset;
            protected override void InternalRead(BinaryReader r)
            {
                name = string.Intern(r.ReadStringFromNextOffset());
                caption = r.ReadStringFromNextOffset();
                width = r.ReadInt32();
                height = r.ReadInt32();
                speed = r.ReadInt32();
                persistent = r.ReadIntBool();
                color = r.ReadBytes(4);
                show_color = r.ReadIntBool();
                code_offset = r.ReadInt32();
                int flags = r.ReadInt32();
                enable_views = (flags & 1) != 0;
                view_clear_screen = (flags & 2) != 0;
                clear_display_buffer = (flags & 14) != 0;

                int backgroundsOffset = r.ReadInt32();
                int viewsOffset = r.ReadInt32();
                int instancesOffset = r.ReadInt32();
                int tilesOffset = r.ReadInt32();
                Backgrounds = ArrayFromOffset<Background>(r, backgroundsOffset);
                if (Backgrounds.Length == 0) Backgrounds = null;
                else // check if we have any intresting backgrounds
                {
                    Backgrounds = Backgrounds.Where(x => x.Background_Index != -1).ToArray();
                    if (Backgrounds.Length == 0) Backgrounds = null;
                }
                Views = ArrayFromOffset<View>(r, viewsOffset);
                if (Views.Length == 0) Views = null;
                else { // same with views
                    Views = Views.Where(x => x.Index != -1).ToArray();
                    if (Views.Length == 0) Views = null;
                }
                Objects = ArrayFromOffset<Instance>(r, instancesOffset);
                if (Objects.Length == 0) Objects = null;
                Tiles = ArrayFromOffset<Tile>(r, tilesOffset);
                if (Tiles.Length == 0) Tiles = null;
            }
        }
        [DataContract]
        public class Font : GameMakerStructure, INamedResrouce
        {
            [DataContract]
            public class Glyph : GameMakerStructure
            {
                [DataMember( Order = 10)]
                public short ch;
                [DataMember(Order = 11)]
                public short x;
                [DataMember(Order = 12)]
                public short y;
                [DataMember(Order = 13)]
                public short width;
                [DataMember(Order = 14)]
                public short height;
                [DataMember(Order = 15)]
                public short shift;
                [DataMember(Order = 16)]
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
        
            [DataMember(EmitDefaultValue = false, Order = 10)] public string Description;
            [DataMember(Order = 11)] public int Size;
            [DataMember(Order = 12)]  public bool Bold;
            [DataMember(Order = 13)] public bool Italic;
            [DataMember(Order = 14)] public int FirstChar;
            [DataMember(Order = 15)] public int LastChar;
            [DataMember(Order = 16)]public int AntiAlias;
            [DataMember(Order = 17)]public int CharSet;
            [DataMember(Order = 18)]public SpriteFrame Frame;
            [DataMember(Order = 19)]public float ScaleW;
            [DataMember(Order = 20)]public float ScaleH;
            [DataMember(Order = 21)]public Glyph[] Glyphs;
            protected override void InternalRead(BinaryReader r)
            {
                name = string.Intern(r.ReadStringFromNextOffset());
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
            int _dataStart;
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
                _dataStart = (int) r.BaseStream.Position;
             //   RawSound = r.ReadBytes(Size);
            }

            public Stream getStream()
            {
                return new MemoryStream(File.rawData, _dataStart, Size, false, false);
            }
        }
        public class Script : GameMakerStructure, IDataResource, INamedResrouce
        {
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
            public int Size { get; protected set; }
            [NonSerialized]
            public ILBlock block = null; // cached
            [NonSerialized]
            protected int codePosition;
            [NonSerialized]
            protected ILBlock _block = null; // used 
            
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

            public virtual ILBlock Block
            {
                get
                {
                    if (_block == null && Size != 0)
                    {
                        lock (this) // we lock this object cause we need to compile for it
                        {
                            if (Context.Version != UndertaleVersion.V10000) throw new Exception("Cannot compile old code using new");
                            // Don't need any of that above since we don't do asms anymore
                            var error = new ErrorContext(name);
                            ILBlock block = new ILBlock();
                            block.Body = new Dissasembler.OldByteCodeAst().Build(this, error);
                            block = new ILAstBuilder().Build(block, error);
                            this._block = block;
                        }
                    }
                    return _block;
                }
            }


            protected override void InternalRead(BinaryReader r)
            {
                name = string.Intern(r.ReadStringFromNextOffset());
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
            public override ILBlock Block
            {
                get
                {
                    if (_block == null)
                    {
                        lock (this) // we lock this object cause we need to compile for it
                        {
                            if (Context.Version != UndertaleVersion.V10001) throw new Exception("Cannot compile new code using old");
                            // Don't need any of that above since we don't do asms anymore
                            var error = new ErrorContext(name);
                            ILBlock block = new ILBlock();
                            block.Body = new Dissasembler.NewByteCodeToAst().Build(this, error);
                            block = new ILAstBuilder().Build(block, error);
                            this._block = block;
                        }
                    }
                    return _block;
                }
            }
            protected override void InternalRead(BinaryReader r)
            {
                name = string.Intern(r.ReadStringFromNextOffset());
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