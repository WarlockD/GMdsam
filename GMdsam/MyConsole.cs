using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using GameMaker.Ast;

namespace GameMaker
{
    // nifty https://gist.github.com/DanielSWolf/0ab6a96899cc5377bf54
    public class ProgressBarPositionalbe : ProgressBar
    {

        protected override void UpdateText(string text)
        {

        }
    }
        /// <summary>
        /// An ASCII progress bar
        /// </summary>
        public class ProgressBar : IDisposable, IProgress<double>
    {
        // wierd tangent,
        private const int blockCount = 10;
        private readonly TimeSpan animationInterval = TimeSpan.FromSeconds(1.0 / 8);
        private const string animation = @"|/-\";

        private readonly Timer timer;
        bool pause = false;
        private double currentProgress = 0;
        private string currentText = string.Empty;
        private bool disposed = false;
        private int animationIndex = 0;
        private string header_text;
        public ProgressBar(string header=null)
        {
            this.header_text = header;
            timer = new Timer(TimerHandler);

            // A progress bar is only for temporary display in a console window.
            // If the console output is redirected to a file, draw nothing.
            // Otherwise, we'll end up with a lot of garbage in the target file.
            if (!Console.IsOutputRedirected)
            {
                ResetTimer();
            }
        }

        public void Report(double value)
        {
            // Make sure value is in [0..1] range
            value = Math.Max(0, Math.Min(1, value));
            Interlocked.Exchange(ref currentProgress, value);
        }
        string TextGraphics()
        {
            int progressBlockCount = (int) (currentProgress * blockCount);
            int percent = (int) (currentProgress * 100);
            StringBuilder sb = new StringBuilder();
            if (header_text != null) sb.Append(header_text);

            sb.AppendFormat("[{0}{1}] {2,3}% {3}", new string('#', progressBlockCount), new string('-', blockCount - progressBlockCount), percent, animation[animationIndex++ % animation.Length]);
            return sb.ToString();
        }
        private void TimerHandler(object state)
        {
            lock (timer)
            {
                if (disposed) return;
                if (!pause) UpdateText(TextGraphics());
                ResetTimer();
            }
        }
        public void Pause()
        {
            pause = true;
            UpdateText(string.Empty);
        }
        public void UnPause()
        {
            pause = false;
        }
        protected virtual void UpdateText(string text)
        {
            // Get length of common portion
            int commonPrefixLength = 0;
            int commonLength = Math.Min(currentText.Length, text.Length);
            while (commonPrefixLength < commonLength && text[commonPrefixLength] == currentText[commonPrefixLength])
            {
                commonPrefixLength++;
            }

            // Backtrack to the first differing character
            StringBuilder outputBuilder = new StringBuilder();
            outputBuilder.Append('\b', currentText.Length - commonPrefixLength);

            // Output new suffix
            outputBuilder.Append(text.Substring(commonPrefixLength));

            // If the new text is shorter than the old one: delete overlapping characters
            int overlapCount = currentText.Length - text.Length;
            if (overlapCount > 0)
            {
                outputBuilder.Append(' ', overlapCount);
                outputBuilder.Append('\b', overlapCount);
            }

            Console.Write(outputBuilder);
            currentText = text;
        }

        private void ResetTimer()
        {
            timer.Change(animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        public void Dispose()
        {
            lock (timer)
            {
                disposed = true;
                UpdateText(string.Empty);
            }
        }

    }
    public interface IMessages
    {
        void Message(string msg);
        void Message(string msg, Ast.ILNode node);
        void Error(string msg);
        void Warning(string msg);
        void Info(string msg);
        void FatalError(string msg);
        void Error(string msg, Ast.ILNode node);
        void Warning(string msg, Ast.ILNode node);
        void Info(string msg, Ast.ILNode node);
        void FatalError(string msg, Ast.ILNode node);
    }
    public static class IMessagesExtensions
    {
        public static void Message(this IMessages msg, string str, params object[] o)
        {
            msg.Message(string.Format(str, o));
        }
        public static void Message(this IMessages msg, string str, ILNode n, params object[] o)
        {
            msg.Message(string.Format(str, o),n);
        }
        public static void Error(this IMessages msg, string str, params object[] o)
        {
            msg.Error(string.Format(str, o));
        }
        public static void Warning(this IMessages msg, string str, params object[] o)
        {
            msg.Warning(string.Format(str, o));
        }
        public static void Info(this IMessages msg, string str, params object[] o)
        {
            msg.Info(string.Format(str, o));
        }
        public static void FatalError(this IMessages msg, string str, params object[] o)
        {
            msg.FatalError(string.Format(str, o));
        }
        public static void Error(this IMessages msg, Ast.ILNode node, string str, params object[] o)
        {
            msg.Error(string.Format(str, o), node);
        }
        public static void Warning(this IMessages msg, Ast.ILNode node, string str, params object[] o)
        {
            msg.Warning(string.Format(str, o), node);
        }
        public static void Info(this IMessages msg, Ast.ILNode node, string str, params object[] o)
        {
            msg.Info(string.Format(str, o), node);
        }
        public static void FatalError(this IMessages msg, Ast.ILNode node, string str, params object[] o)
        {
            msg.FatalError(string.Format(str, o), node);
        }
    }
   
    public class ErrorContext : IMessages
    {
        enum MType
        {
            All=0,
            Info,
            Warning,
            Error,
            Fatal,
            Message, // always goes though
        }
        static StreamWriter fileOutput = null;
        const string ErrorFileName = "errors.txt";
        public static ProgressBar ProgressBar = null;
        string code_name = null;
        ErrorContext() { }
        public ErrorContext(string code_name)
        {
            if (code_name == null) throw new ArgumentNullException("code_name");
            this.code_name = code_name;
        }
        static ErrorContext _singleton = new ErrorContext();
        public static ErrorContext Out {  get { return _singleton;  } }
        public static string TimeStampString
        {
            get
            {
                return DateTime.Now.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        static ErrorContext()
        {
             FileStream stream = new FileStream(ErrorFileName, FileMode.Create);
            fileOutput = new StreamWriter(stream, Console.Out.Encoding);
            fileOutput.AutoFlush = true;
            _singleton = new ErrorContext();
            _singleton.Info("Error Output Starts");
        }
        static MType printMoreThanThis = MType.Warning;
        public static void PrintInfoAndAbove() { printMoreThanThis = MType.Info; }
        public static void PrintErrosAndAbove() { printMoreThanThis = MType.Error; }
        public static void PrintEveything() { printMoreThanThis = MType.All; }
        void DoMessage(MType type, string msg, Ast.ILNode node)
        {
           
            string timestamp = TimeStampString;
            StringBuilder sb = new StringBuilder();
            sb.Append(type.ToString());
            sb.Append(' ');
            sb.Append(timestamp);
            if (code_name != null)
            {
                sb.Append('(');
                sb.Append(code_name);
                sb.Append(')');
            }
            sb.Append(": ");
            if (node != null)
            {
                string header = sb.ToString();
                sb.Append(msg);
                sb.AppendLine();
                using (PlainTextWriter ptext = new PlainTextWriter())
                {
                    ptext.LineHeader = header;
                    ptext.Indent++;
                    ptext.Write(node.ToString());
                    ptext.Indent--;
                    if (ptext.Column > 0) ptext.WriteLine();
                    sb.Append(ptext.ToString());
                }
            }
            else sb.Append(msg);
            lock (_singleton)
            {
               
                string o = sb.ToString();
                if (type >= printMoreThanThis)
                {
                    if (ProgressBar != null) ProgressBar.Pause();
                    Console.WriteLine(o);
                    if (ProgressBar != null) ProgressBar.UnPause();
                    System.Diagnostics.Debug.WriteLine(o); // just because I don't look at console all the time
                }
                fileOutput.WriteLine(o);

                
                
            }
            
        }
        public void FlushAll()
        {
            lock (_singleton)
            {
                fileOutput.Flush();
            }
        }

        public void DebugSave(ILBlock method, string filename, bool move = true)
        {
            if (this.code_name != null)
                method.DebugSave(this.code_name, filename, move);
            else
                method.DebugSave(filename, move);
        }
        public void CheckDebugThenSave(ILBlock block, string filename)
        {
            if (Context.Debug) DebugSave(block, filename);
        }

        public string MakeDebugFileName(string filename,bool move=true)
        {
            return Context.MakeDebugFileName(code_name, filename,move);
        }
        public void Message(string msg)
        {
            DoMessage(MType.Message, msg, null);
        }
        public void Message(string msg, ILNode n)
        {
            DoMessage(MType.Message, msg, n);
        }
        public void Info(string msg)
        {
            DoMessage(MType.Info, msg, null);
        }
        public void Warning(string msg)
        {
            DoMessage(MType.Info, msg,  null);
        }
        public void Error(string msg)
        {
            DoMessage(MType.Error, msg,  null);
        }
        public void FatalError(string msg)
        {
            DoMessage(MType.Fatal, msg, null);
            Program.EnviromentExit(1);
            Environment.Exit(1);
        }
        public void Info(string msg, Ast.ILNode node)
        {
            DoMessage(MType.Info, msg,  node);
        }
        public void Warning(string msg, Ast.ILNode node)
        {
            DoMessage(MType.Warning, msg,  node);
        }
        public void Error(string msg, Ast.ILNode node)
        {
            DoMessage(MType.Error, msg,  node);
        }
        public void FatalError(string msg, Ast.ILNode node)
        {
            DoMessage(MType.Fatal, msg,  node);
            Program.EnviromentExit(1);
        }
    }
}
