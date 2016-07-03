using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameMaker.Dissasembler;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using GameMaker.Writers;

namespace GameMaker
{
  
    static class Program
    {
        class TextWriterSaver : TextWriter
        {
            char prev = default(char);
            StreamWriter redirect;
            TextWriter original;
            bool headerWritten = false;
            TextWriterSaver() { }
            public static void ClearConsoleErrorRedirect()
            {
                TextWriterSaver sww = Console.Error as TextWriterSaver;
                if (sww!= null)
                {
                    Console.SetError(sww.original);
                    sww.redirect.WriteLine();
                    sww.redirect.Close();
                }
            }
            public static void ClearConsoleRedirect()
            {
                TextWriterSaver sww = Console.Out as TextWriterSaver;
                if (sww!= null)
                {
                    Console.SetError(sww.original);
                    sww.redirect.WriteLine();
                    sww.redirect.Close();
                }
            }
            public static void RedirectConsoleError(string filename)
            {
                ClearConsoleErrorRedirect();
                FileStream fs = new FileStream(filename, FileMode.Append);
                TextWriterSaver sww = new TextWriterSaver() { original = Console.Error, redirect = new StreamWriter(fs, Console.Error.Encoding) };
                sww.WriteLine("Started Error Redirect");
                Console.SetError(sww);
            }
            public static void RedirectConsole(string filename)
            {
                ClearConsoleErrorRedirect();
                FileStream fs = new FileStream(filename, FileMode.Append);
                TextWriterSaver sww = new TextWriterSaver() { original = Console.Error, redirect = new StreamWriter(fs, Console.Error.Encoding) };
                Console.SetOut(sww);
                sww.WriteLine("Started Console Redirect");
            }
            void WriteHeadder()
            {
                string time = string.Format("{0}: ", DateTime.Now.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture));
                if (original != null) original.Write(time);
                if (redirect != null) redirect.Write(time);
            }
            public override void WriteLine()
            {
                if (original != null) original.WriteLine();
                if (redirect != null) redirect.WriteLine();
                headerWritten = false;
            }
            public override void Write(char c)
            {
                // fix newlines with a simple state machine
                if (prev == '\n' || prev == '\r')
                {
                    if (prev != c && (c == '\n' || c == '\r'))
                    {
                        prev = default(char);
                        return;// skip
                    }
                }
                prev = c;
                if (!headerWritten)
                {
                    WriteHeadder();
                    headerWritten = true;
                }
                if (c == '\n' || c == '\r') WriteLine();
                else
                {
                    if (original != null) original.Write(c);
                    if (redirect != null) redirect.Write(c);
                }
            }
            public override Encoding Encoding
            {
                get { return original.Encoding; }
            }
        }
       public static void EnviromentExit(int i)
        {
            Ast.ILVariable.SaveAllVarRefs();
            Environment.Exit(i);
        }
        static void InstructionError(string message)
        {
            Console.WriteLine("Useage <exe> data.win <-png> <-mask>  [-all (objects|scripts|paths|codes|textures|sprites|sounds)");
            Console.WriteLine("<-png> <-mask> will cut out and save all the masks and png's for sprites and backgrounds.  This dosn't effect textures though");
            if (message != null)
            {
                Context.FatalError(message);
            }
            EnviromentExit(1);

        }
        static void InstructionError(string message, params object[] o)
        {
            InstructionError(string.Format(message, o));
        }
        static void GoodExit()
        {
            Console.WriteLine("All Done!");
            ErrorContext.StopErrorSystem();
            EnviromentExit(0);
        }
        static void PatchMode()
        {

        }
        static void Main(string[] args)
        {
   
            // Context.doThreads = false;
            //  Context.doXML = true;
            //  Context.doAssigmentOffsets = true;
            // ugh have to do it here?
            string dataWinFileName = args.ElementAtOrDefault(0);
            if (string.IsNullOrWhiteSpace(dataWinFileName))
            {
                InstructionError("Missing data.win file");
            }
            try
            {
                File.LoadDataWin(dataWinFileName);
               
            }
            catch (Exception e)
            {
                Context.Error(e);
                InstructionError("Could not open data.win file {0}\n Exception:", dataWinFileName, e.Message);
            }
            List<string> chunks = new List<string>();
            byte[] changedData = null;
            for(int i=1; i < args.Length; i++) {
                string a = args[i];
                if (string.IsNullOrWhiteSpace(a)) continue; // does this ever happen?
                switch (a)
                {
                    case "-changeInt":
                        {
                            Context.saveChangedDataWin = true;

                            string offset_name = args.ElementAtOrDefault(i + 1);
                            string to_value = args.ElementAtOrDefault(i + 2);
                            i += 2;
                            uint offset_int;
                            short to_int;
                            if (!uint.TryParse(offset_name, out offset_int)) Context.FatalError("Cannot parse offset value in -changeInt");
                            if (!short.TryParse(to_value, out to_int)) Context.FatalError("Cannot parse short value in -changeInt");
                            if (changedData == null)
                            {
                                changedData = File.CopyData();
                                File.LoadEveything();
                            }
                            File.ChangeOffset(changedData, offset_int, to_int);
                        }
                        break;
                    case "-changeShort":
                        {
                            Context.saveChangedDataWin = true;

                            string offset_name = args.ElementAtOrDefault(i + 1);
                            string to_value = args.ElementAtOrDefault(i + 2);
                            i += 2;
                            uint offset_int;
                            int to_int;
                            if (!uint.TryParse(offset_name, out offset_int)) Context.FatalError("Cannot parse offset value in -changeShort");
                            if (!int.TryParse(to_value, out to_int)) Context.FatalError("Cannot parse int value in -changeShort");
                            if (changedData == null)
                            {
                                changedData = File.CopyData();
                                File.LoadEveything();
                            }
                            File.ChangeOffset(changedData, offset_int, to_int);
                        }
                        break;
                    case "-changeVar":
                        {
                            Context.saveChangedDataWin = true;
                            string code_name = args.ElementAtOrDefault(i + 1);
                            string var_name = args.ElementAtOrDefault(i + 2) ?? "";
                            string from_value = args.ElementAtOrDefault(i + 3) ?? "";
                            string to_value = args.ElementAtOrDefault(i + 4) ?? "";
                            int from_int;
                            int to_int;
                            if (!int.TryParse(from_value, out from_int)) Context.FatalError("Cannot parse from value in -change");
                            if (!int.TryParse(to_value, out to_int)) Context.FatalError("Cannot parse from value in -change");
                            i += 4;

                            if (changedData == null)
                            {
                                changedData = File.CopyData();
                                File.LoadEveything();
                            }

                            File.Code code=null;
                            File.Script script=null;
                            if (File.TryLookup(code_name, out code) || File.TryLookup(code_name, out script))
                            {
                                if (script != null) code = script.Code;
                           
                                File.ChangeVarValue(changedData, code, var_name, from_int, to_int);
                            }
                            else Context.Error("'{0}' code/script not found", code_name);
                        }
                        break;
                    case "-output":
                        {
                            i++;
                            string dir = args.ElementAtOrDefault(i);
                            if(string.IsNullOrWhiteSpace(dir)) InstructionError("Missing otuput directory");
                            Context.CheckAndSetOutputDirectory(dir);
                        }
                        break;
                    case "-delete":
                        Context.deleteDirectorys = true;
                        break;
                    case "-lua":
                        Context.doLua = true;
                        Context.doXML = false;
                        break;
                    case "-oneFile":
                        Context.oneFile = true;
                        break;
                    case "-constOffsets":
                        Context.doAssigmentOffsets = true;
                        break;
                    case "-mask":
                        Context.saveAllMasks = true;
                        break;
                    case "-png":
                        Context.saveAllPngs = true;
                        break;
                    case "-xml":
                        Context.doXML = true;
                        break;
                    case "-search":
                        Context.doSearch = true;
                        break;
                    case "-json":
                        Context.doXML = false;
                        break;
                    case "-old":
                        Context.Version = UndertaleVersion.V10000;
                        break;
                    case "-debug":
                        Context.Debug = true;
                        break;
                    case "-nothread":
                        Context.doThreads = false;
                        break;
                    case "-watch":
                        Context.debugSearch = true;
                        break;
                    default:
                        if (a[0] == '-') InstructionError("bad flag '{0}'", a);
                        if (char.IsLetter(a[0])) chunks.Add(a);
                        break;
                }
            }
            Context.CheckAndSetOutputDirectory();
           
            ErrorContext.StartErrorSystem();
            if (Context.saveChangedDataWin)
            {
                using (var file = Context.CreateFileStream("changed_data.win", FileMode.Create, true))
                {
                    file.Write(changedData, 0, changedData.Length);
                    file.Flush();
                }
            } else
            {
                File.LoadEveything();
                var w = new Writers.AllWriter();
                if (Context.doSearch)
                {
                    var results = File.Search(chunks);
                    if (results.Count == 0) Context.Error("No data found in search");
                    string path = ".";
                    if (Context.doThreads)
                    {
                        Parallel.ForEach(results, result => AllWriter.DoSingleItem(path, result));
                    }
                    else
                    {
                        foreach (var result in results) AllWriter.DoSingleItem(path, result);
                    }
                }
                else if (Context.debugSearch)
                {
                    Context.doThreads = false;
                    Context.Debug = true;
                    var results = File.Search(chunks);
                    if (results.Count == 0) Context.FatalError("No data found in search");
                    foreach (var f in new DirectoryInfo(".").GetFiles("*.txt"))
                    {
                        if (System.IO.Path.GetFileName(f.Name) != "errors.txt") f.Delete(); // clear out eveything
                    }
                    foreach (var a in results)
                    {
                        File.Code c = a as File.Code;
                        if (c != null)
                        {
                            var error = new ErrorContext(c.Name);
                            error.Message("Decompiling");
                            Context.Debug = true;
                            var block = c.Block;
                            if (block != null)
                            {
                                using (Writers.BlockToCode to = new Writers.BlockToCode(c.Name + "_watch.js"))
                                    to.Write(block);
                                error.Message("Finished Decompiling");
                            }
                            else error.Message("Block is null");
                        }
                        else Context.Error("Code '{0} not found", a);
                    }
                    //Context.HackyDebugWatch = new HashSet<string>(chunks);
                    // w.AddAction("code");
                }
                else
                {
                    if (chunks.Count == 0) chunks.Add("everything");
                    foreach (var a in chunks) w.AddAction(a);
                }
                w.FinishProcessing();
            }   
            GoodExit();
        }
    }
}