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
    public class ChunkEntries : IEnumerable<ChunkEntry>//, IEnumerator<ChunkEntry>// list of offets that uses ChunkStream to move beetween
    {
        public static bool DebugOutput { get; set; }
        ChunkStream cs;
        int chunkLimit;
        int chunkStart;

        ChunkEntry[] entries;

        public int Count { get { return entries == null ? 0 : entries.Length; } }
        public ChunkEntry this[int i] { get { return entries[i]; } }
        public int Length {  get { return entries.Length; } }
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
        }

        public ChunkEntries(ChunkStream cs, int chunkLimit, bool rangeChecking = true)
        {
            this.cs = cs;
            this.chunkStart = cs.Position;
            this.chunkLimit = chunkLimit;
            ReadEntries(rangeChecking);
        }
        public ChunkEntries(ChunkStream cs,int chunkStart,int chunkLimit,bool rangeChecking=true)
        {
            this.cs = cs;
            this.chunkStart = chunkStart;
            this.chunkLimit = chunkLimit;
            cs.PushSeek(chunkStart);
            ReadEntries(rangeChecking);
            cs.PopPosition();
        }
        class ChunkEntriesEnumerator : IEnumerator<ChunkEntry>
        {
            int pos;
            ChunkEntry[] entries;
            ChunkStream cs;
            ChunkEntry current;
            public ChunkEntriesEnumerator(ChunkStream cs, ChunkEntry[] entries)
            {
                this.cs = cs;
                this.entries = entries;
                this.pos = 0;
                this.current = null;
                cs.PushPosition();
            }
            public ChunkEntry Current { get { return current; } }
            object IEnumerator.Current { get { return current; } }
            public void Dispose() { cs.PopPosition(); }
            public bool MoveNext()
            {
                if (entries != null && pos < entries.Length)
                {
                   // if (DebugOutput) System.Diagnostics.Debug.WriteLine("Moving to {0} out of {1}", pos, entries.Length);
                    ChunkEntry e = entries[pos++];
                    current = e;
                    cs.Position = e.Position;
                    return true;
                }
                current = null;
                return false;
            }
            public void Reset() { pos = 0; }
        }
        // Must implement GetEnumerator, which returns a new StreamReaderEnumerator.
        public IEnumerator<ChunkEntry> GetEnumerator() { return new ChunkEntriesEnumerator(cs, entries);  }
        IEnumerator IEnumerable.GetEnumerator() { return (IEnumerator)this; }
    }
    // http://stackoverflow.com/questions/31078598/c-sharp-create-a-filestream-with-an-offset
    // This class saved me for a workaround for reading a bitmap out of an exisiting file
    class ChunkStreamOffset : Stream
    {
        private readonly Stream instance;
        private readonly long offset;

        public static Stream Decorate(Stream instance)
        {
            if (instance == null) throw new ArgumentNullException("instance");

            Stream decorator = new ChunkStreamOffset(instance);
            return decorator;
        }

        private ChunkStreamOffset(Stream instance)
        {
            this.instance = instance;
            this.offset = instance.Position;
        }

        #region override methods and properties pertaining to the file position/length to transform the file positon using the instance's offset

        public override long Length
        {
            get { return instance.Length - offset; }
        }

        public override void SetLength(long value)
        {
            instance.SetLength(value + offset);
        }

        public override long Position
        {
            get { return instance.Position - this.offset; }
            set { instance.Position = value + this.offset; }
        }

        public override bool CanRead
        {
            get
            {
                return instance.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return instance.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return instance.CanWrite;
            }
        }

        // etc.

        #endregion

        #region override all other methods and properties as simple pass-through calls to the decorated instance.

        public override IAsyncResult BeginRead(byte[] array, int offset, int numBytes, AsyncCallback userCallback, object stateObject)
        {
            return instance.BeginRead(array, offset, numBytes, userCallback, stateObject);
        }

        public override IAsyncResult BeginWrite(byte[] array, int offset, int numBytes, AsyncCallback userCallback, object stateObject)
        {
            return instance.BeginWrite(array, offset, numBytes, userCallback, stateObject);
        }

        public override void Flush()
        {
            instance.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            // if (origin == SeekOrigin.Begin) offset += this.offset;
            return instance.Seek(offset, origin);// - this.offset;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return instance.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            instance.Write(buffer, offset, count);
        }
        #endregion
    }
    /// <summary>
    /// This class takes a stream and limits it at a starting offset from the Beging and optionaly, and ending Length
    /// From the users standpoint, this stream works just like a normal stream. This will throw an IO error if you go outside
    /// of the limits of the stream
    /// </summary>
    public class OffsetStream : Stream
    {
        public class OffsetStreamLimitException : ArgumentException
        {
            public OffsetStreamLimitException() : base("Cannot go outside the limits of an OffsetStream") { }
        }
        Stream BaseStream;
        long _length;
        long _start;
        public OffsetStream(Stream s, long start, long length)
        {
            BaseStream = s;
            _start = start;
            _length = length;
            BaseStream.Position = start;
        }
        public OffsetStream(Stream s, long start) : this(s, start, s.Length) { }
        public override bool CanRead  { get  { return BaseStream.CanRead; } }
        public override bool CanSeek { get { return BaseStream.CanSeek; } }
        public override bool CanWrite { get { return BaseStream.CanWrite; } }
        public override long Length { get { return _length; } }
        public override long Position
        {
            get
            {
                return BaseStream.Position - _start;
            }

            set
            {
                long newPos = value + _start;
                if (newPos < 0 || newPos > (_length+ _start)) throw new OffsetStreamLimitException();
                BaseStream.Position = newPos;
            }
        }

        public override void Flush() { BaseStream.Flush();  }
        public override int Read(byte[] buffer, int offset, int count)
        {
            long limit = Position + count;
            if(limit > (_length+ _start)) throw new OffsetStreamLimitException();
            return BaseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = 0; 
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPos =  offset + _start;
                    break;       
                case SeekOrigin.End:
                    newPos = offset + _start + _length;
                    break;
                case SeekOrigin.Current:
                    newPos = Position + offset;
                    break;
            }
            if (newPos < 0 || newPos > _length) throw new OffsetStreamLimitException();
            BaseStream.Seek(newPos, SeekOrigin.Begin);
            return newPos;
        }

        public override void SetLength(long value) { throw new NotImplementedException(); }

        public override void Write(byte[] buffer, int offset, int count)
        {
            long limit = Position + count;
            if (limit > _length) throw new OffsetStreamLimitException();
            BaseStream.Write(buffer, offset, count);
        }
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

        public Stream StreamFromPosition()
        {
            return ChunkStreamOffset.Decorate(BaseStream);
        }
        public Stream StreamFromPosition(int position)
        {
            PushSeek(position);
            Stream s = ChunkStreamOffset.Decorate(BaseStream);
            PopPosition();
            return s;
        }
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
        public bool readIntBool()
        {
            int b = this.ReadInt32();
            if (b != 1 && b != 0) throw new Exception("Expected bool to be 0 or 1");
            return b != 0;
        }
        public string readFixedString(int count)
        {
            byte[] bytes = ReadBytes(count);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        public string readStringFromOffset()
        {
            int offset = this.ReadInt32();
            PushSeek(offset);
            string s = ReadVString();
            PopPosition();
            return s;
        }
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
