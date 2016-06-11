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
        static string[] eveything = { "code", "textures", "objects", "scripts" };
        public void AddAction(string start_name)
        {
            if (_todo == null) _todo = new List<CodeTask>();
            string name = start_name.Trim().ToLower();
            if (name == "everything")
            {
                foreach (var e in eveything) _AddAction(e);
            }
            else _AddAction(name);
        }
        void _AddAction(string name)
        {
            CodeTask progress = new CodeTask();
            progress.Name = name;
     
            switch (name)
            {
                case "code":
                    progress.Task = RunAllSimple("code", progress, File.Codes, (string path, File.Code s) =>
                    {
                        string filename = Path.ChangeExtension(Path.Combine(path, s.Name), "js");
                        using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(s);
                    });
                    break;
                case "textures":
                    progress.Task = RunAllSimple("textures", progress, File.Textures, (string path, File.Texture t) =>
                        {
                            string filename = Path.ChangeExtension(Path.Combine(path, "texture_" + t.Index), "png");
                            using (FileStream fs = new FileStream(filename, FileMode.Create)) t.Data.CopyTo(fs);
                        });
                    break;
                case "objects":
                    progress.Task = RunAllSimple("objects", progress, File.Objects, (string path, File.GObject o) =>
                    {
                        string filename = Path.Combine(path, o.Name);
                        BlockToCode output = CreateOutput(o.Name);
                        GetScriptWriter(output).Write(o);
                        output.WriteToFile(filename);
                    });
                    break;
                case "scripts":
                    progress.Task = RunAllSimple("scripts", progress, File.Scripts, (string path, File.Script s) =>
                    {
                        string filename = Path.Combine(path, s.Name);
                        BlockToCode output = CreateOutput(s.Name);
                        GetScriptWriter(output).Write(s);
                        output.WriteToFile(filename);

                    });
                   
                    break;
                case "everything":

                default:
                    Context.Error("{0} is not a valid chunk type", name);
                    return;

            }
            _todo.Add(progress);
        }

        class CodeTask : IProgress<double>
        {
            public string Name;
            public Task<TimeSpan> Task;
            double _currentProgress = 0.0;
            public double CurrentProgress { get { return _currentProgress; } }
            public void Report(double value)
            {
                // Make sure value is in [0..1] range
                value = Math.Max(0, Math.Min(1, value));
                Interlocked.Exchange(ref _currentProgress, value);
            }
        }
        public AllWriter()
        {
            /*

          AddAction("scripts", (IProgress<double> progress) =>
          {
              return RunAllSimple("scripts", progress, File.Scripts, (string path, File.Script s) =>
              {
                  string filename = Path.Combine(path, s.Name);
                  BlockToCode output = CreateOutput(s.Name);
                  GetScriptWriter(output).Write(s);
                  output.WriteToFile(filename);

              });

          });
          AddAction("sprites", (IProgress<double> progress) =>
          {
              return RunAllSimple("sprites", progress, File.Sprites);
          });

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
                  if (Context.saveAllMasks)
                  {
                      RunAllSimple("sprites", "Sprite Masks", File.Sprites, (string path, File.Sprite s) =>
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
                      });
                  }
              }
          });
          AddAction("rooms", () =>
          {
              RunAllSimple("rooms", "Rooms", File.Rooms, (string path, File.Room room) =>
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
                  RunOneThing(path, room);
              });
          });
          AddAction("backgrounds" ,() =>
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
          });
          AddAction("sounds", () =>
          {
              RunAllSimple("sounds", "Sounds", File.Sounds, (string path, File.AudioFile s) =>
              {
                  if (s.Data == null) return;
                  string filename = Path.ChangeExtension(Path.Combine(path, s.Name), s.extension);
                  using (FileStream fs = new FileStream(filename, FileMode.Create)) s.Data.CopyTo(fs);
              });
              string settings_filename = Path.Combine(Directory.CreateDirectory("sounds").FullName, "sound_settings");
              RunOneThing(settings_filename, File.Sounds);
          });


          AddAction("fonts" , () =>
          {

              RunAllSimple("fonts", "Fonts", File.Fonts);
              if (Context.saveAllPngs)
              {
                  RunAllSimple("sounds", "Sounds", File.Fonts, (string path, File.Font f) =>
                  {
                      string filename = Path.ChangeExtension(Path.Combine(path, f.Name), ".png"); // we just have one
                      f.Frame.Image.Save(filename);
                  });
              }
          });
          AddAction("paths" , () => RunAllSimple("paths", "Paths", File.Paths));

            */
        }
        //    public IReadOnlyDictionary<string, Action> ActionLookup { get { return actionLookup; } }

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
        public void RunOneThing<T>(string filename, IReadOnlyList<T> o) where T : File.GameMakerStructure, File.INamedResrouce
        {
            using (FileStream writer = new FileStream(FixFilenameExtensionForSerialzer(filename), FileMode.Create))
            {
                var serializer = GetSerializer(o.GetType());
                serializer.WriteObject(writer, o);
            }
        }
        // helper if you need to do one task then another
        public Task<TimeSpan> RunAllSimple<T>(string path_name, IProgress<double> progress, IReadOnlyList<T> list, Action<string, T> action) where T : File.GameMakerStructure
        {
            DateTime start = DateTime.Now;
            var path = DeleteAllAndCreateDirectory(path_name);
            if (Context.doThreads)
            {
                List<Task> insideTasks = new List<Task>();
                foreach (var o in list)
                {
                    Task t = Task.Factory.StartNew(() =>
                    {
                        lock (o) action(path, o);
                    });
                    insideTasks.Add(t);
                }
                Task[] ret = insideTasks.ToArray();
                return Task.Factory.StartNew<TimeSpan>(() =>
                {
                    int tasksDone = 0;
                    while (tasksDone < ret.Length)
                    {
                        int currentDone = 0;
                        foreach (var t in ret) if (t.IsCompleted) currentDone++;
                        if (currentDone != tasksDone)
                        {
                            tasksDone = currentDone;
                            progress.Report((double) tasksDone / (double) ret.Length);

                        }
                        Thread.Sleep(10); // sleep for 10 ms
                    }
                    return DateTime.Now - start;
                });
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var o = list[i];
                    progress.Report((double) i / (double) list.Count);
                    action(path, o);
                }
                return Task.FromResult<TimeSpan>(DateTime.Now - start);
            }
        }
        public Task<TimeSpan> RunAllSimple<T>(string path_name, IProgress<double> progress, IReadOnlyList<T> list) where T : File.GameMakerStructure, File.INamedResrouce
        {
            return RunAllSimple(path_name, progress, list, RunOneThing);
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
            if (_todo != null && _todo.Count > 0)
            {
                // we start here so we can keep track of progress.  But we don't know
                // how many tasks we have till here, so while we SHOULD start them all when they are 
                // created, I like using this progress bar
                using (var progress = new ProgressBar())
                {
                    ErrorContext.ProgressBar = progress;
                    double smallest = 0.0;
                    while (_todo.Count > 0)
                        for (int i = _todo.Count - 1; i >= 0; i--)
                        {
                            var ct = _todo[i];
                            if (ct.Task.IsCompleted)
                            {
                                Context.Message("{0} finished in {1}", ct.Name, ct.Task.Result);
                                _todo.RemoveAt(i);

                            }
                            else
                            {
                               // if (smallest > ct.CurrentProgress)
                                    smallest = ct.CurrentProgress;
                            }
                        }
                    progress.Report(smallest);
                    Thread.Sleep(10);
                }

                ErrorContext.ProgressBar = null;
            }
        }
    }
}