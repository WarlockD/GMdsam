using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameMaker
{
    public class SerializerHelper
    {
        static Regex k__BackingFieldRemoveRegex = new Regex(@"<([\w\d]+)>k__BackingField", RegexOptions.Compiled);
        public interface ISerilizerHelper
        {
            SerializerHelper CreateHelper();
        }
        public interface ISerilizerHelperSimple
        {
            object CreateHelper();
        }
        HashSet<string> fields;
        List<KeyValuePair<string, object>> list;
        public bool isPrimitive { get; protected set; }
        public SerializerHelper()
        {
            this.isPrimitive = true;
            this.fields = new HashSet<string>();
            this.list = new List<KeyValuePair<string, object>>();
        }

        string FixName(string name)
        {
            var match = k__BackingFieldRemoveRegex.Match(name);
            if (match.Success) name = match.Groups[1].Value; /// HACK
            name = name.ToLower();
            return name;
        }
        //  public SerializerHelper(SerializerHelper.ISerilizerHelper o)
        //  {
        //      throw new Exception("This is a recursive call, don't do this!");
        //    }
        public SerializerHelper(object o)
        {
            this.isPrimitive = true;
            this.fields = new HashSet<string>();
            this.list = new List<KeyValuePair<string, object>>();
            Type t = o.GetType();
            FieldInfo[] ofields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (ofields.Length == 0) throw new Exception("Cannot convert value or bad not a simple object");
            foreach (var f in ofields)
            {
                if (!f.IsNotSerialized) AddField(FixName(f.Name), f.GetValue(o));
            }
        }
        void AddObject(string name, object o)
        {
            name = FixName(name);
            if (fields.Contains(name)) throw new ArgumentException("Already contains field name", "name");
            list.Add(new KeyValuePair<string, object>(name, o));
            fields.Add(name);
        }
        public void RemoveField(string name)
        {
            name = FixName(name);
            if (!fields.Contains(name)) throw new ArgumentException("does not contains field name", "name");
            fields.Remove(name);
            list.Remove(list.Where(x => x.Key == name).Single());
        }
        public virtual string NullString { get { return "null"; } }
        public static bool MyInterfaceFilter(Type typeObj, Object criteriaObj)
        {
            if (typeObj.ToString() == criteriaObj.ToString())
                return true;
            else
                return false;
        }
        string ObjectSimpleConvert(object o)
        {
            // Check if null
            if (o == null) return NullString;
            else
            {
                Type t = o.GetType(); // check if its a simple type
                if (t == typeof(string)) return Constants.JISONEscapeString(o as string);
                else if (t == typeof(bool)) return (bool)o ? "true" : "false";
                else if (t.IsPrimitive) return o.ToString();
                else return null;
            }
        }
        object ObjectConvert(object o)
        {
            string simple = ObjectSimpleConvert(o);
            if (simple != null) return simple; // its a simple type
                                               // check if we rewrite the object
            ISerilizerHelperSimple ishelper = o as ISerilizerHelperSimple;
            if (ishelper != null) return ObjectConvert(ishelper.CreateHelper());
            // check if we have a custom interface
            SerializerHelper.ISerilizerHelper ihelper = o as SerializerHelper.ISerilizerHelper;
            if (ihelper != null) return ihelper.CreateHelper();
            // check if its an array
            System.Collections.IEnumerable ie = o as System.Collections.IEnumerable;
            if (ie != null)
            {
                Type elementType = o.GetType().GetElementType();
                // if its a primitive arry, make it an array of strings
                if (elementType.IsPrimitive || elementType == typeof(string))
                {
                    return ie.Cast<object>().Select(x => ObjectSimpleConvert(x)).ToArray();
                }
                else
                {
                    // else, push it though the pump again
                    isPrimitive = false;
                    return ie.Cast<object>().Select(x => ObjectConvert(x) as SerializerHelper).ToArray();
                }
            }
            else return new SerializerHelper(o);
        }
        public void AddField(string name, object o)
        {
            AddObject(name, ObjectConvert(o));
        }
        // needed something like this for a while now, I really need ot make a text formater class that
        // does all sorts of text processing
        static void Write<T>(int ident, StringBuilder sb, IEnumerable<T> la, Func<T, bool> func)
        {
            sb.Append('{');
            if (la.Any())
            {
                bool comma = false;
                bool need_append = false;
                foreach (var v in la)
                {
                    if (need_append) sb.Ident(ident);
                    else need_append = false;
                    if (comma) sb.Append(',');
                    else comma = true;
                    sb.Append(' ');
                    if (func(v)) { sb.AppendLine(); need_append = true; }
                }
                sb.Append(' ');
            }
            sb.Append('}');
        }
        public void Write(StringBuilder sb, int ident)
        {
            if (isPrimitive)
            {
                Write(ident, sb, list, kv =>
                {
                    sb.Ident(ident);
                    sb.Append(kv.Key);
                    sb.Append(" = ");
                    if (kv.Value is string) sb.Append(kv.Value as string);
                    else if (kv.Value is string[])
                    {
                        Write(ident, sb, kv.Value as string[], o =>
                        {
                            sb.Append(o);
                            return false;
                        });
                    }
                    return false;
                });
            }
            else
            {
                ident++;
                Write(ident, sb, list, kv =>
                {
                    sb.Ident(ident);
                    sb.Append(kv.Key);
                    sb.Append(" = ");
                    if (kv.Value is string) sb.Append(kv.Value as string);
                    else if (kv.Value is string[])
                    {
                        Write(ident, sb, kv.Value as string[], o =>
                        {
                            sb.Append(o);
                            return false;
                        });
                    }
                    else if (kv.Value is SerializerHelper) (kv.Value as SerializerHelper).Write(sb, ident);
                    else if (kv.Value is SerializerHelper[])
                    {
                        Write(ident, sb, kv.Value as SerializerHelper[], o =>
                        {
                            o.Write(sb, ident);
                            return true;
                        });
                        return false;
                    }
                    return true;
                });
                ident--;
            }

        }
        public void DebugSave(string filename)
        {
            StringBuilder sb = new StringBuilder(500);
            Write(sb, 0);
            using (StreamWriter sw = new StreamWriter(filename)) sw.Write(sb.ToString());
        }
        public void DebugSave(TextWriter tx)
        {
            StringBuilder sb = new StringBuilder(500);
            Write(sb, 0);
            tx.Write(sb.ToString());
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(500);
            Write(sb, 0);
            return sb.ToString();
        }
    }
}
