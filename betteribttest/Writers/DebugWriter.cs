using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace GameMaker.Writers
{
    public class DebugWriter : BlockToCode
    {
        public override string LineComment
        {
            get
            {
                return "//";
            }
        }

        public DebugWriter(GMContext context) : base(context) { }
        public DebugWriter(GMContext context, TextWriter tw, string filename = null) : base(context,tw,filename) { }
        public DebugWriter(GMContext context, string filename): base(context,filename) { }


        public override void Write(ILBasicBlock block)
        {
            ILLabel start = block.Body.First() as ILLabel;
            ILExpression end = block.Body.Last() as ILExpression;
            WriteLine("BasicBlock Entry={0}", start.ToString());
            for (int i = 1; i < block.Body.Count - 1; i++)
                WriteNode(block.Body[i], true);
            if (end != null)
                WriteLine("BasicBlock Exit={0}", end.Code == GMCode.B ? end.Operand.ToString() : end.ToString());
            else
                WriteNode(block.Body.Last()); 
            // The if and loop replace the last goto, so watch for that on basic blocks
        }
        public override void Write(ILElseIfChain chain)
        {
            
            for(int i=0; i < chain.Conditions.Count; i++)
            {
                var c = chain.Conditions[i];
                if(i == 0) Write("IFElseChain If ");
                WriteNode(c.Condition);
                WriteLine();
                WriteNode(c.TrueBlock); // auto indent
                if (i < chain.Conditions.Count - 1) Write("IFElseChain ElseIf ");
            }
            if(chain.Else != null && chain.Else.Body.Count > 0)
            {
                WriteLine("IFElseChain Else");
                WriteNode(chain.Else); // auto indent
            }
            WriteLine("IFElseChain End ");
        }

        public override void Write(ILExpression expr)
        {
            Write(expr.ToString()); // debug is the expressions to string
        }
        public override void Write(ILVariable v)
        {
            Write(v.ToString());
        }
        public override void Write(ILValue v)
        {
            Write(v.ToString());
        }
        public override void Write(ILLabel label)
        {
            Write("ILabel {0} ", label.ToString());
        }
        public override void Write(ILCall v)
        {
            Write("ILCall?");
        }
        public override void Write(ILAssign assign)
        {
            Write("ILAssign ");
            Write(assign.Variable);
            Write(" = ");
            Write(assign.Expression); // want to make sure we are using the debug
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
            return;
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
    }
}
