using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
namespace tar_cs
{
    public enum EntryType : byte
    {
        File = 0,
        FileObsolete = 0x30,
        HardLink = 0x31,
        SymLink = 0x32,
        CharDevice = 0x33,
        BlockDevice = 0x34,
        Directory = 0x35,
        Fifo = 0x36,
    }

    public class TarException : Exception
    {
        public TarException(string message) : base(message)
        {
        }
    }
    internal class TarHeader
    {
        struct FieldInfo {
          public readonly int offset;
            public readonly int size;
            public FieldInfo(int offset, int size)
            {
                this.offset = offset;
                this.size = size;
            }
            public void clear(byte[] buffer)
            {
                Array.Clear(buffer, offset, size);
            }
            public void fill(byte[] buffer, char c)
            {
                for (int i = offset; i < (size + offset); i++) buffer[i] = (byte)c;
            }
            public long ReadValue(byte[] buffer, int @base = 8)
            {
                string v = ReadString(buffer);
                return Convert.ToInt64(v, @base);
                /*
          
                long ret = 0;
                for (int i = offset; i < (size + offset); i++)
                    if (buffer[i] == 0) break;
                    else ret += (ret * @base) + buffer[i];
                return ret;
                */
            }
            public void WriteValue(byte[] buffer, long v, int @base = 8)
            {
                string s = Convert.ToString(v, 8).PadLeft(size - 1, '0');
                Encoding.ASCII.GetBytes(s, 0, s.Length, buffer, offset);
                buffer[offset+size - 1] = 0;
                return;
                /*
              
                int i = offset + size - 1;
                buffer[i] = 0;
                while (--i >= offset) // old way is sometimes the fastest
                {
                    buffer[i] = (byte)('0' + (v % @base));
                    v /= @base;
                }
                buffer[i] = 0;
                */
            }
            public int WriteString(byte[] buffer, string str)
            {
                clear(buffer);
               // Encoding.ASCII.GetBytes(str, 0, str.Length, buffer, offset);
              //  buffer[i] = 0;
                int i = offset;
                int s = 0;
                while (i < (size + offset - 1) && s < str.Length) buffer[i++] = (byte)str[s++];
                buffer[i] = 0;
                return i;
            }
            public int WriteString(byte[] buffer, string str, Func<char,char> convert)
            {
                clear(buffer);
                int i = offset;
                int s = 0;
                while (i < (size + offset - 1) && s < str.Length) buffer[i++] = (byte)convert(str[s++]);
                buffer[i] = 0;
                return i;
            }
            public string ReadString(byte[] buffer)
            {
                StringBuilder sb = new StringBuilder(size);
                for (int i = offset; i < (size + offset - 1); i++)
                    if (buffer[i] == 0) break;
                    else sb.Append((char)buffer[i]);
                return sb.ToString();
            }
        };
        static readonly FieldInfo NameField = new FieldInfo(0, 100);
        static readonly FieldInfo ModeField = new FieldInfo(100, 8);
        static readonly FieldInfo UidField = new FieldInfo(108, 8);
        static readonly FieldInfo GidField = new FieldInfo(116, 8);
        static readonly FieldInfo SizeField = new FieldInfo(124, 12);
        static readonly FieldInfo MTimeField = new FieldInfo(136, 12);
        static readonly FieldInfo CheckSumField = new FieldInfo(148, 8);
        static readonly FieldInfo TypeFlagField = new FieldInfo(156, 1);
        static readonly FieldInfo LinkNameField = new FieldInfo(157, 100);

        static readonly FieldInfo MagicField = new FieldInfo(257, 6);
        static readonly FieldInfo versionField = new FieldInfo(263, 2);
        static readonly FieldInfo unameField = new FieldInfo(265, 32);
        static readonly FieldInfo gnameField = new FieldInfo(297, 32);
        static readonly FieldInfo devmajorField = new FieldInfo(329, 8);
        static readonly FieldInfo devminorField = new FieldInfo(337, 8);
        static readonly FieldInfo prefixField = new FieldInfo(345, 155);
    
        private readonly byte[] buffer = new byte[512];
        private bool dirty = true;
        public void CopyTo(byte[] data, long offset)
        {
            RecaculateChecksum();
            buffer.CopyTo(data, offset);
        }
        public void CopyTo(Stream stream)
        {
            if (!stream.CanWrite) throw new TarException("Cannot write header to stream");
            RecaculateChecksum();
            stream.Write(buffer, 0, buffer.Length);
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{ Name : '{0}',  ", Name);
            sb.AppendFormat("Type : '{0}', ", EntryType.ToString());
            sb.AppendFormat("Size : {0} }", SizeInBytes);
            return sb.ToString();
        }
        public void Clear()
        {
            Array.Clear(buffer, 0, buffer.Length);
            ModeField.WriteValue(buffer, 0);
            UidField.WriteValue(buffer, 0);
            GidField.WriteValue(buffer, 0);
            SizeField.WriteValue(buffer, 0);
            MTimeField.WriteValue(buffer, 0);
            CheckSumField.WriteValue(buffer, 0);
            TypeFlagField.clear(buffer);
            ModeField.WriteValue(buffer, 0);
            MagicField.WriteString(buffer, "ustar");
            versionField.fill(buffer, '0');
            spaces.CopyTo(buffer, 148); // spaces in checksum?
            dirty = true;
        }
        public TarHeader() { Clear();  }
        public TarHeader(DirectoryInfo info)
        {
            Write(info);
        }
        protected readonly DateTime TheEpoch = new DateTime(1970, 1, 1, 0, 0, 0);
        protected readonly byte[] Magic = Encoding.ASCII.GetBytes("ustar");
        public EntryType EntryType {
            get
            {
                return (EntryType)buffer[156]; 
            }
            set
            {
                buffer[156] = (byte)value;
                dirty = true;
            }
        }
        private static byte[] spaces = Encoding.ASCII.GetBytes("        ");

        public string Name
        {
            get
            {
                return NameField.ReadString(buffer);
            }
        }
        public string DirectoryName
        {
            set
            {
                if (value.Length > 100) throw new TarException("A file name can not be more than 100 chars long");
                int last = NameField.WriteString(buffer, value, c => c == '\\' ? '/' : c); // change the dir 
                if (last > 0 && value.Length > 0 && buffer[last-1] != '/') buffer[last] = (byte)'/';
                EntryType = EntryType.Directory;    
                    Mode = 0x41FF;
                dirty = true;
            }
        }
        public virtual string FileName
        {
            set
            {
                if (value.Length > 100) throw new TarException("A file name can not be more than 100 chars long");
                NameField.WriteString(buffer, value); // change the dir 
                EntryType = EntryType.File;
                Mode = 0x81FF;
                dirty = true;
            }
        }
        public int Mode {
            get
            {
                return (int)ModeField.ReadValue(buffer);
            }
            set
            {
                ModeField.WriteValue(buffer, value);
                dirty = true;
            }
        }
        public int UserID
        {
            get
            {
                return (int)UidField.ReadValue(buffer);
            }
            set
            {
                UidField.WriteValue(buffer, value);
                dirty = true;
            }
        }
        public int GroupID
        {
            get
            {
                return (int)GidField.ReadValue(buffer);
            }
            set
            {
                GidField.WriteValue(buffer, value);
                dirty = true;
            }
        }

        public long SizeInBytes {
            get
            {
                return SizeField.ReadValue(buffer);
            }
            set
            {
                SizeField.WriteValue(buffer, value);
                dirty = true;
            }
        }

        public DateTime LastModification
        {
            get
            {
                return TheEpoch.AddSeconds(MTimeField.ReadValue(buffer));
            }
            set
            {
                MTimeField.WriteValue(buffer, (long)(value - TheEpoch).TotalSeconds);
                dirty = true;
            }
        }

        void RecaculateChecksum()
        {
            if (dirty)
            {
                spaces.CopyTo(buffer, 148);
                int checksum = RecalculateAltChecksum(buffer);
                CheckSumField.WriteValue(buffer, checksum);
                dirty = false;
            }
        }
        public int Checksum
        {
            get
            {
                RecaculateChecksum();
                return (int)CheckSumField.ReadValue(buffer);
            }
        }

        public virtual int HeaderSize
        {
            get { return 512; }
        }


        private int RecalculateAltChecksum(byte[] buf)
        {
            int headerChecksum = 0;
            foreach (byte b in buf)
            {
                if ((b & 0x80) == 0x80)
                {
                    headerChecksum -= b ^ 0x80;
                }
                else
                {
                    headerChecksum += b;
                }
            }
            return headerChecksum;
        }

        public void Write(DirectoryInfo info)
        {
            Clear();
            this.DirectoryName = info.Name;
            this.LastModification = info.LastWriteTime;
            this.SizeInBytes = 0;
            this.UserID = 0;
            this.GroupID = 0;
            this.EntryType = EntryType.Directory;
            this.Mode = 0x41FF;
        }
        public void Write(FileInfo info)
        {
            Clear();
            this.FileName = info.Name;
            this.LastModification = info.LastWriteTime;
            this.SizeInBytes = info.Length;
            this.UserID = 0;
            this.GroupID = 0;
            this.EntryType = EntryType.File;
            this.Mode = 0x81FF;
        }
    }
    class Info
    {
        public DirectoryInfo dir;
        public string path;
        public long offset;
        public void Write(byte[] buffer)
        {
            long pos = offset;
            TarHeader header = new TarHeader();
            if (!string.IsNullOrWhiteSpace(path))
            {
                header.DirectoryName = path;
                header.LastModification = dir.LastWriteTime;
                header.CopyTo(buffer, pos);
                pos += header.HeaderSize;
            }
            foreach (var file in dir.GetFiles())
            {
                header.FileName = file.Name;
                header.LastModification = file.LastWriteTime;
                //  header.Write(file);
                header.CopyTo(buffer, pos);
                pos += header.HeaderSize;
                using (var stream = file.OpenRead())
                    stream.Read(buffer, (int)pos, (int)file.Length);
                pos += (int)file.Length;
            }
        }
    }

    class TarFucker
    {
        TarHeader header = new TarHeader();
        MemoryStream ms = new MemoryStream();
        byte[] buffer = new byte[4096];
        string root_path;
        public TarFucker(string root_path)
        {
            ms = new MemoryStream();
            this.root_path = root_path;
            WriteDirectory(root_path, true);
        }
        public void WriteDirectoryEntry(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            path = path.Replace(root_path, string.Empty).Replace(Path.DirectorySeparatorChar, '/');
            if (path.Length > 0 && path[0] == '/') path = path.Substring(1);
            DateTime lastWriteTime = Directory.Exists(path) ? Directory.GetLastWriteTime(path) : DateTime.Now;
            header.DirectoryName = path;
            header.LastModification = lastWriteTime;
            header.CopyTo(ms);
        }
        public void WriteDirectory(string directory, bool doRecursive)
        {
            if (string.IsNullOrEmpty(directory))  throw new ArgumentNullException("directory");
            WriteDirectoryEntry(directory);
            string[] files = Directory.GetFiles(directory);
            foreach (var fileName in files) WriteFile(fileName);
            string[] directories = Directory.GetDirectories(directory);
            foreach (var dirName in directories)
            {
                WriteDirectoryEntry(dirName);
                if (doRecursive) WriteDirectory(dirName, true);
            }
        }
        public void AlignTo512(long size, bool acceptZero)
        {
            size = size % 512;
            if (size == 0 && !acceptZero) return;
            while (size < 512)
            {
                ms.WriteByte(0);
                size++;
            }
        }
        public void WriteFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentNullException("fileName");
          
            using (FileStream file = File.OpenRead(fileName))
            {
                header.FileName = Path.GetFileName(fileName);
                header.LastModification = File.GetLastWriteTime(file.Name);
                header.CopyTo(ms);
                if(buffer.Length < file.Length) buffer = new byte[file.Length * 2];
                int value = file.Read(buffer, 0, (int)file.Length);
                if (value != file.Length) throw new TarException("FUCK ME");
                ms.Write(buffer, 0, (int)file.Length);
                AlignTo512((int)file.Length, false);
            }
        }
        public  void Close()
        {
            AlignTo512(0, true);
            AlignTo512(0, true);
            using (FileStream test = File.Create("test.tar"))
            {
                var b = ms.GetBuffer();
                test.Write(b, 0, b.Length);
            }
            ms = new MemoryStream((int)ms.Length);
        }
    }
    // memroy ineffecent as we store it completey in memory but have to for preformance
    class TarMaker
    {
      
        byte[] tar_file;
        List<Info> dirs;
        string root_path;
        long m_max_size_of_all_files;
        public TarMaker(string dirPath)
        {
            m_max_size_of_all_files = 0;
            DirectoryInfo root = new DirectoryInfo(dirPath);
            root_path = root.FullName;
            dirs = new List<Info>();
            GetDirectorys(root);
            m_max_size_of_all_files += m_max_size_of_all_files % 512;
           // size % 512
            tar_file = new byte[m_max_size_of_all_files];
            List<Task> tasks = new List<Task>();
            DateTime start = DateTime.Now;
            //   Parallel.ForEach()
            // Parallel.ForEach(dirs, i => i.Write(tar_file));
            
            foreach (var i in dirs) {
             //   tasks.Add(Task.Run(()=> i.Write(tar_file)));
                i.Write(tar_file);
            } // not much faster than just running it in an array
           // Task.WaitAll(tasks.ToArray());
            using (FileStream test = File.Create("test.tar"))
            {
                test.Write(tar_file, 0, tar_file.Length);
            }
            var diff = DateTime.Now - start;

            System.Console.WriteLine("Done in {0}ms", diff.Milliseconds);
        }
        public void AlignTo512(long size, bool acceptZero)
        {
            size = size % 512;
            if (size == 0 && !acceptZero) return;
            while (size < 512)
            {
              //  ms.WriteByte
                size++;
            }
        }
        void GetDirectorys(DirectoryInfo root)
        {
            try
            {
                string path = root.FullName.Replace(root_path, string.Empty);
                if (path.Length > 0 && path.First() == '\\') path = path.Substring(1);
                dirs.Add(new Info() { dir = root, path = path, offset = m_max_size_of_all_files });
                m_max_size_of_all_files += 512;
                foreach (var file in root.GetFiles()) m_max_size_of_all_files += file.Length + 512;
                foreach (var dir in root.GetDirectories()) GetDirectorys(dir); 
            }
            catch (UnauthorizedAccessException UAEx)
            {
                throw UAEx;
            }
            catch (PathTooLongException PathEx)
            {
                throw PathEx;
            }
        }
    }
}
