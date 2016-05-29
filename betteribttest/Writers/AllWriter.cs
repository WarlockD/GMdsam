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

namespace GameMaker.Writers
{
    // this class Writes EVEYTHING.
    // Idea being that we can take all the varriables, figure out when and how they are being used
    // and make sure the initalizers are set up for it

    public class AllWriter
    {
        ConcurrentBag<string> globals_vars = new ConcurrentBag<string>();
        ConcurrentBag<string> globals_arrays = new ConcurrentBag<string>();
        ConcurrentBag<string> scrptnames;
        ConcurrentBag<string> objnames;
        DirectoryInfo scriptDirectory = null;
        DirectoryInfo objectDirectory = null;
        List<Task> tasks = new List<Task>();

        public AllWriter()
        {
     
        }
        void AddGlobals(BlockToCode output)
        {
            foreach(var n in output.VariablesUsed.Select(x => x.Node).Where(x => x.isGlobal))
            {
                if (n.isArray)
                    globals_arrays.Add(n.Name);
                else
                    globals_vars.Add(n.Name);
            }
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
            ICodeFormater formater = null;
            INodeMutater mutater = null;
            switch (Context.outputType)
            {
                case OutputType.LoveLua:
                    formater = new Lua.Formater();
                    mutater = new Lua.Mutater();
                    break;
                case OutputType.GameMaker:
                    formater = new GameMaker.Formater();
                    break;
                default:
                    throw new Exception("Bad output type type");
            }
            BlockToCode output = new BlockToCode(formater,new Context.ErrorContext(name));
            output.Mutater = mutater;
            return output;
        }
        void Run(File.Script s, string filename=null)
        {
            BlockToCode output = CreateOutput(s.Name);
            GetScriptWriter(output).Write(s);
            if (Context.doGlobals) AddGlobals(output);
            output.WriteAsyncToFile(filename);
        }
     
        void Run(File.GObject obj, string filename)
        {
            BlockToCode output = CreateOutput(obj.Name);
            GetScriptWriter(output).Write(obj);
            if (Context.doGlobals) AddGlobals(output);
            output.WriteAsyncToFile(filename);
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