using System;

using Cil = Mono.Cecil.Cil;

namespace ILLink.ControlFlow
{
    using static System.Diagnostics.Debug;

    // An edge that is a component of exception dispatch
    public struct LeaveEdge : IEquatable<LeaveEdge>
    {
        // Backing storage will be committed to memory, so we can use
        // a nested struct for the backing edge data
        internal struct Descriptor
        {
            internal FlowEdge.Descriptor edgeData;
            internal BlockID leaveBlock;
        }
        internal static Descriptor NewDescriptor(EdgeKind kind) => new Descriptor() { edgeData = FlowEdge.NewDescriptor(kind) };

        // A LeaveEdge is a pair of an ID and a graph
        private EdgeID id;
        public EdgeID ID => id;

        private FlowGraph graph;

        // Internal ctor for use by FlowGraph
        internal LeaveEdge(EdgeID id, FlowGraph graph)
        {
            Assert(FlowGraph.IsLeaveEdgeID(id));
            this.id = id;
            this.graph = graph;
        }

        // Internal accessors for backing data
        internal ref Descriptor Data => ref graph.LeaveEdgeData(id);
        internal ref FlowEdge.Descriptor edgeData => ref Data.edgeData;

        // Public accessor for leave block/instruction
        public BasicBlock LeaveBlock => graph.Block(Data.leaveBlock);
        public Cil.Instruction LeaveInstruction => LeaveBlock.LastInstruction;

        // Allow "upcast" to FlowEdge
        public static implicit operator FlowEdge(LeaveEdge edge) => edge.graph.Edge(edge.id);
        private FlowEdge ThisEdge => this;

        // Shared with FlowEdge (repetition is a shame, but we don't have
        // struct inheritance...)
        public BasicBlock Predecessor => ThisEdge.Predecessor;
        public BasicBlock Successor => ThisEdge.Successor;
        public EdgeKind Kind => ThisEdge.Kind;

        // Boilerplate to handle equality/hashing
        public static bool operator ==(LeaveEdge left, LeaveEdge right) => left.id == right.id;
        public static bool operator !=(LeaveEdge left, LeaveEdge right) => !(left == right);
        bool IEquatable<LeaveEdge>.Equals(LeaveEdge other) => this == other;
        public override bool Equals(object obj)
        {
            if (obj is FlowEdge that)
            {
                return this == that;
            }
            if (obj is LeaveEdge edge)
            {
                return this == edge;
            }

            return false;
        }
        public override int GetHashCode() => id.GetHashCode();
    }
}
