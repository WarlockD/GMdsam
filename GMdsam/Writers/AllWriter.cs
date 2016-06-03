using GameMaker.Dissasembler;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.Ast;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Json;

namespace GameMaker.Writers
{
    // this class Writes EVEYTHING.
    // Idea being that we can take all the varriables, figure out when and how they are being used
    // and make sure the initalizers are set up for it
    public class JsonPrittyPrint // used to make Json look a little better
    {
        static Regex string_match = new Regex(@"""[^ ""\\] * (?:\\.[^ ""\\] *)*""", RegexOptions.Compiled);
        static Regex number_match = new Regex(@"\d+", RegexOptions.Compiled);
        static Regex float_match = new Regex(@"(?:^|(?<=\s))[0-9]*\.?[0-9](?=\s|$)");
        HashSet<string> toInline = new HashSet<string>();
        public void AddInline(string name)
        {
            toInline.Add("\"" + name.ToLower() + "\"");
        }
        // cause I am lazy
        string incomming;
        int pos;
        char prev = default(char);
        char current = default(char);
        bool ignoreWhitespace;

        PlainTextWriter writer = null;
        void Reset(string incomming)
        {
            this.incomming = incomming;
            this.pos = 0;
            this.current = default(char);
            this.prev = default(char);
        }
        public JsonPrittyPrint(string incomming)
        {
            Reset(incomming);
            ignoreWhitespace = true;
            this.writer = null;
        }
        char Next()
        {
            prev = current;
            do
            {
                if (pos < incomming.Length)
                {
                    current = char.ToLower(incomming[pos++]);
                    if (char.IsWhiteSpace(current)) continue; // we ignore whitespace
                }
                else current = default(char);
            } while (false);
            return current;
        }
        void ParseArrayInObject()
        {

        }
        char ParseValue(int level,char from = default(char))
        {
            StringBuilder sb = new StringBuilder();
            string name = null;
            char ch = Next();
            char last = default(char);
            while(ch != 0) {
           
                switch (ch)
                {
                    case '"':
                        writer.Write(ch);
                        if (name == null) sb.Clear();
                        while (ch != 0)
                        {
                            ch = Next();
                            if (prev != '\\' && ch == '"') break;
                            if (name == null) sb.Append(ch);
                            writer.Write(ch);
                        }
                        writer.Write(ch);
                        if (name == null) name = sb.ToString();
                        break;
                    case '{':
                        writer.Indent++;
                        if (from == '[')// object array
                        {
                            if(last != ',') // first start
                            {
                           
                                writer.WriteLine();
                                writer.Write(ch);
                                writer.Write(' ');
                                ch = ParseValue(level++, '('); // object inline
                            } else
                            {
                                writer.Write(ch);
                                writer.Write(' ');
                                ch = ParseValue(level++, '('); // object inline
                            }
                        }
                        else if(last == ',')
                        {
                            writer.Write(ch);
                            writer.WriteLine();// usally first level
                            ch = ParseValue(level++, '{');
                        }
                        else writer.WriteLine();// usally first level
                        writer.Indent--;
                        writer.Write(' '); // final space
                        writer.Write(ch); // write the ending bracket
                        break;
                    case '[':

                        writer.Write(ch);

                        ch=ParseValue(level++, '[');
                        writer.Write(ch);
                        break;
                    case '}': return '}';
                    case ']':
                        //writer.Write(ch);
                        if (!char.IsNumber(last)) writer.WriteLine();
                        return ']';
                    case ',':
                        writer.Write(ch);
                        if(from == '[')
                        {
                            if (!char.IsNumber(last)) writer.WriteLine();
                            else if (last == '}') writer.WriteLine();
                            else writer.Write(' ');
                        } else if(from != '(') writer.WriteLine(); // its an object but we want it inline
                        else writer.Write(' ');
                        break;
                    case ':':
                        writer.Write(ch);
                        writer.Write(' ');
                        break;
                    default:
                        writer.Write(ch);
                        break;
                }
                last = ch;
                ch = Next();
            }
            return default(char);
        }
        void Debug()
        {
            using (StreamWriter sw = new StreamWriter("debug.json")) sw.Write(writer.ToString());
        }
        void WriteObjectStart()
        {

        }
        void WriteStart() // test if we are starting on an object or on an array
        {
            char ch = Next();
            if (ch == '{')
            {
                writer.WriteLine('{');
                writer.Indent++;
            
                ParseValue(0);
                writer.WriteLine();
                writer.Indent--;
                writer.Write('}');
            } else if(ch == '[')
            {
                while(ch != ']')
                {
                    writer.Indent++;
                    writer.WriteLine('[');
                    WriteStart();
                    writer.WriteLine();
                    writer.Indent--;
                    ch = Next();
                    if (ch == ',')
                    {
                        writer.WriteLine(',');
                        ch = Next();
                    }
                }
                writer.Indent--;
                writer.WriteLine(']');
            }
        }
        public void Write(TextWriter writer)
        {
            this.pos = 0;
            this.writer = new PlainTextWriter(writer);
            WriteStart();
            this.writer.WriteLine();
           // this.writer.Flush();
            this.writer = null;
        }

    }
    public class AllWriter
    {
        enum JsonState
        {
            InObject,
            InArray,
            InObjectArray
        }
        // http://stackoverflow.com/questions/4580397/json-formatter-in-c
        // copied from there and modified to use my plain textwriter identer class
        // Also to handle first level objects to look a bit cleaner
        // I tried building a full tokenizer but that just wated 4 hours of my life and was still MUCH slower than this:P
        public static string FormatJson(string str, params string[] inlines)
        {
            HashSet<string> toinline = new HashSet<string>(inlines.Select(x => x.ToLower()));
            using (PlainTextWriter writer = new PlainTextWriter())
            {
                StringBuilder sb = new StringBuilder();
                char ch = default(char);
                char pch =  default(char);
                string name = null;
                Stack<JsonState> state = new Stack<JsonState>();
                for (var i = 0; i < str.Length; i++)
                {
                    pch = ch;
                    ch = char.ToLower(str[i]); // eveything is lower case
                    switch (ch)
                    {
                        case '{':
                            writer.Write(ch);
                            var top = JsonState.InObject;
                            if (state.Count > 0) {
                                var peek = state.Peek();
                                if (peek == JsonState.InArray)
                                {
                                    top = JsonState.InObjectArray;
                                    writer.WriteLine();
                                    writer.Indent++;                                
                                }
                                
                            } else
                            {
                                writer.WriteLine();
                                writer.Indent++; // first level ident always
                            }
                            state.Push(JsonState.InObject);
                            break;
                        case '[':
                            writer.Write(ch);
                            state.Push(JsonState.InArray);
                            break;
                        case '}':
                            state.Pop();
                            if (state.Count == 0 || state.Peek() != JsonState.InArray)
                            {
                                writer.WriteLine();
                                writer.Indent--;
                            }
                            writer.Indent--;
                            writer.Write(ch);
                         
                            break;
                        case ']':
                            writer.Write(ch);
                            state.Pop();
                            break;
                        case '"':
                            writer.Write(ch);
                            sb.Clear();
                            i++;
                            for(; i < str.Length; i++)
                            {
                                pch = ch;
                                ch = char.ToLower(str[i]);
                                if (pch != '\\' && ch == '"') break;
                                sb.Append(ch);
                                writer.Write(ch);
                            }
                            writer.Write(ch);
                            if (name == null) name = sb.ToString();
                           
                            break;
                        case ',':
                            writer.Write(ch);
                            writer.WriteLine();
                            break;
                        case ':':
                            writer.Write(ch);
                            writer.Write(' ');
                            break;
                        default:
                            writer.Write(ch);
                            break;
                    }
                }
                return writer.ToString();
            }
        }
        ConcurrentBag<string> globals_vars = new ConcurrentBag<string>();
        ConcurrentBag<string> globals_arrays = new ConcurrentBag<string>();
        ConcurrentBag<string> scrptnames;
        ConcurrentBag<string> objnames;
        DirectoryInfo scriptDirectory = null;
        DirectoryInfo objectDirectory = null;
        List<Task> tasks = new List<Task>();
        public static string QuickCodeToLine(File.Code code,string context=null)
        {
            return new AllWriter().CodeToSingleLine(code, context);
        }

        string CodeToSingleLine(File.Code c, string context = null)
        {
            BlockToCode output = CreateOutput(context ?? c.Name);
            GetScriptWriter(output).WriteCode(c);
            var code = regex_newline.Replace(output.ToString(), ";");
            code = regex_commas.Replace(code, "; "); // replace all double/tripple commas
            return code.Trim();
        }

        public AllWriter()
        {
     
        }

        CodeWriter GetScriptWriter(BlockToCode output)
        {
            switch (Context.outputType)
            {
                case OutputType.LoveLua:
                    return (CodeWriter)new Lua.Writer(output);
                case OutputType.GameMaker:
                    return (CodeWriter) new GameMaker.Writer(output);
                default:
                    throw new Exception("Bad output type");

            }
        }
        public static void CacheForAnalysis()
        {
            ConcurrentBag<string> globals_vars = new ConcurrentBag<string>();
            ConcurrentBag<string> globals_arrays = new ConcurrentBag<string>();
        }
        public static BlockToCode CreateOutput(string name)
        {
            BlockToCode output = new BlockToCode(new Context.ErrorContext(name));
            return output;
        }
        static Regex regex_newline = new Regex(@"\s*(\r\n|\r|\n)\s*", RegexOptions.Compiled);
        static Regex regex_commas = new Regex(@";+", RegexOptions.Compiled);
        //

        async void Run(File.Script s, string filename=null)
        {
            BlockToCode output = CreateOutput(s.Name);
            GetScriptWriter(output).Write(s);
          //  if (Context.doGlobals) AddGlobals(output);
            await output.AsyncWriteToFile(filename);
        }

        async void Run(File.GObject obj, string filename)
        {
            BlockToCode output = CreateOutput(obj.Name);
            GetScriptWriter(output).Write(obj);
         //   if (Context.doGlobals) AddGlobals(output);
            await output.AsyncWriteToFile(filename);
        }

        void RunTask(File.GObject obj, string path)
        {
            if (Context.doThreads)
            {
                Task task = new Task(()=> Run(obj,path), TaskCreationOptions.LongRunning);
                task.ContinueWith(ExceptionHandler, TaskContinuationOptions.OnlyOnFaulted);
                tasks.Add(task);
                task.Start();
            }
            else Run(obj,path);
        }
        void RunTask(Action func)
        {
            if (Context.doThreads)
            {
                Task task = new Task(func);
                task.ContinueWith(ExceptionHandler, TaskContinuationOptions.OnlyOnFaulted);
                tasks.Add(task);
                task.Start();
            }
            else func();
        }
        void RunTask(File.Script s, string path)
        {
            if (Context.doThreads)
            {
                Task task = new Task(() => Run(s, path), TaskCreationOptions.LongRunning);
                task.ContinueWith(ExceptionHandler, TaskContinuationOptions.OnlyOnFaulted);
                tasks.Add(task);
                task.Start();
            }
            else Run(s, path);
        }
        public void Search(string toSearch, bool parents) // also add object parents
        {
            Context.doGlobals = false; // don't do globals on search
            foreach (var a in File.Search(toSearch))
            {
                File.GObject obj = a as File.GObject;
                if (obj != null)
                {
                    Context.Info("Found Object '{0}': ", obj.Name);
                    RunTask(obj, obj.Name);
                    while(obj.Parent > -1)
                    {
                        var p = File.Objects[obj.Parent];
                        Context.Info("    Found Parent '{0}': ", p.Name);
                        RunTask(p,p.Name);
                        obj = p;
                    }
                    continue;
                }
                File.Script s = a as File.Script;
                if (s != null)
                {
                    Context.Info("Found Script '{0}': ", s.Name);
                    RunTask(s, s.Name);
                    continue;
                }
                Context.Info("Found Type '{0}' of Name '{1}': ", a.GetType().ToString(),  a.Name);
            }
        }
        public void StartWriteAllScripts()
        {
            scrptnames = new ConcurrentBag<string>();
            scriptDirectory = Directory.CreateDirectory("scripts");
            string full_name = scriptDirectory.FullName;
            foreach (var s in File.Scripts)
            {
                if (s.Data == null) continue;
                scrptnames.Add(s.Name);
                string filename = Path.Combine(full_name, s.Name);
                RunTask(s, filename);
            }
        }
        static string DeleteAllAndCreateDirectory(string dir)
        {
            var directory = Directory.CreateDirectory(dir);
            foreach (var f in directory.GetFiles()) f.Delete();
            return directory.FullName;
        }
        public void StartWriteAllRooms()
        {
            var path = DeleteAllAndCreateDirectory("rooms");
            foreach (var o in File.Rooms) {
                RunTask(() =>
                {
                    // check if we need to do room code
                    if(o.code_offset > 0 && o.Room_Code == null)
                    {
                            o.Room_Code = QuickCodeToLine(File.Codes[o.code_offset]);    
                    }
                    foreach(var oi in o.Objects)
                    {
                        if (oi.Code_Offset > 0 && oi.Room_Code == null)
                        {
                            oi.Room_Code = QuickCodeToLine(File.Codes[oi.Code_Offset]);
                        }
                    }
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name),"json");
                    //   using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(o);
                    using (MemoryStream ssw = new MemoryStream())
                    {
                        DataContractJsonSerializerSettings set = new DataContractJsonSerializerSettings();
                        DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(File.Room));
                       
                        ser.WriteObject(ssw, o);
                        ssw.Position = 0;
                        StreamReader sr = new StreamReader(ssw);
                        string mstr = sr.ReadToEnd();
                        JsonPrittyPrint test = new JsonPrittyPrint(mstr);

                        StreamWriter sw = new StreamWriter(filename);
                      // sw.Write(mstr);
                        test.Write(sw);
                        sw.Flush();
                        sw.Close();
                    }
                });
            }
        }
        public void StartWriteAllBackgrounds()
        {
            var path = DeleteAllAndCreateDirectory("backgrounds");
            foreach (var o in File.Backgrounds)
            {
                RunTask(() =>
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name), "js");
                    using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(o);
                });
            }
        }
        public void StartWriteAllSprites()
        {
            var path = DeleteAllAndCreateDirectory("sprites");
            foreach (var o in File.Sprites)
            {
                RunTask(() =>
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name), "js");
                    using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(o);
                });
            }
        }
        public void StartWriteAllFonts()
        {
            var path = DeleteAllAndCreateDirectory("fonts");
            foreach (var o in File.Fonts)
            {
                RunTask(() =>
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name), "js");
                    using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(o);
                });
            }
        }
        public void StartWriteAllCode()
        {
            var path = DeleteAllAndCreateDirectory("code");
            foreach (var o in File.Codes)
            {
                RunTask(() =>
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name), "js");
                    using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(o);
                });
            }
        }
        public void StartWriteAllSounds()
        {
            var path = DeleteAllAndCreateDirectory("sounds");
            foreach (var o in File.Sounds)
            {
                RunTask(() =>
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name), "js");
                    using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(o);
                });
            }
        }
        DateTime start;
        public void StartWriteAllObjects()
        {
            objectDirectory = Directory.CreateDirectory("objects");
            objnames = new ConcurrentBag<string>();
            string full_name = objectDirectory.FullName;

            start = DateTime.Now;
            foreach(var o in File.Objects)
            {
                objnames.Add(o.Name);
                string filename = Path.Combine(full_name, o.Name);
                RunTask(o, filename);
            }
        }
    void ExceptionHandler(Task task)
        {
            var exception = task.Exception;
            Console.WriteLine(exception);
            throw exception;
        }
        public void FinishProcessing()
        {
            if (tasks.Count > 0)
            {
                Task.WaitAll(tasks.ToArray());
                Task.WaitAll(tasks.ToArray());
                DateTime stop = DateTime.Now;
                TimeSpan time = stop.Subtract(start);
                Debug.WriteLine("Time : {0}", time);
                tasks.Clear();
            }
            if (ILNode.times.Count > 0)
            {
                using (StreamWriter sw = new StreamWriter(Context.MoveFileToOldErrors("timeCollection.txt")))
                {
                    List<double> timeAverage = new List<double>();
                    foreach (var c in ILNode.times) timeAverage.Add(c.Time.Ticks);

                    sw.WriteLine("Total Tick Average: {0}", timeAverage.Average());
                    sw.WriteLine();
                    foreach (var c in ILNode.times.OrderBy(x=>x.Time)) sw.WriteLine("Count={0}  Time={1}", c.Count, c.Time);
                }
            }
            if(scrptnames!= null && scrptnames.Count > 0)
            {
                using (StreamWriter sw = new StreamWriter("loadScripts.lua"))
                {
                    foreach (var s in scrptnames) sw.WriteLine("require 'scripts/{0}'", s);
                }
            }
           if(objnames != null && objnames.Count > 0)
            {
                using (StreamWriter sw = new StreamWriter("loadObjects.lua"))
                {
                    foreach (var s in objnames) sw.WriteLine("require 'objects/{0}'", s);
                }
            }
            
            if(Context.doGlobals)
            {
                using (StreamWriter sw = new StreamWriter("globals.lua"))
                {
                    sw.WriteLine("local g = {");
                    foreach (var v in globals_vars)
                    {
                        sw.Write("   ");
                        sw.Write(v);
                        sw.WriteLine(" = 0,");
                    }
                    foreach (var v in globals_arrays)
                    {
                        sw.Write("   ");
                        sw.Write(v);
                        sw.WriteLine(" = {},");
                    }
                    sw.WriteLine("} ");
                    sw.WriteLine("globals = g");
                }
            }
           
        }
    }
}