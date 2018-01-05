using System;

namespace ILLink.ControlFlow
{
    public partial struct EHRegion : IEquatable<EHRegion>
    {
        public static class Enumerators
        {
            public struct ChildListImpl : Enumeration<EHRegion, EHRegionID>.IImpl
            {
                EHRegion Enumeration<EHRegion, EHRegionID>.IImpl.Current(EHRegionID current, FlowGraph graph) => graph.EHRegion(current);
                bool Enumeration<EHRegion, EHRegionID>.IImpl.IsValid(EHRegionID id) => (id != EHRegionID.Invalid);
                EHRegionID Enumeration<EHRegion, EHRegionID>.IImpl.NextID(EHRegionID current, FlowGraph graph) => graph.EHRegionData(current).nextLexicalSibling;
            }

            public struct InclusiveBlockListImpl : Enumeration<BasicBlock, BlockID, EHRegionID>.IImpl
            {
                BasicBlock Enumeration<BasicBlock, BlockID, EHRegionID>.IImpl.Current(BlockID current, EHRegionID state, FlowGraph graph) => graph.Block(current);

                bool Enumeration<BasicBlock, BlockID, EHRegionID>.IImpl.IsValid(BlockID elementId, EHRegionID state) => (state != EHRegionID.Invalid);

                BlockID Enumeration<BasicBlock, BlockID, EHRegionID>.IImpl.NextID(BlockID current, ref EHRegionID state, FlowGraph graph)
                {
                    if (current == BlockID.Invalid)
                    {
                        // We're still at the "before the first element" position.
                        // Move to the first block in the region.
                        current = graph.EHRegionData(state).firstBlock;
                    }
                    else
                    {
                        // Advance to the next lexical block.
                        current = graph.BlockData(current).next;
                    }

                    if ((current == BlockID.Invalid)
                        || (graph.BlockData(current).firstInstruction == graph.EHRegionData(state).EndInstruction))

                    {
                        // We've reached the end of the region.
                        state = EHRegionID.Invalid;
                        current = BlockID.Invalid;
                    }

                    return current;
                }
            }

            public struct ExclusiveBlockListImpl : Enumeration<BasicBlock, BlockID, EHRegionID>.IImpl
            {
                BasicBlock Enumeration<BasicBlock, BlockID, EHRegionID>.IImpl.Current(BlockID current, EHRegionID state, FlowGraph graph) => graph.Block(current);

                bool Enumeration<BasicBlock, BlockID, EHRegionID>.IImpl.IsValid(BlockID elementId, EHRegionID state) => (state != EHRegionID.Invalid);

                BlockID Enumeration<BasicBlock, BlockID, EHRegionID>.IImpl.NextID(BlockID current, ref EHRegionID state, FlowGraph graph)
                {
                    if (current == BlockID.Invalid)
                    {
                        // We're still at the "before the first element" position.
                        // Move to the first block in the region.
                        current = graph.EHRegionData(state).firstBlock;
                    }
                    else
                    {
                        // Advance to the next lexical block.
                        current = graph.BlockData(current).next;
                    }

                    for (; ; current = graph.BlockData(current).next)
                    {
                        if ((current == BlockID.Invalid)
                            || (graph.BlockData(current).firstInstruction == graph.EHRegionData(state).EndInstruction))

                        {
                            // We've reached the end of the region.
                            state = EHRegionID.Invalid;
                            current = BlockID.Invalid;
                            break;
                        }

                        if (graph.BlockData(current).lexicalRegion == state)
                        {
                            // This block belongs to this region and not a child.
                            break;
                        }
                    }

                    return current;
                }
            }
        }
    }
}
