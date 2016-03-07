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
    /// <summary>
    /// Statment Class is a statment.  It seperates this between tree values
    /// </summary>
    public class AstStatement : Ast, IEnumerable<AstStatement>
    {
        public Block RawBlock { get; set; }
        protected AstStatement(Instruction i) : base(i) { RawBlock = null; }
        protected AstStatement() : base() { RawBlock = null; }
        public override Ast Invert()
        {
            throw new Exception("Cannot invert a statement");
        }
        public virtual bool ContainsType<T>() where T : AstStatement { return this is T; }

        public virtual void FindType<T>(List<T> types) where T : AstStatement { T t = this as T; if (t != null) types.Add(t); }
        public List<T> FindType<T>() where T : AstStatement {
            List<T> ret = new List<T>();
            FindType<T>(ret);
            return ret;
        }
        public override sealed IEnumerable<Ast> AstEnumerator(bool includeSelf) { throw new Exception("Cannot enumerate Ast in statements"); }
        static readonly IEnumerable<AstStatement> EmptyEnumerator = Enumerable.Empty<AstStatement>();
        public virtual IEnumerator<AstStatement> GetEnumerator() { return EmptyEnumerator.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    // This class is used in filler when the stack is an odd shape between labels
    public class PushStatement : AstStatement
    {
        Ast _value = null;
        public Ast Value { get; protected set; }
        ~PushStatement()
        {
            ParentClear(_value);
        }
        public PushStatement(Ast ast) : base(ast.Instruction) { Value = ast; ParentSet(ast); }
        PushStatement(Instruction i) : base(i) { Value = null; }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write("Push(");
            if (_value != null) _value.DecompileToText(wr); else wr.Write("NullPush");
            wr.Write(")");
            return 0;
        }
        public override Ast Copy()
        {
            PushStatement copy = new PushStatement(Value.Copy());
            return copy;
        }
    }
    public class AssignStatment : AstStatement
    {
        public Ast Variable { get; protected set; }
        public Ast Value { get; protected set; }
        public AssignStatment(Instruction i, Ast variable, Ast value) : base(i)
        {
            Debug.Assert(variable != null && value != null);
            Variable = variable;
            Value = value;
            ParentSet(Variable);
            ParentSet(Value);
        }
        ~AssignStatment()
        {
            ParentClear(Variable);
            ParentClear(Value);
        }
        public override int DecompileToText(TextWriter wr)
        {
            Variable.DecompileToText(wr);
            wr.Write(" = ");
            Value.DecompileToText(wr);
            wr.WriteLine();
            return 1;
        }
        public override Ast Copy()
        {
            AssignStatment n = new AssignStatment(this.Instruction, this.Variable.Copy() as AstVar, this.Value.Copy());
            return n;
        }
    }
    public class CallStatement : AstStatement
    {
        public AstCall Call { get; protected set; }
        public CallStatement(Instruction popz, AstCall call) : base(popz)
        {
            Debug.Assert(call != null);
            ParentSet(call);
            Call = call;
        }
        ~CallStatement()
        {
            ParentClear(Call);
        }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write("void ");
            Call.DecompileToText(wr);
            wr.WriteLine();
            return 1;
        }
        public override Ast Copy()
        {
            CallStatement copy = new CallStatement(this.Instruction, this.Call.Copy() as AstCall);
            return copy;
        }
    }

    // This is filler for a blank statement or a message statment for debugging
    public class CommentStatement : AstStatement
    {
        List<string> _lines;
        public IReadOnlyList<string> Message { get { return _lines; } }
        public CommentStatement(Instruction info, string message) : base(info) { _lines = new List<string>(); AddLine(message); }
        public CommentStatement(string message) : base() { _lines = new List<string>(); AddLine(message); }
        public CommentStatement(IEnumerable<string> message) : base() { _lines = message.ToList(); }
        public override Ast Copy()
        {
            CommentStatement copy = new CommentStatement(_lines);
            return copy;
        }
        public void AddLine(string message)
        {
            _lines.Add(message);
        }
        public int DecompileToText(System.CodeDom.Compiler.IndentedTextWriter wr)
        {
            int count = 1 + _lines.Count;
            wr.Indent++;
            wr.Write("/* ");
            foreach (var line in _lines) wr.WriteLine(line);
            wr.WriteLine(" */");
            wr.Indent--;
            wr.Flush();
            return count;
        }
        public override int DecompileToText(TextWriter wr)
        {
            if (_lines.Count == 0) { wr.WriteLine("// No Comments"); return 1; }
            else if (_lines.Count == 1)
            {
                wr.Write("// ");
                wr.WriteLine(_lines[0]);
                return 1;
            }
            else
            {
                System.CodeDom.Compiler.IndentedTextWriter ident_wr = wr as System.CodeDom.Compiler.IndentedTextWriter;
                if (ident_wr == null) ident_wr = new System.CodeDom.Compiler.IndentedTextWriter(wr); // we are NOT in a statment block so we need to make this
                return DecompileToText(ident_wr);
            }
        }

    }
    public class ExitStatement : AstStatement
    {
        public override Ast Copy()
        {
            ExitStatement copy = new ExitStatement(this.Instruction);
            return copy;
        }
        public ExitStatement(Instruction i) : base(i) { }
        public override int DecompileToText(TextWriter wr)
        {
            wr.WriteLine("return // Exit Statement ");
            return 1;
        }
    }
    public class GotoStatement : AstStatement, IEquatable<Label>, IEquatable<GotoStatement>
    {
        public bool Equals(Label l)
        {
            if (l == null) return false;
            return Target == l;
        }
        public bool Equals(GotoStatement g)
        {
            if (g == null) return false;
            return Target == g.Target;
        }
        public override bool Equals(object obj)
        {
            if (base.Equals(obj)) return true;
            GotoStatement that = obj as GotoStatement;
            if (that != null) return Equals(that);
            Label l = obj as Label;
            if (l != null) return Equals(l);
            return false;
        }
        public override int GetHashCode()
        {
            return Target.Address ^ base.GetHashCode();
        }
        public LabelStatement LabelLinkTo = null;
        public override Ast Copy()
        {
            GotoStatement copy = new GotoStatement(this.Instruction, Target);
            return copy;
        }
        public Label Target { get; protected set; }
        public GotoStatement(Instruction info) : base(info)
        {
            // Debug.Assert(info.GMCode == GMCode.B);
            Target = info.Operand as Label;
        }
        public GotoStatement(Label target) : base()
        {
            Debug.Assert(target != null);
            Target = target;
        }
        public GotoStatement(Instruction info, Label target) : base(info)
        {
            Debug.Assert(target != null);
            Target = target;
        }
        public override int DecompileToText(TextWriter wr)
        {
#if TEST_LATTER
            Debug.Assert(Target != null && Count == 1 && Goto != null,"No Linking Label Statement"); // no linking Label Statement?
#endif
            if (LabelLinkTo != null) wr.Write("/* Linked */");
            wr.Write("goto ");
            wr.Write(Target);
            if (LabelLinkTo != null) wr.WriteLine("/* Linked */");
            else wr.WriteLine();
            return 1;
        }
    }
    // Used for decoding other statements
    public class IfStatement : AstStatement
    {
        public override IEnumerator<AstStatement> GetEnumerator() {
            var result = Enumerable.Empty<AstStatement>();
            result = result.Concat(Then);
            if (Else != null) result = result.Concat(Else);
            return result.GetEnumerator();
        }
        public Ast Condition { get; protected set; }
        public AstStatement Then { get; protected set; }
        public AstStatement Else { get; protected set; }
        public override bool ContainsType<T>()
        {
            if (Then is T || Else is T) return true;
            if (Then.ContainsType<T>()) return true;
            if(Else != null && Else.ContainsType<T>()) return true;
            return false;
        }
        public override void FindType<T>(List<T> types)  {
            base.FindType(types); // check self
            T test = Then as T;
            if (test != null) types.Add(test);
            if(Else != null)
            {
                test = Else as T;
                if (test != null) types.Add(test);
            }
        }
        public override Ast Copy()
        {
            IfStatement copy;
            if (Else == null)
                copy = new IfStatement(this.Instruction, Condition.Copy(), Then.Copy() as AstStatement);
            else
                copy = new IfStatement(this.Instruction, Condition.Copy(), Then.Copy() as AstStatement, Else.Copy() as AstStatement);
            return copy;
        }

        public IfStatement(Instruction info, Ast Condition, AstStatement Then) : base(info)
        {
            this.Condition = Condition;
            this.Then = Then;
            ParentSet(this.Condition);
            ParentSet(this.Then);
            Else = null;
        }
        ~IfStatement()
        {
            ParentClear(this.Else);
            ParentClear(this.Then);
            ParentClear(this.Condition);
        }
        public IfStatement(Instruction info, Ast Condition, AstStatement Then, AstStatement Else) : this(info, Condition, Then)
        {
            this.Else = Else;
            ParentSet(this.Else);
        }
        public IfStatement(Instruction info, Ast Condition, Label target) : base(info)
        {
            this.Condition = Condition;
            this.Then = new GotoStatement(target);
            Else = null;
        }
        public override int DecompileToText(TextWriter wr)
        {
            int count = 1;
            wr.Write("if ");
            this.Condition.DecompileToText(wr);
            wr.Write(" then ");
            count += this.Then.DecompileToText(wr);
            if (this.Else != null)
            {
                wr.Write(" else ");
                count += Else.DecompileToText(wr);
            }
            wr.WriteLine();
            return count;
        }


    }
    /// <summary>
    /// All Children of this calss are calls TO this statement so we can find where they all are
    /// </summary>
    public class LabelStatement : AstStatement, IEquatable<Label>, IEquatable<LabelStatement>
    {
        public List<GotoStatement> CallsHere = null;
        public bool Equals(Label l)
        {
            if (l == null) return false;
            return Target == l;
        }
        public bool Equals(LabelStatement g)
        {
            if (g == null) return false;
            return Target == g.Target;
        }
        public override bool Equals(object obj)
        {
            if (base.Equals(obj)) return true;
            LabelStatement that = obj as LabelStatement;
            if (that != null) return Equals(that);
            Label l = obj as Label;
            if (l != null) return Equals(l);
            return false;
        }
        public override int GetHashCode()
        {
            return Target.Address; // this should be unique as we only should have one statments
        }
        public override Ast Copy()
        {
            LabelStatement copy = new LabelStatement(this.Instruction, Target);
            return copy;
        }
        public Label Target { get; set; }

        public LabelStatement(Label label) : base() { this.Target = label; }
        public LabelStatement(Instruction i) : base(i) { this.Target = i.Label; }
        public LabelStatement(Instruction i, Label label) : base(i) { this.Target = label; }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write(this.Target);
            wr.Write(": ");
            if (CallsHere != null) wr.WriteLine("/* Calls to Here: " + CallsHere.Count + " */");
            else wr.WriteLine();
            return 1;
        }
    }
}
