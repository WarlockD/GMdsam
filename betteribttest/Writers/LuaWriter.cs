using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GameMaker.Writers
{
    public class LuaWriter :  BlockToCode
    {
        public class VarInfo: IEquatable<VarInfo>
        {
            public readonly ILVariable Var;
            public readonly int Line;
            public readonly int Col;
            public readonly string Name;
            public VarInfo(ILVariable v, int line, int col)
            {
                this.Name = v.FullName;
                this.Line = line;
                this.Col = col;
            }
            public bool Equals(VarInfo other)
            {
                return Name == other.Name;
            }
            public override int GetHashCode()
            {
                return Name.GetHashCode();
            }
            public override string ToString()
            {
                return Name + " on line " + Line + " at col " + Col;
            }
        }
        string EnviromentOverride = null;
        List<VarInfo> usedVars = new List<VarInfo>();
        public IReadOnlyList<VarInfo> UsedVars {  get { return usedVars; } }
        public IReadOnlyList<VarInfo> AssignedVars { get { return assignedVars; } }
        List<VarInfo> assignedVars = new List<VarInfo>();
        public LuaWriter(GMContext context, TextWriter tw, string filename = null) : base(context,tw,filename) { }
        public LuaWriter(GMContext context, string filename=null): base(context,filename) { }
        public File.GObject Object { get; set; }
        // since this is a debug writer we have to handle basicblock, otherwise we would never have this or use dynamic here
        public override void Write(ILBasicBlock block)
        {
            throw new Exception("Should not run into basic block here");
        }
        public override void Write(ILElseIfChain chain)
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
            Write("end"); // Side note: ILBlock puts the writeline here
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
            { GMCode.Not,   new ExpresionInfo("not",1,7) }, // lua wierdnes
            { GMCode.Neg,   new ExpresionInfo("-",1,7) },
            { GMCode.Mul,   new ExpresionInfo("*",2,6) },
            { GMCode.Div,   new ExpresionInfo("/" ,2,6)},
            { GMCode.Mod,   new ExpresionInfo("%" ,2,6)},
            { GMCode.Add,   new ExpresionInfo("+",2,5)},
            { GMCode.Sub,   new ExpresionInfo("-",2,5)},
            { GMCode.Concat, new ExpresionInfo(".." ,2,4) } ,// lua wierdnes, watch prec
            // in lua, these have all the same prec
            { GMCode.Sne,   new ExpresionInfo("~=",2,3)  },
            { GMCode.Sge,   new ExpresionInfo(">=" ,2,3) },
            { GMCode.Slt,   new ExpresionInfo("<" ,2,3) },
            { GMCode.Sgt,   new ExpresionInfo(">" ,2,3) },
            { GMCode.Seq,   new ExpresionInfo("==" ,2,3) },
            { GMCode.Sle,   new ExpresionInfo("<=" ,2,3) },
            { GMCode.LogicAnd, new ExpresionInfo("and",2,2) },
            { GMCode.LogicOr, new ExpresionInfo("or",2,1) },
        };
        // all this does is just check to see if the next tree is equal to the last tree of precidence
        // that is (4- 3) +3, the parms don't matter so don't print them, otherwise we need them
        static int Precedence(GMCode code)
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
                    return 5;
                // case GMCode.Pow: // not in gm
                case GMCode.Concat:
                    return 4;
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Slt:
                case GMCode.Sgt:
                case GMCode.Sne:
                case GMCode.Sle:
                    return 3;
                case GMCode.LogicAnd:
                    return 2;
                case GMCode.LogicOr:
                    return 1;
                default:
                    return 8;
            }
        }
        static bool CheckParm(ILExpression expr, int index)
        {
            int ours = Precedence(expr.Code);
            ILExpression e = expr.Arguments.ElementAtOrDefault(index);
            if (e == null) return false;
            int theirs = Precedence(e.Code);
            if (theirs == 8) return false; // its a constant or something dosn't need a parm
            if (theirs < ours) return true;
            else return false;
        }
        void WriteParm(ILExpression expr, int index)
        {
            bool needParm = CheckParm(expr, index);
            if (needParm) Write('(');
            Write(expr.Arguments[index]);
            if (needParm) Write(')');
        }
        public override void Write(ILExpression expr)
        {
            ExpresionInfo info;
            if (GMCodeToLua.TryGetValue(expr.Code, out info)) {
                if(info.Args == 1)
                {
                    Write(info.Kind);
                    WriteParm(expr, 0);
                } else if(info.Args == 2)
                {
                    WriteParm(expr, 0);
                    Write(' ');
                    Write(info.Kind);
                    Write(' ');
                    WriteParm(expr, 1);
                }
            } else
            {
                //  if (Code.isExpression())
                //      WriteExpressionLua(output);
                // ok, big one here, important to get this right
                switch (expr.Code)
                {
                    case GMCode.Call:
                        Write(expr.Operand as ILCall);
                        break;
                    case GMCode.Constant: // primitive c# type
                        Write(expr.Operand as ILValue);
                        break;
                    case GMCode.Var:  // should be ILVariable
                        Write(expr.Operand as ILVariable);
                        break;
                    case GMCode.Exit:
                        Write("return -- exit");
                        break;
                    case GMCode.Ret:
                        Write("return ");
                        Write(expr.Arguments.Single());
                        break;
                    case GMCode.LoopOrSwitchBreak:
                        Write("break");
                        break;
                    case GMCode.LoopContinue:
                        Write("continue");
                        break;
                    default:
                        throw new Exception("Not Implmented! ugh");
                }
            }
           if (expr.Comment != null)  Write("--[[ " + expr.Comment + "--]]");
        }

        //Write(expr.ToString()); // debug is the expressions to string

        public override void Write(ILVariable v)
        {
            VarInfo info = new VarInfo(v, Line, Column);
            usedVars.Add(info);
            if (!v.isResolved) throw new Exception(info.ToString() + " is not resolved");

            if (!v.isLocal && !v.isGenerated)
            {
                if (EnviromentOverride != null && v.Instance is ILValue)
                {
                    int? instance = (v.Instance as ILValue).IntValue;
                    if (instance == null) throw new Exception(info.ToString() + " instance is wierd");
                    if (instance == -1) // self!
                    {
                        Write(EnviromentOverride);
                        Write(".");
                    }
                    else throw new Exception(info.ToString() + " instance is wierder = " + instance);
                }
                else if (v.InstanceName != null) Write(v.InstanceName);
                else if (v.Instance != null) WriteNode(v.Instance);
                else throw new Exception(info.ToString() + " instance bad?");
                Write(".");
            }
            Write(v.Name);

            if (v.isArray)
            {
                Write('[');
                if (v.Index.Code == GMCode.Array2D)
                {
                    WriteNode(v.Index.Arguments[0]);
                    Write("][");
                    WriteNode(v.Index.Arguments[1]);
                }
                else WriteNode(v.Index);
                Write(']');
            }
        }
        public override void Write(ILValue v)
        {
            Write(v.ToString());
        }
        public override void Write(ILLabel label)
        {
            throw new Exception("Should not run into labels here in lua");
        }
        public override void Write(ILCall v)
        {
            string script_name =  v.Name.Replace("gml_Script_", "");
            Debug.Assert(!script_name.Contains("scr_damagestandard"));
            Write(script_name);
            Write('('); // self is normaly the first of eveything
            if (EnviromentOverride != null)
                Write(EnviromentOverride);
            else
                Write("self");
            foreach(var n in v.Arguments)
            {
                Write(',');
                WriteNode(n);
            }
            Write(')');
        }
        public override void Write(ILAssign assign)
        {
            VarInfo info = new VarInfo(assign.Variable, Line, Column);
            usedVars.Add(info);
            assignedVars.Add(info);
            Write(assign.Variable);
            Write(" = ");
            if(assign.TextToReplace != null) { 
                Write(assign.TextToReplace);
                Write(" -- Replaced: ");
            }
            Write(assign.Expression); // want to make sure we are using the debug
        }
        public override void Write(ILCondition condition)
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
            Write("end"); // Side note: ILBlock puts the writeline here
            return;
        }
        public override void Write(ILWhileLoop loop)
        {
            Write("while ");
            Write(loop.Condition); // want to make sure we are using the debug
            WriteLine(" do");
            Write(loop.BodyBlock);
            Write("end"); // Side note: ILBlock puts the writeline here
        }
        static int localGen = 0;
        public override void Write(ILWithStatement with)
        {
            // UGH Now I see why you use withs
            // This cycles though EACH object named in this instance, so it really IS a loop
            // UNLESS its given an instance number, like from a var or call.  In that case its only doing that instance
            if (with.Enviroment.Code == GMCode.Constant) // if its a constant, we have to check each one in the room
            {
                string localVar = "with_" + localGen++;
                Write("for _, {0} in with_each({1}) do", localVar, with.Enviroment.Operand.ToString());
                if (with.EnviromentName != null) WriteLine(" -- Enviroment: {0}", with.EnviromentName);
                else WriteLine();
                Ident++;
                WriteLine("local self = {0} -- change instance", localVar);
                WriteNodes(with.Body.Body, true, false); // manualy write it
                Ident--;
                Write("end");
                if (with.EnviromentName != null) Write(" -- Enviroment: {0}", with.EnviromentName);
            }
            else // else just replace the enviroment
            {
                string env = with.EnviromentName ?? context.InstanceToString(with.Enviroment);
                if (env == null)// last chance
                    using (var w = new LuaWriter(context)) { w.Write(with.Enviroment); env = w.ToString(); }
                Debug.Assert(env != null);
                WriteLine("-- start with({0})", env);
                string old_env = EnviromentOverride;
                WriteNodes(with.Body.Body, true, false); // manualy write it
                EnviromentOverride = old_env;
                WriteLine("-- end with({0})", env);
            }
        }
        public override string LineComment { get { return "--"; } }
    }
}
