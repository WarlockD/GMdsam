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
        struct StringInfo
        {
            public string str;
            public string escaped;
        }
        public bool doLua = false;
        public bool doAsm = false;
        string[] instanceList;
        string[] scriptList;
        string[] fontList;
        string[] spriteList;
        string[] audioList;
        StringInfo[] stringList;
       
        public bool Debug = false;
        public GMContext(ChunkReader cr) {
            stringList = cr.stringList.Select(x => new StringInfo() { escaped = x.escapedString, str = x.str }).ToArray();
            instanceList = cr.objList.Select(x => x.Name).ToArray();
            scriptList = cr.scriptIndex.Select(x => x.script_name).ToArray();
            fontList = cr.resFonts.Select(x => x.Name).ToArray();
            spriteList = cr.spriteList.Select(x => x.Name).ToArray();
            audioList = cr.audioList.Select(x => x.Name).ToArray();
        }
        public string IndexToSpriteName(int index)
        {
            index &= 0x1FFFFF;
            return spriteList[index];
        }
        public string IndexToAudioName(int index)
        {
            index &= 0x1FFFFF;
            return audioList[index];
        }
        public string IndexToScriptName(int index)
        {
            index &= 0x1FFFFF;
            return scriptList[index];
        }
        public string IndexToFontName(int index)
        {
            return fontList[index];
        }
        public string LookupString(int index, bool escape = false)
        {
            index &= 0x1FFFFF;
            return escape ? stringList[index].escaped : stringList[index].str;
        }
        public string InstanceToString(int instance)
        {
            if (instance < 0)
            {
                string instanceName;
                if (GMCodeUtil.instanceLookup.TryGetValue(instance, out instanceName))
                    return instanceName;

            }
            else if (instanceList != null && instance > 0 && instance < instanceList.Length)
            {
                return instanceList[instance];
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
            else if (instanceList != null && instance > 0 && instance < instanceList.Length)
            {
                return new ILExpression(GMCode.Constant, instanceList[instance]);
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
