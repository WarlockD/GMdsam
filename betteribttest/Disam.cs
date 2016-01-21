using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web.UI;
using System.Web;

namespace betteribttest
{
    class Disam
    {
        ChunkReader cr;
        public class Opcode
        {
            public string opcode_text = "";
            public string operand_text = "";
            public string comment_text = "";
            public long pc = 0;
            public uint op = 0;
            public long operand = 0; // used for some decodes
            public double doperand = 0.0;
            public string soperand = null;
            public override string ToString()
            {
                string str = String.Format("{0,-6} {1,-2:X2} {2,-6:X6} : {3,-9} {4,-9}", pc, op >> 24, op & 0x00FFFFFF, opcode_text, operand_text);
                if (!string.IsNullOrWhiteSpace(comment_text)) str += "  ;  " + comment_text;
                return str;
            }
        }
        List<Opcode> codes;
        public Disam(ChunkReader cr)
        {
            this.cr = cr;
        }
        string innerHTML(string code_name)
        {
            StringWriter sw = new StringWriter();
            HtmlTextWriter wr = new HtmlTextWriter(sw);

            foreach (Opcode o in codes)
            {              
                wr.AddAttribute(HtmlTextWriterAttribute.Name, "pc_" + o.pc.ToString());
                if (o.opcode_text == "b" || o.opcode_text == "bt" || o.opcode_text == "bf")
                {
                    uint pc = (uint)(o.pc + o.operand);
                    string url = code_name + ".html#" + pc.ToString();
                    wr.AddAttribute(HtmlTextWriterAttribute.Href, url);
                }
                wr.RenderBeginTag(HtmlTextWriterTag.A);
                wr.Write(HttpUtility.HtmlEncode(o.ToString()));
                wr.RenderEndTag();
                //      wr.WriteBreak();
                wr.WriteLine();
            }
            wr.Flush();
            return sw.ToString();

        }
        public void writeHTMLFile(string code_name)
        {
            // Initialize StringWriter instance.
            StreamWriter s = new StreamWriter(code_name  + ".html");
            HtmlTextWriter wr = new HtmlTextWriter(s);

            wr.RenderBeginTag(HtmlTextWriterTag.Html);
            wr.RenderBeginTag(HtmlTextWriterTag.Body);
            wr.WriteFullBeginTag("pre"); // see http://2e2ba.blogspot.com/2009/12/dont-use-renderbegintagpre.html
            wr.Write(innerHTML(code_name));
            wr.WriteEndTag("pre");
            wr.RenderEndTag(); // body
            wr.RenderEndTag(); // html
            wr.Flush();
            wr.Close();

        }
        public void writeFile(string code_name)
        {
            bool found = false;
            foreach(GMK_Code c in cr.codeList)
            {
                if(c.name.IndexOf(code_name) != -1)
                {
                    found = true;
                    MemoryStream ms = new MemoryStream(c.code);
                    processStream(ms);
                    StreamWriter s = new StreamWriter(c.name + ".txt");
                    foreach (Opcode o in codes)
                    {
                        s.WriteLine(o.ToString());
                    }
                    s.Close();
                    writeHTMLFile(c.name);
                }
            }
            if(!found) throw new Exception(code_name + " not found");
        }
        static Dictionary<int, string> instanceLookup = new Dictionary<int, string>()
        {
            {  0 , "stack" },
            {  -1, "self" },
            {  -2, "other" },
            {  -3, "all" },
            {  -4, "noone" },
            {  -5, "global" },
        };
        static Dictionary<int, string> typeLookup = new Dictionary<int, string>()
        {
            {  0x00, "double" },
            {  0x01, "float" },
            {  0x02, "int" },
            {  0x03, "long" },
            {  0x04, "bool" },
            {  0x05, "ref" },
            {  0x06, "string" },
            {  0x07, "instance" },
            {  0x0f, "short" },
        };
      
        static Dictionary<int, string> opDecode = new Dictionary<int, string>()  {
            {  0x03, "conv" },
            {  0x04, "mul" },
            {  0x05, "div" },
            {  0x06, "rem" },
            {  0x07, "mod" },
            {  0x08, "add" },
            {  0x09, "sub" },
            {  0x0a, "and" },
            {  0x0b, "or" },
            {  0x0c, "xor" },
            {  0x0e, "not" },
            {  0x0f, "sal" },
            {  0x10, "sar" },
            {  0x11, "slt" },
            {  0x12, "sle" },
            {  0x13, "seq" },
            {  0x14, "sne" },
            {  0x15, "sge" },
            {  0x16, "sgt" },
            {  0x41, "pop" }, // multi place
            {  0x82, "dup" },
            {  0x9d, "ret" },
            {  0x9e, "exit" },
            {  0x9f, "popz" },
            {  0xb7, "b" },
            {  0xb8, "bt" },
            {  0xb9, "bf" },
            {  0xbb, "pushenv" },
            {  0xbc, "popenv" },
            {  0xc0, "push" },
            {  0xda, "call" },
            {  0xff, "break" },
        };
        string lookupInstance(int instance)
        {
            string ret;
            if (instanceLookup.TryGetValue(instance, out ret)) return ret;
            GMK_Object o;
            if (cr.objMapId.TryGetValue(instance, out o))
                return o.name;
            //   if (cr.objMapIndex.TryGetValue(instance, out o))
            //        return o.name;
            return instance > 10 ? String.Format("0x{0:X4}", instance) : String.Format("{0}", instance);
        }
        public string PrintInts(List<int> data)
        {
            string v = "";
            int col = 0;
            foreach (int a in data)
            {
                v += String.Format("{0,6} ", a);
                if (++col > 16)
                {
                    v += "\n";
                    col = 0;
                }

            }
            return v;
        }
        public string lookAtPoP(int topType, int secondType, int instance, int load_type,int var_ident)
        {
            string sinstance;
            string ret = "Nothing";
            int end = codes.Count;
            switch(load_type)
            {
                case 0: // not alot of error checking here
                    {
                        long array_index = codes[--end].operand; // Last should be a push for the index, normaly its a push.e
                        long array_instance = instance == 0 ? codes[--end].operand : instance;  // if instance ==0 then its on the stack
                        Opcode array_value = codes[--end]; // Nexted should be the instance where the array is
                        sinstance = lookupInstance((int)array_instance);
                        ret = string.Format("{0}.{1}[{2}]= ", sinstance, var_ident, array_index);
                        if (array_value.opcode_text == "push.s") ret += array_value.soperand;
                        else ret += array_value.operand.ToString();

                    }
                    break;

            }
            return ret;
            // zero seems to be an array assign
        }
        public void processStream(Stream f)
        {
            codes = new List<Opcode>();
            f.Position = 0;
            BinaryReader r = new BinaryReader(f);
            int len = (int)f.Length / 4;
            string scode;
            string soperand;
            string scomment;
            long startPos = f.Position;
            while (f.Position != f.Length)
            {
                Opcode info = new Opcode();
                long startOpPos = f.Position;
                uint op = r.ReadUInt32();
                byte opcode = (byte)(op >> 24);
                soperand = "";
                scomment = "";
                // scode = opDecode[opcode];
                if (!opDecode.TryGetValue(opcode, out scode)) scode = String.Format("E{0:X2}", opcode);
                if (opcode <= 0x16)
                {
                    int topType = (int)((op >> 16) & 0xF);
                    int secondType = (int)((op >> 20) & 0xF);
                    soperand = String.Format("{0}, {1}", typeLookup[topType], typeLookup[secondType]);
                }
                else switch (scode)
                    {
                        case "dup":
                        case "ret":
                        case "exit":
                        case "popz":
                            soperand = String.Format("{0}", typeLookup[(int)((op >> 16) & 0xFF)]);
                            break;
                        case "b":
                        case "bt":
                        case "bf":
                        case "pushenv":
                            op &= 0x00FFFFFF;
                            if ((op & 0x800000) != 0) op |= 0xFF000000;
                            info.operand = (int)op;
                            soperand = String.Format("{0}", (int)op);
                            break;
                        case "popenv":
                            soperand = String.Format("{0}, {1} ; (unknown, to pushenv?", (op >> 16) & 0xFF, (short)(op & 0xFFFF));
                            break;
                        case "pop":
                            {
                                int topType = (int)((op >> 20) & 0xF);
                                int secondType = (int)((op >> 16) & 0xF);
                                int instance = (short)(op & 0xFFFF);
                                string sinstance = lookupInstance(instance);
                                int func = r.ReadInt32();

                                int object_var =  (int)(func & 0x0FFFFFFF); // this COULD be 24 bits?
                                int object_var_type = func >> 24 & 0xFF; // I think this might only be 4 bits
                                string name = null;
                             //   GMK_Data gkd = cr.OffsetDebugLookup(object_var);
                             //   if (name == null && gkd != null) name = gkd.name + " off"; 
                              //  if (name == null && object_var < cr.stringList.Count) name = cr.stringList[object_var].str;
                                if (name == null) name = object_var.ToString();
                                soperand = String.Format("{0} -> {1} ({2} [Type: {3,4:X}, Var: {4}])", typeLookup[topType], typeLookup[secondType], sinstance, object_var_type, name);
                                scomment = lookAtPoP(topType, secondType, instance, object_var_type, object_var);
                            }
                            break;
                        case "push":
                            byte t = (byte)(op >> 16);
                            // string ts = typeLookup[t];

                            switch (t)
                            {
                                case 0x0:
                                    {
                                        double operand = r.ReadDouble();
                                        scode += ".d";
                                        info.doperand = operand;
                                        soperand = String.Format("{0}", operand);
                                    }
                                    break;
                                //   case 0x1:
                                //       {
                                //           float operand = r.read();
                                //          line += String.Format("{0}", operand);
                                //       }
                                //       break;
                                case 0x2:
                                    {
                                        int operand = r.ReadInt32();
                                        scode += ".i";
                                        info.operand = operand;
                                        soperand = String.Format("{0}", operand);
                                    }
                                    break;
                                case 0x3:
                                    {
                                        long operand = r.ReadInt64();
                                        scode += ".l";
                                        info.operand = operand;
                                        soperand = String.Format("{0}", operand);
                                    }
                                    break;
                                case 0x5:
                                    {
                                        int instance = (short)(op & 0xFFFF);
                                        string sinstance = lookupInstance(instance);
                                        short var_ident = r.ReadInt16();
                                        short load_type = r.ReadInt16(); // r.ReadInt16(); // this could be a byte humm


                                        scode += ".v";
                                        soperand = String.Format("{0} (load type: {1,4:X},ident: {2,4:X})", sinstance, load_type, var_ident);
                                        if (load_type == 1) soperand += "; Type is an array";

                                    }
                                    break;
                                case 0x6: // string hum
                                    {
                                        uint operand = r.ReadUInt32();
                                        scode += ".s";
                                        info.soperand = cr.stringList[(int)operand].escapedString;
                                        soperand = String.Format("{0}", info.soperand);
                                    }
                                    break;
                                case 0xF:
                                    {
                                        short operand = (short)(op & 0xFFFF);
                                        scode += ".e";
                                        info.operand = operand;
                                        soperand = String.Format("{0}", operand);
                                    }
                                    break;
                                default:
                                    throw new Exception("Bad type");
                            }
                            break;
                        case "call":
                            {
                                //  byte return_type = (byte)((op >> 16) & 0xFF); // always i
                                // string return_type_string = typeLookup[return_type];
                                int args = (ushort)(op & 0xFFFF);
                                uint fuc = r.ReadUInt32();
                                GMK_Data gkd = cr.OffsetDebugLookup(fuc);
                              //  string fs; funcIndex
                             //   if (gkd != null) fs = gkd.ToString();
                              //  else fs = fuc.ToString() + "[" + fuc.ToString("X4") + "]";
                                string fs = fuc < cr.funcIndex.Count ? cr.funcIndex[(int)fuc].func_name : fuc.ToString(); 
                                soperand = String.Format("{0}({1})", fs, args);
                            }
                            break;
                        case "break":
                            {
                                int brkint = (ushort)(op & 0xFFFF);
                                soperand = String.Format("{0}", brkint);
                            }
                            break;

                    }
                info.pc = (startOpPos - startPos)/4;
                info.op = op;
                info.opcode_text = scode;
                info.operand_text = soperand;
                info.comment_text = scomment;
                this.codes.Add(info);
            }
            r = null;
            f.Close();
            f = null;
          //  System.Diagnostics.Debug.Write(line);
        }
    }
}
