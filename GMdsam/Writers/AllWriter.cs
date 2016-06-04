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
    
    public class AllWriter
    {
        // http://stackoverflow.com/questions/4580397/json-formatter-in-c
      

        ConcurrentBag<string> globals_vars = new ConcurrentBag<string>();
        ConcurrentBag<string> globals_arrays = new ConcurrentBag<string>();
        ConcurrentBag<string> scrptnames;
        ConcurrentBag<string> objnames;
        DirectoryInfo scriptDirectory = null;
        DirectoryInfo objectDirectory = null;
        List<Task> tasks = new List<Task>();
        public static string QuickCodeToLine(File.Code code,string context)
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
        string CodeToSingleLine(File.Code c, string context, bool keep_newline=false)
        {
            string code = null;
            if(c.Size ==0) Debug.WriteLine("Code '" + c.Name + "' has no data but is regestered?");
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
            } return null;
        }
        Dictionary<string, Action> actionLookup;
        public AllWriter()
        {
            actionLookup = new Dictionary<string, Action>();
            actionLookup["backgrounds"] = StartWriteAllBackgrounds;
            actionLookup["objects"] = StartWriteAllObjects;
            actionLookup["scripts"] = StartWriteAllScripts;
            actionLookup["sprites"] = StartWriteAllSprites;
            actionLookup["rooms"] = StartWriteAllRooms;
            actionLookup["code"] = StartWriteAllCode;
            actionLookup["fonts"] = StartWriteAllFonts;
            actionLookup["sounds"] = StartWriteAllSounds;
            actionLookup["textures"] = StartAllTextures;
        }
        public IReadOnlyDictionary<string,Action> ActionLookup {  get { return actionLookup; } }

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
        // Used to replace all repeating ;; and to put a space
        static Regex regex_commas = new Regex(@";(;*|[^ ])", RegexOptions.Compiled);
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

        public void StartAllTextures()
        {
            var path = DeleteAllAndCreateDirectory("textures");
            foreach (File.Texture o in File.Textures)
            {
                RunTask(() =>
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, "texture_" + o.Index), "png");
                    using (FileStream fs = new FileStream(filename, FileMode.Create)) o.getStream().CopyTo(fs);
                });
            }
        }
        public void StartWriteAllRooms()
        {
            var path = DeleteAllAndCreateDirectory("rooms");
            foreach (var o in File.Rooms) {
                RunTask(() =>
                {
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name), "json");
                    using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(o);
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
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name), "json");
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
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name), "json");
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
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name), "json");
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
                    string filename = Path.ChangeExtension(Path.Combine(path, o.Name), "json");
                    using (ResourceFormater fmt = new ResourceFormater(filename)) fmt.Write(o);
                });
            }
        }
        public void StartWriteAllSounds()
        {
            var path = DeleteAllAndCreateDirectory("sounds");
            RunTask(() =>
            {
                string sounds_info = Path.ChangeExtension(Path.Combine(path, "sound_settings"), "json");
                ResourceFormater fmt = new ResourceFormater(sounds_info);
                fmt.WriteAll(File.Sounds);
            });
            foreach (var o in File.Sounds)
            {
                Stream data = o.Data;
                if(data != null)
                {
                    RunTask(() =>
                    {
                        string filename = Path.ChangeExtension(Path.Combine(path, o.Name), o.extension);
                        using (FileStream fs = new FileStream(filename, FileMode.Create)) data.CopyTo(fs);
                    });
                }  
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