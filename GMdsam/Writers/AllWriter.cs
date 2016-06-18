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

    public class AllWriter
    {

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
                foreach(var v in chunkActions.Values) v(_todo);
            }
            else
            {
                Action<List<CodeTask>> action;
                if (chunkActions.TryGetValue(name, out action)) action(_todo);
                else Context.FatalError("Unkonwn chunk name'{0}'", start_name);
            }
        }
        class CodeTask : IProgress<int> , IProgress<double>
        {
            public static void RunOneThing<T>(string filename, IReadOnlyList<T> o) where T : File.GameMakerStructure, File.INamedResrouce
            {
                using (FileStream writer = new FileStream(FixFilenameExtensionForSerialzer(filename), FileMode.Create))
                {
                    var serializer = GetSerializer(o.GetType());
                    serializer.WriteObject(writer, o);
                }
            }
            public static void RunOneThing<T>(string path, T o) where T : File.GameMakerStructure, File.INamedResrouce
            {
                string filename = FixFilenameExtensionForSerialzer(Path.Combine(path, o.Name));
                using (FileStream writer = new FileStream(filename, FileMode.Create))
                {
                    var serializer = GetSerializer(o.GetType());
                    serializer.WriteObject(writer, o);
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
                var path = DeleteAllAndCreateDirectory(path_name);
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
            static void faultMessage(AggregateException exception, CodeTask ct)
            {

            }
            public static CodeTask Create<T>(string path_name, string task_name, IReadOnlyList<T> list, Action<string, T> action) where T : File.GameMakerStructure
            {
                DateTime start = DateTime.Now;
                task_name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(task_name.ToLower());
                var path = DeleteAllAndCreateDirectory(path_name);
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
            int _tasksFinished=0;
            double _percentDone = 0.0;
            public double PercentDone {  get { return TotalTasks != 0 ? (double)_tasksFinished / TotalTasks : _percentDone; } }
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
        static AllWriter()
        {
            chunkActions = new Dictionary<string, Action<List<CodeTask>>>();

            chunkActions["code"] = (List<CodeTask> tasks) =>
            {
                tasks.Add(CodeTask.Create("code", File.Codes, (string path, File.Code s) =>
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, s.Name), "js");
                    using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(s);
                }));
            };

            chunkActions["textures"] = (List<CodeTask> tasks) =>
            {
                tasks.Add(CodeTask.Create("textures", File.Textures, (string path, File.Texture t) =>
                            {
                                string filename = Path.ChangeExtension(Path.Combine(path, "texture_" + t.Index), "png");
                                using (FileStream fs = new FileStream(filename, FileMode.Create)) t.Data.CopyTo(fs);
                            }));
            };

            chunkActions["objects"] = (List<CodeTask> tasks) =>
            {
                tasks.Add(CodeTask.Create("objects", File.Objects, (string path, File.GObject o) =>
                        {
                            string filename = Path.Combine(path, o.Name);
                            BlockToCode output = CreateOutput(o.Name);
                            GetScriptWriter(output).Write(o);
                            output.WriteToFile(filename);
                        }));
            };


            chunkActions["scripts"] = (List<CodeTask> tasks) =>
            {
                tasks.Add(CodeTask.Create("scripts", File.Scripts, (string path, File.Script s) =>
                        {
                            string filename = Path.Combine(path, s.Name);
                            BlockToCode output = CreateOutput(s.Name);
                            GetScriptWriter(output).Write(s);
                            output.WriteToFile(filename);

                        }));
            };

            chunkActions["sprites"] = (List<CodeTask> tasks) =>
            {
                tasks.Add(CodeTask.Create("sprites", File.Sprites));

                if (Context.saveAllPngs)
                {
                    tasks.Add(CodeTask.Create("sprites", "sprite images", File.Sprites, (string path, File.Sprite s) =>
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
                     }));
                }
                if (Context.saveAllMasks)
                {
                    tasks.Add(CodeTask.Create("sprites", "sprite masks", File.Sprites, (string path, File.Sprite s) =>
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
                            }));
                }
            };
            chunkActions["rooms"] = (List<CodeTask> tasks) =>
            {
                tasks.Add(CodeTask.Create("rooms", File.Rooms, (string path, File.Room room) =>
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
                        }));
            };
            chunkActions["backgrounds"] = (List<CodeTask> tasks) =>
            {
                tasks.Add(CodeTask.Create("backgrounds", File.Backgrounds));
                if (Context.saveAllPngs)
                {   // We want to save each individual sprite.  this eats ALOT of time
                    tasks.Add(CodeTask.Create("backgrounds", "background Images", File.Backgrounds, (string path, File.Background b) =>
                            {
                                string filename = Path.ChangeExtension(Path.Combine(path, b.Name), ".png"); // we just have one
                                b.Frame.Image.Save(filename);
                            }));
                }
            };
            chunkActions["sounds"] = (List<CodeTask> tasks) =>
            {
                tasks.Add(CodeTask.Create("sounds", "sound settings", File.Sounds, (string path, IReadOnlyList<File.AudioFile> list, IProgress<double> progress) =>
                        {
                            string settings_filename = Path.Combine(Directory.CreateDirectory("sounds").FullName, "sound_settings");
                            CodeTask.RunOneThing(settings_filename, File.Sounds);
                            progress.Report(1.0);
                        }));
                tasks.Add(CodeTask.Create("sounds", File.Sounds, (string path, File.AudioFile s) =>
                        {
                            if (s.Data == null) return;
                            string filename = Path.ChangeExtension(Path.Combine(path, s.Name), s.extension);
                            using (FileStream fs = new FileStream(filename, FileMode.Create)) s.Data.CopyTo(fs);
                        }));

            };
            chunkActions["fonts"] = (List<CodeTask> tasks) =>
            {
                tasks.Add(CodeTask.Create("fonts", File.Fonts));

                if (Context.saveAllPngs)
                {
                    tasks.Add(CodeTask.Create("sounds", "Sounds", File.Fonts, (string path, File.Font f) =>
                            {
                                string filename = Path.ChangeExtension(Path.Combine(path, f.Name), ".png"); // we just have one
                                f.Frame.Image.Save(filename);
                            }));
                }
            };
            chunkActions["paths"] = (List<CodeTask> tasks) =>
            {
                tasks.Add(CodeTask.Create("paths", File.Paths));
            };
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

        static HashSet<string> directoryDeleted = new HashSet<string>();
        static string DeleteAllAndCreateDirectory(string dir)
        {
            var directory = Directory.CreateDirectory(dir);
            if (!directoryDeleted.Contains(dir))
            {
                //       foreach (var f in directory.GetFiles()) f.Delete(); // skip this for right now
                directoryDeleted.Add(dir);
            }
            return directory.FullName;
        }

        public void DoneMessage(Task[] tasks, DateTime start, string name)
        {
            if (tasks != null) Task.WaitAll(tasks);
            Context.Message("{0} Finished in {1}", name, DateTime.Now - start);
        }
        public void DoneMessage(Task task, DateTime start, string name)
        {
            if (task != null) task.Wait();
            Context.Message("{0} Finished in {1}", name, DateTime.Now - start);
        }
        public void RunOneThing<T>(string path, T o) where T : File.GameMakerStructure, File.INamedResrouce
        {
            string filename = FixFilenameExtensionForSerialzer(Path.Combine(path, o.Name));
            using (FileStream writer = new FileStream(filename, FileMode.Create))
            {
                var serializer = GetSerializer(o.GetType());
                serializer.WriteObject(writer, o);
            }
        }
        static string FixFilenameExtensionForSerialzer(string filename)
        {
            return Path.ChangeExtension(filename, Context.doXML ? "xml" : "json");
        }
        static XmlObjectSerializer GetSerializer(Type t)
        {
            return Context.doXML ? (XmlObjectSerializer) new DataContractSerializer(t) : (XmlObjectSerializer) new DataContractJsonSerializer(t);
        }
     

        void ExceptionHandler(string name, string filename, Task task)
        {
            foreach (var e in task.Exception.Flatten().InnerExceptions)
            {
                Context.Error("{0}: Exception: {1}", e.Message);
            }
        }
        public void FinishProcessing()
        {
            if (!Context.doThreads) return;

            if (_todo != null && _todo.Count > 0)
            {
                // ok, we have the jobs start them all up!
                foreach(var t in _todo)
                {
                    if (t.Task.Status != TaskStatus.WaitingToRun) t.Task.Start();
                }
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
                            if (ct == null || ct.Task.IsCompleted) _todo.RemoveAt(i);
                            else
                            {
                                totalTasks += ct.TotalTasks;
                                tasksDone += ct.TasksFinished;
                            }
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