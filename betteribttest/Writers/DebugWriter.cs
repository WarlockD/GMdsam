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
    public class DebugFormater : ICodeFormater 
    {

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
        protected override int Precedence(GMCode c)
        {
            throw new NotImplementedException();
            // we don't use this cause we put parms on eveything
        }
        public override string GMCodeToString(GMCode c)
        {
            // don't use
            return null;
        }
        void WriteField<T>(string field, T n) where T: ILNode
        {
            writer.Write(" ,");
            writer.Write(field);
            writer.Write('=');
            writer.WriteNode(n,false);
        }


        public override void Write(ILBasicBlock block)
        {
            writer.WriteLine("BasicBlock:");
            writer.Indent++;
            foreach (var n in block.Body)
                writer.WriteNode(n, true);
            writer.Indent--;
            // The if and loop replace the last goto, so watch for that on basic blocks
        }
        public override void Write(ILFakeSwitch f)
        {
            writer.WriteLine("ILFakeSwitch Condition={0}", f.Condition);
            writer.Indent++;
            foreach(var c in f.Cases)
            {
                writer.Write("ILFakeSwitch.ILCase Value=");
                writer.Write(c.Value);
                writer.Write(" Goto=");
                writer.Write(c.Goto);
                writer.WriteLine();
            }
            writer.Indent--;
        }
        public override void Write(ILSwitch f)
        {
            writer.WriteLine("ILSwitch Condition={0}", f.Condition);
            writer.Indent++;
            foreach (var c in f.Cases)
            {
                writer.Write("ILSwitch.ILCase Value=");
                writer.WriteNodesComma(c.Values);
                writer.WriteLine();
                writer.Write((ILBlock)c);
            }
            if(f.Default != null && f.Default.Body.Count > 0)
            {
                writer.WriteLine();
                writer.WriteLine("ILSwitch Default", f.Condition);
                writer.Write(f.Default);
            }
            writer.Indent--;
        }
        public override void Write(ILElseIfChain chain)
        {
            
            for(int i=0; i < chain.Conditions.Count; i++)
            {
                var c = chain.Conditions[i];
                if(i == 0) writer.Write("IFElseChain If ");
                writer.Write(c.Condition);
                writer.WriteLine();
                writer.Write(c.TrueBlock); // auto indent
                if (i < chain.Conditions.Count - 1) writer.Write("IFElseChain ElseIf ");
            }
            if(chain.Else != null && chain.Else.Body.Count > 0)
            {
                writer.WriteLine("IFElseChain Else");
                writer.Write(chain.Else); // auto indent
            }
            writer.WriteLine("IFElseChain End ");
        }


        public override void Write(ILExpression expr)
        {
            if ((expr.Parent is ILBlock || expr.Parent is ILBasicBlock))
            {
                StringBuilder sb = new StringBuilder();
                expr.AppendHeader(sb);
                expr.ToStringBuilder(sb, 0);
                writer.Write(sb.ToString());
            }else writer.Write(expr.ToString());
        }
        public override void Write(ILVariable v)
        {
            writer.Write(v.ToString());
        }
        void WriteObject(object o)
        {
            if (o is ILNode)
            {
                if (o is ILValue) writer.Write(o as ILValue);
                else if (o is ILVariable) writer.Write(o as ILVariable);
                else if (o is ILCall) writer.Write(o as ILCall);
                else if (o is ILLabel) writer.Write(o as ILLabel);
                else if (o is string) writer.Write("$'" + o.ToString() + "'$");
                else writer.WriteNode(o as ILNode, false);
            }
            else if (o.GetType().IsPrimitive)
                writer.Write(o.ToString());
            else writer.Write("?" + o.ToString() + "?");
        }
        public override void Write(ILValue v)
        {
            writer.Write(v.ToString());
        }
        public override void Write(ILLabel label)
        {
            writer.Write(":{0}:", label.Name);
        }
        public override void Write(ILCall v)
        {
            writer.Write("ILCall?");
        }
        public override void Write(ILCondition condition)
        {
            writer.Write("ILCondition If ");
            writer.Write(condition.Condition); // want to make sure we are using the debug
            writer.WriteLine("then");
            writer.Write(condition.TrueBlock);
            if (condition.FalseBlock != null && condition.FalseBlock.Body.Count > 0)
            {
                writer.WriteLine("else");
                writer.Write(condition.FalseBlock);
            }
            writer.WriteLine("ILCondition end");
        }
        public override void Write(ILWhileLoop loop)
        {
            writer.Write("ILWhileLoop If ");
            writer.Write(loop.Condition); // want to make sure we are using the debug
            writer.WriteLine(" do");
            writer.Write(loop.BodyBlock);
            writer.WriteLine("ILWhileLoop end");
        }
        public override void Write(ILWithStatement with)
        {
            writer.Write("ILWithStatement with ");
            writer.Write(with.Enviroment); // want to make sure we are using the debug
            writer.WriteLine(" do");
            writer.Write(with.Body);
            writer.WriteLine("ILWithStatement end");
        }


        public override string Extension { get { return "_d.txt"; } }

    }
}
