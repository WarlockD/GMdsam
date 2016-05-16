using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.Dissasembler;


namespace GameMaker.Writers
{
    public class LoveObjectWriter :  BlockToCode
    {
        public LoveObjectWriter(GMContext context) : base(context) { }
        public LoveObjectWriter(GMContext context, TextWriter tw, string filename = null) : base(context,tw,filename) { }
        public LoveObjectWriter(GMContext context, string filename): base(context,filename) { }
        public File.GObject Object { get; set; }
        // since this is a debug writer we have to handle basicblock, otherwise we would never have this or use dynamic here
        protected override void Write(ILBasicBlock block)
        {
            throw new Exception("Should not run into basic block here");
        }
        protected override void Write(ILElseIfChain chain)
        {
            for (int i = 0; i < chain.Conditions.Count; i++)
            {
                var c = chain.Conditions[i];
                if (i == 0) Write("if ");
                WriteNode(c.Condition);
                Write(" then");
                WriteLine();
                WriteNode(c.TrueBlock); // auto indent
                if (i < chain.Conditions.Count - 1) Write("elseif ");
            }
            if (chain.Else != null && chain.Else.Body.Count > 0)
            {
                WriteLine("else");
                WriteNode(chain.Else); // auto indent
            }
            WriteLine("end");
        }

        protected override void Write(ILExpression expr)
        {
            Write(expr.ToString()); // debug is the expressions to string
        }
        protected override void Write(ILVariable v)
        {
            Write(v.ToString());
        }
        protected override void Write(ILValue v)
        {
            Write(v.ToString());
        }
        protected override void Write(ILLabel label)
        {
            throw new Exception("Should not run into labels here in lua");
        }
        protected override void Write(ILCall v)
        {
            Write("ILCall?");
        }
        protected override void Write(ILAssign assign)
        {
            Write(assign.Variable);
            Write(" = ");
            Write(assign.Expression); // want to make sure we are using the debug
        }
        protected override void Write(ILCondition condition)
        {
            Write("if ");
            Write(condition.Condition); // want to make sure we are using the debug
            WriteLine(" then");
            Write(condition.TrueBlock);
            if (condition.FalseBlock != null && condition.FalseBlock.Body.Count > 0)
            {
                WriteLine("else");
                Write(condition.FalseBlock);
            }
            WriteLine("end");
            return;
        }
        protected override void Write(ILWhileLoop loop)
        {
            Write("while ");
            Write(loop.Condition); // want to make sure we are using the debug
            WriteLine(" do");
            Write(loop.BodyBlock);
            WriteLine("end");
        }
        protected override void Write(ILWithStatement with)
        {
            Write("ILWithStatement with ");
            Write(with.Enviroment); // want to make sure we are using the debug
            WriteLine(" do");
            Write(with.Body);
            WriteLine("ILWithStatement end");
        }
        protected override void WriteMethodHeader()
        {
            if (FileName != null) WriteLine("Filename: {0}", FileName);
            WriteLine("MethodName: {0}", MethodName);
        }
    }
}
