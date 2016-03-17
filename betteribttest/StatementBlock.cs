using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections;
using System.IO;

namespace betteribttest
{

    public static class AstIListExtinson
    {
        public static bool ContainsType<T>(this List<Ast> list) where T : Ast
        {
            foreach (var ast in list) if (ast.HasType<T>()) return true;
            return false;
        }
        public static List<T> StatementEnumerator<T>(this AstStatement s) where T : AstStatement
        { 
            List<T> list = s.Where(o => o is T).Select(statement => statement as T).ToList();
            if (list == null || list.Count == 0) return null;
            else return list;
        }
    }

    /// <summary>
    /// Main block used for searching statements for patterns.  Moved to another file because its important
    /// and I need to make custom extension methods to make this thing much MCUH cleaner
    /// </summary>
    public class StatementBlock : AstStatement, IList<AstStatement>, ICollection<AstStatement>, IReadOnlyList<AstStatement>, IReadOnlyCollection<AstStatement>
    {
        List<AstStatement> _statements;
#if OLD
        public override IEnumerable<AstStatement> StatementEnumerator() {
            // to cache this, lets make a list
            List<AstStatement> list = new List<AstStatement>();
            foreach(var a in _statements)
            {
                list.Add(a);
                foreach (var rs in a.StatementEnumerator()) list.Add(rs); /// this is probery best all things considered
            }
            return list;
        }
#endif
        public override void FindType<T>(List<T> types)  {
            base.FindType(types);
            foreach (var s in _statements) s.FindType(types);
        }
        public StatementBlock() : base() { _statements = new List<AstStatement>(); }
        public StatementBlock(IEnumerable<AstStatement> list) : this()
        {
            foreach (var a in list)
            {
                Ast copy = a.Copy();
                ParentSet(copy);
                _statements.Add(a);
            }
        }
        // In case we build the block outside of this function, we can use this to assign it
        public StatementBlock(List<AstStatement> list) : base()
        {
            _statements = list;
            _statements.ForEach(o => ParentSet(o));
        }
        public int DecompileToText(System.CodeDom.Compiler.IndentedTextWriter wr)
        {
            int count = 0; // the two {}
            if (this.Parent != null){count++; wr.WriteLine('{');}

            wr.Indent++;
            foreach (var statement in _statements)
            {
#if DEBUG
                int line_count = statement.DecompileToText(wr);
                Debug.Assert(line_count != 0); // all statments should return atleast 1
                count += line_count;
#else
                count+= statement.DecompileToText(wr);
#endif
            }
            wr.Indent--;
            if (this.Parent != null){count++; wr.WriteLine('}'); }
            wr.Flush();
            return count;
        }
        public override int DecompileToText(TextWriter wr)
        {
            if (_statements.Count == 0) { wr.WriteLine("{ Empty Statment Block }"); return 1; }
            else if (_statements.Count == 1) { return _statements[0].DecompileToText(wr); }
            else
            {
                System.CodeDom.Compiler.IndentedTextWriter ident_wr = wr as System.CodeDom.Compiler.IndentedTextWriter;
                if (ident_wr == null) ident_wr = new System.CodeDom.Compiler.IndentedTextWriter(wr); // we are NOT in a statment block so we need to make this
                return DecompileToText(ident_wr);
            }
        }
        public override bool ContainsType<T>()  {
            if (base.ContainsType<T>()) return true;
            else foreach (var s in _statements) if (s.ContainsType<T>()) return true;
            return false;
        }

        public override Ast Copy()
        {

            StatementBlock copy = new StatementBlock();
            foreach (var s in _statements) copy.Add(s.Copy() as AstStatement);
            return copy;
        }
#region IList Interface
        public AstStatement this[int index]
        {
            get
            {
                return _statements[index];
            }

            set
            {
                ParentClear(_statements[index]);
                ParentSet(value);
                _statements[index] = value;
            }
        }

        public int Count { get { return _statements.Count; } }

        public bool IsReadOnly { get { return false; } }

        public void Add(AstStatement item)
        {
            ParentSet(item);
            _statements.Add(item);
        }

        public void Clear()
        {
            _statements.ForEach(o => ParentClear(o));
            _statements.Clear();
        }

        public bool Contains(AstStatement item)
        {
            return item.Parent == this && _statements.Contains(item);
        }


        public void CopyTo(AstStatement[] array, int arrayIndex)
        {
            for (int i = arrayIndex; i < array.Length; i++) ParentSet(array[i]);
            _statements.CopyTo(array, arrayIndex);
        }
        public  IEnumerator<AstStatement> GetEnumerator()
        {
            return _statements.GetEnumerator();
        }
        public int IndexOf(AstStatement item)
        {
            return _statements.IndexOf(item);
        }

        public void Insert(int index, AstStatement item)
        {
            ParentSet(item);
            _statements.Insert(index, item);
        }

        public bool Remove(AstStatement item)
        {
            if (item.Parent != this) throw new Exception("Item not in this thing");
            ParentClear(item);
            return _statements.Remove(item);
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index > _statements.Count - 1) throw new ArgumentOutOfRangeException("index", "Index out of Range");
            ParentClear(_statements[index]);
            _statements.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _statements.GetEnumerator();
        }

#endregion
    }
}
