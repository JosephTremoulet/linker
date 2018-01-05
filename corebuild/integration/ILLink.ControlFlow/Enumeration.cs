using System;
using System.Collections;
using System.Collections.Generic;

namespace ILLink.ControlFlow
{
    public static class Enumeration<TElement, TID>
    {
        public interface IImpl
        {
            TID NextID(TID current, FlowGraph graph);
            TElement Current(TID current, FlowGraph graph);
            bool IsValid(TID id);
        }

        public struct Enumerator<TImpl> : IEnumerator<TElement>, IEnumerator
        where TImpl : struct, IImpl
        {
            private FlowGraph graph;
            private TID currentID;
            private bool started;

            internal Enumerator(TID initialID, FlowGraph graph)
            {
                this.currentID = initialID;
                this.graph = graph;
                this.started = false;
            }

            public bool MoveNext()
            {
                if (!started)
                {
                    started = true;
                    return true;
                }
                var impl = default(TImpl);
                if (!impl.IsValid(currentID))
                {
                    return false;
                }
                currentID = impl.NextID(currentID, graph);
                return impl.IsValid(currentID);
            }

            public TElement Current => default(TImpl).Current(currentID, graph);

            object IEnumerator.Current => Current;
            void IEnumerator.Reset() => throw new NotImplementedException();
            void IDisposable.Dispose() { }
        }

        public struct Enumerable<TImpl> : IEnumerable<TElement>, IEnumerable
            where TImpl : struct, IImpl
        {
            private FlowGraph graph;
            private TID initialID;

            internal Enumerable(TID initialID, FlowGraph graph)
            {
                this.initialID = initialID;
                this.graph = graph;
            }

            public Enumerator<TImpl> GetEnumerator() => new Enumerator<TImpl>(initialID, graph);

            IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }

    public static class Enumeration<TElement, TElementID, TState>
    {
        public interface IImpl
        {
            TElementID NextID(TElementID current, ref TState state, FlowGraph graph);
            TElement Current(TElementID current, TState state, FlowGraph graph);
            bool IsValid(TElementID elementId, TState state);
        }

        public struct Enumerator<TImpl> : IEnumerator<TElement>, IEnumerator
        where TImpl : struct, IImpl
        {
            private FlowGraph graph;
            private TElementID currentID;
            private TState state;

            internal Enumerator(TElementID initialID, TState state, FlowGraph graph)
            {
                this.currentID = initialID;
                this.graph = graph;
                this.state = state;
            }

            public bool MoveNext()
            {
                var impl = default(TImpl);
                if (!impl.IsValid(currentID, state))
                {
                    // After end of collection.
                    return false;
                }
                currentID = impl.NextID(currentID, ref state, graph);
                return impl.IsValid(currentID, state);
            }

            public TElement Current => default(TImpl).Current(currentID, state, graph);

            object IEnumerator.Current => Current;
            void IEnumerator.Reset() => throw new NotImplementedException();
            void IDisposable.Dispose() { }
        }

        public struct Enumerable<TImpl> : IEnumerable<TElement>, IEnumerable
            where TImpl : struct, IImpl
        {
            private TElementID initialID;
            private TState initialState;
            private FlowGraph graph;

            internal Enumerable(TElementID initialID, TState initialState, FlowGraph graph)
            {
                this.initialID = initialID;
                this.initialState = initialState;
                this.graph = graph;
            }

            public Enumerator<TImpl> GetEnumerator() => new Enumerator<TImpl>(initialID, initialState, graph);

            IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
