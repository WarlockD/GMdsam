using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;


namespace GameMaker
{
    public class SerializerHelper : IEnumerable<SerializerHelper.SerizlierObject>
    {
        public struct SerizlierObject : IEquatable<SerizlierObject>
        {
            public readonly string Name;
            public readonly object Value;
            public readonly bool isSimple;
            public readonly bool isArray;
            public IEnumerable<string> GetSimpleArray() { return Value as string[]; }
            public IEnumerable<SerializerHelper> GetComplexArray() { return Value as SerializerHelper[]; }
            internal SerizlierObject(string name, object value)
            {
                this.Name = name;
                this.Value = value;
                this.isSimple = value is string || value is string[];
                this.isArray = value.GetType().IsArray;
            }
            public override int GetHashCode()
            {
                return this.Name.GetHashCode();
            }
            public void ToStringBuilder(StringBuilder sb)
            {
                sb.Append(Name);
                sb.Append(" = ");
                if (isSimple)
                {
                    if (isArray)
                    {
                        sb.Append('{');
                        sb.AppendCommaDelimited(GetSimpleArray(), akv => { sb.Append(akv); return false; });
                        sb.Append('}');
                    }
                    else sb.Append(Value);
                }
                else
                {
                    if (isArray)
                        sb.AppendCommaDelimited(GetComplexArray(), akv => { sb.Append(akv.ToString()); return false; });
                    else
                        sb.Append((Value as SerializerHelper));
                }
            }
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder(20);
                ToStringBuilder(sb);
                return sb.ToString();
            }
            public bool Equals(SerizlierObject other)
            {
                return Name == other.Name;
            }
        }
        HashSet<string> fields;
        LinkedList<SerizlierObject> list;
        public bool isPrimitive { get; protected set; }
        public IEnumerator<SerializerHelper.SerizlierObject> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
        static Regex k__BackingFieldRemoveRegex = new Regex(@"<([\w\d]+)>k__BackingField", RegexOptions.Compiled);
        public interface ISerilizerHelper
        {
            void CreateHelper(SerializerHelper help);
        }
        public interface ISerilizerHelperSimple
        {
            object CreateHelper();
        }
       
        public SerializerHelper()
        {
            this.isPrimitive = true;
            this.fields = new HashSet<string>();
            this.list = new LinkedList<SerizlierObject>();
        }

        string FixName(string name)
        {
            var match = k__BackingFieldRemoveRegex.Match(name);
            if (match.Success) name = match.Groups[1].Value; /// HACK
            name = name.ToLower();
            return name;
        }

        public SerializerHelper(object o)
        {
            this.isPrimitive = true;
            this.fields = new HashSet<string>();
            this.list = new LinkedList<SerizlierObject>();
            Type t = o.GetType();
            FieldInfo[] ofields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (ofields.Length == 0) throw new Exception("Cannot convert value or bad not a simple object");


            foreach (var f in ofields)
            {
                if (!f.IsNotSerialized) AddLast(FixName(f.Name), f.GetValue(o));
            }
            SerializerHelper.ISerilizerHelper ihelper = o as SerializerHelper.ISerilizerHelper;
            if (ihelper != null) ihelper.CreateHelper(this);
        }
        public void RemoveField(string name)
        {
            name = FixName(name);
            if (!fields.Contains(name)) throw new ArgumentException("does not contains field name", "name");
            fields.Remove(name);
            list.Remove(list.Where(x => x.Name == name).Single());
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
        public void AddLast(string name, object o)
        {
            name = FixName(name);
            if (fields.Contains(name)) throw new ArgumentException("Already contains field name", "name");
            list.AddLast(new SerizlierObject(name, ObjectConvert(o)));
            fields.Add(name);
        }
        public void AddFirst(string name, object o)
        {
            name = FixName(name);
            if (fields.Contains(name)) throw new ArgumentException("Already contains field name", "name");
            list.AddFirst(new SerizlierObject(name, ObjectConvert(o)));
            fields.Add(name);
        }
        // needed something like this for a while now, I really need ot make a text formater class that

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(500);
            sb.Append('{');
            if (list.Count > 0)
            {
                sb.AppendCommaDelimited(list, kv =>
                {
                    kv.ToStringBuilder(sb);
                    return false;
                });
            }
            sb.Append('}');
            return sb.ToString();
        }

    
    }
}
