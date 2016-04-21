using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using betteribttest.Dissasembler;

namespace betteribttest
{
    public class GMContext
    {
        public ChunkReader cr;
        public List<string> InstanceList;
        public List<string> scriptList;
        public string CurrentScript = null;
        public string LookupString(int index, bool escape = false)
        {
            index &= 0x1FFFFF;
            return escape ? cr.stringList[index].escapedString : cr.stringList[index].str;
        }
        public string InstanceToString(int instance)
        {
            if (instance < 0)
            {
                string instanceName;
                if (GMCodeUtil.instanceLookup.TryGetValue(instance, out instanceName))
                    return instanceName;

            }
            else if (InstanceList != null && instance > 0 && instance < InstanceList.Count)
            {
                return InstanceList[instance];
            }
            // fallback
            return '$' + instance.ToString() + '$';
        }
        public ILExpression InstanceToExpression(int instance)
        {
            if (instance < 0)
            {
                string instanceName;
                if (GMCodeUtil.instanceLookup.TryGetValue(instance, out instanceName))
                    return new ILExpression(GMCode.Constant, instanceName);

            }
            else if (InstanceList != null && instance > 0 && instance < InstanceList.Count)
            {
                return new ILExpression(GMCode.Constant, InstanceList[instance]);
            }
            // fallback
            return new ILExpression(GMCode.Constant, instance);
        }
        public ILExpression InstanceToExpression(ILExpression instance)
        {
            switch (instance.Code)
            {
                case GMCode.Constant:
                    {
                        ILValue value = instance.Operand as ILValue;
                        if (value.Type == GM_Type.Short || value.Type == GM_Type.Int)
                        {
                            value.ValueText = InstanceToString((int)value);
                        }
                    }
                    break;
                case GMCode.Push: // it was a push, pull the arg out and try it
                    return InstanceToExpression(instance.Arguments.Single());
                case GMCode.Var:
                    break; // if its a var like global.var.something = then just pass it though
                case GMCode.Pop:
                    break; // this is filler in to be filled in latter?  yea
                default:
                    throw new Exception("Something went wrong?");
            }
            return instance;// eveything else we just return as we cannot simplify it
        }
    }
}
