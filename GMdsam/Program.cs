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
            string progress_string = null;
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
        static void InstructionError(string message, params object[] o)
        {
            Console.WriteLine("Useage <exe> data.win <-asm> [-s search_term] [-all (objects|scripts)");
            Console.WriteLine("search_term will search all scripts or object names for the text and save that file as a *.cpp");
            Console.WriteLine("-asm will also write the bytecode dissasembly");
            Console.WriteLine("There will be some wierd gotos/labels in case statements.  Ignore them, I am still trying to find that bug");
            if (message != null)
            {
                Console.WriteLine("Error: ");
                if (o.Length > 0) message = string.Format(message, o);
                foreach (var s in message.Split('\n'))
                {
                    string msg;
                    if (s.Last() == '\r') msg = s.Remove(s.Length - 1);
                    else msg = s;
                    Console.WriteLine("   " + msg);
                    Debug.WriteLine(msg);
                }
            }
            Environment.Exit(-1);
        }
        static void GoodExit()
        {
            Console.WriteLine("All Done!");
            Environment.Exit(0);
        }
        static void Main(string[] args)
        {
         //   TextWriterSaver.RedirectConsole("console.txt");
            string dataWinFileName = args.ElementAtOrDefault(0);
            if (string.IsNullOrWhiteSpace(dataWinFileName))
            {
                InstructionError("Missing data.win file");
            }
#if !DEBUG
            try
            {
#endif
                File.LoadDataWin(dataWinFileName);
                File.LoadEveything();
#if !DEBUG
            }

            catch (Exception e)
            {
                InstructionError("Could not open data.win file {0}\n Exception:", dataWinFileName, e.Message);
            }
#endif
         //  Context.doThreads = false;
            string toSearch = null;
            int pos = 1;
            var w = new Writers.AllWriter();
            while (pos < args.Length)
            {
                switch (args[pos])
                {
                    case "-s":
                        {
                            pos++;
                            toSearch = args.ElementAtOrDefault(pos);
                            string option = args.ElementAtOrDefault(pos);
                            w.Search(toSearch, true);
                            pos = args.Length;
                        }
                        break;
                    case "-old":
                        pos++;
                        Context.Version = UndertaleVersion.V10000;
                        break;
                    case "-debug":
                        pos++;
                        Context.Debug = true;
                        break;
                    case "-nothreading":
                        pos++;
                        Context.doThreads = false;
                        break;
                    case "-all":
                        {
                            pos++;
                            string option = args.ElementAtOrDefault(pos);
                            List<Action> actions;
                            if (string.IsNullOrWhiteSpace(option) || option.ToLower() == "everything")
                                actions = w.ActionLookup.Select(x => x.Value).ToList();
                            else
                            {
                                actions = new List<Action>();
                                do
                                {
                                    Action action;
                                    if (!w.ActionLookup.TryGetValue(option.ToLower(), out action))
                                    {
                                        InstructionError("Invalide option '{0}' for -all", option);
                                    }
                                    actions.Add(action);
                                    option = args.ElementAtOrDefault(++pos);
                                } while (!string.IsNullOrWhiteSpace(option));


                            }
                            foreach (var a in actions) a();
                            w.FinishProcessing();
                        }
                        pos = args.Length;
                        break;
                    case "-lua":
                        Context.outputType = OutputType.LoveLua;
                        pos++;
                        break;
                    default:
                        InstructionError("Invalide option '{0}'", args[pos]);
                        break;
                }
            }
            w.FinishProcessing();
            GoodExit();
        }
    }
}