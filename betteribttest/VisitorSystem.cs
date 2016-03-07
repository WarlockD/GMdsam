using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest
{
    public interface IVisitorEngine
    {
        void Run(AstStatement root);
    }
    public class LinkAllGotosAndStatements : IVisitorEngine
    {
        public LinkAllGotosAndStatements() { }
        public HashSet<GotoStatement> _gotoes;
        public HashSet<LabelStatement> _labels;

        public void Run(AstStatement root)
        {
            _gotoes = new HashSet<GotoStatement>();
            _labels = new HashSet<LabelStatement>();
            foreach (AstStatement a in root) Visit((dynamic)a);
        }
        protected virtual void Visit(AstStatement node) { }
        protected void Visit(GotoStatement node)
        {
            if (!_gotoes.Add(node)) throw new Exception("node already there?"); 
        }
        protected void Visit(LabelStatement node)
        {
            if (!_labels.Add(node)) throw new Exception("node already there?");
        }
    }
}
