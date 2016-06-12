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
        static void InstructionError(string message, params object[] o)
        {
            Console.WriteLine("Useage <exe> data.win <-png> <-mask>  [-all (objects|scripts|paths|codes|textures|sprites|sounds)");
            Console.WriteLine("<-png> <-mask> will cut out and save all the masks and png's for sprites and backgrounds.  This dosn't effect textures though");
            if(message != null)
            {
                Context.FatalError(message);
            }
            Environment.Exit(-1);
        }
        static void GoodExit()
        {
            Console.WriteLine("All Done!");
            Environment.Exit(0);
        }
        static int SetPreFlags(string[] args)
        {
            for(int i=1;i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
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
                    case "-json":
                        Context.doXML = false;
                        break;
                    case "-old":
                        Context.Version = UndertaleVersion.V10000;
                        break;
                    case "-debug":
                        Context.Debug = true;
                        break;
                    case "-nothreading":
                        Context.doThreads = false;
                        break;
                    case "-all":
                        return i;
                    default:
                        InstructionError("bad flag '{0}'", a);
                        return -1; // done
                }
            }
            return -1;
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
            int pos = SetPreFlags(args);
            try
            {
                File.LoadDataWin(dataWinFileName);
                File.LoadEveything();
            } catch (Exception e)
            {
                InstructionError("Could not open data.win file {0}\n Exception:", dataWinFileName, e.Message);
            }
            
            if(pos > 0)
            {
                var w = new Writers.AllWriter();
                string option = args[pos];
                switch (option)
                {
                    case "-all":
                        {
                            option = args.ElementAtOrDefault(++pos);
                            if (string.IsNullOrWhiteSpace(option) || option.ToLower() == "everything")
                                w.AddAction("everything");
                            else
                            {
                                do
                                {
                                    w.AddAction(option);
                                    option = args.ElementAtOrDefault(++pos);
                                } while (!string.IsNullOrWhiteSpace(option));
                            }
                        }
                        break;
                }
                w.FinishProcessing();
            }

           
            GoodExit();
        }
    }
}