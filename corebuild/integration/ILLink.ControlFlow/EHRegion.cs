using System;

using Cil = Mono.Cecil.Cil;

namespace ILLink.ControlFlow
{
    using static System.Diagnostics.Debug;

    public enum EHRegionKind
    {
        Try,
        Catch,
        Finally,
        Fault,
        FilterFilter,
        FilterHandler
    }

    public enum ExceptionPass
    {
        FirstPass, // Find handler that will handle
        SecondPass // Unwind stack, execute finally/fault handlers
    }

    public enum EHRegionID { Invalid = -1 }
    public partial struct EHRegion : IEquatable<EHRegion>
    {
        // Nesting/ordering relationship between regions
        public enum LexicalRelation
        {
            Before,
            After,
            Inside,
            Outside,
            Same
        }

        internal struct Descriptor
        {
            internal Cil.ExceptionHandler handler;
            internal EHRegionID lexicalParent;         // Lexically-innermost region this one is lexically enclosed in
            internal EHRegionID firstLexicalChild;     // Reverse of lexical parent (part 1 of 2)
            internal EHRegionID nextLexicalSibling;    // Reverse of lexical parent (part 2 of 2)
            internal EHRegionID nextSameHandlerRegion; // Circular list of regions for the same ExceptionHandler, try -> handler -> filter for filters
            internal BlockID    firstBlock;            // Lexically first block contained in this region

            internal EHRegionKind kind;

            // Convenience accessors for appropriate start/end instructions
            internal Cil.Instruction StartInstruction
            {
                get
                {
                    if (kind == EHRegionKind.Try)
                    {
                        return handler.TryStart;
                    }
                    if (kind == EHRegionKind.FilterFilter)
                    {
                        return handler.FilterStart;
                    }
                    return handler.HandlerStart;
                }
            }
            internal Cil.Instruction EndInstruction
            {
                get
                {
                    if (kind == EHRegionKind.Try)
                    {
                        return handler.TryEnd;
                    }
                    if (kind == EHRegionKind.FilterFilter)
                    {
                        return handler.HandlerStart;
                    }
                    return handler.HandlerEnd;
                }
            }

            // Lexical relation comparison (used during FlowGraph construction).
            internal LexicalRelation CompareLexicalExtent(ref Descriptor that)
            {
                if (this.StartInstruction == that.StartInstruction)
                {
                    if (this.EndInstruction == that.EndInstruction)
                    {
                        return LexicalRelation.Same;
                    }
                    if (this.EndInstruction == null)
                    {
                        return LexicalRelation.Outside;
                    }
                    if (that.EndInstruction == null)
                    {
                        return LexicalRelation.Inside;
                    }
                    if (this.EndInstruction.Offset < that.EndInstruction.Offset)
                    {
                        return LexicalRelation.Inside;
                    }
                    return LexicalRelation.Outside;
                }

                if (this.StartInstruction.Offset < that.StartInstruction.Offset)
                {
                    if (this.EndInstruction == null)
                    {
                        return LexicalRelation.Outside;
                    }

                    if (this.EndInstruction.Offset <= that.StartInstruction.Offset)
                    {
                        return LexicalRelation.Before;
                    }

                    Assert(this.EndInstruction.Offset >= that.EndInstruction.Offset);
                    return LexicalRelation.Outside;
                }

                if (that.EndInstruction == null)
                {
                    return LexicalRelation.Inside;
                }

                if (that.EndInstruction.Offset <= this.StartInstruction.Offset)
                {
                    return LexicalRelation.After;
                }

                Assert(that.EndInstruction.Offset >= this.EndInstruction.Offset);
                return LexicalRelation.Inside;
            }
        }
        internal static Descriptor NewDescriptor(EHRegionKind kind) =>
            new Descriptor()
            {
                kind = kind,
                lexicalParent = EHRegionID.Invalid,
                firstLexicalChild = EHRegionID.Invalid,
                nextLexicalSibling = EHRegionID.Invalid,
                nextSameHandlerRegion = EHRegionID.Invalid,
                firstBlock = BlockID.Invalid
            };

        private EHRegionID id;
        public EHRegionID ID => id;
        private FlowGraph graph;

        internal EHRegion(EHRegionID id, FlowGraph graph)
        {
            this.id = id;
            this.graph = graph;
        }

        internal ref Descriptor Data => ref graph.EHRegionData(id);

        // Public access to region kind
        public EHRegionKind Kind => Data.kind;

        // Public access to associated Cecil ExceptionHandler
        public Cil.ExceptionHandler Handler => Data.handler;

        // Public access to lexical relationships
        public Cil.Instruction StartInstruction => Data.StartInstruction;
        public Cil.Instruction EndInstruction => Data.EndInstruction;
        public LexicalRelation CompareLexicalExtent(EHRegion that) =>
            Data.CompareLexicalExtent(ref that.Data);
        public BasicBlock FirstBlock => graph.Block(Data.firstBlock);

        // Accessors for related regions
        public bool TryGetLexicalParent(out EHRegion parent) => graph.TryGetEHRegion(Data.lexicalParent, out parent);
        // Given anything but a try region, get the try region for the same `ExceptionHandler`
        public bool TryGetTryRegion(out EHRegion tryRegion)
        {
            EHRegionID tryID;
            switch (Kind)
            {
                case EHRegionKind.Try:
                    tryID = EHRegionID.Invalid;
                    break;
                case EHRegionKind.FilterHandler:
                    tryID = graph.EHRegionData(Data.nextSameHandlerRegion).nextSameHandlerRegion;
                    break;
                default:
                    tryID = Data.nextSameHandlerRegion;
                    break;
            }
            return graph.TryGetEHRegion(tryID, out tryRegion);
        }
        // Given anything but a handler region, get the handler region for the same `ExceptionHandler`
        // Note that this is *not* always the region where exceptions raised here are handled -- see
        // TryGetThrowSuccessor for that.
        public bool TryGetHandlerRegion(out EHRegion handlerRegion)
        {
            EHRegionID handlerID;
            if (Kind == EHRegionKind.Try)
            {
                handlerID = Data.nextSameHandlerRegion;
            }
            else if (Kind == EHRegionKind.FilterFilter)
            {
                handlerID = graph.EHRegionData(Data.nextSameHandlerRegion).nextSameHandlerRegion;
            }
            else
            {
                handlerID = EHRegionID.Invalid;
            }
            return graph.TryGetEHRegion(handlerID, out handlerRegion);
        }
        // Given a Try or FilterHandler region for a filter `ExceptionHandler`, get the FilterFilter region
        public bool TryGetFilterRegion(out EHRegion filterRegion)
        {
            EHRegionID filterID;
            if (Kind == EHRegionKind.Try)
            {
                var nextID = Data.nextSameHandlerRegion;
                ref var nextRegion = ref graph.EHRegionData(nextID);
                if (nextRegion.kind == EHRegionKind.FilterHandler)
                {
                    filterID = nextRegion.nextSameHandlerRegion;
                }
                else
                {
                    filterID = EHRegionID.Invalid;
                }
            }
            else if (Kind == EHRegionKind.FilterHandler)
            {
                filterID = Data.nextSameHandlerRegion;
            }
            else
            {
                filterID = EHRegionID.Invalid;
            }
            return graph.TryGetEHRegion(filterID, out filterRegion);
        }

        // Where an exception raised by code in this region gets filtered/handled
        public bool TryGetThrowSuccessor(out EHRegion successor)
        {
            EHRegion region = this;

            do
            {
                if (region.Kind == EHRegionKind.Try)
                {
                    EHRegionID successorID;

                    var nextRegionID = Data.nextSameHandlerRegion;
                    ref var nextRegion = ref graph.EHRegionData(nextRegionID);
                    if (nextRegion.kind == EHRegionKind.FilterHandler)
                    {
                        // Exceptions in a try are tested by the filter first.
                        successorID = nextRegion.nextSameHandlerRegion;
                        Assert(graph.EHRegionData(successorID).kind == EHRegionKind.FilterFilter);
                    }
                    else
                    {
                        // Exceptions in other kinds of try are processed in their handler.
                        successorID = nextRegionID;
                    }
                    successor = graph.EHRegion(successorID);
                    return true;
                }

                if (region.Kind == EHRegionKind.FilterFilter)
                {
                    // When a new exception is raised during execution of a filter,
                    // the new exception is swallowed by the runtime, and dispatch
                    // of the original exception is resumed.
                    return region.TryGetContinueDispatchSuccessor(ExceptionPass.FirstPass, out successor);
                }

                // Exceptions thrown from a handler have the same throw
                // successor as their lexical parent.
            } while (region.TryGetLexicalParent(out region));

            // A top-level handler region's exceptions propagate to
            // the caller, which is not a region in this method.
            successor = region;
            return false;
        }

        // For filters/handlers, where dispatch continues (on no match for catch/filter, on completion for finally/fault)
        public bool TryGetContinueDispatchSuccessor(ExceptionPass pass, out EHRegion successor)
        {
            if (Kind == EHRegionKind.FilterHandler)
            {
                // Filter handlers aren't part of the dispatch search and
                // don't resume dispatch on exit.
                successor = graph.EHRegion(EHRegionID.Invalid);
                return false;
            }

            if (!TryGetTryRegion(out EHRegion tryRegion))
            {
                // This must be a try region, which isn't part of the
                // dispatch search.
                Assert(Kind == EHRegionKind.Try);
                successor = tryRegion;
                return false;
            }

            // Filters only participate in pass 1, and their handlers
            // only participate in pass 2.
            var wrongKind =
                (pass == ExceptionPass.FirstPass ? EHRegionKind.FilterHandler : EHRegionKind.FilterFilter);

            if (Kind == wrongKind)
            {
                successor = graph.EHRegion(EHRegionID.Invalid);
                return false;
            }

            // Dispatch will continue at the throw successor of the next
            // outer try containing this try, or its handler.
            while (tryRegion.TryGetLexicalParent(out tryRegion))
            {
                if (tryRegion.Kind != EHRegionKind.Try)
                {
                    // Keep walking ancestors until we find a try.
                    continue;
                }

                if (!tryRegion.TryGetThrowSuccessor(out successor))
                {
                    // Dispatch will continue out to the caller.
                    return false;
                }

                if ((pass == ExceptionPass.SecondPass)
                    && (successor.Kind == EHRegionKind.FilterFilter))
                {
                    // Second-pass dispatch will continue at the handler,
                    // not the filter.
                    successor.TryGetHandlerRegion(out successor);
                }
                return true;
            }

            // There is no outer try, so exception propagation will
            // continue to the caller.
            successor = tryRegion;
            return false;
        }

        // Iterator for visiting lexical children
        public Enumeration<EHRegion, EHRegionID>.Enumerable<Enumerators.ChildListImpl> LexicalChildren =>
            new Enumeration<EHRegion, EHRegionID>.Enumerable<Enumerators.ChildListImpl>(Data.firstLexicalChild, graph);
        // Iterators for visiting constituent blocks
        public Enumeration<BasicBlock, BlockID, EHRegionID>.Enumerable<Enumerators.InclusiveBlockListImpl> InclusiveBlocks =>
            new Enumeration<BasicBlock, BlockID, EHRegionID>.Enumerable<Enumerators.InclusiveBlockListImpl>(BlockID.Invalid, id, graph);
        public Enumeration<BasicBlock, BlockID, EHRegionID>.Enumerable<Enumerators.ExclusiveBlockListImpl> ExclusiveBlocks =>
            new Enumeration<BasicBlock, BlockID, EHRegionID>.Enumerable<Enumerators.ExclusiveBlockListImpl>(BlockID.Invalid, id, graph);

        // Boilerplate to handle equality/hashing
        public static bool operator ==(EHRegion left, EHRegion right) => left.id == right.id;
        public static bool operator !=(EHRegion left, EHRegion right) => !(left == right);
        bool IEquatable<EHRegion>.Equals(EHRegion other) => this == other;
        public override bool Equals(object obj)
        {
            if (obj is EHRegion that)
            {
                return this == that;
            }

            return false;
        }
        public override int GetHashCode() => id.GetHashCode();
    }
}
