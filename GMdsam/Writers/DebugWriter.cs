using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.Ast;

namespace GameMaker.Writers
{
    public class DebugFormater  : BlockToCode
    {
        public DebugFormater(IMessages error, bool catch_infos = false) : base(error, catch_infos) { }

        public override string LineComment
        {
            get
            {
                return "//";
            }
        }

        public override string NodeEnding
        {
            get
            {
                return null;
            }
        }

        void WriteField<T>(string field, T n) where T: ILNode
        {
            Write(" ,");
            Write(field);
            Write('=');
            Write(n);
        }

        public override void Write(ILSwitch f)
        {
            WriteLine("ILSwitch Condition={0}", f.Condition);
            Indent++;
            foreach (var c in f.Cases)
            {
                Write("ILSwitch.ILCase Value=");
                WriteNodesComma(c.Values);
                WriteLine();
                Write((ILBlock)c);
            }
            if(f.Default != null && f.Default.Body.Count > 0)
            {
                WriteLine();
                WriteLine("ILSwitch Default", f.Condition);
                Write(f.Default);
            }
            Indent--;
        }



        public override void Write(ILExpression expr)
        {
            if ((expr.Parent is ILBlock || expr.Parent is ILBasicBlock))
            {
                StringBuilder sb = new StringBuilder();
                expr.AppendHeader(sb);
                expr.ToStringBuilder(sb, 0);
                Write(sb.ToString());
            }else Write(expr.ToString());
        }
        public override void Write(ILVariable v)
        {
            Write(v.ToString());
        }
        void WriteObject(object o)
        {
            if (o is ILNode)
            {
                if (o is ILValue) Write(o as ILValue);
                else if (o is ILVariable) Write(o as ILVariable);
                else if (o is ILCall) Write(o as ILCall);
                else if (o is ILLabel) Write(o as ILLabel);
                else if (o is string) Write("$'" + o.ToString() + "'$");
                else Write(o as ILNode);
            }
            else if (o.GetType().IsPrimitive)
                Write(o.ToString());
            else Write("?" + o.ToString() + "?");
        }
        public override void Write(ILValue v)
        {
            Write(v.ToString());
        }
        public override void Write(ILLabel label)
        {
            Write(":{0}:", label.Name);
        }
        public override void Write(ILCall v)
        {
            Write("ILCall?");
        }
        public override void Write(ILCondition condition)
        {
            Write("ILCondition If ");
            Write(condition.Condition); // want to make sure we are using the debug
            WriteLine("then");
            Write(condition.TrueBlock);
            if (condition.FalseBlock != null && condition.FalseBlock.Body.Count > 0)
            {
                WriteLine("else");
                Write(condition.FalseBlock);
            }
            WriteLine("ILCondition end");
        }
        public override void Write(ILWhileLoop loop)
        {
            Write("ILWhileLoop If ");
            Write(loop.Condition); // want to make sure we are using the debug
            WriteLine(" do");
            Write(loop.BodyBlock);
            WriteLine("ILWhileLoop end");
        }
        public override void Write(ILWithStatement with)
        {
            Write("ILWithStatement with ");
            Write(with.Enviroment); // want to make sure we are using the debug
            WriteLine(" do");
            Write(with.Body);
            WriteLine("ILWithStatement end");
        }

        public override string Extension { get { return "_d.txt"; } }
        public override string BlockCommentStart { get { return "/*"; } }
        public override string BlockCommentEnd { get { return "*/"; } }
    }
}
