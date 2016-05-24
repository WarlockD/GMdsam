﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Reflection;
using System.Collections.Specialized;

namespace GameMaker
{

    class SimplerLuaFormatter : IFormatter
    {
        SerializationBinder binder;
        StreamingContext context;
        ISurrogateSelector surrogateSelector;

        public SimplerLuaFormatter()
        {
            context = new StreamingContext(StreamingContextStates.File);
        }

        public object Deserialize(System.IO.Stream serializationStream)
        {
            StreamReader sr = new StreamReader(serializationStream);

            // Get Type from serialized data.
            string line = sr.ReadLine();
            char[] delim = new char[] { '=' };
            string[] sarr = line.Split(delim);
            string className = sarr[1];
            Type t = Type.GetType(className);

            // Create object of just found type name.
            Object obj = FormatterServices.GetUninitializedObject(t);

            // Get type members.
            MemberInfo[] members = FormatterServices.GetSerializableMembers(obj.GetType(), Context);

            // Create data array for each member.
            object[] data = new object[members.Length];

            // Store serialized variable name -> value pairs.
            StringDictionary sdict = new StringDictionary();
            while (sr.Peek() >= 0)
            {
                line = sr.ReadLine();
                sarr = line.Split(delim);

                // key = variable name, value = variable value.
                sdict[sarr[0].Trim()] = sarr[1].Trim();
            }
            sr.Close();

            // Store for each member its value, converted from string to its type.
            for (int i = 0; i < members.Length; ++i)
            {
                FieldInfo fi = ((FieldInfo) members[i]);
                if (!sdict.ContainsKey(fi.Name))
                    throw new SerializationException("Missing field value : " + fi.Name);
                data[i] = System.Convert.ChangeType(sdict[fi.Name], fi.FieldType);
            }

            // Populate object members with theri values and return object.
            return FormatterServices.PopulateObjectMembers(obj, members, data);
        }
        void SerializeArray(StreamWriter sw, System.Collections.IEnumerable e)
        {
            sw.WriteLine("{ ");
            bool comma = false;
            foreach (var o in e)
            {
                if (comma) { comma = true; sw.Write(", "); }
                SerializePump(sw, e);
            }
            sw.WriteLine(" }");
        }
        void SerializePump(StreamWriter sw, object graph,string table=null)
        {
            MemberInfo[] members = FormatterServices.GetSerializableMembers(graph.GetType(), Context);
            object[] objs = FormatterServices.GetObjectData(graph, members);
            if (table != null) sw.Write("{0} = ", table);
            sw.Write("{ ");
            for (int i = 0; i < objs.Length; ++i)
            {
                sw.Write(members[i].Name);
                sw.Write(" = ");
                SerializeValue(sw,objs[i]);
            }
            sw.WriteLine(" }");
        }
        void SerializeValue(StreamWriter sw, object o)
        {
            switch (Type.GetTypeCode(o.GetType()))
            {
                case TypeCode.String:
                    sw.Write("\"");
                    sw.Write(o.ToString());
                    sw.Write("\"");
                    break;
                case TypeCode.Boolean:
                    sw.Write((bool) o ? "true" : "false");
                    break;
                default: // otherwise check if its an array
                    if (o.GetType().IsPrimitive) sw.Write(o.ToString()); // default
                    else if (o is System.Collections.IEnumerable) SerializeArray(sw, o as System.Collections.IEnumerable);
                    else SerializePump(sw, o);
                    break;
            }
        }
        public void Serialize(System.IO.Stream serializationStream, object graph)
        {
            StreamWriter sw = new StreamWriter(serializationStream);
           
            sw.WriteLine(" local ClassName=\"{0}\"", graph.GetType().FullName);
            sw.WriteLine("local self = {}");
            SerializePump(sw, graph);
            serializationStream.Flush();
        }

        //  

   

        public ISurrogateSelector SurrogateSelector
        {
            get { return surrogateSelector; }
            set { surrogateSelector = value; }
        }
        public SerializationBinder Binder
        {
            get { return binder; }
            set { binder = value; }
        }
        public StreamingContext Context
        {
            get { return context; }
            set { context = value; }
        }
    }

    public static class BinaryReaderExtensions
    {
        public static T[] ReadArray<T>(this BinaryReader r, int offset, int count) where T : struct
        {
            var pos = r.BaseStream.Position;
            r.BaseStream.Position = offset;
            T[] ret = r.ReadArray<T>(count);
            r.BaseStream.Position = pos;
            return ret;
        }
        public static int ReadBigInt32(this BinaryReader r)
        {
            byte[] data = new byte[4];
            r.Read(data, 0, 4);
            return (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
        }

        public static T[] ReadArray<T>(this BinaryReader r, int count) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] bytes = r.ReadBytes(count * size);
            T[] array = new T[count];
            Buffer.BlockCopy(bytes, 0, array, 0, array.Length * size);
            return array;
        }
        public static uint[] ReadUInt32(this BinaryReader r, int offset, int count)
        {
            return r.ReadArray<uint>(offset, count);
        }
        public static uint[] ReadUInt32(this BinaryReader r, int count)
        {
            return r.ReadArray<uint>(count);
        }
        public static int[] ReadInt32(this BinaryReader r, int offset, int count)
        {
            return r.ReadArray<int>(offset, count);
        }
        public static int[] ReadInt32(this BinaryReader r, int count)
        {
            return r.ReadArray<int>(count);
        }
        public static short[] ReadInt16(this BinaryReader r, int offset, int count)
        {
            return r.ReadArray<short>(offset, count);
        }
        public static short[] ReadInt16(this BinaryReader r, int count)
        {
            return r.ReadArray<short>(count);
        }
        /// <summary>
        /// Reads a Bool as an entire int and chceks if it is 1 or 0
        /// </summary>
        /// <returns></returns>
        public static bool ReadIntBool(this BinaryReader r)
        {
            int b = r.ReadInt32();
            if (b != 1 && b != 0) throw new Exception("Expected bool to be 0 or 1");
            return b != 0;
        }
        /// <summary>
        /// Reads a string of a fixed lenght at position
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public static string ReadFixedString(this BinaryReader r, int count)
        {
            byte[] bytes = r.ReadBytes(count);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        // reads a zero terminated string from offset
        public static string ReadStringFromNextOffset(this BinaryReader r)
        {
            int offset = r.ReadInt32();
            return r.ReadStringFromOffset(offset);
        }
        public static string ReadStringFromOffset(this BinaryReader r, int offset)
        {
            var pos = r.BaseStream.Position;
            r.BaseStream.Position = offset;
            string s = r.ReadZeroTerminatedString();
            r.BaseStream.Position = pos;
            return s;
        }
        const int MaxZeroTerminatedStringSize = 64;
        /// <summary>
        /// Tries to read a string, assumes strings arn't bigger than 64 charaters
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public static string ReadZeroTerminatedString(this BinaryReader r)
        {
            var pos = r.BaseStream.Position;
            byte[] block = r.ReadBytes(MaxZeroTerminatedStringSize);
            for (int i = 0; i < block.Length; i++)
            {
                byte b = block[i];
                if (b == 0)
                {
                    r.BaseStream.Position = pos = i + 1; // rewind, but skip the 0
                    return System.Text.Encoding.UTF8.GetString(block, 0, i);
                }
            }
            throw new Exception("String not zero terminated");
        }
        public struct Entry
        {
            public readonly int Index;
            public readonly int Position;
            public readonly int Next; // Can be used to caculate the size
            public Entry(int Index, int Position, int Next) { this.Index = Index; this.Position = Position; this.Next = Next; }
        }
        public static Entry[] ReadChunkEntries(this BinaryReader r)
        {
            int count = r.ReadInt32();
            if (count == 0) return new Entry[0];
            else
            {
                Entry[] entries = new Entry[count];
                int[] ientries = r.ReadInt32(count);
                for (int i = 0; i < count; i++)
                    entries[i] = new Entry(i, ientries[i], i + 1 < count ? ientries[i] : -1);
                return entries;
            }
        }
        public static Entry[] ReadChunkEntries(this BinaryReader r, int offset)
        {
            var pos = r.BaseStream.Position;
            r.BaseStream.Position = offset;
            var ret = r.ReadChunkEntries();
            r.BaseStream.Position = pos;
            return ret;
        }
        public static IEnumerable<Entry> ForEachEntry(this BinaryReader r, Entry[] entries)
        {
            foreach (var o in entries)
            {
                r.BaseStream.Position = o.Position;
                yield return o;
            }
        }
        public static IEnumerable<Entry> ForEachEntry(this BinaryReader r, int offset)
        {
            var pos = r.BaseStream.Position;
            r.BaseStream.Position = offset;
            var entries = r.ReadChunkEntries();
            foreach (var o in entries)
            {
                r.BaseStream.Position = o.Position;
                yield return o;
            }
            r.BaseStream.Position = pos;
        }
        /// <summary>
        /// special case, we read the table, but save the position AFTER the table
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public static IEnumerable<Entry> ForEachEntry(this BinaryReader r)
        {
            var entries = r.ReadChunkEntries();
            var pos = r.BaseStream.Position;
            foreach (var o in entries)
            {
                r.BaseStream.Position = o.Position;
                yield return o;
            }
            r.BaseStream.Position = pos;
        }

    }
    public partial class File
    {
      
        class Chunk
        {
            public readonly int start;
            public readonly int end;
            public readonly int size;
            public readonly string name;
            public Chunk(string name, int start, int size) { this.name = name; this.start = start; this.end = start + size; this.size = size; }
        }
        static Dictionary<string, Chunk> fileChunks = null;
        static byte[] rawData = null;
        static string filename = null;

        static Dictionary<int, string> stringCache = null;
        static List<string> stringList = null;

        static List<SpriteFrame> spriteframes = null;
        static List<Texture> textures = null;
        static List<Sprite> sprites = null;
        static List<GObject> objects = null;
        static List<Room> rooms = null;
        static List<Background> backgrounds = null;
        static List<AudioFile> sounds = null;
        static List<RawAudio> rawAudio = null;
        static List<Font> fonts = null;
        static List<Code> codes = null;
        static List<Script> scripts = null;
        static Dictionary<string, GameMakerStructure> namedResourceLookup = new Dictionary<string, GameMakerStructure>();
        static void InternalLoad()
        {
            if (filename == null) throw new FileNotFoundException("No file name defined");
            if (rawData != null) return; // already loaded
            // Clear all the tables in case they arn't already
            stringList = null;
            textures = null;
            sprites = null;
            objects = null;
            rooms = null;
            backgrounds = null;
            sounds = null;
            rawAudio = null;
            fonts = null;
            codes = null;
            scripts = null;
            spriteframes = null;


            using (FileStream sr = System.IO.File.Open(filename, FileMode.Open, FileAccess.Read))
            {
                rawData = new byte[sr.Length];
                sr.Read(rawData, 0, rawData.Length);
            }
            fileChunks = new Dictionary<string, Chunk>();
            MemoryStream ms = new MemoryStream(rawData);
            int full_size = rawData.Length;
            Chunk chunk = null;
            using (BinaryReader r = new BinaryReader(ms))
            {
                while (r.BaseStream.Position < full_size)
                {
                    string chunkName = r.ReadFixedString(4);
                    int chunkSize = r.ReadInt32();
                    int chuckStart = (int) r.BaseStream.Position;
                    chunk = new Chunk(chunkName, chuckStart, chunkSize);
                    fileChunks[chunkName] = chunk;
                    if (chunkName == "FORM") full_size = chunkSize; // special case for form
                    else r.BaseStream.Position = chuckStart + chunkSize; // make sure we are always starting at the next chunk               
                }
            }
        }

        public static IEnumerable<INamedResrouce> Search(string name)
        {
            return namedResourceLookup.Where(x => x.Key.Contains(name)).Select(x => (INamedResrouce) x.Value);
        }

        public static bool TryLookup<T>(string name, out T ret) where T : GameMakerStructure
        {
            GameMakerStructure data;
            if (namedResourceLookup.TryGetValue(name, out data))
            {
                T t = data as T;
                if (t != null)
                {
                    ret = t;
                    return true;
                }
            }
            ret = default(T);
            return false;
        }
        static void CheckStrings()
        {
            if (stringList == null)
            {
                stringList = new List<string>();
                stringCache = new Dictionary<int, string>();
                Chunk chunk = fileChunks["STRG"];
                using (BinaryReader r = new BinaryReader(new MemoryStream(rawData)))
                {
                    foreach (var e in r.ForEachEntry(chunk.start))
                    {
                        int string_size = r.ReadInt32(); //size 
                        byte[] bstr = r.ReadBytes(string_size);
                        string str = System.Text.Encoding.UTF8.GetString(bstr, 0, string_size);
                        stringList.Add(str);
                        stringCache[e.Position + 4] = str;
                    }
                }
            }
        }
        // Ok, this is stupid, but I get it
        // All the vars and function names are on a link list from one to another that tie to the name
        // to make this much easyer for the disassembler, we are going to change all these refrences
        // to be string indexes to strlist.  To DO this however, requires us to read the entire code section
        // as a bunch of ints, go though the etnire thing with the refrences, repeat then figure out the code section
        // into seperate scripts blocks.  Meh
        // Also a side note is that object refrences as well as instance creation MUST fit in a 16bit value
        // If thats the case are objects manged by an index or am I reading objects wrong?
        class RefactorCodeManager
        {
            BinaryReader r;
            Dictionary<string, CodeNameRefrence> refs = new Dictionary<string, CodeNameRefrence>();
            Dictionary<int, CodeNameRefrence> offsetLookup = new Dictionary<int, CodeNameRefrence>();
            Dictionary<string, int> stringToIndex = new Dictionary<string, int>();
            public RefactorCodeManager(byte[] rawData)
            {
                r = new BinaryReader(new MemoryStream(rawData, true));
                for (int i = 0; i < File.Strings.Count; i++) stringToIndex[File.Strings[i]] = i;
            }
            class CodeNameRefrence
            {
                public string Name;
                public int Index;
                public int Count;
                public int Start;
                public int[] Offsets;
                public CodeNameRefrence(BinaryReader r, int index)
                {
                    Index = index;
                    Name = r.ReadStringFromNextOffset();
                    Count = r.ReadInt32();
                    Start = r.ReadInt32();
                    Offsets = new int[Count];
                    int save = (int) r.BaseStream.Position;
                    r.BaseStream.Position = Start;
                    if (Count > 0)
                    {
                        for (int i = 0; i < Count; i++)
                        {
                            uint first = r.ReadUInt32(); // skip the first pop/push/function opcode
                            int position = (int) r.BaseStream.Position;
                            var code = GMCodeUtil.getFromRaw(first);
                            Debug.Assert(code == GMCode.Push || code == GMCode.Pop || code == GMCode.Call);
                            int offset = r.ReadInt32() & 0x00FFFFFF;
                            Offsets[i] = position;
                            r.BaseStream.Position += offset - 8;
                        }
                    }
                    r.BaseStream.Position = save;
                }
            }
            public void AddRefs(int start, int size)
            {
                r.BaseStream.Position = start;
                int refCount = size / 12;

                //   int refCount = r.ReadInt32(); // what is this first number? refrence count?
                //   System.Diagnostics.Debug.WriteLine("Reading {0} refrences from {1}", refCount, refChunk.name);
                //  System.Diagnostics.Debug.WriteLine("Reading {0} refrences from {1}", count, refChunk.name);
                // Each ref is 3 ints long, firt is name refrence, second is number of refs, and thrid is the chain
                for (int i = 0; i < refCount; i++)
                {
                    CodeNameRefrence nref = new CodeNameRefrence(r, i);
                    refs.Add(nref.Name, nref);
                    foreach (var o in nref.Offsets) offsetLookup.Add(o, nref);
                }
            }
            public void WriteAllChangesToBytes()
            {
                foreach (var r in offsetLookup)
                {
                    int offset = r.Key;
                    int debug0 = BitConverter.ToInt32(rawData, offset);
                    int index = stringToIndex[r.Value.Name];
                    rawData[offset + 0] = (byte) ((index) & 0xFF);
                    rawData[offset + 1] = (byte) ((index >> 8) & 0xFF);
                    rawData[offset + 2] = (byte) ((index >> 16) & 0xFF);
                }
            }
        }
        static void RefactorCode()
        {
            if (codes == null)
            {
                CheckList("CODE", ref codes); // fill out all the scripts first
                RefactorCodeManager rcm = new RefactorCodeManager(File.rawData);
                Chunk funcChunk = fileChunks["FUNC"]; // function names
                Chunk varChunk = fileChunks["VARI"]; // var names
                rcm.AddRefs(funcChunk.start, funcChunk.size);
                rcm.AddRefs(varChunk.start, varChunk.size); // add all the reffs
                rcm.WriteAllChangesToBytes();
            }
        }
        public static void LoadEveything()
        {
            CheckStrings();
            CheckList("TXTR", ref textures);
            CheckList("BGND", ref backgrounds);
            CheckList("TPAG", ref spriteframes);
            CheckList("SPRT", ref sprites);
            CheckList("ROOM", ref rooms);
            CheckList("AUDO", ref rawAudio);
            CheckList("SOND", ref sounds);
            CheckList("FONT", ref fonts);
            CheckList("OBJT", ref objects);
            CheckList("SCPT", ref scripts);
            RefactorCode();

            DebugPring();
        }
        public static IReadOnlyList<Script> Scripts { get { CheckList("SCPT", ref scripts); return scripts; } }
        public static IReadOnlyList<string> Strings { get { CheckStrings(); return stringList; } }
        public static IReadOnlyList<Code> Codes { get { RefactorCode(); return codes; } }
        public static IReadOnlyList<Font> Fonts { get { CheckList("FONT", ref fonts); return fonts; } }
        public static IReadOnlyList<Texture> Textures { get { CheckList("TXTR", ref textures); return textures; } }
        public static IReadOnlyList<SpriteFrame> SpriteFrames { get { CheckList("TPAG", ref spriteframes); return spriteframes; } }
        public static IReadOnlyList<Sprite> Sprites { get { CheckList("SPRT", ref sprites); return sprites; } }
        public static IReadOnlyList<GObject> Objects { get { CheckList("OBJT", ref objects); return objects; } }
        public static IReadOnlyList<Room> Rooms { get { CheckList("ROOM", ref rooms); return rooms; } }
        public static IReadOnlyList<Background> Backgrounds { get { CheckList("BGND", ref backgrounds); return backgrounds; } }
        public static IReadOnlyList<AudioFile> Sounds { get { CheckList("AUDO", ref rawAudio); CheckList("SOND", ref sounds); return sounds; } }
        static void CheckList<T>(string chunkName, ref List<T> list) where T : GameMakerStructure, new()
        {
            if (rawData == null) throw new FileLoadException("Data.win file not open");
            if (list == null) list = new List<T>();
            if (list.Count == 0)
            {
                Chunk chunk;
                BinaryReader r = new BinaryReader(new MemoryStream(rawData));
                if (fileChunks.TryGetValue(chunkName, out chunk)) ReadList(list, r, chunk.start, chunk.end);// textures
            }
        }
        static void ReadList<T>(List<T> list, BinaryReader r, int start, int end) where T : GameMakerStructure, new()
        {
            bool isNamedResouce = typeof(T).GetInterfaces().Contains(typeof(INamedResrouce));
            list.Clear();
            foreach (var e in r.ForEachEntry(start))
            {
                T t = new T();
                t.Read(r, e.Index);
                list.Add(t);
                if (isNamedResouce)
                {
                    INamedResrouce nr = t as INamedResrouce;
                    if (namedResourceLookup == null) namedResourceLookup = new Dictionary<string, GameMakerStructure>();
                    namedResourceLookup.Add(nr.Name, t);
                }
            }
        }

        public static void DebugPrint<T>(List<T> list, TextWriter w, string globalTableName, bool nameLookup = true) where T : ILuaObject
        {
            w.WriteLine("-- Start {0} Init", globalTableName);
            w.WriteLine("{0} = {{}}", globalTableName);
            string localTableName = "loc_" + globalTableName.Replace('.', '_');
            w.WriteLine("local {0} = {1}", localTableName, globalTableName);
            // we set up an index array lookup
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                var ls = o.ToLuaStructure();
                w.Write("{0}[{1,-4}]= ", localTableName, i);
                ls.ToStringBuilder(w);
                w.WriteLine();

            }
            if (nameLookup)
            {
                string localMapName = "loc_" + globalTableName + "NameMap";
                w.WriteLine();
                w.WriteLine("-- Set up name look up here");
                w.WriteLine("{0}NameMap = {{}}", globalTableName);
                w.WriteLine("local {0} = {1}NameMap", localMapName, globalTableName);
                w.WriteLine("for k, v in ipairs({0}) do", localMapName);
                w.WriteLine("\tmap[v.name] = v");
                w.WriteLine("\tif v.index == nil or v.index ~= k then");
                w.WriteLine("\t\tv.index = k");
                w.WriteLine("\tend");
                w.WriteLine("end");
            }
            w.WriteLine("-- End {0} Init", globalTableName);

        }
        public static void DebugPring()
        {
            using (StreamWriter sr = new StreamWriter("object_info.txt"))
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    var o = objects[i];
                    sr.Write("{0,-4}: Name: {1:-20}", i, o.Name);
                    if (o.Parent > 0) sr.Write("  Parent({0}): {1}", o.Parent, File.Objects[o.Parent].Name);
                    sr.WriteLine();
                }
            }
            using (StreamWriter sr = new StreamWriter("sprite_info.lua"))
            {
                DebugPrint(sprites, sr, "_sprites", true);
            }
            using (StreamWriter sr = new StreamWriter("font_info.lua"))
            {
                DebugPrint(fonts, sr, "_fonts", true);
            }
            using (StreamWriter sr = new StreamWriter("room_info.txt"))
            {
                for (int i = 0; i < rooms.Count; i++)
                {
                    var o = rooms[i];
                    sr.Write("{0,-4}: Name: {1:-20} Size({2},{3})", i, o.Name, o.Width, o.Height);
                    sr.WriteLine();
                    if (o.Objects.Length > 0)
                        for (int j = 0; j < o.Objects.Length; j++)
                        {
                            var oo = o.Objects[j];
                            var obj = objects[oo.Index];
                            sr.WriteLine("       Object: {0}  Pos({1},{2}", obj.Name, oo.X, oo.Y);
                        }
                }
            }
            { // xmltest
                const string filename = "room_all.xml";
                if (System.IO.File.Exists(filename)) System.IO.File.Delete(filename);
                using (System.IO.FileStream file = new System.IO.FileStream(filename, FileMode.Create))
                {
                    System.Xml.Serialization.XmlSerializer writer = new System.Xml.Serialization.XmlSerializer(typeof(File.AudioFile));
                    writer.Serialize(file, Sounds[45]);
                    file.Flush();
                } 
            }
            {
                const string filename = "room_all.lua";
                if (System.IO.File.Exists(filename)) System.IO.File.Delete(filename);
                using (System.IO.FileStream file = new System.IO.FileStream(filename, FileMode.Create))
                {
                    file.Position = 0;
                    SimplerLuaFormatter writer = new SimplerLuaFormatter();
                    writer.Serialize(file, Sounds[45]);
                    file.Flush();
                    file.Close();
                }
            }
            //  
            // var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "//SerializationOverview.xml";
        }
        
        public static void LoadDataWin(string filename)
        {
            if (File.filename != null && File.rawData != null && File.filename == filename) return; // don't do anything, file already loaded
            File.rawData = null; // clear old data
            File.filename = filename;
            InternalLoad();


        }
    }
}
