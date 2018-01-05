using System;

using Cil = Mono.Cecil.Cil;

namespace ILLink.ControlFlow
{
    using static System.Diagnostics.Debug;

    [Flags]
    public enum ExceptionEdgeSources
    {
        None = 0,
        Instruction = 1,  // Exception is raised by some instruction in this block
        Head = 2,         // Exception dispatch may "drive by" in pass 1 or pass 2
        Tail = 4,         // Exception dispatch may continue in pass 1 or pass 2
    }

    [Flags]
    public enum ExceptionEdgeKinds
    {
        // Empty value for flags enum
        None = 0,

        // Exception raised by instruction in block
        RaiseException = 1,

        // First pass flow from head across a catch to the next
        // outer handler (happens when the exception's type is
        // not the type caught by this catch)
        CatchWrongType = 2,

        // Second pass flow from head across a catch to the next
        // outer handler (happens when this catch lies outside
        // a finally/fault and inside the ultimate handler)
        BypassCatch = 4,

        // First pass flow from head across a fault to the next
        // outer handler (this is always how fault handlers
        // behave in the first pass)
        BypassFault = 8,

        // Second pass flow from tail endfault/endfinally to the
        // next outer handler
        EndFault = 16,

        // First pass flow from head across a finally to the next
        // outer handler (this is always how finally handlers
        // behave in the first pass)
        BypassFinally = 32,

        // Second pass flow from tail endfinally to the next
        // outer handler
        EndFinally = 64,

        // Transition from tail of a filter which matches to
        // its handler block (possibly then across that via
        // a FilterRewind edge)
        FilterMatch = 128,

        // First pass flow from tail of a filter which doesn't
        // match to the next outer handler
        FilterFail = 256,

        // Transition flow from head across a FilterHandler, whose
        // filter didn't match, to an inner handler where the
        // second pass will begin (possibly transitively across
        // intermediate FilterHandlers)
        FilterRewind = 512,

        // Second pass flow across head of a FilterHandler to
        // the next outer handler (happens when the filter lies
        // outside a finally/fault and inside the ultimate
        // handler)
        BypassFilterHandler = 1024,

        // These exception edge kinds flow from the head of the
        // block, before any of its instructions execute
        HeadSourceKinds =
            CatchWrongType | BypassCatch | BypassFault | BypassFinally | FilterRewind | BypassFilterHandler,

        // These exception edge kinds flow from the tail of the
        // block, after all of its instructions have executed
        TailSourceKinds =
            EndFault | EndFinally | FilterMatch | FilterFail,

        // These exception edge kinds flow from some instruction
        // somewhere in the block
        InstructionSourceKinds =
            RaiseException,

        // These exception edge kinds are part of the first pass
        // of exception dispatch
        FirstPassKinds =
            CatchWrongType | BypassFault | BypassFinally | FilterFail,

        // These exception kinds are part of the second pass
        // of exception dispatch
        SecondPassKinds =
            BypassCatch | EndFault | EndFinally | BypassFilterHandler,

        // These exception kinds are part of the transition from
        // non-exception to first pass, or from first pass to
        // second pass
        PassTransitionKinds =
            RaiseException | FilterMatch | FilterRewind
    }

    // An edge that is a component of exception dispatch
    public struct ExceptionEdge : IEquatable<ExceptionEdge>
    {
        // Backing storage will be committed to memory, so we can use
        // a nested struct for the backing edge data
        internal struct Descriptor
        {
            internal FlowEdge.Descriptor edgeData;
            internal ExceptionEdgeKinds kinds;
        }
        internal static Descriptor NewDescriptor() => new Descriptor() { edgeData = FlowEdge.NewDescriptor(EdgeKind.Exception) };

        // A ExceptionEdge is a pair of an ID and a graph
        private EdgeID id;
        public EdgeID ID => id;

        private FlowGraph graph;

        // Internal ctor for use by FlowGraph
        internal ExceptionEdge(EdgeID id, FlowGraph graph)
        {
            Assert(FlowGraph.IsExceptionEdgeID(id));
            this.id = id;
            this.graph = graph;
        }

        // Internal accessors for backing data
        internal ref Descriptor Data => ref graph.ExceptionEdgeData(id);
        internal ref FlowEdge.Descriptor edgeData => ref Data.edgeData;

        // Public accessors for exception source data
        public ExceptionEdgeKinds Kinds => Data.kinds;
        public ExceptionEdgeSources Sources
        {
            get
            {
                var sources = ExceptionEdgeSources.None;

                if ((Kinds & (ExceptionEdgeKinds.InstructionSourceKinds)) != 0)
                {
                    sources |= ExceptionEdgeSources.Instruction;
                }
                if ((Kinds & (ExceptionEdgeKinds.HeadSourceKinds)) != 0)
                {
                    sources |= ExceptionEdgeSources.Head;
                }
                if ((Kinds & (ExceptionEdgeKinds.TailSourceKinds)) != 0)
                {
                    sources |= ExceptionEdgeSources.Tail;
                }

                return sources;
            }
        }

        // Allow "upcast" to FlowEdge
        public static implicit operator FlowEdge(ExceptionEdge edge) => edge.graph.Edge(edge.id);
        private FlowEdge ThisEdge => this;

        // Shared with FlowEdge (repetition is a shame, but we don't have
        // struct inheritance...)
        public BasicBlock Predecessor => ThisEdge.Predecessor;
        public BasicBlock Successor => ThisEdge.Successor;
        public EdgeKind Kind => ThisEdge.Kind;

        // Boilerplate to handle equality/hashing
        public static bool operator ==(ExceptionEdge left, ExceptionEdge right) => left.id == right.id;
        public static bool operator !=(ExceptionEdge left, ExceptionEdge right) => !(left == right);
        bool IEquatable<ExceptionEdge>.Equals(ExceptionEdge other) => this == other;
        public override bool Equals(object obj)
        {
            if (obj is FlowEdge that)
            {
                return this == that;
            }
            if (obj is ExceptionEdge edge)
            {
                return this == edge;
            }

            return false;
        }
        public override int GetHashCode() => id.GetHashCode();
    }
}
