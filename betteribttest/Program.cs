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
            Context.DumpMessages();
            Console.WriteLine("All Done!");
            Environment.Exit(0);
        }
        static void Main(string[] args)
        {
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
                            if (string.IsNullOrWhiteSpace(option)) option = "eveything";
                            switch (option)
                            {
                                case "scripts":
                                    w.StartWriteAllScripts();
                                    break;
                                case "objects":
                                    w.StartWriteAllObjects();
                                    break;
                                case "rooms":
                                    w.StartWriteAllRooms();
                                    break;
                                case "eveything":
                                    w.StartWriteAllScripts();
                                    w.StartWriteAllObjects();
                                    break;
                                default:
                                    InstructionError("Invalide option '{0}' for -all", option);
                                    break;

                            }
                            pos = args.Length;

                        }
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