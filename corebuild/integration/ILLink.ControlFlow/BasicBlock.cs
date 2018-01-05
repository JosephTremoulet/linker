using System;

using Cil = Mono.Cecil.Cil;

namespace ILLink.ControlFlow
{
    using static System.Diagnostics.Debug;

    public enum BlockID { Invalid = 0 }

    public partial struct BasicBlock : IEquatable<BasicBlock>
    {
        // Internal backing data for a block, ID-centric
        internal struct Descriptor
        {
            internal EdgeID firstPredecessor;
            internal EdgeID firstSuccessor;
            internal Cil.Instruction firstInstruction;
            internal Cil.Instruction lastInstruction;
            internal BlockID next;
            internal BlockID previous;
            internal EHRegionID lexicalRegion;
        }
        internal static Descriptor NewDescriptor() => new Descriptor() { firstPredecessor = EdgeID.Invalid, firstSuccessor = EdgeID.Invalid, next = BlockID.Invalid, previous = BlockID.Invalid, lexicalRegion = EHRegionID.Invalid };

        // A BasicBlock is a pair of an ID and a graph.
        private BlockID id;
        public BlockID ID => id;

        private FlowGraph graph;

        // Internal ctor for use by FlowGraph
        internal BasicBlock(BlockID id, FlowGraph graph)
        {
            this.id = id;
            this.graph = graph;
        }

        // Internal accessor to backing data
        internal ref Descriptor Data => ref graph.BlockData(id);

        // Public access to Instruction range
        public Cil.Instruction FirstInstruction => Data.firstInstruction;
        public Cil.Instruction LastInstruction => Data.lastInstruction;

        public InstructionRange Instructions => new InstructionRange(FirstInstruction, LastInstruction.Next);

        // Public access to lexical neighbors
        public bool TryGetNextLexicalBlock(out BasicBlock next) => graph.TryGetBlock(Data.next, out next);
        public bool TryGetPreviousLexicalBlock(out BasicBlock previous) => graph.TryGetBlock(Data.previous, out previous);

        // Public access to EH Region info
        public bool TryGetLexicalRegion(out EHRegion region) => graph.TryGetEHRegion(Data.lexicalRegion, out region);

        // Support "pattern-matching" to handler blocks
        public bool Is(out HandlerBlock handler)
        {
            if (FlowGraph.IsHandlerBlockID(id))
            {
                handler = graph.HandlerBlock(id);
                return true;
            }

            handler = default(HandlerBlock);
            return false;
        }

        // Public accessor for iterating a block's predecessor edges
        public Enumeration<FlowEdge, EdgeID>.Enumerable<Enumerators.PredecessorListImpl> PredecessorEdges =>
            new Enumeration<FlowEdge, EdgeID>.Enumerable<Enumerators.PredecessorListImpl>(Data.firstPredecessor, graph);

        // Public accessor for iterating a block's successor edges
        public Enumeration<FlowEdge, EdgeID>.Enumerable<Enumerators.SuccessorListImpl> SuccessorEdges =>
            new Enumeration<FlowEdge, EdgeID>.Enumerable<Enumerators.SuccessorListImpl>(Data.firstSuccessor, graph);

        // Boilerplate to handle equality/hashing
        public static bool operator ==(BasicBlock left, BasicBlock right) => left.id == right.id;
        public static bool operator !=(BasicBlock left, BasicBlock right) => !(left == right);
        bool IEquatable<BasicBlock>.Equals(BasicBlock other) => this == other;
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
