using System;

using Cil = Mono.Cecil.Cil;

namespace ILLink.ControlFlow
{
    using static System.Diagnostics.Debug;

    public enum EdgeKind
    {
        Goto,               // Unconditional branch
        FallThrough,        // Unconditional (barring EH) fall-through
        TakenConditional,   // Taken edge from conditional branch
        UntakenConditional, // Not-taken (fall-through) edge from conditional branch
        SwitchCase,         // Case arm of a switch instruction
        SwitchDefault,      // Fall-through from a switch instruction
        Exception,          // Component of exception dispatch
        BeginLeave,         // Flow from leave to innermost enclosing finally
        ContinueLeave,      // Flow from endfinally to outer finally (for a leave)
        FinishLeave,        // Flow from endfinally to leave target
    }

    public enum EdgeID { Invalid = 0 }
    public struct FlowEdge : IEquatable<FlowEdge>
    {
        internal struct Descriptor
        {
            internal BlockID predecessor;
            internal BlockID successor;
            internal EdgeID nextPredecessorEdgeID;
            internal EdgeID nextSuccessorEdgeID;
            internal EdgeKind kind;
        }
        internal static Descriptor NewDescriptor(EdgeKind kind) =>
            new Descriptor { kind = kind, predecessor = BlockID.Invalid, successor = BlockID.Invalid, nextPredecessorEdgeID = EdgeID.Invalid, nextSuccessorEdgeID = EdgeID.Invalid };

        private EdgeID id;
        public EdgeID ID => id;
        private FlowGraph graph;

        internal FlowEdge(EdgeID id, FlowGraph graph)
        {
            this.id = id;
            this.graph = graph;
        }

        internal ref Descriptor Data => ref graph.EdgeData(id);
        public BasicBlock Predecessor => graph.Block(Data.predecessor);
        public BasicBlock Successor => graph.Block(Data.successor);
        public EdgeKind Kind => Data.kind;

        // Support "pattern-matching" to exception/leave edges
        public bool Is(out ExceptionEdge edge)
        {
            if (FlowGraph.IsExceptionEdgeID(id))
            {
                edge = graph.ExceptionEdge(id);
                return true;
            }

            edge = default(ExceptionEdge);
            return false;
        }
        public bool Is(out LeaveEdge edge)
        {
            if (FlowGraph.IsLeaveEdgeID(id))
            {
                edge = graph.LeaveEdge(id);
                return true;
            }

            edge = default(LeaveEdge);
            return false;
        }

        // Boilerplate to handle equality/hashing
        public static bool operator ==(FlowEdge left, FlowEdge right) => left.id == right.id;
        public static bool operator !=(FlowEdge left, FlowEdge right) => !(left == right);
        bool IEquatable<FlowEdge>.Equals(FlowEdge other) => this == other;
        public override bool Equals(object obj)
        {
            switch (obj)
            {
                case FlowEdge that:
                    return this == that;
                case ExceptionEdge that:
                    return this == that;
                case LeaveEdge that:
                    return this == that;
                default:
                    return false;
            }
        }
        public override int GetHashCode() => id.GetHashCode();
    }
}
