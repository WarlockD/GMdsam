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
    }
    public abstract class Ast : IEquatable<Ast>, IList<Ast>
    {
        List<Ast> Children;
        protected static bool _debugOn = true;
        public static bool DebugOutput { get { return _debugOn; } set { _debugOn = value; } }
        protected void CopyChildren(Ast target)
        {
            foreach (var child in Children)
            {
                Ast copy = child.Copy();
                copy.Parent = null; // make sure its null
                target.Add(copy);
            }
        }
        public void SaveToFile(string filename)
        {
            using (StreamWriter wr = new StreamWriter("temp_statement.txt"))
            {
                this.DecompileToText(wr);
            }
        }
        static protected void AddParm(TextWriter wr, string s)
        {
            wr.Write('(');
            wr.Write(s);
            wr.Write(')');
        }
        protected void WriteChildParms(TextWriter wr, int index)
        {
            bool needParns0 = this[index] is AstTree;
            if (needParns0) wr.Write('(');
            wr.Write(this[index].ToString());
            if (needParns0) wr.Write(')');
        }
        /// <summary>
        /// Makes a copy of this Ast
        /// </summary>
        /// <returns>Deap copy of this ast</returns>
        public abstract Ast Copy();
        public Ast Parent { get; private set; }
        public int ParentIndex { get; private set; }
        public virtual bool TryParse(out int value) { value = 0; return false; }
        
        public Instruction Instruction { get; private set; }

        protected Ast(Instruction i) { Instruction = i; Children = new List<Ast>(); ParentIndex = -1; }
        protected Ast() { Instruction = null; Children = new List<Ast>(); ParentIndex = -1; }

        /// <summary>
        /// So this is the main function that decompiles a statment to text.  override this instead of
        /// ToString in ALL inherted functions
        /// </summary>
        /// <param name="indent">Amount of spaces to indent</param>
        /// <param name="sb">String bulder that the line gets added to</param>
        /// <returns>Lenght of line or longest line, NOT the amount of text added</returns>
        public abstract int DecompileToText(TextWriter wr);

        public override string ToString()
        {
            StringWriter wr = new StringWriter();
            DecompileToText(wr);
            return wr.ToString();
        }
        public bool Equals(Ast that)
        {
            if (this.Count != that.Count) return false;
            for (int i = 0; i < Children.Count; i++)
                if (!(this.Children[i].Equals(that.Children[i]))) return false;
            return true;
        }
        /// <summary>
        /// Sealed Equals, eveything below this is sealed as all leafs use children
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (obj.GetType() != this.GetType()) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            Ast that = obj as Ast;
            return Equals(that);
        }
        public override int GetHashCode()
        {
            return Instruction != null ? Instruction.Offset : base.GetHashCode();
        }
        public virtual Ast Invert() // just too lazy
        {
            AstTree tree = this as AstTree;
            if (tree != null) return tree.Invert();
            else
            {
                AstTree ntree = new AstTree(Instruction, GMCode.Not);
                ntree.Add(this.Copy());
                return ntree;
            }
        }

        #region IList Interface
        public virtual void Remove()
        {
            if (this.Parent == null) throw new Exception("Cannot remove a null parrent");
            this.Parent.Remove(this); // if we are at the end of the parent array, do this
        }

        // I created these AddItem Event and Remove Item events so inherted objects don't have
        // to override a bunch of the IList.  These are called After the parent has been set
        // but before its added to the list

        /// <summary>
        /// Called before the 
        /// </summary>
        /// <param name="ast">Child to be removed</param>
        /// <returns>Return true if child removeal was handled by override</returns>
        protected virtual void OnRemoveChild(Ast ast) {
            ast.Parent = null;
            ast.ParentIndex = -1;
        }
        protected virtual void OnInsertChild(Ast ast, int index) {
            ast.Parent = this;
            ast.ParentIndex = index;
        }
        public int Count { get { return Children.Count; } }
        public virtual bool IsReadOnly { get { return false; } }
        public Ast this[int index] { get { return Children[index]; }
            set {
                if (index < 0 || index > Count) throw new IndexOutOfRangeException("Index out of range");
                if (value == null) throw new ArgumentNullException("value", "Ast is null");
                ParentClear(Children[index]);
                ParentSet(value, index);
                Children[index] = value;
            }
        }
        void ParentSet(Ast item, int index)
        {
            if (item == null) throw new ArgumentNullException("Item is null", "item");
            OnInsertChild(item, index);
            if (!object.ReferenceEquals(item.Parent, this)) throw new ArgumentException("Ast parrent is not set", "item");
        }
        void ParentClear(Ast item)
        {
            if (item == null) throw new ArgumentNullException("Item is null", "item");
            OnRemoveChild(item);
            if (object.ReferenceEquals(item.Parent, this)) throw new ArgumentException("Parent still set for this", "item");
        }
        public void Add(Ast item) {
            ParentSet(item, Count);
            Children.Add(item);
        }
        public bool isBlock { get { return this is StatementBlock; } }
        public IEnumerable<T> FindType<T>(bool recursive = true) where T : Ast
        {
            List<T> ret = new List<T>();
            foreach (var child in Children)
            {
                T test = child as T;
                if (test != null) yield return test;
                else if (recursive) foreach (var subChild in child.FindType<T>(recursive)) yield return subChild;
            }
        }
        public bool HasType<T>() where T : Ast
        {
            if (this is T) return true;
            else foreach (var child in Children) if (child.HasType<T>()) return true;
            return false;
        }
        public void Clear() {
            for (int i = 0; i < Count; i++)
            {
                Ast child = Children[i];
                ParentClear(child);
            }
            Children.Clear();
        }
        public bool Contains(Ast item) {
            if (item == null) throw new ArgumentNullException("Item is null", "item");
#if DEBUG
            Debug.Assert((Children.Contains(item) && object.ReferenceEquals(this, item.Parent)) || (!Children.Contains(item) && !object.ReferenceEquals(this, item.Parent)));
#endif
            return object.ReferenceEquals(this, item.Parent);
        } // could just test if the parrent equals this
        public void CopyTo(Ast[] array, int arrayIndex) {
            if (array == null) throw new ArgumentNullException("Array is null", "array");
            for (int i = arrayIndex; i < array.Length; i++)
            {
                Ast item = array[i];
                if (item == null) throw new ArgumentNullException("Item is null", "array[" + i + "]");
                if (item.Parent != null) throw new ArgumentException("Ast already has parent", "array[" + i + "]");
                ParentSet(item, i);
            }
            Children.CopyTo(array, arrayIndex);
        }
        void ResetParentIndexs()
        {
            for (int i = 0; i < Children.Count; i++) Children[i].ParentIndex = i;
        }
        public bool Remove(Ast item) {
            if (item == null) throw new ArgumentNullException("Item is null", "item");
            if (!Contains(item)) return false;
            ParentClear(item);
            bool ret = Children.Remove(item);
            ResetParentIndexs();
            return ret;
        }
        public int IndexOf(Ast item) { return Children.IndexOf(item); }
        public void Insert(int index, Ast item) {
            ParentSet(item, index);
            Children.Insert(index, item);
            ResetParentIndexs();
        }
        public void RemoveAt(int index) {
            if (index < 0 || index > Count) throw new IndexOutOfRangeException("Index out of range");
            Ast item = Children[index];
            ParentClear(item);
            Children.RemoveAt(index);
            ResetParentIndexs();
        }
        public IEnumerator<Ast> GetEnumerator() { return Children.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return Children.GetEnumerator(); }
        #endregion
    }
    /// <summary>
    /// Statment Class is a statment, duh.  Ast can NEVER be by itself unless its in here 
    /// </summary>
    public abstract class AstStatement : Ast
    {
        public AstStatement(Instruction i) : base(i) { }
        public AstStatement() : base() { }
        protected int DecompileLinesOfText(System.CodeDom.Compiler.IndentedTextWriter wr)
        {
            int count = 2; // the two {}
            wr.WriteLine('{');
            wr.Indent++;
            foreach (var statement in this)
            {
#if DEBUG
                Debug.Assert(statement is AstStatement); // these all should be statements
                int line_count = statement.DecompileToText(wr);
                Debug.Assert(line_count != 0); // all statments should return atleast 1
                count += line_count;
#else
                count+= statement.DecompileToText(wr);
#endif
            }
            wr.Indent--;
            wr.WriteLine('}');
            wr.Flush();
            return count;
        }
        protected int DecompileLinesOfText(TextWriter wr)
        {
            if (Count == 0) { wr.WriteLine("{ Empty Statment Block }"); return 1; }
            else if (Count == 1) { return this[0].DecompileToText(wr); }
            else
            {
                System.CodeDom.Compiler.IndentedTextWriter ident_wr = wr as System.CodeDom.Compiler.IndentedTextWriter;
                if (ident_wr == null) ident_wr = new System.CodeDom.Compiler.IndentedTextWriter(wr); // we are NOT in a statment block so we need to make this
                return DecompileToText(ident_wr);
            }
        }
    }
    // This class is used as filler leaf on an invalid stack
    public class AstPop : Ast
    {
        public AstPop() : base(null) { }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write("Pop()");
            return 0;
        }
        public override Ast Copy()
        {
            AstPop copy = new AstPop();
            CopyChildren(copy);
            return copy;
        }
    }
    // This class is used in filler when the stack is an odd shape between labels
    public class PushStatement : AstStatement
    {
        public PushStatement(Ast ast) : base(ast.Instruction) { Add(ast); }
        PushStatement(Instruction i) : base(i) { }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write("Push(");
            if (Count > 0) this[0].DecompileToText(wr); else wr.Write("NullPush");
            wr.Write(")");
            return 0;
        }
        public override Ast Copy()
        {
            PushStatement copy = new PushStatement(this.Instruction);
            CopyChildren(copy);
            return copy;
        }
    }
    public class LogicalOr : Ast
    {
        public LogicalOr() : base() { }
        public override Ast Invert() // Never call base.Invert as it will recursive loop for resons
        {
            LogicalAnd or = new LogicalAnd();
            foreach (var child in this) or.Add(child.Invert());
            return or;
        }
        public override Ast Copy()
        {
            LogicalOr copy = new LogicalOr();
            CopyChildren(copy);
            return copy;
        }
        public override int DecompileToText(TextWriter wr)
        {
            bool need_logical = false;
            foreach (var child in this)
            {
                if (need_logical) wr.Write(" || ");
                child.DecompileToText(wr);
                need_logical = true;
            }
            return 0;
        }
    }
    public class LogicalAnd : Ast
    {
        public LogicalAnd() : base() {  }
        public override Ast Invert() // Never call base.Invert as it will recursive loop for resons
        {
            LogicalOr or = new LogicalOr();
            foreach(var child in this) or.Add(child.Invert());
            return or;
        }
        public override Ast Copy()
        {
            LogicalAnd copy = new LogicalAnd();
            CopyChildren(copy);
            return copy;
        }
        public override int DecompileToText(TextWriter wr)
        {
            bool need_logical = false;
            foreach (var child in this)
            {
                if (need_logical) wr.Write(" && ");
                child.DecompileToText(wr);
                need_logical = true;
            }
            return 0;
        }
    }
    public class AstTree : Ast
    {
        public GMCode op;
        public AstTree(Instruction i, GMCode op) : base(i) { this.op = op; }
        public override Ast Invert() // Never call base.Invert as it will recursive loop for resons
        {
            if (op == GMCode.Not) return this[0].Copy();// we just remove it
            GMCode io = op.getInvertedOp();
            Debug.Assert(io != GMCode.BadOp);
            AstTree ntree = new AstTree(Instruction, io);
            CopyChildren(ntree);
            return ntree;
        }
        public override Ast Copy()
        {
            AstTree copy = new AstTree(this.Instruction,this.op);
            CopyChildren(copy);
            return copy;
        }
        void AddParm(TextWriter wr, string s)
        {
            wr.Write('(');
            wr.Write(s);
            wr.Write(')');
        }
        void WriteChild(TextWriter wr, int index)
        {
            bool needParns0 = this[index] is AstTree;
            if (needParns0) wr.Write('(');
            wr.Write(this[index].ToString());
            if (needParns0) wr.Write(')');
        }
        public override int DecompileToText(TextWriter wr)
        {
            int count = op.getOpTreeCount();
            string s = op.getOpTreeString();
            if (count == 0 || count != Count) wr.Write("Bad Op '" + op.GetName() + "'");
            else if(count == 1)
            {
                wr.Write(s);
                WriteChild(wr, 0);
            }
            else
            {
                WriteChild(wr, 0);
                wr.Write(s);
                WriteChild(wr, 1);
            }
            return 0;
        }
    }
    public class VarInstance : Ast 
    {
        public int ObjectIndex { get; private set; }
        public override bool TryParse(out int value) { value = ObjectIndex; return true; }
        public string ObjectName { get; private set; }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write(ObjectName);
            return 0;
        }
        private VarInstance(Ast ast, string name, int index) : base() { ObjectIndex = index; ObjectName = name; if(ast != null) Add(ast); }
        private VarInstance(string name, int index) : base() { ObjectIndex = index; ObjectName = name;  }
        public override Ast Copy()
        {
            VarInstance copy = new VarInstance(this.ObjectName, this.ObjectIndex);
            CopyChildren(copy);
            return copy;
        }
        public static VarInstance getInstance(int value)
        {
            if (value == -5) return new VarInstance("global", -5);
            else if (value == -1) return new VarInstance("self", -1);
            else if (value == -80) return new VarInstance("builtin", -80);
            else return new VarInstance(null, "Object(" + value + ")", value);
        }
        public static VarInstance getInstance(Ast ast)
        {
            VarInstance ret = null;// if its a simple constant its simple to do
            int value;
            if(ast.TryParse(out value)) ret = getInstance(value);
            if(ret == null) ret= new VarInstance(ast, "Object(" + ast.ToString() + ")",-10000);
            return ret;
        }
    }
   
    public class AstCall : Ast
    {
        public int Arguments { get { return Count; } }
        public string Name { get; protected set; }
        public override int DecompileToText(TextWriter wr)
        {
            StringBuilder sb = new StringBuilder();
            wr.Write(Name);
            bool need_comma = false;
            wr.Write('(');
            foreach (var child in this)
            {
                if (need_comma) sb.Append(',');
                else need_comma = true;
                if (this[0].DecompileToText(wr) != 0) throw new Exception("Wierd expression in call");
            }
            wr.Write(')');
            return 0;
        }
        public AstCall(Instruction i, string name) : base(i) { Name = name; }
        public override Ast Copy()
        {
            AstCall copy = new AstCall(this.Instruction,this.Name);
            CopyChildren(copy);
            return copy;
        }
    }
    public class AstConstant : Ast
    {
        int _parsedValue;
        public string Value { get; private set; }
        public GM_Type Type { get; private set; }
        public override bool TryParse(out int value)
        {
            if(Type ==  GM_Type.Int || Type == GM_Type.Short || Type == GM_Type.Long || Type == GM_Type.Bool)
            {
                value = _parsedValue;
                return true;
            }
            else
            {
                value = 0;
                return false;
            }
        }
        private AstConstant(Instruction i, string value, GM_Type type, int parsedValue) :base(i)
        {
            Value = value;
            Type = type;
            _parsedValue = parsedValue;
        }
        public override Ast Copy()
        {
            AstConstant copy = new AstConstant(this.Instruction, this.Value, this.Type,_parsedValue);
           // CopyChildren(copy);
            return copy;
        }
        public AstConstant(int i) : base() { Value = i.ToString(); Type = GM_Type.Int; _parsedValue = i; }
        public AstConstant(string value) : base() { Value = value; Type = GM_Type.String; _parsedValue = 0; }
        public AstConstant(Instruction i, short s) : base(i) { Value = s.ToString(); Type = GM_Type.Short; _parsedValue = s; }
        private AstConstant(Instruction i, object v, GM_Type type) : base(i) {
            Value = v.ToString();
            Type = type;
            if (Type == GM_Type.Int || Type == GM_Type.Short || Type == GM_Type.Long) _parsedValue = int.Parse(Value);
            else if (Type == GM_Type.Bool) _parsedValue = (bool)(v) ? 1 : 0;
            else _parsedValue = 0;
        }
        public static AstConstant FromInstruction(Instruction i)
        {
            Debug.Assert(i.GMCode == GMCode.Push && i.FirstType != GM_Type.Var);
            AstConstant con = null;
            switch (i.FirstType)
            {
                case GM_Type.Double:
                case GM_Type.Float:
                case GM_Type.Int:
                case GM_Type.Long:
                case GM_Type.String:
                    con = new AstConstant(i, i.Operand, i.FirstType); break;
                case GM_Type.Short:
                    con = new AstConstant(i, i.Instance); break;
                default:
                    throw new Exception("Bad Type");
            }
            return con;
        }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write(Value);
            return 0;
        }
    }

    public class AstVar : Ast
    {
        public string Name { get; set; }
        public GM_Type Type { get { return GM_Type.Var; } }
        public VarInstance Instance { get { return this[0] as VarInstance; } }
        public Ast ArrayIndex { get { return Count > 2 ? this[1] : null; } }
        public int VarMetadate = 0;
        public AstVar(Instruction i, int instance, string name) : base(i)
        {
            Name = name;
            Add(VarInstance.getInstance(instance));
        }
        public AstVar(Instruction i, Ast instance, string name) : base(i)
        {
            Name = name;
            Add(VarInstance.getInstance(instance));
        }
        public AstVar(Instruction i, int instance, string name, Ast Index) : this(i, instance, name)
        {
            if (Index != null) Add(Index);
        }
        public AstVar(Instruction i, Ast instance, string name, Ast Index) : this(i, instance, name)
        {
            if(Index != null) Add(Index);
        }
        private AstVar(Instruction i,  string name) : base(i) { Name = name; }
        public override Ast Copy()
        {
            AstVar copy = new AstVar(this.Instruction, this.Name);
            CopyChildren(copy);
            return copy;
        }
        public static bool _showSelf = false;
        public static bool Debug_DisplaySelf { get { return _showSelf; } set { _showSelf = false; } }
        public override int DecompileToText(TextWriter wr)
        {
            if (_showSelf || Instance.ObjectIndex != -1)
            {
                this[0].DecompileToText(wr);
                wr.Write('.');
            }
            wr.Write(Name);
            if (Count > 1)
            {
                wr.Write('[');
                this[1].DecompileToText(wr);
                wr.Write(']');
            }
            return 0;
        }
    }
    public class AssignStatment : AstStatement
    {
        public AstVar Variable { get { return this[0] as AstVar; } }
        public AstVar Expression { get { return this[1] as AstVar; } }
        public AssignStatment(Instruction i) : base(i) { }
        public override int DecompileToText(TextWriter wr)
        {
            if(Count > 0) this[0].DecompileToText(wr); else wr.Write("NullVariable");
            wr.Write(" = ");
            if (Count > 1) this[1].DecompileToText(wr); else wr.Write("NullExpression");
            wr.WriteLine();
            return 1;
        }
        public override Ast Copy()
        {
            AssignStatment copy = new AssignStatment(this.Instruction);
            CopyChildren(copy);
            return copy;
        }
    }
    public class CallStatement : AstStatement
    {
        public AstCall Call { get { return this[0] as AstCall; } }
        public CallStatement(Instruction popz, AstCall call) : base(popz)
        {
            Debug.Assert(call != null);
            Add(call);
        }
        CallStatement(Instruction i) : base(i)
        {
        }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write("void ");
            if (Count >0) this[0].DecompileToText(wr); else wr.Write("NullCall()");
            wr.WriteLine();
            return 1;
        }
        public override Ast Copy()
        {
            CallStatement copy = new CallStatement(this.Instruction);
            CopyChildren(copy);
            return copy;
        }
    }
    public class StatementBlock : AstStatement
    {
        public Dictionary<Label, LabelStatement> LabelStatments = new Dictionary<Label, LabelStatement>();
        protected override void OnRemoveChild(Ast ast) {
            base.OnRemoveChild(ast); // just in case if I screw with the base class atall
            LabelStatement lstatement = ast as LabelStatement;
            if(lstatement != null) LabelStatments.Remove(lstatement.Target);
        }
        public StatementBlock() : base() { }
        public StatementBlock(IEnumerable<Ast> list,bool copyList) : base() {
            foreach(var a in list)
            {
                Debug.Assert(a is AstStatement);
                if (copyList) Add(a.Copy());
                else
                {
                    a.Remove();
                    Add(a);
                }
            }
        }
        public override Ast Copy()
        {
            StatementBlock copy = new StatementBlock();
            CopyChildren(copy);
            return copy;
        }
        protected override void OnInsertChild(Ast ast,int index) {
            base.OnInsertChild(ast,index);// just in case if I screw with the base class atall
            LabelStatement lstatement = ast as LabelStatement;
            if (lstatement != null) LabelStatments.Add(lstatement.Target, lstatement);
        }
        public int IndexOfLabelStatement(Label l)
        {
            for (int i = 0; i < Count; i++)
            {
                LabelStatement lstate = this[i] as LabelStatement;
                if (lstate != null) return i;
            }
            return -1;
        }

        public int DecompileToText(System.CodeDom.Compiler.IndentedTextWriter wr)
        {
            int count = 2; // the two {}
            wr.WriteLine('{');
            wr.Indent++;
            foreach (var statement in this)
            {
#if DEBUG
                Debug.Assert(statement is AstStatement); // these all should be statements
                int line_count = statement.DecompileToText(wr);
                Debug.Assert(line_count != 0); // all statments should return atleast 1
                count += line_count;
#else
                count+= statement.DecompileToText(wr);
#endif
            }
            wr.Indent--;
            wr.WriteLine('}');
            wr.Flush();
            return count;
        }
        public override int DecompileToText(TextWriter wr)
        {
            if (Count == 0) { wr.WriteLine("{ Empty Statment Block }"); return 1; }
            else if (Count == 1) { return this[0].DecompileToText(wr); }
            else
            {
                System.CodeDom.Compiler.IndentedTextWriter ident_wr = wr as System.CodeDom.Compiler.IndentedTextWriter;
                if (ident_wr == null) ident_wr = new System.CodeDom.Compiler.IndentedTextWriter(wr); // we are NOT in a statment block so we need to make this
                return DecompileToText(ident_wr);
            }
        }
    }
    // This is filler for a blank statement or a message statment for debugging
    public class CommentStatement : AstStatement
    {
        public override Ast Copy()
        {
            StatementBlock copy = new StatementBlock();
            CopyChildren(copy);
            return copy;
        }
        public void AddLine(string message)
        {
            Add(new AstConstant(message));
        }
        public int DecompileToText(System.CodeDom.Compiler.IndentedTextWriter wr)
        {
            int count = 1; // the two {}
            wr.Indent++;
            wr.Write("/* ");
            foreach (var line in this)
            {
                Debug.Assert(line is AstConstant); // these all should be statements
                int line_count = line.DecompileToText(wr);
                Debug.Assert(line_count == 0); // all statments should return atleast 1
                count++;
            }
            wr.WriteLine(" */");
             wr.Indent--;
            wr.Flush();
            return count;
        }
        public override int DecompileToText(TextWriter wr)
        {
            if (Count == 0) { wr.WriteLine("// No Comments"); return 1; }
            else if (Count == 1) {
                wr.Write("//");
                return this[0].DecompileToText(wr);
            }
            else
            {
                System.CodeDom.Compiler.IndentedTextWriter ident_wr = wr as System.CodeDom.Compiler.IndentedTextWriter;
                if (ident_wr == null) ident_wr = new System.CodeDom.Compiler.IndentedTextWriter(wr); // we are NOT in a statment block so we need to make this
                return DecompileToText(ident_wr);
            }
        }

        public CommentStatement(Instruction info, string message) : base(info) { AddLine(message); }
        public CommentStatement(Instruction info) : base(info) {  }
        public CommentStatement(string message) : base() { AddLine(message); }
    }
    public class ExitStatement : AstStatement
    {
        public override Ast Copy()
        {
            ExitStatement copy = new ExitStatement(this.Instruction);
            CopyChildren(copy);
            return copy;
        }
        public ExitStatement(Instruction i) : base(i) { }
        public override int DecompileToText(TextWriter wr)
        {
            wr.WriteLine("return // Exit Statement ");
            return 1;
        }
    }
    public class GotoStatement : AstStatement
    {
        public LabelStatement LabelLinkTo=null;
        public override Ast Copy()
        {
            GotoStatement copy = new GotoStatement(this.Instruction, Target);
            CopyChildren(copy);
            return copy;
        }
        public Label Target { get; set; }
        public GotoStatement(Instruction info) : base(info) {
           // Debug.Assert(info.GMCode == GMCode.B);
            Target = info.Operand as Label;
        }
        public GotoStatement(Label target) : base() {
            Debug.Assert(target != null);
            Target = target;
        }
        public GotoStatement(Instruction info,Label target) : base(info)
        {
            Debug.Assert(target != null);
            Target = target;
        }
        public override int DecompileToText(TextWriter wr)
        {
#if TEST_LATTER
            Debug.Assert(Target != null && Count == 1 && Goto != null,"No Linking Label Statement"); // no linking Label Statement?
#endif
            if (_debugOn && LabelLinkTo != null) wr.Write("/* Linked */");
            wr.Write("goto ");
            wr.WriteLine(Target);
            
            return 1;
        }
    }
    // Used for decoding other statements
    public class IfStatement : AstStatement
    {
        public override Ast Copy()
        {
            IfStatement copy = new IfStatement(this.Instruction);
            CopyChildren(copy);
            return copy;
        }
        public Ast Condition { get { return this[0]; }  }
        public AstStatement Then { get { if (Count > 1) return this[1] as AstStatement; else return null; } }
        public AstStatement Else { get { if (Count > 2) return this[2] as AstStatement; else return null; } }
        public IfStatement(Instruction info) : base(info) {  }
        public override int DecompileToText(TextWriter wr)
        {
            int count=0;
#if DEBUG
            Debug.Assert(Count > 1, "Not enough children in IfStatement"); // no linking Label Statement?
#endif
            wr.Write("if ");
            count= this[0].DecompileToText(wr);
#if DEBUG
            Debug.Assert(count== 0, "Statements in Condition"); // no linking Label Statement?
#endif
            wr.Write(" then "); // we could have a block here
            count += this[1].DecompileToText(wr);
            if(Count >2) // have an else
            {
                wr.Write(" else "); // we could have a block here
                count += this[2].DecompileToText(wr);
            }
            if(count == 0) { count = 1; wr.WriteLine(); }
            return count;
        }
    }
    /// <summary>
    /// All Children of this calss are calls TO this statement so we can find where they all are
    /// </summary>
    public class LabelStatement : AstStatement
    {
        public List<GotoStatement> CallsHere = null;
       
        public override Ast Copy()
        {
            LabelStatement copy = new LabelStatement(this.Instruction,Target);
            return copy;
        }
        public Label Target { get;  set; }
        
        public LabelStatement(Label label) : base() { this.Target = label; }
        public LabelStatement(Instruction i) : base(i) { this.Target = i.Label; }
        public LabelStatement(Instruction i, Label label) : base(i) { this.Target = label; }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write(this.Target);
            if (_debugOn && CallsHere != null) wr.Write("/* Calls to Here: " + CallsHere.Count + " */");
            wr.WriteLine(":");
            
            return 1;
        }
    }
}
