using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.Dissasembler;
using GameMaker.Ast;
using System.IO;
using System.Threading;
using GameMaker.Writers;
using System.Text.RegularExpressions;
using System.Web.Configuration;
using System.Security.AccessControl;
using System.Security.Principal;

namespace GameMaker
{
    public enum OutputType
    {
        LoveLua,
        GameMaker
    }
    public enum UndertaleVersion
    {
        V10000,
        V10001
    }

    public static class Context 
    {
      


        public static Regex ScriptArgRegex = new Regex(@"argument(\d+)", RegexOptions.Compiled);


        // found this regex http://stackoverflow.com/questions/62771/how-do-i-check-if-a-given-string-is-a-legal-valid-file-name-under-windows/62888#62888
        // not to shabby
        static Regex validatePath = new Regex("(^(PRN|AUX|NUL|CON|COM[1-9]|LPT[1-9]|(\\.+)$)(\\..*)?$)|(([\\x00-\\x1f\\\\?*:\";‌​|/<>‌​])+)|([\\. ]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // works too
        //   Regex containsABadCharacter = new Regex("["
        //     + Regex.Escape(new string(System.IO.Path.GetInvalidPathChars())) + "]");
        //  if (containsABadCharacter.IsMatch(testName)) { return false; };

        //http://stackoverflow.com/questions/1410127/c-sharp-test-if-user-has-write-access-to-a-folder
        public static bool HasFolderWritePermission(string destDir)
        {
            do
            {
                if (string.IsNullOrEmpty(destDir) || !Directory.Exists(destDir)) break;
                try
                {
                    DirectorySecurity security = Directory.GetAccessControl(destDir);
                    SecurityIdentifier users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                    foreach (System.Security.AccessControl.AuthorizationRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                    {
                        if (rule.IdentityReference == users)
                        {
                            FileSystemAccessRule rights = ((FileSystemAccessRule)rule);
                            if (rights.AccessControlType == AccessControlType.Allow)
                            {
                                if (rights.FileSystemRights == (rights.FileSystemRights | FileSystemRights.Modify | FileSystemRights.CreateDirectories | FileSystemRights.CreateFiles | FileSystemRights.Delete)) return true;
                            }
                        }
                    }
                    break;
                }
                catch
                {
                    break;
                }
            } while (false);
            // ok, so the failsafe is to just try to create a directory, try to create a file in that directory, delete the file, then the directory.
            // if we fail THAT then we know we cannot do it
            try
            {
                string tempDir = Path.Combine(destDir,Path.GetRandomFileName());
                var dir = Directory.CreateDirectory(tempDir);// can we create a folder
                string tempFilename = Path.Combine(dir.FullName, Path.GetRandomFileName());
                using (FileStream file = new FileStream(Path.Combine(dir.FullName,  tempFilename),FileMode.CreateNew)) {
                    file.WriteByte(0); // can we write?
                    file.Position = 0;
                    file.ReadByte(); // can we read?
                }
                System.IO.File.Delete(tempFilename); // we can delete
                dir.Delete();
                return true; // its all good
            } catch(Exception e)
            {
               
                Context.Error(e);
                Context.FatalError("Do not have write permissions for '{0}'", destDir);
            }
            return false;
        }
        static HashSet<string> directoryDeleted = new HashSet<string>();
        // ugh, this could be called in a thread, so be sure to lock directoryDeleted
        public static string DeleteAllAndCreateDirectory(string dir, bool overrideDelete=false)
        {
            string exePath = Context.outputDirectory.FullName;
            string path = Path.Combine(exePath, dir);
            try
            {
                if (!directoryDeleted.Contains(path))
                {
                    lock (directoryDeleted)
                    {
                        if (!directoryDeleted.Contains(path)) // I can't beleve I ran into this.  check again in the lock incase 
                        {
                            if (!Directory.Exists(path))
                            {
                                Directory.CreateDirectory(path);
                                Context.Info("Creating directory '{0}' ", path);
                            } else if (Context.deleteDirectorys)
                            {
                                Directory.Delete(path, true);
                                Context.Info("Clearing old '{0}'  directory", path);
                            }
                            directoryDeleted.Add(path);
                        }
                    }

                }
            }
            catch (Exception e)
            {
                Context.Error("Exception caught trying to crate '{0}' directory", path);
                Context.FatalError(e);
                throw e;
            }
            return path;
        }
        // Used to create a file stream to make sure its created in the correct path
        public static FileStream CreateFileStream(string filename, FileMode mode, bool fix_path)
        {
            FileStream file = null;
            try
            {
                if (fix_path) filename = Path.Combine(outputDirectory.FullName, filename);
                file = new FileStream(filename, mode);
                return file;
            }
            catch (Exception e)
            {
                Context.Error(e);
                Context.FatalError("Could not create '{0}'", filename);
            }
            Context.Info("Wrote file '{0}'", filename);
            return file;
        }
        public static StreamWriter CreateStreamWriter(string filename, bool fix_path)
        {
            return new StreamWriter(CreateFileStream(filename, FileMode.Create, fix_path));
        }
        public static StreamWriter CreateStreamReader(string filename, bool fix_path)
        {
            return new StreamWriter(CreateFileStream(filename, FileMode.Open,fix_path));
        }
        public static void CheckAndSetOutputDirectory(string path=null)
        {
            if (outputDirectory != null) return; // we have our output directory anyway
            if (string.IsNullOrWhiteSpace(path)) path = Environment.CurrentDirectory;//    Directory.GetCurrentDirectory();
            if ((outputDirectory != null && outputDirectory.FullName == path)) return; // don't do anything
            var badchars = Path.GetInvalidPathChars();
            for(int i = 0; i < path.Length; i++)
            {
                char c = path[i];
                if (badchars.Contains(c))
                {
                    Context.Error("{0}", path);
                    Context.Error(new string(' ', i) + "^");
                    Context.FatalError("Invalid Char in Output Path:");
                }
            }

#if false
            // The validate dosn't work? humm
            Match match = validatePath.Match(path);
            if (match.Success)
            {
                Context.Error("{0}", path);
                Context.Error(new string(' ',match.Length ) + "^");
                Context.FatalError("Invalid Char in Output Path:");
            }
#endif
            if(!Directory.Exists(path)) Context.FatalError("Path Does not exisit '{0}', striping filename", path);
            HasFolderWritePermission(path); // we drop the application in this case
            outputDirectory = new DirectoryInfo(path);

            // whew, path string is valid lets verify we can write to the directory
            // 
        }
        public static string FormatDebugOffset(byte[] data, int index)
        {
            short svalue = BitConverter.ToInt16(data, index);
            int ivalue = BitConverter.ToInt32(data, index);
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Offset=0x{0:X8} ", index);
            sb.AppendFormat("Int32(0x{0:X8},0) ", ivalue);
            sb.AppendFormat("Int16(0x{0:X4},0) ", svalue);
            sb.AppendFormat("Raw(");
            for(int i = index;i < (index+4); i++)
            {
                if (i != 0) sb.Append(',');
                sb.AppendFormat("0x{0:X2}",data[i]);
            }
            sb.Append(")");
            return sb.ToString();
        }
        static public CancellationToken ct;
        static public DirectoryInfo outputDirectory = null;
        static public bool saveChangedDataWin = false;
        static public bool dataWinChanged = false;
        static public bool deleteDirectorys = false;
        static public bool doLua = false;
        static public bool oneFile = false;
        static public bool doSearch = false;
        static public bool debugSearch = false;
        static public bool doAssigmentOffsets = false;
        static public bool doGlobals = true;
        static public bool makeObject = false;
        static public bool doXML = true;
        static public bool saveAllPngs = false;
        static public bool saveAllMasks = false;
        static public OutputType outputType = OutputType.GameMaker;
        static public bool doAsm = false;
        static public bool doThreads = true;
        static public bool Debug = false;
        static public UndertaleVersion Version = UndertaleVersion.V10001;

        public static bool HasFatalError { get; private set; }

        const string ObjectNameHeader = "gml_Object_";
        public static void Info(string msg, params object[] o)
        {
            ErrorContext.Out.Info(msg, o);
        }
        public static void Message(string msg, params object[] o)
        {
            ErrorContext.Out.Message(msg, o);
        }
        public static void Warning(string msg, params object[] o)
        {
            ErrorContext.Out.Warning(msg, o);
        }
        public static void Error(string msg, params object[] o)
        {
            ErrorContext.Out.Error(msg, o);
        }
        public static void Error(Exception e)
        {
            Context.Error("Exception: {0}",  e.Message);
            if (e.InnerException != null) Context.Error("Inner Exception: {0}",  e.Message);
            Context.Error("Stack Trace:\r\n{0}",  e.StackTrace);
            Context.Error("Source: {0}",  e.Source);
        }
        public static void FatalError(Exception e)
        {
            Error(e);
            Context.FatalError("Exception was fatal");
        }
        public static void Error(string name, Exception e)
        {
            Context.Error("{0}: Exception: {1}", name, e.Message);
            if (e.InnerException != null) Context.Error("{0}: Inner Exception: {1}", name, e.Message);
            Context.Error("{0}: Stack Trace: {1}", name, e.StackTrace);
            Context.Error("{0}: Source: {1}", name, e.Source);
        }
        public static void FatalError(string name, Exception e)
        {
            Error(name, e);
            Context.FatalError("Exception was fatal");
        }
        public static void FatalError(string msg, params object[] o)
        {
            ErrorContext.Out.FatalError(msg, o);
        }
        public static void Info(string msg, ILNode node, params object[] o)
        {
            ErrorContext.Out.Info(msg,node, o);
        }
        public static void Warning(string msg, ILNode node, params object[] o)
        {
            ErrorContext.Out.Warning(msg, node, o);
        }
        public static void Error(string msg, ILNode node, params object[] o)
        {
            ErrorContext.Out.Error(msg, node, o);
        }
        public static void FatalError(string msg, ILNode node, params object[] o)
        {
            ErrorContext.Out.FatalError(msg, node, o);
        }
        public static string ChangeEndOfFileName(string filename, string toAdd)
        {
            return Path.ChangeExtension((Path.GetFileNameWithoutExtension(filename) + toAdd), Path.GetExtension(filename));
        }
        public static string TimeStamp
        {
            get
            {
                DateTime now = DateTime.Now;
                return now.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        public static string DateTimeStamp
        {
            get
            {
                DateTime now = DateTime.Now;
                return now.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        private static HashSet<string> old_files_moved = new HashSet<string>();

        public static string MoveFileToOldErrors(string dfilename,bool move = true)
        {
            System.Diagnostics.Debug.Assert(dfilename == Path.GetFileName(dfilename));
            string ffilename = Path.Combine(outputDirectory.FullName, dfilename);
            if (move && !old_files_moved.Contains(ffilename))
            {
                lock (old_files_moved) // hack for now
                {
                    if (move && !old_files_moved.Contains(ffilename))
                    {
                        old_files_moved.Add(ffilename);
                        if (System.IO.File.Exists(ffilename))
                        {
                            string old_errors_path = Context.DeleteAllAndCreateDirectory("old_errors", true);
                            old_errors_path = Path.Combine(old_errors_path, dfilename);
                            // Search for a good file name
                            for (int count = 0; System.IO.File.Exists(old_errors_path = ChangeEndOfFileName(dfilename, "_" + count)); count++) ;
                            System.IO.File.Move(ffilename, old_errors_path); // move it
                        }
                    }
                }
            }
            return ffilename;
        }
        static Context()
        {
        }
        static public string MakeDebugFileName(File.Code code, string file,bool move = true)
        {
            return MakeDebugFileName(code.Name, file,move);
        }
        static public string MakeDebugFileName(string code_name, string file, bool move = true)
        {
            string filename = Path.GetFileNameWithoutExtension(file);
            filename = code_name + "_" + filename + Path.GetExtension(file);
            string path = Path.Combine(Context.outputDirectory.FullName, filename);

          //  filename = file.Replace(Path.GetFileName(file), filename); // so we keep any path information

            return move ? MoveFileToOldErrors(path) : path;
        }
        static public string MakeDebugFileName(string filename, bool move = true)
        {
            string path = Path.Combine(Context.outputDirectory.FullName, filename);
            return move ? MoveFileToOldErrors(path) : path;
        }
        public static string EscapeChar(char v)
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
        static IEnumerable<string> InternalSimpleEscape(string s)
        {
            yield return "\"";
            foreach (var c in s) yield return EscapeChar(c);
            yield return "\"";
        }
        // escapes the string and appeneds it
        public static void EscapeAndAppend(this StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s) sb.Append(EscapeChar(c));
            sb.Append('"');
        }
       
        public static string EscapeString(string s)
        {
            return string.Concat(InternalSimpleEscape(s));
        }

        static public string LookupString(int index, bool escape = false)
        {
            index &= 0x1FFFFF;
            return escape ? Context.EscapeString(File.Strings[index].String)  : File.Strings[index].String ;
        }
        static public string InstanceToString(int instance)
        {
            
            switch (instance)
            {
                case 0: return "stack";
                case -1:
                    return "self";
                case -2:
                    return "other";
                case -3:
                    return "all";
                case -4:
                    return "noone";
                case -5:
                    return "global";
                default:
                    if (instance < 0 || instance >= File.Objects.Count) throw new ArgumentException("Instance out of range", "instance");
                    return File.Objects[instance].Name;
            }
        }
        static public string InstanceToString(ILValue value)
        {
            if (value.Type == GM_Type.Short || value.Type == GM_Type.Int)
            {
                return InstanceToString((int)value);
            }
            throw new ArgumentException("Bad ILValue type","value");
        }

        public static string KeyToString(int key)
        {
            string str = null;
            switch (key)
            {
                case 0:
                    {
                        str = "NOKEY";
                        break;
                    }
                case 1:
                    {
                        str = "ANYKEY";
                        break;
                    }
                case 8:
                    {
                        str = "BACKSPACE";
                        break;
                    }
                case 9:
                    {
                        str = "TAB";
                        break;
                    }
                case 13:
                    {
                        str = "ENTER";
                        break;
                    }
                case 16:
                    {
                        str = "SHIFT";
                        break;
                    }
                case 17:
                    {
                        str = "CTRL";
                        break;
                    }
                case 18:
                    {
                        str = "ALT";
                        break;
                    }
                case 19:
                    {
                        str = "PAUSE";
                        break;
                    }
                case 27:
                    {
                        str = "ESCAPE";
                        break;
                    }
                case 32:
                    {
                        str = "SPACE";
                        break;
                    }
                case 33:
                    {
                        str = "PAGEUP";
                        break;
                    }
                case 34:
                    {
                        str = "PAGEDOWN";
                        break;
                    }
                case 35:
                    {
                        str = "END";
                        break;
                    }
                case 36:
                    {
                        str = "HOME";
                        break;
                    }
                case 37:
                    {
                        str = "LEFT";
                        break;
                    }
                case 38:
                    {
                        str = "UP";
                        break;
                    }
                case 39:
                    {
                        str = "RIGHT";
                        break;
                    }
                case 40:
                    {
                        str = "DOWN";
                        break;
                    }
                case 45:
                    {
                        str = "INSERT";
                        break;
                    }
                case 46:
                    {
                        str = "DELETE";
                        break;
                    }
                case 48:
                    {
                        str = "0";
                        break;
                    }
                case 49:
                    {
                        str = "1";
                        break;
                    }
                case 50:
                    {
                        str = "2";
                        break;
                    }
                case 51:
                    {
                        str = "3";
                        break;
                    }
                case 52:
                    {
                        str = "4";
                        break;
                    }
                case 53:
                    {
                        str = "5";
                        break;
                    }
                case 54:
                    {
                        str = "6";
                        break;
                    }
                case 55:
                    {
                        str = "7";
                        break;
                    }
                case 56:
                    {
                        str = "8";
                        break;
                    }
                case 57:
                    {
                        str = "9";
                        break;
                    }
                case 65:
                    {
                        str = "A";
                        break;
                    }
                case 66:
                    {
                        str = "B";
                        break;
                    }
                case 67:
                    {
                        str = "C";
                        break;
                    }
                case 68:
                    {
                        str = "D";
                        break;
                    }
                case 69:
                    {
                        str = "E";
                        break;
                    }
                case 70:
                    {
                        str = "F";
                        break;
                    }
                case 71:
                    {
                        str = "G";
                        break;
                    }
                case 72:
                    {
                        str = "H";
                        break;
                    }
                case 73:
                    {
                        str = "I";
                        break;
                    }
                case 74:
                    {
                        str = "J";
                        break;
                    }
                case 75:
                    {
                        str = "K";
                        break;
                    }
                case 76:
                    {
                        str = "L";
                        break;
                    }
                case 77:
                    {
                        str = "M";
                        break;
                    }
                case 78:
                    {
                        str = "N";
                        break;
                    }
                case 79:
                    {
                        str = "O";
                        break;
                    }
                case 80:
                    {
                        str = "P";
                        break;
                    }
                case 81:
                    {
                        str = "Q";
                        break;
                    }
                case 82:
                    {
                        str = "R";
                        break;
                    }
                case 83:
                    {
                        str = "S";
                        break;
                    }
                case 84:
                    {
                        str = "T";
                        break;
                    }
                case 85:
                    {
                        str = "U";
                        break;
                    }
                case 86:
                    {
                        str = "V";
                        break;
                    }
                case 87:
                    {
                        str = "W";
                        break;
                    }
                case 88:
                    {
                        str = "X";
                        break;
                    }
                case 89:
                    {
                        str = "Y";
                        break;
                    }
                case 90:
                    {
                        str = "Z";
                        break;
                    }
                case 96:
                    {
                        str = "NUM_0";
                        break;
                    }
                case 97:
                    {
                        str = "NUM_1";
                        break;
                    }
                case 98:
                    {
                        str = "NUM_2";
                        break;
                    }
                case 99:
                    {
                        str = "NUM_3";
                        break;
                    }
                case 100:
                    {
                        str = "NUM_4";
                        break;
                    }
                case 101:
                    {
                        str = "NUM_5";
                        break;
                    }
                case 102:
                    {
                        str = "NUM_6";
                        break;
                    }
                case 103:
                    {
                        str = "NUM_7";
                        break;
                    }
                case 104:
                    {
                        str = "NUM_8";
                        break;
                    }
                case 105:
                    {
                        str = "NUM_9";
                        break;
                    }
                case 106:
                    {
                        str = "NUM_STAR";
                        break;
                    }
                case 107:
                    {
                        str = "NUM_PLUS";
                        break;
                    }
                case 109:
                    {
                        str = "NUM_MINUS";
                        break;
                    }
                case 110:
                    {
                        str = "NUM_DOT";
                        break;
                    }
                case 111:
                    {
                        str = "NUM_DIV";
                        break;
                    }
                case 112:
                    {
                        str = "F1";
                        break;
                    }
                case 113:
                    {
                        str = "F2";
                        break;
                    }
                case 114:
                    {
                        str = "F3";
                        break;
                    }
                case 115:
                    {
                        str = "F4";
                        break;
                    }
                case 116:
                    {
                        str = "F5";
                        break;
                    }
                case 117:
                    {
                        str = "F6";
                        break;
                    }
                case 118:
                    {
                        str = "F7";
                        break;
                    }
                case 119:
                    {
                        str = "F8";
                        break;
                    }
                case 120:
                    {
                        str = "F9";
                        break;
                    }
                case 121:
                    {
                        str = "F10";
                        break;
                    }
                case 122:
                    {
                        str = "F11";
                        break;
                    }
                case 123:
                    {
                        str = "F12";
                        break;
                    }
                case 144:
                    {
                        str = "NUM_LOCK";
                        break;
                    }
                case 145:
                    {
                        str = "SCROLL_LOCK";
                        break;
                    }

                case 186:
                    {
                        str = "SEMICOLON";
                        break;
                    }
                case 187:
                    {
                        str = "PLUS";
                        break;
                    }
                case 188:
                    {
                        str = "COMMA";
                        break;
                    }
                case 189:
                    {
                        str = "MINUS";
                        break;
                    }
                case 190:
                    {
                        str = "FULLSTOP";
                        break;
                    }
                case 191:
                    {
                        str = "FWSLASH";
                        break;
                    }
                case 192:
                    {
                        str = "AT";
                        break;
                    }

                case 219:
                    {
                        str = "RIGHTSQBR";
                        break;
                    }
                case 220:
                    {
                        str = "BKSLASH";
                        break;
                    }
                case 221:
                    {
                        str = "LEFTSQBR";
                        break;
                    }
                case 222:
                    {
                        str = "HASH";
                        break;
                    }
                case 223:
                    {
                        str = "TILD";
                        break;
                    }
            }
            if(str == null)
            {
                str = "UNKNOWN(" + key.ToString() + ")";
            }
            return str;
        }
        public static string EventToString(int @event, int subevent)
        {
            string str;
            switch (@event)
            {
                case 0:
                    {
                        str = "CreateEvent";
                        break;
                    }
                case 1:
                    {
                        str = "DestroyEvent";
                        break;
                    }
                case 2:
                    {
                        switch (subevent)
                        {
                            case 0:
                                {
                                    str = "ObjAlarm0";
                                    break;
                                }
                            case 1:
                                {
                                    str = "ObjAlarm1";
                                    break;
                                }
                            case 2:
                                {
                                    str = "ObjAlarm2";
                                    break;
                                }
                            case 3:
                                {
                                    str = "ObjAlarm3";
                                    break;
                                }
                            case 4:
                                {
                                    str = "ObjAlarm4";
                                    break;
                                }
                            case 5:
                                {
                                    str = "ObjAlarm5";
                                    break;
                                }
                            case 6:
                                {
                                    str = "ObjAlarm6";
                                    break;
                                }
                            case 7:
                                {
                                    str = "ObjAlarm7";
                                    break;
                                }
                            case 8:
                                {
                                    str = "ObjAlarm8";
                                    break;
                                }
                            case 9:
                                {
                                    str = "ObjAlarm9";
                                    break;
                                }
                            case 10:
                                {
                                    str = "ObjAlarm10";
                                    break;
                                }
                            case 11:
                                {
                                    str = "ObjAlarm11";
                                    break;
                                }
                            default:
                                {
                                    str = "unknown";
                                    break;
                                }
                        }
                        break;
                    }
                case 3:
                    {
                        switch (subevent)
                        {
                            case 0:
                                {
                                    str = "StepNormalEvent";
                                    break;
                                }
                            case 1:
                                {
                                    str = "StepBeginEvent";
                                    break;
                                }
                            case 2:
                                {
                                    str = "StepEndEvent";
                                    break;
                                }
                            default:
                                {
                                    str = "unknown";
                                    break;
                                }
                        }
                        break;
                    }
                case 4:
                    {
                        str = "CollisionEvent_";
                        str += subevent.ToString();
                        break;
                    }
                case 5:
                    {
                        str = string.Concat("Key_", KeyToString(subevent));
                        break;
                    }
                case 6:
                    {
                        switch (subevent)
                        {
                            case 0:
                                {
                                    str = "LeftButtonDown";
                                    break;
                                }
                            case 1:
                                {
                                    str = "RightButtonDown";
                                    break;
                                }
                            case 2:
                                {
                                    str = "MiddleButtonDown";
                                    break;
                                }
                            case 3:
                                {
                                    str = "NoButtonPressed";
                                    break;
                                }
                            case 4:
                                {
                                    str = "LeftButtonPressed";
                                    break;
                                }
                            case 5:
                                {
                                    str = "RightButtonPressed";
                                    break;
                                }
                            case 6:
                                {
                                    str = "MiddleButtonPressed";
                                    break;
                                }
                            case 7:
                                {
                                    str = "LeftButtonReleased";
                                    break;
                                }
                            case 8:
                                {
                                    str = "RightButtonReleased";
                                    break;
                                }
                            case 9:
                                {
                                    str = "MiddleButtonReleased";
                                    break;
                                }
                            case 10:
                                {
                                    str = "MouseEnter";
                                    break;
                                }
                            case 11:
                                {
                                    str = "MouseLeave";
                                    break;
                                }
                            case 12:
                            case 13:
                            case 14:
                            case 15:
                            case 20:
                            case 29:
                            case 30:
                            case 35:
                            case 44:
                            case 45:
                            case 46:
                            case 47:
                            case 48:
                            case 49:
                            case 59:
                                {
                                    str = "unknown";
                                    break;
                                }
                            case 16:
                                {
                                    str = "Joystick1Left";
                                    break;
                                }
                            case 17:
                                {
                                    str = "Joystick1Right";
                                    break;
                                }
                            case 18:
                                {
                                    str = "Joystick1Up";
                                    break;
                                }
                            case 19:
                                {
                                    str = "Joystick1Down";
                                    break;
                                }
                            case 21:
                                {
                                    str = "Joystick1Button1";
                                    break;
                                }
                            case 22:
                                {
                                    str = "Joystick1Button2";
                                    break;
                                }
                            case 23:
                                {
                                    str = "Joystick1Button3";
                                    break;
                                }
                            case 24:
                                {
                                    str = "Joystick1Button4";
                                    break;
                                }
                            case 25:
                                {
                                    str = "Joystick1Button5";
                                    break;
                                }
                            case 26:
                                {
                                    str = "Joystick1Button6";
                                    break;
                                }
                            case 27:
                                {
                                    str = "Joystick1Button7";
                                    break;
                                }
                            case 28:
                                {
                                    str = "Joystick1Button8";
                                    break;
                                }
                            case 31:
                                {
                                    str = "Joystick2Left";
                                    break;
                                }
                            case 32:
                                {
                                    str = "Joystick2Right";
                                    break;
                                }
                            case 33:
                                {
                                    str = "Joystick2Up";
                                    break;
                                }
                            case 34:
                                {
                                    str = "Joystick2Down";
                                    break;
                                }
                            case 36:
                                {
                                    str = "Joystick2Button1";
                                    break;
                                }
                            case 37:
                                {
                                    str = "Joystick2Button2";
                                    break;
                                }
                            case 38:
                                {
                                    str = "Joystick2Button3";
                                    break;
                                }
                            case 39:
                                {
                                    str = "Joystick2Button4";
                                    break;
                                }
                            case 40:
                                {
                                    str = "Joystick2Button5";
                                    break;
                                }
                            case 41:
                                {
                                    str = "Joystick2Button6";
                                    break;
                                }
                            case 42:
                                {
                                    str = "Joystick2Button7";
                                    break;
                                }
                            case 43:
                                {
                                    str = "Joystick2Button8";
                                    break;
                                }
                            case 50:
                                {
                                    str = "GlobalLeftButtonDown";
                                    break;
                                }
                            case 51:
                                {
                                    str = "GlobalRightButtonDown";
                                    break;
                                }
                            case 52:
                                {
                                    str = "GlobalMiddleButtonDown";
                                    break;
                                }
                            case 53:
                                {
                                    str = "GlobalLeftButtonPressed";
                                    break;
                                }
                            case 54:
                                {
                                    str = "GlobalRightButtonPressed";
                                    break;
                                }
                            case 55:
                                {
                                    str = "GlobalMiddleButtonPressed";
                                    break;
                                }
                            case 56:
                                {
                                    str = "GlobalLeftButtonReleased";
                                    break;
                                }
                            case 57:
                                {
                                    str = "GlobalRightButtonReleased";
                                    break;
                                }
                            case 58:
                                {
                                    str = "GlobalMiddleButtonReleased";
                                    break;
                                }
                            case 60:
                                {
                                    str = "MouseWheelUp";
                                    break;
                                }
                            case 61:
                                {
                                    str = "MouseWheelDown";
                                    break;
                                }
                            default:
                                {
                                    goto case 59;
                                }
                        }
                        break;
                    }
                case 7:
                    {
                        switch (subevent)
                        {
                            case 0:
                                {
                                    str = "OutsideEvent";
                                    break;
                                }
                            case 1:
                                {
                                    str = "BoundaryEvent";
                                    break;
                                }
                            case 2:
                                {
                                    str = "StartGameEvent";
                                    break;
                                }
                            case 3:
                                {
                                    str = "EndGameEvent";
                                    break;
                                }
                            case 4:
                                {
                                    str = "StartRoomEvent";
                                    break;
                                }
                            case 5:
                                {
                                    str = "EndRoomEvent";
                                    break;
                                }
                            case 6:
                                {
                                    str = "NoLivesEvent";
                                    break;
                                }
                            case 7:
                                {
                                    str = "AnimationEndEvent";
                                    break;
                                }
                            case 8:
                                {
                                    str = "EndOfPathEvent";
                                    break;
                                }
                            case 9:
                                {
                                    str = "NoHealthEvent";
                                    break;
                                }
                            case 10:
                                {
                                    str = "UserEvent0";
                                    break;
                                }
                            case 11:
                                {
                                    str = "UserEvent1";
                                    break;
                                }
                            case 12:
                                {
                                    str = "UserEvent2";
                                    break;
                                }
                            case 13:
                                {
                                    str = "UserEvent3";
                                    break;
                                }
                            case 14:
                                {
                                    str = "UserEvent4";
                                    break;
                                }
                            case 15:
                                {
                                    str = "UserEvent5";
                                    break;
                                }
                            case 16:
                                {
                                    str = "UserEvent6";
                                    break;
                                }
                            case 17:
                                {
                                    str = "UserEvent7";
                                    break;
                                }
                            case 18:
                                {
                                    str = "UserEvent8";
                                    break;
                                }
                            case 19:
                                {
                                    str = "UserEvent9";
                                    break;
                                }
                            case 20:
                                {
                                    str = "UserEvent10";
                                    break;
                                }
                            case 21:
                                {
                                    str = "UserEvent11";
                                    break;
                                }
                            case 22:
                                {
                                    str = "UserEvent12";
                                    break;
                                }
                            case 23:
                                {
                                    str = "UserEvent13";
                                    break;
                                }
                            case 24:
                                {
                                    str = "UserEvent14";
                                    break;
                                }
                            case 25:
                                {
                                    str = "UserEvent15";
                                    break;
                                }
                            case 26:
                            case 27:
                            case 28:
                            case 29:
                            case 31:
                            case 32:
                            case 33:
                            case 34:
                            case 35:
                            case 36:
                            case 37:
                            case 38:
                            case 39:
                            case 48:
                            case 49:
                            case 59:
                            case 64:
                            case 65:
                                {
                                    str = "unknown";
                                    break;
                                }
                            case 30:
                                {
                                    str = "CloseButtonEvent";
                                    break;
                                }
                            case 40:
                                {
                                    str = "OutsideView0Event";
                                    break;
                                }
                            case 41:
                                {
                                    str = "OutsideView1Event";
                                    break;
                                }
                            case 42:
                                {
                                    str = "OutsideView2Event";
                                    break;
                                }
                            case 43:
                                {
                                    str = "OutsideView3Event";
                                    break;
                                }
                            case 44:
                                {
                                    str = "OutsideView4Event";
                                    break;
                                }
                            case 45:
                                {
                                    str = "OutsideView5Event";
                                    break;
                                }
                            case 46:
                                {
                                    str = "OutsideView6Event";
                                    break;
                                }
                            case 47:
                                {
                                    str = "OutsideView7Event";
                                    break;
                                }
                            case 50:
                                {
                                    str = "BoundaryView0Event";
                                    break;
                                }
                            case 51:
                                {
                                    str = "BoundaryView1Event";
                                    break;
                                }
                            case 52:
                                {
                                    str = "BoundaryView2Event";
                                    break;
                                }
                            case 53:
                                {
                                    str = "BoundaryView3Event";
                                    break;
                                }
                            case 54:
                                {
                                    str = "BoundaryView4Event";
                                    break;
                                }
                            case 55:
                                {
                                    str = "BoundaryView5Event";
                                    break;
                                }
                            case 56:
                                {
                                    str = "BoundaryView6Event";
                                    break;
                                }
                            case 57:
                                {
                                    str = "BoundaryView7Event";
                                    break;
                                }
                            case 58:
                                {
                                    str = "AnimationUpdateEvent";
                                    break;
                                }
                            case 60:
                                {
                                    str = "WebImageLoadedEvent";
                                    break;
                                }
                            case 61:
                                {
                                    str = "WebSoundLoadedEvent";
                                    break;
                                }
                            case 62:
                                {
                                    str = "WebAsyncEvent";
                                    break;
                                }
                            case 63:
                                {
                                    str = "WebUserInteractionEvent";
                                    break;
                                }
                            case 66:
                                {
                                    str = "WebIAPEvent";
                                    break;
                                }
                            case 67:
                                {
                                    str = "WebCloudEvent";
                                    break;
                                }
                            case 68:
                                {
                                    str = "NetworkingEvent";
                                    break;
                                }
                            case 69:
                                {
                                    str = "SteamEvent";
                                    break;
                                }
                            case 70:
                                {
                                    str = "SocialEvent";
                                    break;
                                }
                            case 71:
                                {
                                    str = "PushNotificationEvent";
                                    break;
                                }
                            case 72:
                                {
                                    str = "AsyncSaveLoadEvent";
                                    break;
                                }
                            case 73:
                                {
                                    str = "AudioRecordingEvent";
                                    break;
                                }
                            case 74:
                                {
                                    str = "AudioPlaybackEvent";
                                    break;
                                }
                            case 75:
                                {
                                    str = "SystemEvent";
                                    break;
                                }
                            default:
                                {
                                    goto case 65;
                                }
                        }
                        break;
                    }
                case 8:
                    {
                        switch (subevent)
                        {
                            case 64:
                                {
                                    str = "DrawGUI";
                                    break;
                                }
                            case 65:
                                {
                                    str = "DrawResize";
                                    break;
                                }
                            case 66:
                            case 67:
                            case 68:
                            case 69:
                            case 70:
                            case 71:
                                {
                                    str = "DrawEvent";
                                    break;
                                }
                            case 72:
                                {
                                    str = "DrawEventBegin";
                                    break;
                                }
                            case 73:
                                {
                                    str = "DrawEventEnd";
                                    break;
                                }
                            case 74:
                                {
                                    str = "DrawGUIBegin";
                                    break;
                                }
                            case 75:
                                {
                                    str = "DrawGUIEnd";
                                    break;
                                }
                            case 76:
                                {
                                    str = "DrawPre";
                                    break;
                                }
                            case 77:
                                {
                                    str = "DrawPost";
                                    break;
                                }
                            default:
                                {
                                    goto case 71;
                                }
                        }
                        break;
                    }
                case 9:
                    {
                        str = string.Concat("KeyPressed_", KeyToString(subevent));
                        break;
                    }
                case 10:
                    {
                        str = string.Concat("KeyReleased_", KeyToString(subevent));
                        break;
                    }
                case 11:
                    {
                        str = "Trigger";
                        break;
                    }
                default:
                    {
                        str = "unknown";
                        break;
                    }
            }
            return str;
        }
    }
}
