using GameMaker.FlowAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.Dissasembler;

namespace GameMaker.FlowAnalysis
{
    public class RealStackAnalysis
    {
        /// <summary> Immutable </summary>
        GMContext context;
        ILBlock method;
        bool optimize;
        List<ILVariable> allrefs;
        public RealStackAnalysis(GMContext context,bool optimize=false)
        {
            this.optimize = optimize;
            this.context = context;
        }
        sealed class VariableInfo
        {
            public ILVariable Variable;
            public List<ByteCode> Defs;
            public List<ByteCode> Uses;
        }

        // Create temporary structure for the stack analysis
        /// <summary>
		/// If possible, separates local variables into several independent variables.
		/// It should undo any compilers merging.
		/// </summary>
		void ConvertLocalVariables(List<ByteCode> body, Dictionary<string, int> allvars)
        {
            allrefs = method.GetSelfAndChildrenRecursive<ILVariable>().ToList();
            foreach (var varDef in allvars)
            {
                var all = method.GetSelfAndChildrenRecursive<ILVariable>(x => x.FullName == varDef.Key);
                // var defs = all.Except(body.Where(x=> x.IsVariableDefinition != null).Select(x=> x.IsVariableDefinition)).ToList();
                // var uses = all.Except(defs).ToList();
                var defs = body.Where(b => b.IsVariableDefinition != null && b.IsVariableDefinition.FullName == varDef.Key).ToList();
                var uses = body.Where(b => b.IsVariableDefinition == null && (b.Node.GetSelfAndChildrenRecursive<ILVariable>(x => x.FullName == varDef.Key).ToList().Count) > 0).ToList();


                List<VariableInfo> newVars;

                // If the variable is pinned, use single variable.
                // If any of the uses is from unknown definition, use single variable
                // If any of the uses is ldloca with a nondeterministic usage pattern, use  single variable
                if (!optimize || uses.Any(b => b.VariablesBefore[varDef.Value].UnknownDefinition ))
                {
                    newVars = new List<VariableInfo>(1) { new VariableInfo() {
                        Variable = new ILVariable() {
                            Name = string.IsNullOrEmpty(varDef.Key) ? "var_" + varDef.Value : varDef.Key,
                            Type = GM_Type.NoType,
                            isGenerated = true,
                            isLocal = true
                        },
                        Defs = defs,
                        Uses = uses
                    }};
                }
                else
                {
                    // Create a new variable for each definition
                    newVars = defs.Select(def => new VariableInfo()
                    {
                        Variable = new ILVariable()
                        {
                            Name = (string.IsNullOrEmpty(varDef.Key) ? "var_" + varDef.Value : varDef.Key) + "_" + def.Offset.ToString("X2"),
                            Type = GM_Type.NoType,
                            isGenerated = true,
                            isLocal = true
                        },
                        Defs = new List<ByteCode>() { def },
                        Uses = new List<ByteCode>()
                    }).ToList();

                    // VB.NET uses the 'init' to allow use of uninitialized variables.
                    // We do not really care about them too much - if the original variable
                    // was uninitialized at that point it means that no store was called and
                    // thus all our new variables must be uninitialized as well.
                    // So it does not matter which one we load.

                    // TODO: We should add explicit initialization so that C# code compiles.
                    // Remember to handle cases where one path inits the variable, but other does not.

                    // Add loads to the data structure; merge variables if necessary
                    foreach (ByteCode use in uses)
                    {
                        ByteCode[] useDefs = use.VariablesBefore[varDef.Value].Definitions;
                        if (useDefs.Length == 1)
                        {
                            VariableInfo newVar = newVars.Single(v => v.Defs.Contains(useDefs[0]));
                            newVar.Uses.Add(use);
                        }
                        else
                        {
                            List<VariableInfo> mergeVars = newVars.Where(v => v.Defs.Intersect(useDefs).Any()).ToList();
                            VariableInfo mergedVar = new VariableInfo()
                            {
                                Variable = mergeVars[0].Variable,
                                Defs = mergeVars.SelectMany(v => v.Defs).ToList(),
                                Uses = mergeVars.SelectMany(v => v.Uses).ToList()
                            };
                            mergedVar.Uses.Add(use);
                            newVars = newVars.Except(mergeVars).ToList();
                            newVars.Add(mergedVar);
                        }
                    }
                }

                // Set bytecode operands
                foreach (VariableInfo newVar in newVars)
                {
                    foreach (ByteCode def in newVar.Defs)
                    {
                        def.Operand = newVar.Variable;
                    }
                    foreach (ByteCode use in newVar.Uses)
                    {
                        use.Operand = newVar.Variable;
                    }
                }
            }
        }

        List<ByteCode> StackAnalysis(ILBlock method)
        {
            this.method = method;
            List<ILNode> block = method.Body;
            Dictionary<ILNode, ByteCode> nodeToByteCode = new Dictionary<ILNode, ByteCode>();
            List<ByteCode> body = new List<ByteCode>(block.Count);
            Dictionary<string, int> allvars = new Dictionary<string, int>();
            int varCount = 0; // not sure how we get this
            foreach (ILVariable v in method.GetSelfAndChildrenRecursive<ILVariable>())
            {
                string name = v.FullName;
                if (!allvars.ContainsKey(name)) allvars.Add(name, varCount++);
            }
            foreach (var node in block)
            {
                ByteCode byteCode = new ByteCode(node);
                byteCode.Offset = body.Count;
                ILAssign a = node as ILAssign;

                body.Add(byteCode);
            }

            for (int i = 0; i < body.Count - 1; i++) body[i].Next = body[i + 1];
            Stack<ByteCode> agenda = new Stack<ByteCode>();
            agenda.Push(body[0]);


            body[0].StackBefore = new StackSlot[0];
            body[0].VariablesBefore = VariableSlot.MakeUknownState(varCount);
            // Process agenda
            while (agenda.Count > 0)
            {
                ByteCode byteCode = agenda.Pop();

                // Calculate new stack
                StackSlot[] newStack = StackSlot.ModifyStack(byteCode.StackBefore, byteCode.PopCount, byteCode.PushCount, byteCode);

                // Calculate new variable state
                VariableSlot[] newVariableState = VariableSlot.CloneVariableState(byteCode.VariablesBefore);
                if (byteCode.IsVariableDefinition != null)
                {
                    string name = byteCode.IsVariableDefinition.FullName;
                    newVariableState[allvars[name]] = new VariableSlot(new[] { byteCode }, false);
                }


                // Find all successors
                List<ByteCode> branchTargets = new List<ByteCode>();
                if (!byteCode.Code.IsUnconditionalControlFlow())
                {
                    branchTargets.Add(byteCode.Next);
                }
                if (byteCode.Operand is ILLabel[])
                {
                    foreach (ILLabel l in (ILLabel[]) byteCode.Operand)
                    {
                        ByteCode target = nodeToByteCode[l];
                        branchTargets.Add(target);
                        // The target of a branch must have label
                        if (target.Label == null) target.Label = l;
                    }
                }
                else if (byteCode.Operand is Instruction)
                {
                    ILLabel l = byteCode.Operand as ILLabel;
                    ByteCode target = nodeToByteCode[l];
                    branchTargets.Add(target);
                    // The target of a branch must have label
                    if (target.Label == null) target.Label = l;
                }

                // Apply the state to successors
                foreach (ByteCode branchTarget in branchTargets)
                {
                    if (branchTarget.StackBefore == null && branchTarget.VariablesBefore == null)
                    {
                        if (branchTargets.Count == 1)
                        {
                            branchTarget.StackBefore = newStack;
                            branchTarget.VariablesBefore = newVariableState;
                        }
                        else
                        {
                            // Do not share data for several bytecodes
                            branchTarget.StackBefore = StackSlot.ModifyStack(newStack, 0, 0, null);
                            branchTarget.VariablesBefore = VariableSlot.CloneVariableState(newVariableState);
                        }
                        agenda.Push(branchTarget);
                    }
                    else
                    {
                        if (branchTarget.StackBefore.Length != newStack.Length)
                        {
                            throw new Exception("Inconsistent stack size at " + byteCode.Name);
                        }

                        // Be careful not to change our new data - it might be reused for several branch targets.
                        // In general, be careful that two bytecodes never share data structures.

                        bool modified = false;

                        // Merge stacks - modify the target
                        for (int i = 0; i < newStack.Length; i++)
                        {
                            ByteCode[] oldDefs = branchTarget.StackBefore[i].Definitions;
                            ByteCode[] newDefs = oldDefs.Union(newStack[i].Definitions);
                            if (newDefs.Length > oldDefs.Length)
                            {
                                branchTarget.StackBefore[i] = new StackSlot(newDefs, null);
                                modified = true;
                            }
                        }

                        // Merge variables - modify the target
                        for (int i = 0; i < newVariableState.Length; i++)
                        {
                            VariableSlot oldSlot = branchTarget.VariablesBefore[i];
                            VariableSlot newSlot = newVariableState[i];
                            if (!oldSlot.UnknownDefinition)
                            {
                                if (newSlot.UnknownDefinition)
                                {
                                    branchTarget.VariablesBefore[i] = newSlot;
                                    modified = true;
                                }
                                else
                                {
                                    ByteCode[] oldDefs = oldSlot.Definitions;
                                    ByteCode[] newDefs = oldDefs.Union(newSlot.Definitions);
                                    if (newDefs.Length > oldDefs.Length)
                                    {
                                        branchTarget.VariablesBefore[i] = new VariableSlot(newDefs, false);
                                        modified = true;
                                    }
                                }
                            }
                        }

                        if (modified)
                        {
                            agenda.Push(branchTarget);
                        }
                    }
                }
            }
            // Occasionally the compilers or obfuscators generate unreachable code (which might be intentionally invalid)
            // I believe it is safe to just remove it
            body.RemoveAll(b => b.StackBefore == null);
            // Generate temporary variables to replace stack
            foreach (ByteCode byteCode in body)
            {
                int argIdx = 0;
                int popCount = byteCode.PopCount;
                for (int i = byteCode.StackBefore.Length - popCount; i < byteCode.StackBefore.Length; i++)
                {
                    ILVariable tmpVar = new ILVariable() { Name = string.Format("arg_{0:X2}_{1}", i, argIdx), isGenerated = true };
                    byteCode.StackBefore[i] = new StackSlot(byteCode.StackBefore[i].Definitions, tmpVar);
                    foreach (ByteCode pushedBy in byteCode.StackBefore[i].Definitions)
                    {
                        if (pushedBy.StoreTo == null)
                        {
                            pushedBy.StoreTo = new List<ILVariable>(1);
                        }
                        pushedBy.StoreTo.Add(tmpVar);
                    }
                    argIdx++;
                }
            }

            // Try to use single temporary variable insted of several if possilbe (especially useful for dup)
            // This has to be done after all temporary variables are assigned so we know about all loads
            foreach (ByteCode byteCode in body)
            {
                if (byteCode.StoreTo != null && byteCode.StoreTo.Count > 1)
                {
                    var locVars = byteCode.StoreTo;
                    // For each of the variables, find the location where it is loaded - there should be preciesly one
                    var loadedBy = locVars.Select(locVar => body.SelectMany(bc => bc.StackBefore).Single(s => s.LoadFrom == locVar)).ToList();
                    // We now know that all the variables have a single load,
                    // Let's make sure that they have also a single store - us
                    if (loadedBy.All(slot => slot.Definitions.Length == 1 && slot.Definitions[0] == byteCode))
                    {
                        // Great - we can reduce everything into single variable
                        ILVariable tmpVar = new ILVariable() { Name = string.Format("expr_{0}", byteCode.Offset), isGenerated = true };
                        byteCode.StoreTo = new List<ILVariable>() { tmpVar };
                        foreach (ByteCode bc in body)
                        {
                            for (int i = 0; i < bc.StackBefore.Length; i++)
                            {
                                // Is it one of the variable to be merged?
                                if (locVars.Contains(bc.StackBefore[i].LoadFrom))
                                {
                                    // Replace with the new temp variable
                                    bc.StackBefore[i] = new StackSlot(bc.StackBefore[i].Definitions, tmpVar);
                                }
                            }
                        }
                    }
                }
            }

            // Split and convert the normal local variables
            ConvertLocalVariables(body,allvars);


            DumpBody(body);

            //Debug.Assert(false);
            return body;
        }
        List<ILNode> ConvertToAst(List<ByteCode> body)
        {
            List<ILNode> ast = new List<ILNode>();
            foreach (ByteCode byteCode in body)
            {
                ILExpression expr = byteCode.Node as ILExpression;
                if (expr == null)
                {
                    ast.Add(byteCode.Node); // resolved just add
                    continue;
                }

                // Reference arguments using temporary variables
                int popCount = byteCode.PopCount;
                for (int i = byteCode.StackBefore.Length - popCount; i < byteCode.StackBefore.Length; i++)
                {
                    StackSlot slot = byteCode.StackBefore[i];
                    expr.Arguments.Add(new ILExpression(GMCode.Var, slot.LoadFrom));
                }
                // Store the result to temporary variable(s) if needed
                if (byteCode.StoreTo == null || byteCode.StoreTo.Count == 0)
                {
                    ast.Add(expr);
                }
                else if (byteCode.StoreTo.Count == 1)
                {
                    ast.Add(new ILExpression(GMCode.Pop, byteCode.StoreTo[0], expr));
                  //  ast.Add(new ILExpression(ILCode.Stloc, byteCode.StoreTo[0], expr));
                }
                else
                {
                    ILVariable tmpVar = new ILVariable() { Name = "expr_" + byteCode.Offset.ToString(), isGenerated = true, isLocal = true };
                    ast.Add(new ILExpression(GMCode.Pop, tmpVar, expr));
                    foreach (ILVariable storeTo in byteCode.StoreTo.AsEnumerable().Reverse())
                    {
                        ast.Add(new ILExpression(GMCode.Pop, storeTo, tmpVar.ToExpresion()));
                    }
                }
            }
            return ast;
        }
        public List<ILNode> Build(ILBlock method) {
            this.method = method;
            List<ByteCode> body = StackAnalysis(method);
            var ast = ConvertToAst(body);
            return ast;
        }
        struct StackSlot
        {
            public readonly ByteCode[] Definitions;  // Reaching definitions of this stack slot
            public readonly ILVariable LoadFrom;     // Variable used for storage of the value

            public StackSlot(ByteCode[] definitions, ILVariable loadFrom)
            {
                this.Definitions = definitions;
                this.LoadFrom = loadFrom;
            }

            public static StackSlot[] ModifyStack(StackSlot[] stack, int popCount, int pushCount, ByteCode pushDefinition)
            {
                StackSlot[] newStack = new StackSlot[stack.Length - popCount + pushCount];
                Array.Copy(stack, newStack, stack.Length - popCount);
                for (int i = stack.Length - popCount; i < newStack.Length; i++)
                {
                    newStack[i] = new StackSlot(new[] { pushDefinition }, null);
                }
                return newStack;
            }
        }
        /// <summary> Immutable </summary>
		struct VariableSlot
        {
            public readonly ByteCode[] Definitions;       // Reaching deinitions of this variable
            public readonly bool UnknownDefinition; // Used for initial state and exceptional control flow

            static readonly VariableSlot UnknownInstance = new VariableSlot(new ByteCode[0], true);

            public VariableSlot(ByteCode[] definitions, bool unknownDefinition)
            {
                this.Definitions = definitions;
                this.UnknownDefinition = unknownDefinition;
            }

            public static VariableSlot[] CloneVariableState(VariableSlot[] state)
            {
                VariableSlot[] clone = new VariableSlot[state.Length];
                Array.Copy(state, clone, state.Length);
                return clone;
            }

            public static VariableSlot[] MakeUknownState(int varCount)
            {
                VariableSlot[] unknownVariableState = new VariableSlot[varCount];
                for (int i = 0; i < unknownVariableState.Length; i++)
                {
                    unknownVariableState[i] = UnknownInstance;
                }
                return unknownVariableState;
            }
        }
        void DumpBody(List<ByteCode> code,string filename=null)
        {
            using (StreamWriter sw = new StreamWriter(filename ?? "dump.txt"))
            {
                for (int i = 0; i < code.Count; i++)
                {
                    var b = code[i];

                    sw.Write("{0}: ", i);
                    sw.WriteLine(b.ToString());


                }
            }
        }
        sealed class ByteCode
        {
            public ILLabel Label;      // Non-null only if needed
            public GMCode Code;
            public object Operand = null;
            public ILNode Node;
            public int PopCount;
            public int PushCount;
            public ByteCode(ILNode n)
            {
                Node = n;
                Name = n.ToString();
                ILExpression e = n as ILExpression;
                ILAssign a = n as ILAssign;
                if (e != null)
                {
                    Code = e.Code;
                    Operand = e.Operand;

                    switch (e.Code)
                    {
                        case GMCode.Bf:
                        case GMCode.Bt:
                        case GMCode.Ret:
                            PushCount = 0;
                            if (e.Arguments[0].Code == GMCode.Pop)
                                PopCount = 1;
                            else
                                PopCount = 0;// allready optimized
                            break; 
                        case GMCode.Dup:
                            
                            if ((int) e.Operand == 0)
                            {
                                PopCount = 1;
                                PushCount = 2;
                            }
                            else
                            {
                                PopCount = 2;
                               PushCount = 2;
                            }
                            break;
                        case GMCode.Call:
                            PushCount = 1;
                            PopCount = e.Extra;
                            break;
                        case GMCode.Push:
                            {
                                PopCount = 0;
                                PushCount = 1;
                                ILVariable v = Operand as ILVariable;
                                if (v == null && e.Arguments.Count > 0 && e.Arguments[0].Code == GMCode.Var)
                                    v = e.Arguments[0].Operand as ILVariable;
                                if (v != null && v.isResolved) PopCount = v.isArray ? 2 : 1;

                            }
                            break;
                        case GMCode.Pop: // havn't generated an asisign yet
                            {
                                ILVariable v = e.Operand as ILVariable;
                                PopCount = 1;
                                PushCount = 0;
                                if (!v.isResolved) PopCount += v.isArray ? 2 : 1;
                            }
                            break;
                        default:
                            PopCount = Code.GetPopDelta();
                            PushCount = Code.GetPushDelta();
                            break;
                    }
                }
                else if (a != null)
                {
                    Code = GMCode.BadOp;
                    PopCount = PushCount = 0;
                    IsVariableDefinition = a.Variable;
                }
                else
                {
                    Code = GMCode.BadOp;
                    PopCount = PushCount = 0;
                }
                Label = null;
            }
            public int Offset;
            public string Name;
            public ByteCode Next;
            public StackSlot[] StackBefore;     // Unique per bytecode; not shared
            public VariableSlot[] VariablesBefore; // Unique per bytecode; not shared
            public List<ILVariable> StoreTo;         // Store result of instruction to those AST variables

            public ILVariable IsVariableDefinition = null;



            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();

                // Label
                sb.Append(Name);
                sb.Append(':');
                if (this.Label != null)
                    sb.Append('*');

                sb.AppendLine();
                sb.Append("\t\t"); // indent
                if (this.StackBefore != null)
                {
                    sb.Append(" StackBefore={");
                    bool first = true;
                    foreach (StackSlot slot in this.StackBefore)
                    {
                        if (!first) sb.Append(",");
                        bool first2 = true;
                        foreach (ByteCode defs in slot.Definitions)
                        {
                            if (!first2) sb.Append("|");
                            sb.AppendFormat("IL_{0}", defs.Offset);
                            first2 = false;
                        }
                        first = false;
                    }
                    sb.Append("}");
                }

                if (this.StoreTo != null && this.StoreTo.Count > 0)
                {
                    sb.Append(" StoreTo={");
                    bool first = true;
                    foreach (ILVariable stackVar in this.StoreTo)
                    {
                        if (!first) sb.Append(",");
                        sb.Append(stackVar.Name);
                        first = false;
                    }
                    sb.Append("}");
                }

                if (this.VariablesBefore != null)
                {
                    sb.Append(" VarsBefore={");
                    bool first = true;
                    foreach (VariableSlot varSlot in this.VariablesBefore)
                    {
                        if (!first) sb.Append(",");
                        if (varSlot.UnknownDefinition)
                        {
                            sb.Append("?");
                        }
                        else
                        {
                            bool first2 = true;
                            foreach (ByteCode storedBy in varSlot.Definitions)
                            {
                                if (!first2) sb.Append("|");
                                sb.AppendFormat("IL_{0}", storedBy.Offset);
                                first2 = false;
                            }
                        }
                        first = false;
                    }
                    sb.Append("}");
                }

                return sb.ToString();
            }
        }
    }
}
