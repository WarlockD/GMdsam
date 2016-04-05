using betteribttest.GMAst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest
{

    public interface IVisitorEngine
    {
        void Run(ILNode root);
    }
    public class LinkAllGotosAndStatements : IVisitorEngine
    {
        public LinkAllGotosAndStatements() { }
        public HashSet<ILExpression> _gotoes;
        public HashSet<ILLabel> _labels;

        public void Run(ILNode root)
        {
            _gotoes = new HashSet<ILExpression>();
            _labels = new HashSet<ILLabel>();
            foreach (ILNode a in root.GetSelfAndChildrenRecursive<ILNode>()) Visit((dynamic)a);
        }
        protected virtual void Visit(ILNode node) { }
        protected void Visit(ILExpression node)
        {
            if (node.Code != GMCode.B) return;
            if (!_gotoes.Add(node)) throw new Exception("node already there?"); 
        }
        protected void Visit(ILLabel node)
        {
            if (!_labels.Add(node)) throw new Exception("node already there?");
        }
    }
}
