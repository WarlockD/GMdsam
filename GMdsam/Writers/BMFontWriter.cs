using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using GameMaker.Ast;

namespace GameMaker.Writers
{
    public class BMFontWriter
    {
        BMFontWriter() { }
        StreamWriter sw = null;
        void AppendField(string name, string textvalue)
        {
            sw.Write(name);
            sw.Write('=');
            sw.Write('"');
            if(!string.IsNullOrWhiteSpace(textvalue))  sw.Write(textvalue);
            sw.Write('"');
            sw.Write(' ');
        }
        void AppendField(string name, int value)
        {
            sw.Write(name);
            sw.Write('=');
            sw.Write(value.ToString());
            sw.Write(' ');
        }
        void AppendField(string name, bool value)
        {
            sw.Write(name);
            sw.Write('=');
            sw.Write(value ? '1' : '0');
            sw.Write(' ');
        }
        void AppendField(string name, params int[] values)
        {
            if (values == null || values.Length == 0) throw new Exception("Need atleast one value");
            sw.Write(name);
            sw.Write('=');
            sw.Write(values[0].ToString());
            foreach (var v in values.Skip(1))
            {
                sw.Write(',');
                sw.Write(v.ToString());
            }
            sw.Write(' ');
        }

        public static void SaveAsBMFont(string filename, File.Font f)
        {
            BMFontWriter writer = new BMFontWriter();
            writer.sw = new StreamWriter(filename);
            writer.WriteInfoTag(f);
            writer.WriteCommon(f);
            writer.WritePage(filename, f);
            writer.WriteChars(f);
            writer.sw.Flush();
            writer.sw.Close();
        }
        void WriteInfoTag(File.Font f)
        {
            // Info tag
            sw.Write("info ");
            AppendField("face", f.Description);
            AppendField("size", f.Size);
            AppendField("bold", f.Bold);
            AppendField("italic", f.Italic);
            AppendField("charset", ""); // not sure if we need this
                                        // AppendField("unicode", false);
            AppendField("aa", f.AntiAlias);
            //AppendField("smooth", false);
            AppendField("padding", 0, 0, 0, 0);
            AppendField("spacing", 0);
            AppendField("outline", 0);
            sw.WriteLine();
        }
        void WritePage(string filename, File.Font f)
        {
            string nfilename = Path.ChangeExtension(filename, ".png"); // we just have one
            f.Frame.Image.Save(nfilename); // just in case -png was not set
            sw.Write("page ");
            sw.Write("id", 0);
            sw.Write("file", nfilename);
            sw.WriteLine();
        }
        void WriteChars(File.Font f)
        {
            AppendField("chars count", f.Glyphs.Length);
            sw.WriteLine();
            foreach(var g in f.Glyphs)
            {
                sw.Write("char ");
                sw.WriteLine("char id={0,-3} x={1,-4} y={2,-4} width={3,-4} height={4,-4} xoffset={5,-4} yoffset={6,-4} xadvance={7,-4} page=0 chnl=15",
                    g.ch,g.x, g.y, g.width, g.height, 0, g.offset, g.shift);
            }
            sw.WriteLine();
        }
        void WriteCommon(File.Font f)
        {
            sw.Write("common ");
            AppendField("lineHeight", f.Size);
            AppendField("base", f.Size); // hope this is right
            AppendField("scaleW", f.Frame.Width);
            AppendField("scaleH", f.Frame.Height);
            AppendField("pages", 1);
            AppendField("packed", 0);
            AppendField("alphaChnl", 0);
            AppendField("redChnl", 0);
            AppendField("greenChnl", 0);
            AppendField("blueChnl", 0);
            sw.WriteLine();
        }
    }
}
