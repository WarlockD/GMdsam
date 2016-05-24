using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GameMaker.FlowAnalysis
{
    public static class StringExtensions
    {
        // I am using string extensions WAY to much
        public static char CharAtOrDefault(this string s, int index)
        {
            if (index >= 0 && index < s.Length) return s[index];
            else return default(char);
        }
        static readonly string EscapedNewLine;
        static StringExtensions()
        {
            string s = Environment.NewLine;
            // there are only a few standard cases
            switch(Environment.NewLine)
            {
                case "\r": EscapedNewLine = "\\r"; break;
                case "\n": EscapedNewLine = "\\n"; break;
                case "\r\n": EscapedNewLine = "\\r\\n"; break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        public static void EscapeString(this StringBuilder sb, string text, bool withQuotes = true, bool withEndingNewLine = false, string NewLineReplacment = null)
        {
            if (withQuotes) sb.Append('"');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char p = text.CharAtOrDefault(i + 1);
                switch (c)
                {
                    case '\\':
                        if (p != '\\') goto default;
                        sb.Append("\\\\");
                        break;
                    case '\n':
                    case '\r':
                        if (p != c && (p == '\n' || p == '\r')) i++;
                        // new line detected
                        if (NewLineReplacment != null) sb.Append(NewLineReplacment);
                        else sb.Append(EscapedNewLine);
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            if (withEndingNewLine)
            {
                if (NewLineReplacment != null) sb.Append(NewLineReplacment);
                else sb.Append(EscapedNewLine);
            }
            if (withQuotes) sb.Append('"');
        }
        public static string EscapeStringWithOutQuotes(this string text, string NewLineReplacment = null)
        {
            StringBuilder sb = new StringBuilder();
            sb.EscapeString(text, false, false, NewLineReplacment);
            return sb.ToString();
        }
        public static string EscapeStringWithEndingNewLine(this string text, string NewLineReplacment = null)
        {
            StringBuilder sb = new StringBuilder();
            sb.EscapeString(text, true, true, NewLineReplacment);
            return sb.ToString();
        }

    }
    /// <summary>
    /// GraphViz graph.
    /// </summary>
    public sealed class GraphVizGraph
    {
        List<GraphVizNode> nodes = new List<GraphVizNode>();
        List<GraphVizEdge> edges = new List<GraphVizEdge>();

        public string rankdir;
        public string Title;

        public void AddEdge(GraphVizEdge edge)
        {
            edges.Add(edge);
        }

        public void AddNode(GraphVizNode node)
        {
            nodes.Add(node);
        }

        public void Save(string fileName)
        {
            using (StreamWriter writer = new StreamWriter(fileName))
                Save(writer);
        }

        public void Show()
        {
            Show(null);
        }

        public void Show(string name)
        {
            if (name == null)
                name = Title;
            if (name != null)
                foreach (char c in Path.GetInvalidFileNameChars())
                    name = name.Replace(c, '-');
            string fileName = name != null ? Path.Combine(Path.GetTempPath(), name) : Path.GetTempFileName();
            Save(fileName + ".gv");
            Process.Start("dot", "\"" + fileName + ".gv\" -Tpng -o \"" + fileName + ".png\"").WaitForExit();
            Process.Start(fileName + ".png");
        }
        ///public static string EscapeNewLineReplace = "\\n";
        public static string EscapeNewLineReplace = "\\l"; // left justify
        static string Escape(string text)
        {
            if (Regex.IsMatch(text, @"^[\w\d]+$"))
            {
                return text;
            }
            else {
                return text.EscapeStringWithEndingNewLine(EscapeNewLineReplace);
            }
        }

        static void WriteGraphAttribute(TextWriter writer, string name, string value)
        {
            if (value != null)
                writer.WriteLine("{0}={1};", name, Escape(value));
        }

        internal static void WriteAttribute(TextWriter writer, string name, double? value, ref bool isFirst)
        {
            if (value != null)
            {
                WriteAttribute(writer, name, value.Value.ToString(CultureInfo.InvariantCulture), ref isFirst);
            }
        }

        internal static void WriteAttribute(TextWriter writer, string name, bool? value, ref bool isFirst)
        {
            if (value != null)
            {
                WriteAttribute(writer, name, value.Value ? "true" : "false", ref isFirst);
            }
        }

        internal static void WriteAttribute(TextWriter writer, string name, string value, ref bool isFirst)
        {
            if (value != null)
            {
                if (isFirst)
                    isFirst = false;
                else
                    writer.Write(',');
                writer.Write("{0}={1}", name, Escape(value));
            }
        }

        public void Save(TextWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");
            writer.WriteLine("digraph G {");
            writer.WriteLine("node [fontsize = 16];");
            WriteGraphAttribute(writer, "rankdir", rankdir);
            foreach (GraphVizNode node in nodes)
            {
                node.Save(writer);
            }
            foreach (GraphVizEdge edge in edges)
            {
                edge.Save(writer);
            }
            writer.WriteLine("}");
        }
    }

    public sealed class GraphVizEdge
    {
        public readonly string Source, Target;

        /// <summary>edge stroke color</summary>
        public string color;
        /// <summary>use edge to affect node ranking</summary>
        public bool? constraint;

        public string label;

        public string style;

        /// <summary>point size of label</summary>
        public int? fontsize;

        public GraphVizEdge(string source, string target)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (target == null)
                throw new ArgumentNullException("target");
            this.Source = source;
            this.Target = target;
        }

        public GraphVizEdge(int source, int target)
        {
            this.Source = source.ToString(CultureInfo.InvariantCulture);
            this.Target = target.ToString(CultureInfo.InvariantCulture);
        }

        public void Save(TextWriter writer)
        {
            writer.Write("{0} -> {1} [", Source, Target);
            bool isFirst = true;
            GraphVizGraph.WriteAttribute(writer, "label", label, ref isFirst);
            GraphVizGraph.WriteAttribute(writer, "style", style, ref isFirst);
            GraphVizGraph.WriteAttribute(writer, "fontsize", fontsize, ref isFirst);
            GraphVizGraph.WriteAttribute(writer, "color", color, ref isFirst);
            GraphVizGraph.WriteAttribute(writer, "constraint", constraint, ref isFirst);
            writer.WriteLine("];");
        }
    }

    public sealed class GraphVizNode
    {
        public readonly string ID;
        public string label;

        public string labelloc;

        /// <summary>point size of label</summary>
        public int? fontsize;

        /// <summary>minimum height in inches</summary>
        public double? height;

        /// <summary>space around label</summary>
        public string margin;

        /// <summary>node shape</summary>
        public string shape;

        public GraphVizNode(string id)
        {
            if (id == null)
                throw new ArgumentNullException("id");
            this.ID = id;
        }

        public GraphVizNode(int id)
        {
            this.ID = id.ToString(CultureInfo.InvariantCulture);
        }

        public void Save(TextWriter writer)
        {
            writer.Write(ID);
            writer.Write(" [");
            bool isFirst = true;
            GraphVizGraph.WriteAttribute(writer, "label", label, ref isFirst);
            GraphVizGraph.WriteAttribute(writer, "labelloc", labelloc, ref isFirst);
            GraphVizGraph.WriteAttribute(writer, "fontsize", fontsize, ref isFirst);
            GraphVizGraph.WriteAttribute(writer, "margin", margin, ref isFirst);
            GraphVizGraph.WriteAttribute(writer, "shape", shape, ref isFirst);
            writer.WriteLine("];");
        }
    }
}