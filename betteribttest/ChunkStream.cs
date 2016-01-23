using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace betteribttest
{
    // Been rewriting alot of code aniliziing these stupid chunks so thought I might as well make a class for it
    public class ChunkEntry : IEquatable<ChunkEntry>
    {
        public int ChunkSize { get; private set; } // size is -1 if we don't have a size
        public int Position { get; private set; }
        public int Limit { get; private set; } // might be next offset if we have it
        public ChunkEntry(int position, int limit, int size) { Position = position; Limit = limit; ChunkSize = size; }
        public bool Equals(ChunkEntry obj)
        {
            return Position == obj.Position;
        }
        public override bool Equals(object obj)
        {
            ChunkEntry c = obj as ChunkEntry;
            if (c != null) return Equals(c);
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return Position; // this is the easiest as all Position are all diffrent
        }
    }
    public class ChunkEntries : IEnumerable<ChunkEntry>, IEnumerator<ChunkEntry>// list of offets that uses ChunkStream to move beetween
    {
        public static bool DebugOutput { get; set; }
        ChunkStream cs;
        int chunkLimit;
        int chunkStart;

        ChunkEntry[] entries;
        int pos;
        public int Count { get { return entries == null ? 0 : entries.Length; } }
        public ChunkEntry this[int i] { get { return entries[i]; } }
        void ReadEntries(bool rangeChecking)
        {
            int entriesCount = cs.ReadInt32();
            if(entriesCount > 100000) throw new ArgumentOutOfRangeException("Count", "Entries WAY out of range for resonable people (more than 100k)");
            int offset = 0, next_offset = 0, size = 0; // these always get assgiend but the compiler gets ansy if I don't put some number in it
            if (DebugOutput) System.Diagnostics.Debug.WriteLine("ChunkEntries: {0}", entriesCount);
            if (entriesCount == 0)
            {
                // empty entries, so set it up as one
                this.entries = null;
                return;
            }
            else if (entriesCount == 1)
            {
                offset = cs.ReadInt32();
                if (rangeChecking && (offset >= chunkLimit || offset <= chunkStart)) throw new ArgumentOutOfRangeException("Index 0", "Out of Chunk range");
                this.entries = new ChunkEntry[] { new ChunkEntry(offset, chunkLimit, chunkLimit - offset) };
                return;
            } // Solve all this above for the two special cases.  This code dosn't look elegant at all, but it works
            List<ChunkEntry> entries = new List<ChunkEntry>(entriesCount);
            int[] offsets = cs.ReadInt32(entriesCount);
            for (int i = 0; i < (entriesCount - 1); i++)
            {
                offset = offsets[i];
                if (rangeChecking && (offset >= chunkLimit || offset <= chunkStart)) throw new ArgumentOutOfRangeException("Index " + i, "Out of Chunk range");
                next_offset = offsets[i + 1];
                size = next_offset - offset;
                entries.Add(new ChunkEntry(offset, next_offset, size));
                offset = next_offset;
                offset = offsets[i];
                // entries[i] = new ChunkEntry(offset, next_offset, size);
            }

            if (rangeChecking && (next_offset >= chunkLimit || next_offset <= chunkStart)) throw new ArgumentOutOfRangeException("Index " + (entriesCount - 1), "Out of Chunk Limit");
            double average = entries.Average(c => c.ChunkSize);
            double sumOfSquaresOfDifferences = entries.Select(c => (c.ChunkSize - average) * (c.ChunkSize - average)).Sum();
            double sd = Math.Sqrt(sumOfSquaresOfDifferences / entries.Count);

            if (((int)sd == 0) && size == (int)Math.Round(average))
            {// they are all fixed size, so its easy
                entries.Add(new ChunkEntry(next_offset, next_offset + size, size)); // ChunkLimit should eqal = next_offset + size, but it dosn't have to at this point
                if (DebugOutput) System.Diagnostics.Debug.WriteLine("This has a Fixed size of {0} after testing {1} entries", size, entriesCount);
            }
            else
            {
                double dsize = Math.Abs((chunkLimit - next_offset) - average);
                if (dsize <= sd) entries.Add(new ChunkEntry(next_offset, next_offset, chunkLimit - next_offset)); // then the end of the chunkLimit IS the last offset
                else
                {
                    if (DebugOutput)
                    {
                        System.Diagnostics.Debug.WriteLine("Could not find end of next offset, so wild guess from {0} entries", entriesCount);
                        System.Diagnostics.Debug.WriteLine("ChunkEntrie: Average: {0:0.00}  SumOfSquares: {1:0.00}  StandardDeveation {2:0.00}  dsize {3:0.00}", average, sumOfSquaresOfDifferences, sd, dsize);

                    }
                    entries.Add(new ChunkEntry(next_offset, chunkLimit, -1)); // then the end of the chunkLimit IS the last offset
                }
            }

            this.entries = entries.ToArray();
            cs.Position = entries[pos].Position; // move to the first offset
            Current = default(ChunkEntry);
        }
        public ChunkEntries(ChunkStream cs, int chunkLimit, bool rangeChecking = true)
        {
            this.cs = cs;
            this.pos = 0;
            this.chunkStart = cs.Position;
            this.chunkLimit = chunkLimit;
            ReadEntries(rangeChecking);
        }
        public ChunkEntries(ChunkStream cs,int chunkStart,int chunkLimit,bool rangeChecking=true)
        {
            this.cs = cs;
            this.pos = 0;
            this.chunkStart = chunkStart;
            this.chunkLimit = chunkLimit;
            cs.Position = chunkStart;
            ReadEntries(rangeChecking);
        }
        ~ChunkEntries() {  }
        public void Dispose() { }
        // Must implement GetEnumerator, which returns a new StreamReaderEnumerator.
        public IEnumerator<ChunkEntry> GetEnumerator()
        {
            return this;
        }

        public bool MoveNext()
        {
            if(entries != null && pos < entries.Length)
            {
                if (DebugOutput) System.Diagnostics.Debug.WriteLine("Moving to {0} out of {1}", pos, entries.Length);
                ChunkEntry e = entries[pos++];
                Current = e;
                cs.Position = e.Position;
                return true;
            }
            Current = default(ChunkEntry);
            return false;
        }
        public void Reset()
        {
            pos = 0;
        }
        // Humm, I wonder if I need to error check this or not since it IS invalid without MoveNext run first
        public ChunkEntry Current { get; private set; }
        // required interfaces
        IEnumerator IEnumerable.GetEnumerator() { return (IEnumerator)this; }
        object IEnumerator.Current { get { return Current; } }
    }

    public class ChunkStream : BinaryReader
    {
        Stack<int> posStack = new Stack<int>();
        // used on reading strings so we don't go creating a new one for thousands of things
        Dictionary<int, string> stringCache = new Dictionary<int, string>(); 

        public byte[] ChunkData { get; private set; }
        public ChunkStream(Stream s) : base(s) { DebugPosition = false; ChunkData = null;  }
        public ChunkStream(Stream s, Encoding e) : base(s,e) { DebugPosition = false; ChunkData = null; }
        public ChunkStream(Stream s, Encoding e,bool leaveOpen) : base(s, e, leaveOpen) { DebugPosition = false; ChunkData = null; }
        public ChunkStream(byte[] chunk) : base(new MemoryStream(chunk, false)) { DebugPosition = false; ChunkData = chunk; }
        public bool DebugPosition { get; set; }
        public int Position {  get { return (int)BaseStream.Position; } set { BaseStream.Position = value; } }
        public int Length { get { return (int)BaseStream.Length; } }
        public void PushPosition()
        {
            BaseStream.Flush();
            posStack.Push(Position);
        }
        public void PushSeek(int position)
        {
            PushPosition();
            Position = position;
        }
        public void PopPosition()
        {
            BaseStream.Flush();
            Position = posStack.Pop();
        }
        public ChunkStream readChunk(int chunkStart, int chunkEnd)
        {
            PushSeek(chunkStart);
            byte[] data = ReadBytes((int)(chunkEnd - chunkStart));
            PopPosition();
            return new ChunkStream(data);
        }
        public uint[] ReadUInt32(int count, int position)
        {
            PushSeek(position);
            uint[] ret = ReadUInt32(count);
            PopPosition();
            return ret;
        }
        public uint[] ReadUInt32(int count)
        {
            byte[] bytes = ReadBytes(count * sizeof(uint));
            uint[] intArray = new uint[count];
            Buffer.BlockCopy(bytes, 0, intArray, 0, intArray.Length * sizeof(uint));
            return intArray;
        }
        public int[] ReadInt32(int count, int position)
        {
            PushSeek(position);
            int[] ret = ReadInt32(count);
            PopPosition();
            return ret;
        }
        public int[] ReadInt32(int count) {
            // after looking at the code for ReadInt32 at microsoft, its better to read a buffer
            // of bytes and convert that then using ReadInt repeadedly.  Looks like alot of overhead with it
            byte[] bytes = ReadBytes(count * sizeof(int));
            int[] intArray = new int[count];
            Buffer.BlockCopy(bytes, 0, intArray, 0, intArray.Length * sizeof(int));
            return intArray;
        }
        public short[] ReadInt16(int count, int position)
        {
            PushSeek(position);
            short[] ret = ReadInt16(count);
            PopPosition();
            return ret;
        }
        public short[] ReadInt16(int count)
        {
            byte[] bytes = ReadBytes(count * sizeof(short));
            short[] shortArray = new short[count];
            Buffer.BlockCopy(bytes, 0, shortArray, 0, shortArray.Length * sizeof(short));
            return shortArray;
        }
        /*
        public int[] CollectEntries(int limit, bool advance = true, bool ignorelimit = false)
        {
            List<int> entries = new List<int>();
            if (!advance) PushPosition();
            int entriesCount = ReadInt32();
            int entryStart = (int)(BaseStream.Position + entriesCount);
            for (int i = 0; i < entriesCount; i++)
            {
                int entry = ReadInt32();
                if (!ignorelimit && (entry < entryStart || entry > limit)) throw new ArgumentOutOfRangeException("Index_" + i, "Offset out of limit");
                entries.Add(entry);
            }
            if(!advance) PopPosition();
            return entries.ToArray();
        }
        public int[] CollectEntries(bool advance = true)
        {
            return CollectEntries(0, advance, true);
        }
        */
       
        string ReadVString() // We shouldn't throw here
        {
            string str;
            int posStart = Position;
            if (stringCache.TryGetValue(posStart, out str)) return str;
            List<byte> bytes = new List<byte>();
            for (;;)
            {
                if (Position >= Length) throw new EndOfStreamException("End of stream before end of string");
                byte b = ReadByte();
                if (b == 0) break;
                bytes.Add(b);
            }
            if (bytes.Count == 0) return null; // null if we just read a 0
            str = System.Text.Encoding.UTF8.GetString(bytes.ToArray());
            stringCache[posStart] = str;
            return str;
        }
        string ReadVString(int position)
        {
            string s;
            int posStart = Position; // we do it again here for speed
            if (stringCache.TryGetValue(Position, out s)) return s;
            s = ReadVString();
            Position = posStart;
            return s;
        }

    }
}
