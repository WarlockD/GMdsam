using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.GMAst
{
    public partial class ILAstOptimizer
    {
     
        bool IntroduceWithStatements(IList<ILNode> body)
        {
            bool modified = false;
            for (int i = 0; i < body.Count; i++)
            {
                modified |= IntroduceWithStatements(body, i);
            }
            return modified;
        }
        bool IntroduceWithStatements(IList<ILNode> body, int i)
        {
            ILLabel pushEnviroment;
            ILLabel popEnviroment;
            int endPos;
            if (!MatchWithInitializer(body, i, out pushEnviroment, out popEnviroment, out endPos))
                return false;
            int startPos = i;
            ILWithStatement withStmt = new ILWithStatement();
            withStmt.Enviroment = (body[i++] as ILExpression).Arguments.Single(); // get enviroment name
            while (i != endPos) withStmt.Body.Body.Add(body[i++]);
            for (i = startPos; i < endPos; i++) body.RemoveAt(i);
            body[i] = withStmt;
            return true;
        }
        bool IsNullOrZero(ILExpression expr)
        {
            if (expr.Code == GMCode.Push)
            {
                ILValue value = expr.Operand as ILValue;
                if (value != null)
                {
                    int test;
                    if (value.TryParse(out test)) return test == 0;
                }
            }
            return false;
        }
        bool MatchWithInitializer(IList<ILNode> body, int i, out ILLabel startEnviroment, out ILLabel endEnviroment, out int exitPos)
        {
            exitPos = i;
            startEnviroment = null;
            endEnviroment = null;
            if (body[i].Match(GMCode.Pushenv, out startEnviroment))
            {
                Debug.Assert(startEnviroment != null);
                for (; i < body.Count; i++)
                {
                    if (body[exitPos].Match(GMCode.Popenv, out endEnviroment))
                    {
                        exitPos = i;
                        return true;
                    }
                }
                throw new Exception("can't find out pop?");
            }
            else
                return false;
        }
    }
}
