using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using GameMaker.Ast;

namespace GameMaker.Writers.GameMaker
{
    public class Formater : CodeFormater
    {
        // since this is a debug writer we have to handle basicblock, otherwise we would never have this or use dynamic here
       
        public override void Write(ILSwitch f)
        {
            writer.Write("switch(");
            writer.Write(f.Condition);
            writer.WriteLine(") {");
            writer.Indent++;
            foreach (var c in f.Cases)
            {
                foreach (var v in c.Values)
                {
                    writer.Write("case ");
                    writer.Write(v);
                    writer.WriteLine(":");
                }
                writer.Write((ILBlock)c); // write it as a block
            }
            if (f.Default != null && f.Default.Body.Count > 0)
            {
                writer.Write("default:");
                writer.Write(f.Default);
            }
            writer.Indent--;
            writer.Write("}");
        }
        public override void Write(ILBasicBlock block)
        {
            throw new Exception("Should not run into basic block here");
        }

        // this is a switch statement so we will just write it as such
        public override void Write(ILElseIfChain chain)
        {
            ILExpression left = chain.Conditions[0].Condition.Arguments[0]; // should be the left one
            writer.Write("switch(");
            writer.Write(left);
            writer.WriteLine(") {");
            writer.Indent++;
            for (int i = 0; i < chain.Conditions.Count; i++)
            {
                ILCondition c = chain.Conditions[i];
                writer.Write("case ");
                writer.Write(c.Condition.Arguments[1]);
                writer.WriteLine(":");
                // just want to save writing some brackets here
                writer.Indent++;
                writer.WriteNodes(c.TrueBlock.Body, true, false);
                writer.WriteLine("break;");
                writer.Indent--;
            }
            if (chain.Else != null && chain.Else.Body.Count > 0)
            {
                writer.WriteLine("default:");
                writer.Indent++;
                writer.WriteNodes(chain.Else.Body, true, false);
                writer.WriteLine("break;");
                writer.Indent--;
            }
            writer.Indent--;
            writer.Write("}}");// Side note: ILBlock puts the writeline here
        }
        class ExpresionInfo
        {
            public readonly string Kind;
            public readonly int Args;
            public readonly int Precedence;
            public ExpresionInfo(string k, int args, int prec)
            {
                this.Kind = k;
                this.Args = args;
                this.Precedence = prec;
            }
        }
        static readonly Dictionary<GMCode, ExpresionInfo> GMCodeToLua = new Dictionary<GMCode, ExpresionInfo>()
        {
            { GMCode.Not,   new ExpresionInfo("!",1,7) }, // lua wierdnes
            { GMCode.Neg,   new ExpresionInfo("-",1,7) },
            { GMCode.Mul,   new ExpresionInfo("*",2,6) },
            { GMCode.Div,   new ExpresionInfo("/" ,2,6)},
            { GMCode.Mod,   new ExpresionInfo("%" ,2,6)},
            { GMCode.Add,   new ExpresionInfo("+",2,5)},
            { GMCode.Sub,   new ExpresionInfo("-",2,5)},
            { GMCode.Concat, new ExpresionInfo("+" ,2,4) } ,// javascript concat is just a +
            // in lua, these have all the same prec
            { GMCode.Sne,   new ExpresionInfo("!=",2,3)  },
            { GMCode.Sge,   new ExpresionInfo(">=" ,2,3) },
            { GMCode.Slt,   new ExpresionInfo("<" ,2,3) },
            { GMCode.Sgt,   new ExpresionInfo(">" ,2,3) },
            { GMCode.Seq,   new ExpresionInfo("==" ,2,3) },
            { GMCode.Sle,   new ExpresionInfo("<=" ,2,3) },
            { GMCode.LogicAnd, new ExpresionInfo("&&",2,2) },
            { GMCode.LogicOr, new ExpresionInfo("||",2,1) },
        };

        public override string GMCodeToString(GMCode c)
        {
            return GMCodeToLua[c].Kind;
        }
        // all this does is just check to see if the next tree is equal to the last tree of precidence
        // that is (4- 3) +3, the parms don't matter so don't print them, otherwise we need them
        protected override int Precedence(GMCode code)
        {
            switch (code)
            {
                case GMCode.Not:
                case GMCode.Neg:
                    return 7;
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Mod:
                    return 6;
                case GMCode.Add:
                case GMCode.Sub:
                // case GMCode.Pow: // not in gm
                case GMCode.Concat: // add goes here
                    return 5;

                case GMCode.Sge:
                case GMCode.Slt:
                case GMCode.Sgt:
                case GMCode.Sle:
                    return 4;
                case GMCode.Sne:
                case GMCode.Seq:
                    return 3;
                case GMCode.LogicAnd:
                    return 2;
                case GMCode.LogicOr:
                    return 1;
                default:
                    return 8;
            }
        }

        void WriteAssign(ILVariable v, ILExpression right)
        {
            // Lets make assign nice here, instead of having to
            // make another rule to fix the ast
            string vstring = writer.NodeToString(v);
            writer.Write(vstring);

            // I could check the second leaf, but meh
            if (right.Arguments.Count == 2 &&   right.Arguments[0].TreeEqual(v))
            {
                ILValue cv;
                switch (right.Code)
                {
                    case GMCode.Add:
                        if (right.Arguments[1].Match(GMCode.Constant, out cv) && cv.IntValue == 1)
                            writer.Write("++");
                        else
                        {
                            writer.Write(" += ");
                            writer.Write(right.Arguments[1]);
                        }
                        break;
                    case GMCode.Sub:
                        if (right.Arguments[1].Match(GMCode.Constant, out cv) && cv.IntValue == 1)
                            writer.Write("--");
                        else
                        {
                            writer.Write(" -= ");
                            writer.Write(right.Arguments[1]);
                        }
                        break;
                    case GMCode.Mul:
                        writer.Write(" *= ");
                        writer.Write(right.Arguments[1]);
                        break;
                    case GMCode.Div:
                        writer.Write(" /= ");
                        writer.Write(right.Arguments[1]);
                        break;
                    case GMCode.Mod:
                        writer.Write(" %= ");
                        writer.Write(right.Arguments[1]);
                        break;
                    default:
                        writer.Write(" = "); // default
                        writer.Write(right);
                        break;
                }
            }
            else
            {
                writer.Write(" = "); // default
                writer.Write(right);
            }

        }

        public override void Write(ILExpression expr)
        {
            ExpresionInfo info;
            if (GMCodeToLua.TryGetValue(expr.Code, out info))
            {
                if (info.Args == 1)
                {
                    writer.Write(info.Kind);
                    WriteParm(expr, 0);
                }
                else if (info.Args == 2)
                {
                    WriteParm(expr, 0);
                    writer.Write(' ');
                    writer.Write(info.Kind); // incase concat gets in there?
                    writer.Write(' ');
                    WriteParm(expr, 1);
                }
            }
            else
            {
                //  if (Code.isExpression())
                //      WriteExpressionLua(output);
                // ok, big one here, important to get this right
                switch (expr.Code)
                {
                    case GMCode.Call:
                        writer.Write(expr.Operand as ILCall);
                        break;
                    case GMCode.Constant: // primitive c# type
                        writer.Write(expr.Operand as ILValue);
                        break;
                    case GMCode.Var:  // should be ILVariable
                        writer.Write(expr.Operand as ILVariable);
                        break;
                    case GMCode.Exit:
                        writer.Write("return // exit");
                        break;
                    case GMCode.Ret:
                        writer.Write("return ");
                        writer.Write(expr.Arguments.Single());
                        break;
                    case GMCode.LoopOrSwitchBreak:
                        writer.Write("break");
                        break;
                    case GMCode.LoopContinue:
                        writer.Write("continue");
                        break;
                    case GMCode.Assign:
                        WriteAssign(expr.Operand as ILVariable, expr.Arguments.Single());
                        break;

                    default:
                        throw new Exception("Not Implmented! ugh");
                }
            }
            if (expr.Comment != null) writer.Write("--[[ " + expr.Comment + "--]]");
        }

        //Write(expr.ToString()); // debug is the expressions to string
        static Regex ScriptArgRegex = new Regex(@"argument(\d+)", RegexOptions.Compiled);
        public override void Write(ILVariable v)
        {
            string instanceName = Context.InstanceToString(v.Instance, writer);
            if (instanceName != "self")
            {
                writer.Write(instanceName);
                writer.Write('.');
            } else if(Constants.IsDefined(v.Name))
            {
                writer.Write("builtin.");
            }
            writer.Write(v.Name);
            if (v.isArray)
            {
                writer.Write('[');
                if(v.Index.Code == GMCode.Array2D)
                {
                    writer.Write(v.Index.Arguments[0]);
                    writer.Write(',');
                    writer.Write(v.Index.Arguments[1]);
                } else writer.Write(v.Index);
                writer.Write(']');
            }
        }
        public override void Write(ILValue v)
        {
            writer.Write(v.ToString());
        }
        public override void Write(ILLabel label)
        {
            throw new Exception("Should not run into labels here in lua");
        }
        public override void Write(ILCall v)
        {
            if (v.FullTextOverride != null) // we havea ful name override so just write that
            {
                writer.Write(v.FullTextOverride); // have to assump its a proper function
            }
            else
            {
                string script_name;
                if (v.FunctionNameOverride != null)
                    script_name = v.FunctionNameOverride;
                else
                    script_name = v.Name;
                //   Debug.Assert(!script_name.Contains("scr_damagestandard"));
                writer.Write(script_name);
                bool need_comma = false;
                writer.Write('('); // self is normaly the first of eveything
                writer.WriteNodesComma(v.Arguments, need_comma);
                writer.Write(')');
            }
            if (v.Comment != null) writer.Write(" */ {0}  */", v.Comment);

        }

        void WriteSingleLineOrBlock(ILBlock block)
        {
            if (block.Body.Count == 1)
                writer.Write(block, true);
            else
            {
                writer.WriteLine(" {");
                writer.Write(block, false);
                writer.Write("}"); // extra ';' here but can I live with that?
            }
        }
        public override void Write(ILCondition condition)
        {
            writer.Write("if(");
            writer.Write(condition.Condition); // want to make sure we are using the debug
            writer.Write(") ");
            WriteSingleLineOrBlock(condition.TrueBlock);
            if (condition.FalseBlock != null && condition.FalseBlock.Body.Count > 0)
            {
                writer.Write(" else ");
                WriteSingleLineOrBlock(condition.FalseBlock);
            }
        }
        public override void Write(ILWhileLoop loop)
        {
            writer.Write("while(");
            writer.Write(loop.Condition); // want to make sure we are using the debug
            writer.Write(") ");
            WriteSingleLineOrBlock(loop.BodyBlock);
        }
        public override void Write(ILWithStatement with)
        {
            string env = with.Enviroment.Code == GMCode.Constant ? Context.InstanceToString(with.Enviroment,writer) : null;
            if (env != null) writer.WriteLine("// {0}", env);
            writer.Write("with(");
            writer.Write(with.Enviroment); // want to make sure we are using the debug
            writer.Write(") ");
            WriteSingleLineOrBlock(with.Body);
        }


        public override string LineComment { get { return "//"; } }
        public override string NodeEnding { get { return ";"; } }
        public override string Extension { get { return "js"; } }
        public override string BlockCommentStart { get { return "/*"; } }
        public override string BlockCommentEnd { get { return "*/"; } }
    }

}
