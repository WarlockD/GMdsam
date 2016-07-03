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
using System.Threading;
using System.Runtime.Serialization;

namespace GameMaker.Writers
{
    static class JsonHelper_Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> ie, Action<T> action)
        {
            foreach (var i in ie)
            {
                action(i);
            }
        }
    }
    public class AllWriter
    {
       
        // http://stackoverflow.com/questions/4580397/json-formatter-in-c
        class JsonHelper
        {
            private const string INDENT_STRING = "    ";
            public static string FormatJson(string str)
            {
                var indent = 0;
                var quoted = false;
                var sb = new StringBuilder();
                for (var i = 0; i < str.Length; i++)
                {
                    var ch = str[i];
                    switch (ch)
                    {
                        case '{':
                        case '[':
                            sb.Append(ch);
                            if (!quoted)
                            {
                                sb.AppendLine();
                                Enumerable.Range(0, ++indent).ForEach(item => sb.Append(INDENT_STRING));
                            }
                            break;
                        case '}':
                        case ']':
                            if (!quoted)
                            {
                                sb.AppendLine();
                                Enumerable.Range(0, --indent).ForEach(item => sb.Append(INDENT_STRING));
                            }
                            sb.Append(ch);
                            break;
                        case '"':
                            sb.Append(ch);
                            bool escaped = false;
                            var index = i;
                            while (index > 0 && str[--index] == '\\')
                                escaped = !escaped;
                            if (!escaped)
                                quoted = !quoted;
                            break;
                        case ',':
                            sb.Append(ch);
                            if (!quoted)
                            {
                                sb.AppendLine();
                                Enumerable.Range(0, indent).ForEach(item => sb.Append(INDENT_STRING));
                            }
                            break;
                        case ':':
                            sb.Append(ch);
                            if (!quoted)
                                sb.Append(" ");
                            break;
                        default:
                            sb.Append(ch);
                            break;
                    }
                }
                return sb.ToString();
            }
        }

        ConcurrentBag<string> globals_vars = new ConcurrentBag<string>();
        ConcurrentBag<string> globals_arrays = new ConcurrentBag<string>();
        ConcurrentBag<Task> tasks = new ConcurrentBag<Task>();


        public static string QuickCodeToLine(File.Code code, string context)
        {
            return new AllWriter().CodeToSingleLine(code, context);
        }
        public static string QuickCodeToLine(File.Code code, bool keep_newline = false)
        {
            return new AllWriter().CodeToSingleLine(code, code.Name, keep_newline);
        }
        public static string QuickCodeToLine(File.Code code, string context, bool keep_newline = false)
        {
            return new AllWriter().CodeToSingleLine(code, context, keep_newline);
        }
        string CodeToSingleLine(File.Code c, string context, bool keep_newline = false)
        {
            string code = null;
            if (c.Size == 0) Debug.WriteLine("Code '" + c.Name + "' has no data but is regestered?");
            if (c.Size > 0)
            {
                BlockToCode output = CreateOutput(context);
                GetScriptWriter(output).WriteCode(c);
                code = output.ToString();
                Debug.Assert(!string.IsNullOrWhiteSpace(code));
                if (!keep_newline)
                {
                    code = regex_newline.Replace(code, ";");
                    // replace all double/tripple commas and puts a space next to any statements so its slightly easyer to read in a line
                    code = regex_commas.Replace(code, "; ");
                    code = code.Trim();
                }
                return code;
            }
            return null;
        }
        List<CodeTask> _todo = null;
        public void AddAction(string start_name)
        {
            if (_todo == null) _todo = new List<CodeTask>();
            string name = string.IsNullOrWhiteSpace(start_name) ? "everything" : start_name.Trim().ToLower();
            if (name == "everything")
            {
                foreach (var v in chunkActions.Values) v(_todo);
            }
            else
            {
                Action<List<CodeTask>> action;
                if (chunkActions.TryGetValue(name, out action)) action(_todo);
                else Context.FatalError("Unkonwn chunk name'{0}'", start_name);
            }
        }
        class CodeTask : IProgress<int>, IProgress<double>
        {
            public static void RunOneThing<T>(string filename, IReadOnlyList<T> o) where T : File.GameMakerStructure
            {
                if (Context.doLua)
                {
                    filename = Path.ChangeExtension(filename, "lua");
                    Context.doXML = false; // to make sure we are making a json file
                    using (MemoryStream writer = new MemoryStream())
                    {
                        var serializer = GetSerializer(o.GetType());
                        serializer.WriteObject(writer, o);
                        StreamWriter sw = new StreamWriter(filename);
                        sw.Write(JsonToLua(writer));
                        sw.Flush();
                        sw.Close();
                    }
                }
                else
                {
                    filename = FixFilenameExtensionForSerialzer(filename);
                    using (FileStream writer = new FileStream(filename, FileMode.Create))
                    {
                        var serializer = GetSerializer(o.GetType());
                        serializer.WriteObject(writer, o);
                    }
                }
            }
                static Regex match_json_name = new Regex(@"(""\w+""):", RegexOptions.Compiled);
            public static string JsonToLua(Stream s)
            {
                s.Position = 0;
                StreamReader sr = new StreamReader(s);
                StringBuilder sb = new StringBuilder((int)sr.BaseStream.Length);
                string eveything = sr.ReadToEnd(); // very hacky
                var array_replacement = eveything.Replace('[', '{').Replace(']', '}');
                var eqfix = match_json_name.Replace(array_replacement, (Match m) => "[" + m.Groups[1] + "]=");
                var final = "local t=" + eqfix + "\r\n";
                final += "return t\r\n";
                return final;
            }
            public static void RunOneThing<T>(string path, T o) where T : File.GameMakerStructure, File.INamedResrouce
            {
                if (Context.doLua)
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name),"lua");
                    Context.doXML = false; // to make sure we are making a json file
                    using (MemoryStream writer = new MemoryStream())
                    {
                        var serializer = GetSerializer(o.GetType());
                        serializer.WriteObject(writer, o);
                        StreamWriter sw = new StreamWriter(filename);
                        sw.Write(JsonToLua(writer));
                        sw.Flush();
                        sw.Close();
                    }
                }
                else
                {
                    string filename = FixFilenameExtensionForSerialzer(Path.Combine(path, o.Name));
                    using (FileStream writer = new FileStream(filename, FileMode.Create))
                    {
                        var serializer = GetSerializer(o.GetType());
                        serializer.WriteObject(writer, o);
                    }
                }    
            }
            public static CodeTask Create<T>(string path_name, IReadOnlyList<T> list) where T : File.GameMakerStructure, File.INamedResrouce
            {
                return Create<T>(path_name, list, RunOneThing);
            }
            public static CodeTask Create<T>(string path_name, IReadOnlyList<T> list, Action<string, T> action) where T : File.GameMakerStructure
            {
                return Create<T>(path_name, path_name, list, action);
            }
            public static CodeTask Create<T>(string path_name, string task_name, IReadOnlyList<T> list, Action<string, IReadOnlyList<T>, IProgress<double>> action) where T : File.GameMakerStructure
            {
                DateTime start = DateTime.Now;
                task_name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(task_name.ToLower());
                var path = Context.DeleteAllAndCreateDirectory(path_name);
                if (Context.doThreads)
                {
                    CodeTask ct = new CodeTask();
                    ct.Name = task_name;
                    ct.TotalTasks = 1;
                    ct.Task = new Task(() =>
                    {
                        lock (list)
                        {
                            action(path_name, list, ct);
                            ct.Report(1);
                        }
                        Context.Message("{0} finished in {1}", task_name, DateTime.Now - start);
                        ct._alldone = true;
                    }, TaskCreationOptions.LongRunning);
                    return ct;
                }
                else
                {
                    using (var progress = new ProgressBar(task_name))
                    {
                        ErrorContext.ProgressBar = progress;
                        action(path, list, progress);
                        ErrorContext.ProgressBar = null;
                    }
                    Context.Message("{0} finished in {1}", task_name, DateTime.Now - start);
                    return null; // no task as its done
                }
            }
            public static CodeTask CreateSingle(string task_name, Action action) 
            {

                if (Context.doThreads)
                {
                    CodeTask ct = new CodeTask();
                    ct.Name = task_name;
                    ct.TotalTasks = 1;
                    ct.Task = new Task(action);
                    return ct;
                }
                else
                {
                    action();
                    return null;
                }
            }
            public static CodeTask CreateSingle<T>(string path_name, string task_name, T o, Action<string, T> action) where T : File.GameMakerStructure
            {
                if (Context.doThreads)
                {
                    CodeTask ct = new CodeTask();
                    ct.Name = task_name;
                    ct.TotalTasks = 1;
                    ct.Task = new Task(() => action(path_name, o));
                    return ct;
                }
                else
                {
                    action(path_name, o);
                    return null;
                }
            }
            public static CodeTask Create<T>(string path_name, string task_name, IReadOnlyList<T> list, Action<string, T> action) where T : File.GameMakerStructure
            {
                DateTime start = DateTime.Now;
                task_name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(task_name.ToLower());
                var path = Context.DeleteAllAndCreateDirectory(path_name);
                if (Context.doThreads)
                {
                    CodeTask ct = new CodeTask();
                    ct.Name = task_name;
                    ct.TotalTasks = list.Count;
                    ct.Task = new Task(() =>
                    {
                        int tasksDone = 0;
                        int tasksTotal = list.Count;
                        ParallelOptions opts = new ParallelOptions();
                        Parallel.ForEach(list, opts, (T o) =>
                        {
                            lock (o) action(path, o);
                            Interlocked.Increment(ref tasksDone);
                            ct.Report(tasksDone);
                        });
                        Context.Message("{0} finished in {1}", task_name, DateTime.Now - start);

                        ct._alldone = true;
                    }, TaskCreationOptions.LongRunning);
                    return ct;
                }
                else
                {
                    using (var progress = new ProgressBar(task_name))
                    {
                        ErrorContext.ProgressBar = progress;
                        for (int i = 0; i < list.Count; i++)
                        {
                            var o = list[i];

                            action(path, o);
                            progress.Report((double)i + 1 / list.Count);
                        }
                        ErrorContext.ProgressBar = null;
                    }
                    Context.Message("{0} finished in {1}", task_name, DateTime.Now - start);
                    return null; // no task as its done
                }
            }
            public string Name { get; private set; }
            public Task Task { get; private set; }
            public int TotalTasks { get; private set; }
            bool _alldone = false;
            public bool isCompleted { get { return _alldone; } }
            int _tasksFinished = 0;
            double _percentDone = 0.0;
            public double PercentDone { get { return TotalTasks != 0 ? (double)_tasksFinished / TotalTasks : _percentDone; } }
            public int TasksFinished { get { return _tasksFinished; } }
            public void Report(int value)
            {
                if (TotalTasks > 0)
                {

                    // value = Math.Max(0, Math.Min(TotalTasks, value));
                    Interlocked.Exchange(ref _tasksFinished, value);
                }
            }
            public void Report(double value)
            {
                if (TotalTasks == 0)
                {
                    value = Math.Max(0, Math.Min(1, value));
                    Interlocked.Exchange(ref _percentDone, value);
                }
            }
        }
        static readonly Dictionary<string, Action<List<CodeTask>>> chunkActions = null;
        public static void DoSingleItem(string path, File.GameMakerStructure o)
        {
            Context.Message("Saving '{0}'", ((File.INamedResrouce)o).Name);
            // Sooo cheap, but quick and easy
            DoFileWrite(path, (dynamic)o);
        }
       public  static void DoSearchList(string path, List<File.GameMakerStructure> list)
        {
            foreach (var o in list) DoSingleItem(path, o);
        }
        static void DoFileWrite(string path, File.Code s) {
            string filename = Path.ChangeExtension(Path.Combine(path, s.Name), "js");
            using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(s);
        }
        static void DoFileWrite(string path, File.Texture t)
        {
            string filename = Path.ChangeExtension(Path.Combine(path, "texture_" + t.Index), "png");
            using (FileStream fs = new FileStream(filename, FileMode.Create)) t.Data.CopyTo(fs);
        }
        static void DoFileWrite(string path, File.GObject o)
        {
            string filename = Path.Combine(path, o.Name);
            BlockToCode output = CreateOutput(o.Name);
            GetScriptWriter(output).Write(o);
            output.WriteToFile(filename);
        }
        static void DoFileWrite(string path, File.Script s)
        {
            string filename = Path.Combine(path, s.Name);
            BlockToCode output = CreateOutput(s.Name);
            GetScriptWriter(output).Write(s);
            output.WriteToFile(filename);
        }
        static void DoFileWrite(string path, File.Sprite s)
        {
            CodeTask.RunOneThing(path, s);
            if (Context.saveAllPngs)
            {
                if (s.Frames.Length == 0) return;
                else if (s.Frames.Length == 1)
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, s.Name), ".png"); // we just have one
                    s.Frames[0].Image.Save(filename);
                }
                else // we want to cycle though them all
                {
                    for (int i = 0; i < s.Frames.Length; i++)
                    {
                        string filename = Path.ChangeExtension(Path.Combine(path, s.Name + "_" + i), ".png"); // we just have one
                        s.Frames[i].Image.Save(filename);
                    }
                }
            }
            if (Context.saveAllMasks)
            {
                if (s.Masks.Count == 0) return;
                else if (s.Masks.Count == 1)
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, s.Name + "_mask"), ".png"); // we just have one
                    s.Masks[0].Save(filename);
                }
                else
                {
                    for (int i = 0; i < s.Masks.Count; i++)
                    {
                        string filename = Path.ChangeExtension(Path.Combine(path, s.Name + "_mask_" + i), ".png"); // we just have one
                        s.Masks[i].Save(filename);
                    }
                }
            }
        }
        static void DoFileWrite(string path, File.Room room)
        {
            if (room.code_offset > 0 && room.Room_Code == null) // fill in room init
            {
                room.Room_Code = AllWriter.QuickCodeToLine(File.Codes[room.code_offset]);
            }
            foreach (var oi in room.Objects) // fill in instance init
            {
                if (oi.Code_Offset > 0 && oi.Room_Code == null)
                {
                    oi.Room_Code = AllWriter.QuickCodeToLine(File.Codes[oi.Code_Offset]);
                }
                if (oi.Object_Index > -1 && oi.Object_Name == null)
                {
                    oi.Object_Name = File.Objects[oi.Object_Index].Name;
                }
            }
            CodeTask.RunOneThing(path, room);
        }
        static void DoFileWrite(string path, File.Background b)
        {
            CodeTask.RunOneThing(path, b);
            if (Context.saveAllPngs)
            {
                string filename = Path.ChangeExtension(Path.Combine(path, b.Name), ".png"); // we just have one
                b.Frame.Image.Save(filename);
            }
        }
        static void DoFileWrite(string path, File.AudioFile s)
        {
            CodeTask.RunOneThing(path, s);
            if (s.Data == null) return;
            string filename = Path.ChangeExtension(Path.Combine(path, s.Name), s.extension);
            using (FileStream fs = new FileStream(filename, FileMode.Create)) s.Data.CopyTo(fs);
        }
        static void DoFileWrite(string path, File.Font f)
        {
            CodeTask.RunOneThing(path, f);
            if (Context.saveAllPngs)
            {
                string filename = Path.ChangeExtension(Path.Combine(path, f.Name), ".fnt"); // we just have one
                BMFontWriter.SaveAsBMFont(filename, f);

           
               // f.Frame.Image.Save(filename);
            }
        }


        static void DoFileWrite(string path, File.Path p)
        {
            CodeTask.RunOneThing(path, p);
        }
        static void DoStrings(string filename)
        {
            //  CodeTask.RunOneThing("strings", File.Strings)
            //  filename = FixFilenameExtensionForSerialzer(filename);
            var path = Context.DeleteAllAndCreateDirectory("strings");
            using (MemoryStream writer = new MemoryStream())
            {
                var serializer = GetSerializer(File.Strings.GetType());
                serializer.WriteObject(writer, File.Strings);
                writer.Position = 0;
                string json = new StreamReader(writer).ReadToEnd();
                if(!Context.doXML) json = JsonHelper.FormatJson(json);
                filename = FixFilenameExtensionForSerialzer(Path.Combine(path,filename));
                using (StreamWriter sw = new StreamWriter(filename))
                    sw.Write(json);
            }
        }
        static AllWriter()
        {
            chunkActions = new Dictionary<string, Action<List<CodeTask>>>();
            chunkActions["code"] = (List<CodeTask> tasks) => tasks.Add(CodeTask.Create("code", File.Codes, DoFileWrite));
            chunkActions["textures"] = (List<CodeTask> tasks) => tasks.Add(CodeTask.Create("textures", File.Textures, DoFileWrite));
            chunkActions["objects"] = (List<CodeTask> tasks) => tasks.Add(CodeTask.Create("objects", File.Objects, DoFileWrite));
            chunkActions["scripts"] = (List<CodeTask> tasks) => tasks.Add(CodeTask.Create("scripts", File.Scripts, DoFileWrite));
            chunkActions["sprites"] = (List<CodeTask> tasks) => tasks.Add(CodeTask.Create("sprites", File.Sprites, DoFileWrite));
            chunkActions["rooms"] = (List<CodeTask> tasks) => tasks.Add(CodeTask.Create("rooms", File.Rooms, DoFileWrite));
            chunkActions["backgrounds"] = (List<CodeTask> tasks) => tasks.Add(CodeTask.Create("backgrounds", File.Backgrounds, DoFileWrite));
            chunkActions["sounds"] = (List<CodeTask> tasks) => tasks.Add(CodeTask.Create("sounds", File.Sounds, DoFileWrite));
            chunkActions["fonts"] = (List<CodeTask> tasks) => tasks.Add(CodeTask.Create("fonts", File.Fonts, DoFileWrite));
            chunkActions["paths"] = (List<CodeTask> tasks) => tasks.Add(CodeTask.Create("fonts", File.Fonts, DoFileWrite));
            chunkActions["strings"] = (List<CodeTask> tasks) => tasks.Add(CodeTask.CreateSingle("String List", () => DoStrings("strings")));
        }

     
        public AllWriter()
        {
         
        }


        static CodeWriter GetScriptWriter(BlockToCode output)
        {
            switch (Context.outputType)
            {
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
            BlockToCode output = new BlockToCode(new ErrorContext(name));
            return output;
        }
        static Regex regex_newline = new Regex(@"\s*(\r\n|\r|\n)\s*", RegexOptions.Compiled);
        // Used to replace all repeating ;; and to put a space
        static Regex regex_commas = new Regex(@";(;*|[^ ])", RegexOptions.Compiled);
        //


        // Search disabled for now
        /*
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
                    while (obj.Parent > -1)
                    {
                        var p = File.Objects[obj.Parent];
                        Context.Info("    Found Parent '{0}': ", p.Name);
                        RunTask(p, p.Name);
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
                Context.Info("Found Type '{0}' of Name '{1}': ", a.GetType().ToString(), a.Name);
            }
        }
        */
      



    

        static string FixFilenameExtensionForSerialzer(string filename)
        {
            return Path.ChangeExtension(filename, Context.doXML ? "xml" : "json");
        }
        public static XmlObjectSerializer GetSerializer(Type t)
        {
            if (Context.doXML)
            {
                var ser = new DataContractSerializer(t);
                return (XmlObjectSerializer)ser;
            }
            else
            {
                var settings = new DataContractJsonSerializerSettings();
                settings.UseSimpleDictionaryFormat = true;
                var ser = new DataContractJsonSerializer(t, settings);
                return (XmlObjectSerializer)ser;
            }
       }

        void ExceptionHandler(string name, Task task)
        {
            foreach (var e in task.Exception.Flatten().InnerExceptions)
            {
                Context.Error(name, e);
            }
        }
        public void FinishProcessing()
        {
            if (Context.oneFile)
            {
                CodeTask.RunOneThing("sprites", File.Sprites);
                CodeTask.RunOneThing("rooms", File.Rooms);
                CodeTask.RunOneThing("objects", File.Objects);
                CodeTask.RunOneThing("backgrounds", File.Backgrounds);
                CodeTask.RunOneThing("sounds", File.Sounds);
                CodeTask.RunOneThing("paths", File.Paths);
                CodeTask.RunOneThing("fonts", File.Fonts);
                CodeTask.RunOneThing("strings", File.Strings);
            }
            if (!Context.doThreads) return;

            if (_todo != null && _todo.Count > 0)
            {
                // ok, we have the jobs start them all up!
                foreach (var t in _todo) t.Task.Start();
           
                using (var progress = new ProgressBar())
                {
                    ErrorContext.ProgressBar = progress;
                    int totalTasks = _todo.Count;
                    int tasksDone = 0;
                    // foreach (var ct in _todo) totalTasks += ct.TotalTasks;
                    while (tasksDone < totalTasks)
                    {
                        totalTasks = 0;
                        tasksDone = 0;
                        for (int i = _todo.Count - 1; i >= 0; i--)
                        {
                            var ct = _todo[i];
                            if (ct != null)
                            {
                                if(ct.Task.IsFaulted)
                                {
                                    ExceptionHandler(ct.Name, ct.Task);
                                    Context.FatalError("Fatal exception in task");
                                }
                                if(ct.Task.IsCompleted) _todo.RemoveAt(i);
                                else
                                {
                                    totalTasks += ct.TotalTasks;
                                    tasksDone += ct.TasksFinished;
                                }
                            } else _todo.RemoveAt(i);
                        }
                        if(totalTasks > 0) progress.Report((double)tasksDone / totalTasks);
                        Thread.Sleep(50);
                    };
                }

                ErrorContext.ProgressBar = null;
            }
        }
    }
}