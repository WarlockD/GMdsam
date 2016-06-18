using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace GameMaker
{


    class PListUtility { 

            delegate void XMLWriteTypeDelgate(XmlWriter w, object v);
        
        static string FormatValue(Point p) { return "{" + p.X + "," + p.Y + "}"; }
        static string FormatValue(Size p) { return "{" + p.Width + "," + p.Height + "}"; }
        static string FormatValue(Rectangle r) { return "{" + FormatValue(r.Location) + "," + FormatValue(r.Size) + "}"; }
        static string FormatValue<T>(T value) { return value.ToString(); }

        static void WriteValue(XmlWriter w, int v) { w.WriteElementString("integer", FormatValue(v)); }
        static void WriteValue(XmlWriter w, short v) { w.WriteElementString("integer", FormatValue(v)); }
        static void WriteValue(XmlWriter w, long v) { w.WriteElementString("integer", FormatValue(v)); }
        static void WriteValue(XmlWriter w, uint v) { w.WriteElementString("integer", FormatValue(v)); }
        static void WriteValue(XmlWriter w, ushort v) { w.WriteElementString("integer", FormatValue(v)); }
        static void WriteValue(XmlWriter w, ulong v) { w.WriteElementString("integer", FormatValue(v)); }
        static void WriteValue(XmlWriter w, float v) { w.WriteElementString("real", FormatValue(v)); }
        static void WriteValue(XmlWriter w, double v) { w.WriteElementString("real", FormatValue(v)); }
        static void WriteValue(XmlWriter w, bool v) { w.WriteElementString(v ? "true" : "false", ""); }
        static void WriteValue(XmlWriter w, string v) { w.WriteElementString("string", v); }

        static void WriteValue(XmlWriter w, Point v) { w.WriteElementString("string", FormatValue(v)); }
        static void WriteValue(XmlWriter w, Size v) { w.WriteElementString("string", FormatValue(v)); }
        static void WriteValue(XmlWriter w, Rectangle v) { w.WriteElementString("string", FormatValue(v)); }
        static void WriteValue(XmlWriter w, object v) { WriteObject(w, v); }
        static void WriteValue<T>(XmlWriter w, T value) { typeDelegates[typeof(T)](w, value); }
        // Used for unboxing objects
        static readonly Dictionary<Type, XMLWriteTypeDelgate> typeDelegates = new Dictionary<Type, XMLWriteTypeDelgate>()
            {
                {  typeof(byte),(XmlWriter w, object v)=> { WriteValue(w,(byte)v); } },
                {  typeof(short),(XmlWriter w, object v)=> { WriteValue(w,(short)v); } },
                {  typeof(int),(XmlWriter w, object v)=> { WriteValue(w,(int)v); } },
                {  typeof(long),(XmlWriter w, object v)=> {WriteValue(w,(long)v); } },
                {  typeof(uint),(XmlWriter w, object v)=> { WriteValue(w,(uint)v); } },
                {  typeof(ushort),(XmlWriter w, object v)=> { WriteValue(w,(ushort)v); } },
                {  typeof(ulong),(XmlWriter w, object v)=> { WriteValue(w,(ulong)v); } },
                {  typeof(float),(XmlWriter w, object v)=> { WriteValue(w,(float)v); } },
                {  typeof(double),(XmlWriter w, object v)=> { WriteValue(w,(double)v); } },
                {  typeof(bool),(XmlWriter w, object v)=> {  WriteValue(w,(bool)v); } },
                {  typeof(string),(XmlWriter w, object v)=> {  WriteValue(w,(string)v); }  },
                // we go into the refrence objects now
                // Some coscos formated stuff
                {  typeof(Point),(XmlWriter w, object v)=> { WriteValue(w,(Point)v); } },
                {  typeof(Size),(XmlWriter w, object v)=> { WriteValue(w,(Size)v); } },
                {  typeof(Rectangle),(XmlWriter w, object v)=> { WriteValue(w,(Rectangle)v); } },
            };

        static void WriteGenericDictonary<TKey, TValue>(XmlWriter w, IDictionary<TKey,TValue> data)
        {
            w.WriteStartElement("dict");
            bool isValue = typeof(TValue) == typeof(string) || typeof(TValue).IsValueType;
            foreach (var pair in data)
            {
                w.WriteElementString("key", FormatValue(pair.Key));
                if (isValue) WriteValue(w, pair.Value); else WriteObject(w, pair.Value);        
            }
            w.WriteEndElement();
        }
        static void WriteGenericArray<TValue>(XmlWriter w, IEnumerable<TValue> data)
        {
            w.WriteStartElement("array");
            bool isValue = typeof(TValue) == typeof(string) || typeof(TValue).IsValueType;
            foreach (TValue value in data) if (isValue) WriteValue(w, value); else WriteObject(w, value);
            w.WriteEndElement();
        }
        // we only support a few types of enumerable objects in this order
        // A> if its a simple value we put it in
        // B> if its a Dictonary<string,object>  then we enumerate the dictionary
        // C> if it has an IEnumerable interface, we put it in like an array
        // D> otherwise we use the generic object toString function and put it in as a string
        static void WriteObject(XmlWriter w, object obj)
        {
            if (obj == null) return;
            Type type = obj.GetType();
            XMLWriteTypeDelgate toRun = null;
            if (typeDelegates.TryGetValue(type, out toRun)) { toRun(w, obj); return; }
            if(type.IsArray)
            {
                var method = typeof(PListUtility).GetMethod("WriteGenericArray", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var genric = method.MakeGenericMethod(type.GetGenericArguments());
                genric.Invoke(null, new object[] { w, obj });
                return;
            }
            foreach (Type iType in obj.GetType().GetInterfaces())
            {
                if (iType.IsGenericType)
                {
                    Type typeDef = iType.GetGenericTypeDefinition();
                    if (typeDef == typeof(IDictionary<,>)) // dict
                    {
                        var method = typeof(PListUtility).GetMethod("WriteGenericDictonary", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                        var genric = method.MakeGenericMethod(iType.GetGenericArguments());
                        genric.Invoke(null, new object[] { w, obj });
                        return;
                    }
                }
            } // Ugh we HAVE to do two loops here
            foreach (Type iType in obj.GetType().GetInterfaces())
            {
                if (iType.IsGenericType)
                {
                    Type typeDef = iType.GetGenericTypeDefinition();
                    if (typeDef == typeof(IEnumerable<>)) // arrays
                    {
                        var method = typeof(PListUtility).GetMethod("WriteGenericArray", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                        var genric = method.MakeGenericMethod(iType.GetGenericArguments());
                        genric.Invoke(null, new object[] { w, obj });
                        return;
                    }
                }
            }
            IEnumerable finalCheck = obj as IEnumerable;
            if(obj != null) // last chance 
            {
                w.WriteStartElement("array");
                foreach (object value in finalCheck) WriteObject(w, value);
                w.WriteEndElement();
                return;
            }
            FieldInfo[] fi = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo[] pi = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if(fi.Length == 0 && pi.Length == 0) throw new Exception("Don't have a clue what type this is");
            w.WriteStartElement("dict");
            foreach (FieldInfo f in fi)
            {
                w.WriteElementString("key", FormatValue(f.Name));
                WriteObject(w, f.GetValue(obj));
            }
            foreach (PropertyInfo p in pi)
            {
                if(p.CanRead)
                {
                    w.WriteElementString("key", FormatValue(p.Name));
                    WriteObject(w, p.GetMethod.Invoke(obj, null));
                }
                
            }
            w.WriteElementString("key", "toString");
            w.WriteElementString("string", obj.ToString()); // Mainly used for debugging?
            w.WriteEndElement();  
        }
        public static void WriteFile(string filename, object obj)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "\t"; // I like tabs, they show up better in Notpad++
            settings.Encoding = Encoding.UTF8;
            using (XmlWriter w = XmlWriter.Create(filename, settings)) WriteObject(w, obj);
        }
    }
    // Dumb wrapper to make Plists easyer
    interface PList
    {
        void WritePlist(string filename);
    }
    class PListArray :  List<object>, PList
    {
        public PListArray AddArray()
        {
            PListArray n = new PListArray();
            Add(n);
            return n;
        }
        public PListDict AddDictonary()
        {
            PListDict n = new PListDict();
            Add(n);
            return n;
        }
        public void WritePlist(string filename)
        {
            PListUtility.WriteFile(filename, this);
        }
    }
    class PListDict : Dictionary<string, object>, PList
    {
        public PListArray AddArray(string name)
        {
            PListArray n = new PListArray();
            Add(name,n);
            return n;
        }
        public PListDict AddDictonary(string name)
        {
            PListDict n = new PListDict();
            Add(name,n);
            return n;
        }
        public void WritePlist(string filename)
        {
            PListUtility.WriteFile(filename, this);
        }
    }
}
