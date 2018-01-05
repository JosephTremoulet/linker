using System;

using Cil = Mono.Cecil.Cil;

namespace ILLink.ControlFlow
{
    using static System.Diagnostics.Debug;

    // A block that starts a handler region
    public struct HandlerBlock : IEquatable<HandlerBlock>
    {
        // Backing storage will be committed to memory, so we can use
        // a nested struct for the backing block data
        internal struct Descriptor
        {
            internal BasicBlock.Descriptor blockData;
            // REVIEW: this will always be the same as the lexicalRegion in the BasicBlock.
            //   Change this to be state on BasicBlock instead of subtype?
            internal EHRegionID handlerRegion;  // May be FilterFilter, or any of the handler types.  Not try.
        }
        internal static Descriptor NewDescriptor() => new Descriptor() { blockData = BasicBlock.NewDescriptor(), handlerRegion = EHRegionID.Invalid };

        // A HandlerBlock is a pair of an ID and a graph
        private BlockID id;
        public BlockID ID => id;

        private FlowGraph graph;

        // Internal ctor for use by FlowGraph
        internal HandlerBlock(BlockID id, FlowGraph graph)
        {
            Assert(FlowGraph.IsHandlerBlockID(id));
            this.id = id;
            this.graph = graph;
        }

        // Internal accessors for backing data
        internal ref Descriptor Data => ref graph.HandlerBlockData(id);
        internal ref BasicBlock.Descriptor BlockData => ref Data.blockData;

        // Public accessor for started region
        public EHRegion HandlerRegion => graph.EHRegion(Data.handlerRegion);

        // Allow "upcast" to BasicBlock
        public static implicit operator BasicBlock(HandlerBlock handler) => handler.graph.Block(handler.id);
        private BasicBlock ThisBlock => this;

        // Shared with BasicBlock (repetition is a shame, but we don't have
        // struct inheritance...)
        public Cil.Instruction FirstInstruction => ThisBlock.FirstInstruction;
        public Cil.Instruction LastInstruction => ThisBlock.LastInstruction;
        public bool TryGetNextLexicalBlock(out BasicBlock next) => ThisBlock.TryGetNextLexicalBlock(out next);
        public bool TryGetPreviousLexicalBlock(out BasicBlock previous) => ThisBlock.TryGetPreviousLexicalBlock(out previous);
        public bool TryGetLexicalRegion(out EHRegion region) => ThisBlock.TryGetLexicalRegion(out region);
        public Enumeration<FlowEdge, EdgeID>.Enumerable<BasicBlock.Enumerators.PredecessorListImpl> PredecessorEdges =>
            ThisBlock.PredecessorEdges;
        public Enumeration<FlowEdge, EdgeID>.Enumerable<BasicBlock.Enumerators.SuccessorListImpl> SuccessorEdges =>
            ThisBlock.SuccessorEdges;

        // Boilerplate to handle equality/hashing
        public static bool operator ==(HandlerBlock left, HandlerBlock right) => left.id == right.id;
        public static bool operator !=(HandlerBlock left, HandlerBlock right) => !(left == right);
        bool IEquatable<HandlerBlock>.Equals(HandlerBlock other) => this == other;
        public override bool Equals(object obj)
        {
            if (obj is BasicBlock that)
            {
                return this == that;
            }
            if (obj is HandlerBlock handler)
            {
                return this == handler;
            }

            return false;
        }
        public override int GetHashCode() => id.GetHashCode();
    }
}
