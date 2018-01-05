using System;

namespace ILLink.ControlFlow
{
    public sealed partial class FlowGraph
    {
        public static class Enumerators
        {
            public struct BlockListImpl : Enumeration<BasicBlock, BlockID>.IImpl
            {
                BasicBlock Enumeration<BasicBlock, BlockID>.IImpl.Current(BlockID current, FlowGraph graph) => graph.Block(current);
                bool Enumeration<BasicBlock, BlockID>.IImpl.IsValid(BlockID id) => id != BlockID.Invalid;
                BlockID Enumeration<BasicBlock, BlockID>.IImpl.NextID(BlockID current, FlowGraph graph) => graph.BlockData(current).next;
            }
        }
    }
}
