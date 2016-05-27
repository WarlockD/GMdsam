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
namespace GameMaker.Writers.JavaScript
{
    public class Formater : ICodeFormater
    {
        string EnviromentOverride = null;
        // since this is a debug writer we have to handle basicblock, otherwise we would never have this or use dynamic here
        public override void Write(ILSwitch f)
        {
            writer.Write("switch(");
            writer.Write(f.Condition);
            writer.WriteLine(") {");
            writer.Indent++;
           foreach(var c in f.Cases)
            {
                foreach(var v in c.Values)
                {
                    writer.Write("case ");
                    writer.Write(v);
                    writer.WriteLine(":");
                }
                writer.Write((ILBlock)c); // write it as a block
            }
            if(f.Default != null && f.Default.Body.Count > 0)
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
        public override void Write(ILFakeSwitch f)
        {
            throw new Exception("Should not have fake switch here, don't make it an ilnode?");
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
                        writer.Write("return -- exit");
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
            if (!v.isResolved) throw new Exception(v.FullName + " is not resolved");

            if (!v.isLocal && !v.isGenerated && !ScriptArgRegex.IsMatch(v.Name))
            {
                if (v.Instance is ILVariable)
                {
                    writer.Write(v.Instance as ILVariable);
                }
                else if (v.Instance is ILValue)
                {
                    ILValue value = v.Instance as ILValue;
                    if (EnviromentOverride != null)
                    {
                        int? instance = value.IntValue;
                        if (instance == null) throw new Exception(" instance is wierd");
                        if (instance == -1) writer.Write(EnviromentOverride);// self!
                        else throw new Exception(v.FullName + " instance is wierder = " + instance);
                    }
                    else if (v.InstanceName != null) writer.Write(v.InstanceName);
                    else throw new Exception("We got to have an instantname for a constant int");
                }
                else writer.WriteNode(v.Instance);

                //  else throw new Exception(info.ToString() + " instance bad?");
                writer.Write(".");
            }
            if (v.Name == "in")  // reserved word in lua, so we change it here
                v.Name = "_" + v.Name;
            writer.Write(v.Name);

            if (v.isArray)
            {
                writer.Write('[');
                if (v.Index.Code == GMCode.Array2D)
                {
                    writer.Write(v.Index.Arguments[0]);
                    writer.Write("][");
                    writer.Write(v.Index.Arguments[1]);
                }
                else writer.Write(v.Index);
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
                writer.Write(block);
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
        static int localGen = 0;
        public override void Write(ILWithStatement with)
        {
            // UGH Now I see why you use withs
            // This cycles though EACH object named in this instance, so it really IS a loop
            // UNLESS its given an instance number, like from a var or call.  In that case its only doing that instance
            // the with function in code returns a ipairs table of either all the instances OR just the single instance
            // with must be able to tell the diffrence when a string, int or table is sent
            string localVar = "with_" + localGen++;
            string cenvoment = writer.NodeToString(with.Enviroment);
            string env = with.Enviroment.Code == GMCode.Constant ? Context.InstanceToString(with.Enviroment) : null;
            writer.Write("var {0} = search_instances({0})", cenvoment);
            if (env != null) writer.Write(" // Enviroment: {0}", env);
            writer.WriteLine();

            writer.WriteLine("for(var ins=0; ins < {0}.length; ins++) {");
            writer.Indent++;
            writer.WriteLine("var self = {0}[ins]", localVar);
            writer.Write(with.Body, false, false);  // manuay write it
            writer.Indent--;
            writer.Write("}");
            writer.Write(" // end with({0})", cenvoment);
            if (env != null) writer.Write(" // Enviroment: {0}", env);
            writer.WriteLine();
 
        }


        public override string LineComment { get { return "//"; } }
        public override string NodeEnding { get { return ";"; } }
        public override string Extension { get { return "js"; } }
    }

    class LuaVarCheckCashe
    {
        public class VarInfo : IEquatable<VarInfo>
        {
            public string Name;
            public string Instance = null;
            public bool isGlobal { get { return Instance == "global"; } }
            public bool isArray = false;
            public bool Equals(VarInfo o)
            {
                return o.Name == Name && o.Instance == Instance;
            }
            public override bool Equals(object obj)
            {
                if (object.ReferenceEquals(obj, null)) return false;
                if (object.ReferenceEquals(obj, this)) return true;
                VarInfo v = obj as VarInfo;
                return v != null && Equals(v);
            }

            public override int GetHashCode()
            {
                return Name.GetHashCode();
            }
            public override string ToString()
            {
                if (Instance != null) return Instance + '.' + Name;
                else return Name;
            }
        }
        Dictionary<string, VarInfo> allvars = new Dictionary<string, VarInfo>();

        HashSet<VarInfo> allvarsset = new HashSet<VarInfo>();
        HashSet<VarInfo> allpinned = new HashSet<VarInfo>();

        public void AddVar(ILVariable v)
        {
            string name = v.FullName;
            if (allvars.ContainsKey(name)) return;
            VarInfo vi = new VarInfo();
            vi.Name = v.Name;
            if (!v.isLocal && !v.isGenerated) vi.Instance = v.InstanceName ?? v.Instance.ToString();

            vi.isArray = v.Index != null;
            allvars.Add(name, vi);
            allvarsset.Add(vi);
        }
        public void AddVars(ILBlock method)
        { // what we do here is make sure
            foreach (var v in method.GetSelfAndChildrenRecursive<ILVariable>()) AddVar(v);
        }
        public IEnumerable<VarInfo> GetAll()
        {
            return allvarsset;
        }
        public IEnumerable<VarInfo> GetAll(Func<VarInfo, bool> pred)
        {
            return GetAll().Where(pred);
        }
        public IEnumerable<VarInfo> GetAllUnpinned()
        {
            return allvarsset.Except(allpinned);
        }
        public IEnumerable<VarInfo> GetAllUnpinned(Func<VarInfo, bool> pred)
        {
            return GetAllUnpinned().Where(pred);
        }
    }
}