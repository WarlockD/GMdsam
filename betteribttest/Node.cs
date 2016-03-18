using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Specialized;

namespace betteribttest
{
    public class NullNode : Node
    {
        public override void WriteTo(TextWriter r) { r.Write("nullNode"); }
    }
    public abstract class Expression
    {
        public abstract Instruction Code { get; }
    }

    public abstract class Node
    {
        public override string ToString()
        {
            StringWriter sr = new StringWriter();
            WriteTo(sr);
            return sr.ToString();
        }

        public bool isUnconditionalControlFlow()
        {
            ///return this instanceof Expression &&
            //     ((Expression)this).getCode().isUnconditionalControlFlow();
            return false;
        }
        public abstract void WriteTo(TextWriter r);
        void accumulateSelfAndChildrenRecursive<T>(List<T> list, Predicate<T> predicate, bool childrenFirst) where T : Node
        {
            T test = this as T;
            if (!childrenFirst) if (test != null && (predicate == null || predicate(test))) list.Add(test);
            foreach (var child in getChildren()) child.accumulateSelfAndChildrenRecursive<T>(list, predicate, childrenFirst);
            if (childrenFirst) if (test != null && (predicate == null || predicate(test))) list.Add(test);
        }

        public virtual List<Node> getChildren() { return new List<Node>(); } // empty list
        public List<Node> getSelfAndChildrenRecursive()
        {
            List<Node> results = new List<Node>();
            accumulateSelfAndChildrenRecursive(results, null, false);
            return results;
        }

        public List<Node> getSelfAndChildrenRecursive(Predicate<Node> predicate)
        {
            List<Node> results = new List<Node>();
            accumulateSelfAndChildrenRecursive(results, predicate, false);
            return results;
        }

        public List<T> getSelfAndChildrenRecursive<T>() where T : Node
        {
            List<T> results = new List<T>();
            accumulateSelfAndChildrenRecursive(results, null, false);
            return results;
        }
        public List<T> getSelfAndChildrenRecursive<T>(Predicate<T> predicate) where T : Node
        {
            List<T> results = new List<T>();
            accumulateSelfAndChildrenRecursive(results, null, false);
            return results;
        }
        public List<Node> getChildrenAndSelfRecursive()
        {
            List<Node> results = new List<Node>();
            accumulateSelfAndChildrenRecursive(results, null, true);
            return results;
        }

        public List<Node> getChildrenAndSelfRecursive(Predicate<Node> predicate)
        {
            List<Node> results = new List<Node>();
            accumulateSelfAndChildrenRecursive(results, predicate, true);
            return results;
        }

        public List<T> getChildrenAndSelfRecursive<T>() where T : Node
        {
            List<T> results = new List<T>();
            accumulateSelfAndChildrenRecursive(results, null, true);
            return results;
        }
        public List<T> getChildrenAndSelfRecursive<T>(Predicate<T> predicate) where T : Node
        {
            List<T> results = new List<T>();
            accumulateSelfAndChildrenRecursive(results, null, true);
            return results;
        }
    }
    
}