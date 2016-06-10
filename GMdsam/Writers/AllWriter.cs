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
        ConcurrentBag<string> scrptnames;
        ConcurrentBag<string> objnames;
        DirectoryInfo scriptDirectory = null;
        DirectoryInfo objectDirectory = null;
        List<Task> tasks = new List<Task>();
 

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
        Dictionary<string, Action> actionLookup;
        public AllWriter()
        {
            actionLookup = new Dictionary<string, Action>();
            actionLookup["backgrounds"] = () =>
            {
                RunAllSimple("backgrounds", "Backgrounds", File.Backgrounds);
                if (Context.saveAllPngs)
                {   // We want to save each individual sprite.  this eats ALOT of time
                    RunAllSimple("backgrounds", "Background Images", File.Backgrounds, (string path, File.Background b) =>
                    {
                        string filename = Path.ChangeExtension(Path.Combine(path, b.Name), ".png"); // we just have one
                        b.Frame.Image.Save(filename);
                    });
                }
            };
            actionLookup["objects"] = () =>
            {

                RunAllSimple("objects", "Objects", File.Objects, (string path, File.GObject o) =>
                {
                    string filename = Path.Combine(path, o.Name);
                    BlockToCode output = CreateOutput(o.Name);
                    GetScriptWriter(output).Write(o);
                    output.WriteToFile(filename);
                });
            };
            actionLookup["scripts"] = () =>
            {
                RunAllSimple("scripts", "Scripts", File.Scripts, (string path, File.Script s) =>
                {
                    string filename = Path.Combine(path,s.Name);
                    BlockToCode output = CreateOutput(s.Name);
                    GetScriptWriter(output).Write(s);
                    output.WriteToFile(filename);

                });

            }; ;
            actionLookup["sprites"] = () =>
            {
                RunAllSimple("sprites", "Sprites", File.Sprites);
                if (Context.saveAllPngs)
                {   // We want to save each individual sprite.  this eats ALOT of time
                    RunAllSimple("sprites", "Sprite Images", File.Sprites, (string path, File.Sprite s) =>
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

                    });
                }
                if (Context.saveAllMasks)
                {
                    RunAllSimple("sprites", "Sprite Masks", File.Sprites, (string path, File.Sprite s) =>
                    {
                        if (s.Masks.Count == 0) return;
                        else if (s.Masks.Count == 1)
                        {
                            string filename = Path.ChangeExtension(Path.Combine(path, s.Name + "_mask"), ".png"); // we just have one
                            s.Masks[0].Save(filename);
                        } else
                        {
                            for (int i = 0; i < s.Masks.Count; i++)
                            {
                                string filename = Path.ChangeExtension(Path.Combine(path, s.Name + "_mask_" + i), ".png"); // we just have one
                                s.Masks[i].Save(filename);
                            }
                        }
                    });
                }
            };
            actionLookup["rooms"] = () =>
            {
                RunAllSimple("rooms", "Rooms: ", File.Rooms);
            };
            actionLookup["code"] = () =>
            {
                RunAllSimple("code", "Codes", File.Codes, (string path, File.Code s) =>
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, s.Name),"js");
                    using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(s);

                });

            }; RunAllSimple("code", "Code", File.Codes);
            actionLookup["fonts"] = () =>
            {
                RunAllSimple("fonts", "Fonts", File.Fonts);
            };
            actionLookup["sounds"] = () =>
            {
                RunAllSimple("sounds", "Sounds", File.Sounds, (string path, File.AudioFile s) =>
                {
                    if (s.getStream() == null) return;
                    string filename = Path.ChangeExtension(Path.Combine(path, s.Name), s.extension);
                    using (FileStream fs = new FileStream(filename, FileMode.Create)) s.getStream().CopyTo(fs);
                });
                string settings_filename = Path.Combine(Directory.CreateDirectory("sounds").FullName, "sound_settings");
                RunOneThing(settings_filename, File.Sounds);
            };
            actionLookup["textures"] = () =>
            {
                RunAllSimple("textures", "Textures", File.Textures, (string path, File.Texture t) =>
                  {
                      string filename = Path.ChangeExtension(Path.Combine(path, "texture_" + t.Index), "png");
                      using (FileStream fs = new FileStream(filename, FileMode.Create)) t.getStream().CopyTo(fs);
                  });
            };
        }
        public IReadOnlyDictionary<string, Action> ActionLookup { get { return actionLookup; } }

        static CodeWriter GetScriptWriter(BlockToCode output)
        {
            switch (Context.outputType)
            {
                case OutputType.LoveLua:
                    return (CodeWriter) new Lua.Writer(output);
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
            if (!directoryDeleted.Contains(dir)) {
         //       foreach (var f in directory.GetFiles()) f.Delete(); // skip this for right now
                directoryDeleted.Add(dir);
            }
            return directory.FullName;
        }

        public void DoneMessage(Task[] tasks, DateTime start, string name)
        {
            if(tasks != null) Task.WaitAll(tasks);
            Context.Message("{0} Finished in {1}", name, DateTime.Now - start);
        }
        public void RunOneThing<T>(string path, T o) where T : File.GameMakerStructure, File.INamedResrouce
        {
            string filename = Path.Combine(path, o.Name);
            if (Context.doXML)
            {
                Type t = o.GetType();
                FileStream writer = new FileStream(Path.ChangeExtension(filename, "xml"), FileMode.Create);
                DataContractSerializer fmt = new DataContractSerializer(t);
                fmt.WriteObject(writer, o);
                writer.Close();
            }
            else
            {
                using (ResourceFormater fmt = new ResourceFormater(Path.ChangeExtension(filename, "json"))) fmt.Write((dynamic) o);
            }
        }
        public void RunOneThing<T>(string filename, IReadOnlyList<T> o) where T : File.GameMakerStructure, File.INamedResrouce
        {
            if (Context.doXML)
            {
                Type t = o.GetType();
                FileStream writer = new FileStream(Path.ChangeExtension(filename, "xml"), FileMode.Create);
                DataContractSerializer fmt = new DataContractSerializer(t);
                fmt.WriteObject(writer, o);
                writer.Close();
            }
            else
            {
                using (ResourceFormater fmt = new ResourceFormater(Path.ChangeExtension(filename, "json"))) fmt.Write((dynamic) o);
            }
        }
       
        public void RunAllSimple<T>(string path_name, string name, IReadOnlyList<T> list, Action<string,T> action) where T : File.GameMakerStructure
        {
            DateTime start = DateTime.Now;
            var path = DeleteAllAndCreateDirectory(path_name);
            if (Context.doThreads)
            {
                List<Task> insideTasks = new List<Task>();
                foreach (var o in list)
                {
                    insideTasks.Add(Task.Factory.StartNew(() =>
                    {
                        lock (o) action(path, o);
                    }));
                }
                   
               
                tasks.AddRange(insideTasks);
                if(name != null)
                {
                    Task fin = Task.Factory.StartNew(() => DoneMessage(insideTasks.ToArray(), start, name));
                    tasks.Add(fin);
                }
            }
            else
            {
                using (ProgressBar bar = new ProgressBar(name))
                {
                    ErrorContext.ProgressBar = bar;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var o = list[i];
                        bar.Report((double) i / list.Count);
                        action(path, o);
                    }
                    ErrorContext.ProgressBar = null;
                }
                DoneMessage(null, start, name);
            }
        }
        public void RunAllSimple<T>(string path_name, string name, IReadOnlyList<T> list) where T : File.GameMakerStructure, File.INamedResrouce
        {
            RunAllSimple(path_name, name, list, RunOneThing);
        }


        void ExceptionHandler(Task task)
        {
            var exception = task.Exception;
            Context.Error(exception.ToString());
            throw exception;
        }
        public void FinishProcessing()
        {
            if (tasks.Count > 0)
            {
                // we start here so we can keep track of progress.  But we don't know
                // how many tasks we have till here, so while we SHOULD start them all when they are 
                // created, I like using this progress bar
                using(var progress = new ProgressBar()) {
                    ErrorContext.ProgressBar = progress;
                    int total = tasks.Count;
                    int tasksDone = 0;
                //    foreach (var t in tasks) t.Start();
                    while (tasksDone < total)
                    {
                        tasksDone = 0;
                        foreach (var t in tasks) if (t.IsCompleted) tasksDone++;
                        progress.Report((double) tasksDone / total);
                    }
                    ErrorContext.ProgressBar = null;
                }
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
                    foreach (var c in ILNode.times.OrderBy(x => x.Time)) sw.WriteLine("Count={0}  Time={1}", c.Count, c.Time);
                }
            }
        }
    }
}