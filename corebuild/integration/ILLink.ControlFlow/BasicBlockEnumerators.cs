using System;

namespace ILLink.ControlFlow
{
    public partial struct BasicBlock : IEquatable<BasicBlock>
    {
        public static class Enumerators
        {
            public struct PredecessorListImpl : Enumeration<FlowEdge, EdgeID>.IImpl
            {
                FlowEdge Enumeration<FlowEdge, EdgeID>.IImpl.Current(EdgeID current, FlowGraph graph) => graph.Edge(current);
                bool Enumeration<FlowEdge, EdgeID>.IImpl.IsValid(EdgeID id) => id != EdgeID.Invalid;
                EdgeID Enumeration<FlowEdge, EdgeID>.IImpl.NextID(EdgeID current, FlowGraph graph) => graph.EdgeData(current).nextPredecessorEdgeID;
            }

            public struct SuccessorListImpl : Enumeration<FlowEdge, EdgeID>.IImpl
            {
                FlowEdge Enumeration<FlowEdge, EdgeID>.IImpl.Current(EdgeID current, FlowGraph graph) => graph.Edge(current);
                bool Enumeration<FlowEdge, EdgeID>.IImpl.IsValid(EdgeID id) => id != EdgeID.Invalid;
                EdgeID Enumeration<FlowEdge, EdgeID>.IImpl.NextID(EdgeID current, FlowGraph graph) => graph.EdgeData(current).nextSuccessorEdgeID;
            }
        }
    }
}
