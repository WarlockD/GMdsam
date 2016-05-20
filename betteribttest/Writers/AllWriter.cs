using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace GameMaker.Writers
{
    // this class Writes EVEYTHING.
    // Idea being that we can take all the varriables, figure out when and how they are being used
    // and make sure the initalizers are set up for it

    public class AllWriter
    {
        HashSet<ILVariable> globals = new HashSet<ILVariable>();
        List<string> scrptnames = new List<string>();
        List<string> objnames = new List<string>();
        DirectoryInfo scriptDirectory = null;
        DirectoryInfo objectDirectory = null;
        List<Task> tasks = new List<Task>();

        public AllWriter()
        {
        }
        void AddGlobals(BlockToCode output)
        {
            lock (globals)
            {
                var list = output.VariablesUsed;
                var nlist = output.VariablesUsed.Select(x => x.Node).Where(x => x.isGlobal).ToList();
                globals.UnionWith(nlist);
            }
        }
        IScriptWriter GetScriptWriter()
        {
            switch (Context.outputType)
            {
                case OutputType.LoveLua:
                    return (IScriptWriter)new Lua.ScriptWriter();
                case OutputType.JavaScript:
                    return (IScriptWriter) new JavaScript.ScriptWriter();
                default:
                    throw new Exception("Bad output type");

            }
        }
        IObjectWriter GetObjectWriter()
        {
            switch (Context.outputType)
            {
                case OutputType.LoveLua:
                    return (IObjectWriter) new Lua.ObjectWriter();
                case OutputType.JavaScript:
                    return (IObjectWriter) new JavaScript.ObjectWriter();
                default:
                    throw new Exception("Bad output type");

            }
        }
        BlockToCode CreateOutput(string filename)
        {
            ICodeFormater formater = null;
            INodeMutater mutater = null;
            switch (Context.outputType)
            {
                case OutputType.LoveLua:
                    formater = new Lua.Formater();
                    mutater = new Lua.Mutater();
                    break;
                case OutputType.JavaScript:
                    formater = new JavaScript.Formater();
                    break;
                default:
                    throw new Exception("Bad output type type");
            }
            BlockToCode output = new BlockToCode( formater, filename);
            output.Mutater = mutater;
            return output;
        }
        void Run(File.Script s, string filename=null)
        {
            Context.DebugName = s.Name;
            using (BlockToCode output = CreateOutput(filename))
            {
                GetScriptWriter().WriteScript( s, output);
                if (Context.doGlobals) AddGlobals(output);
            }
        }
        void Run(File.GObject obj, string filename=null)
        {
            BlockToCode output = CreateOutput(filename);
            GetObjectWriter().WriteObject( obj, output);
            if (Context.doGlobals) AddGlobals(output);
        }
        void RunTask(File.GObject obj, string path=null)
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
        void RunTask(File.Script s, string path = null)
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
                    RunTask(obj);
                    while(obj.Parent > -1)
                    {
                        var p = File.Objects[obj.Parent];
                        Context.Info("    Found Parent '{0}': ", p.Name);
                        RunTask(p);
                        obj = p;
                    }
                    continue;
                }
                File.Script s = a as File.Script;
                if (s != null)
                {
                    Context.Info("Found Script '{0}': ", s.Name);
                    RunTask(s);
                    continue;
                }
                Context.Info("Found Type '{0}' of Name '{1}': ", a.GetType().ToString(),  a.Name);
            }
        }
        public void StartWriteAllScripts()
        {
            scriptDirectory = Directory.CreateDirectory("scripts");
            foreach (var s in File.Scripts)
            {
                if (s.Data == null) continue;
                scrptnames.Add(s.Name);
                string filename = Path.Combine(scriptDirectory.FullName, s.Name);
                RunTask(s, filename);
            }
        }
        public void StartWriteAllObjects()
        {
            objectDirectory = Directory.CreateDirectory("objects");
            foreach (var o in File.Objects)
            {
                objnames.Add(o.Name);
                string filename = Path.Combine(scriptDirectory.FullName, o.Name);
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
                tasks.Clear();
            }
            if(scrptnames.Count > 0)
            {
                using (StreamWriter sw = new StreamWriter("loadScripts.lua"))
                {
                    foreach (var s in scrptnames) sw.WriteLine("require 'scripts/{0}'", s);
                }
            }
           if(objnames.Count > 0)
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
                    bool need_comma = false;
                    foreach (var v in globals)
                    {
                        if (need_comma) sw.Write(", ");
                        else sw.Write("  ");
                        sw.Write(v.Name);
                        sw.Write(" = ");
                        if (v.isArray) sw.Write("{}");
                        else sw.Write("0");
                        sw.WriteLine();
                        need_comma = true;
                    }
                    sw.WriteLine("} ");
                    sw.WriteLine("globals = g");
                }
            }
           
        }
    }
}