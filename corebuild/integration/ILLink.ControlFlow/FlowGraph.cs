using System.Collections.Generic;

using Mono.Collections.Generic;

using Cil = Mono.Cecil.Cil;

namespace ILLink.ControlFlow
{
    using ILLink.ControlFlow.Collections;
    using ILLink.ControlFlow.CecilExtensions;
    using static System.Diagnostics.Debug;
    using System;

    public sealed partial class FlowGraph
    {
        // Backing data for regular blocks
        private RefList<BasicBlock.Descriptor> blocks;

        // Backing data for handler blocks
        private RefList<HandlerBlock.Descriptor> handlerBlocks;

        // Backing data for edges
        private RefList<FlowEdge.Descriptor> edges;

        // Backing data for exception edges
        private RefList<ExceptionEdge.Descriptor> exceptionEdges;

        // Backing data for leave edges
        private RefList<LeaveEdge.Descriptor> leaveEdges;

        // Backing data for EH regions
        private RefList<EHRegion.Descriptor> ehRegions;

        // Head of lexical block list
        private BlockID firstBlock = BlockID.Invalid;

        // Root of first region tree in forest (sibling is next root)
        private EHRegionID firstRegion = EHRegionID.Invalid;

        [System.Diagnostics.Conditional("DEBUG")]
        internal void CheckRefs()
        {
            blocks.CheckOldData();
            handlerBlocks.CheckOldData();
            edges.CheckOldData();
            exceptionEdges.CheckOldData();
            leaveEdges.CheckOldData();
            ehRegions.CheckOldData();
        }

        // Block data accessors; use positive IDs for regular blocks, negative for handler blocks
        internal static bool IsHandlerBlockID(BlockID id) => id < 0;
        internal ref HandlerBlock.Descriptor HandlerBlockData(BlockID id) => ref handlerBlocks[~(int)id];
        internal ref BasicBlock.Descriptor BlockData(BlockID id)
        {
            if (IsHandlerBlockID(id))
            {
                return ref HandlerBlockData(id).blockData;
            }

            return ref blocks[(int)id - 1];
        }
        // Make space for a new block, return its ID.
        private BlockID AllocateHandlerBlock()
        {
            int arrayIndex = handlerBlocks.Count;
            handlerBlocks.Add(ILLink.ControlFlow.HandlerBlock.NewDescriptor());
            return (BlockID)(~arrayIndex);
        }
        private BlockID AllocateBasicBlock()
        {
            int arrayIndex = blocks.Count;
            blocks.Add(BasicBlock.NewDescriptor());
            return (BlockID)(arrayIndex + 1);
        }

        // Edge data accessors; use negative IDs for regular edges, even for exception edges, odd for leave edges
        internal static bool IsExceptionEdgeID(EdgeID id) => ((id > 0) && (((int)id & 1) == 0));
        internal static bool IsLeaveEdgeID(EdgeID id) => ((id > 0) && (((int)id & 1) != 0));
        internal ref ExceptionEdge.Descriptor ExceptionEdgeData(EdgeID id) => ref exceptionEdges[(int)id >> 1];
        internal ref LeaveEdge.Descriptor LeaveEdgeData(EdgeID id) => ref leaveEdges[(int)id >> 1];
        internal ref FlowEdge.Descriptor EdgeData(EdgeID id)
        {
            if (id < 0)
            {
                Assert(!IsExceptionEdgeID(id));
                Assert(!IsLeaveEdgeID(id));
                return ref edges[~(int)id];
            }

            if (((int)id & 1) == 0)
            {
                Assert(IsExceptionEdgeID(id));
                return ref ExceptionEdgeData(id).edgeData;
            }

            Assert(IsLeaveEdgeID(id));
            return ref LeaveEdgeData(id).edgeData;
        }

        private EdgeID AllocateExceptionEdge()
        {
            int arrayIndex = exceptionEdges.Count;
            exceptionEdges.Add(ILLink.ControlFlow.ExceptionEdge.NewDescriptor());
            return (EdgeID)(arrayIndex << 1);
        }
        private EdgeID AllocateLeaveEdge(EdgeKind kind)
        {
            int arrayIndex = leaveEdges.Count;
            leaveEdges.Add(ILLink.ControlFlow.LeaveEdge.NewDescriptor(kind));
            return (EdgeID)((arrayIndex << 1) + 1);
        }
        // Make space for a new edge, return its ID.
        private EdgeID AllocateEdge(EdgeKind kind)
        {
            switch (kind)
            {
                case EdgeKind.Exception:
                    return AllocateExceptionEdge();
                case EdgeKind.BeginLeave:
                case EdgeKind.ContinueLeave:
                case EdgeKind.FinishLeave:
                    return AllocateLeaveEdge(kind);
            }

            int arrayIndex = edges.Count;
            edges.Add(FlowEdge.NewDescriptor(kind));
            return (EdgeID)(~arrayIndex);
        }

        // Convert IDs to corresponding entities
        public BasicBlock Block(BlockID id) => new BasicBlock(id, this);
        internal HandlerBlock HandlerBlock(BlockID id) => new HandlerBlock(id, this);
        public FlowEdge Edge(EdgeID id) => new FlowEdge(id, this);
        internal ExceptionEdge ExceptionEdge(EdgeID id) => new ExceptionEdge(id, this);
        internal LeaveEdge LeaveEdge(EdgeID id) => new LeaveEdge(id, this);
        public EHRegion EHRegion(EHRegionID id) => new EHRegion(id, this);

        // Try-Convert IDs to entries, accepting Invalid IDs
        public bool TryGetBlock(BlockID id, out BasicBlock block)
        {
            block = Block(id);
            return (id != BlockID.Invalid);
        }
        public bool TryGetEdge(EdgeID id, out FlowEdge edge)
        {
            edge = Edge(id);
            return (id != EdgeID.Invalid);
        }
        public bool TryGetEHRegion(EHRegionID id, out EHRegion region)
        {
            region = EHRegion(id);
            return (id != EHRegionID.Invalid);
        }

        // Region data accessor
        internal ref EHRegion.Descriptor EHRegionData(EHRegionID id) => ref ehRegions[(int)id];
        // Make space for a new region, return its ID.
        private EHRegionID AllocateEHRegion(EHRegionKind kind)
        {
            int arrayIndex = ehRegions.Count;
            ehRegions.Add(ILLink.ControlFlow.EHRegion.NewDescriptor(kind));
            return (EHRegionID)(arrayIndex);
        }

        // Lexical list of blocks
        public Enumeration<BasicBlock, BlockID>.Enumerable<Enumerators.BlockListImpl> Blocks =>
            new Enumeration<BasicBlock, BlockID>.Enumerable<Enumerators.BlockListImpl>(firstBlock, this);

        // Build a flow graph for a given MethodBody
        public FlowGraph(Cil.MethodBody method)
        {
            // First, convert the handler list to an EHRegion forest
            InitializeEHRegions(method);

            // Next, create the block descriptors.
            var blockIDs = AllocateBlockDescriptors(method, out InstructionVector blockFirstInstructions);

            // Add the edges for fall-through, branches, and exceptions.
            InitializeEdges(blockIDs, blockFirstInstructions);

            // Make sure dangling refs across reallocations didn't
            // cause any problems.
            CheckRefs();
        }

        private void InitializeEHRegions(Cil.MethodBody method)
        {
            // Allocate descriptor list.
            this.ehRegions = new RefList<EHRegion.Descriptor>();

            // Create appropriate nodes for each handler.
            var regionIDs = new List<EHRegionID>();
            foreach (var handler in method.ExceptionHandlers)
            {
                EHRegionID AllocateRegion(EHRegionKind kind, EHRegionID next = EHRegionID.Invalid)
                {
                    var id = AllocateEHRegion(kind);
                    regionIDs.Add(id);
                    ref var newRegion = ref EHRegionData(id);
                    newRegion.handler = handler;
                    newRegion.nextSameHandlerRegion = next;
                    return id;
                }

                // Every clause has a try region.
                var tryID = AllocateRegion(EHRegionKind.Try);

                // Every clause has one or two filter/handler regions.
                EHRegionID handlerID;
                switch (handler.HandlerType)
                {
                    case Cil.ExceptionHandlerType.Catch:
                        handlerID = AllocateRegion(EHRegionKind.Catch, tryID);
                        break;
                    case Cil.ExceptionHandlerType.Fault:
                        handlerID = AllocateRegion(EHRegionKind.Fault, tryID);
                        break;
                    case Cil.ExceptionHandlerType.Finally:
                        handlerID = AllocateRegion(EHRegionKind.Finally, tryID);
                        break;
                    case Cil.ExceptionHandlerType.Filter:
                        var filterID = AllocateRegion(EHRegionKind.FilterFilter, tryID);
                        handlerID = AllocateRegion(EHRegionKind.FilterHandler, filterID);
                        break;
                    default:
                        Assert(false);
                        handlerID = EHRegionID.Invalid;
                        break;
                }

                // Now fix up the nextInHandler link in the try node.
                EHRegionData(tryID).nextSameHandlerRegion = handlerID;
            }

            // Sort the region nodes by start offset and with outer regions first.
            regionIDs.Sort((EHRegionID leftID, EHRegionID rightID) =>
            {
                ref var left = ref EHRegionData(leftID);
                ref var right = ref EHRegionData(rightID);

                switch (left.CompareLexicalExtent(ref right))
                {
                    case ControlFlow.EHRegion.LexicalRelation.Before:
                    case ControlFlow.EHRegion.LexicalRelation.Outside:
                        return -1;

                    case ControlFlow.EHRegion.LexicalRelation.After:
                    case ControlFlow.EHRegion.LexicalRelation.Inside:
                        return 1;

                    case ControlFlow.EHRegion.LexicalRelation.Same:
                        // Fall down to list search.
                        break;

                    // All cases handled.
                    default:
                        Assert(false);
                        break;
                }

                // These two regions exactly overlap.
                var leftHandler = left.handler;
                var rightHandler = right.handler;
                if (leftHandler == rightHandler)
                {
                    // Same region.
                    return 0;
                }

                // Different exactly-overlapping regions should only be
                // possible if the regions are mutually-protected try
                // regions for different handlers.
                Assert(left.kind == EHRegionKind.Try);
                Assert(right.kind == EHRegionKind.Try);

                //The handler table lists inner clauses before outer clauses,
                // so whichever appears first in that list should be last here.
                foreach (var handler in method.ExceptionHandlers)
                {
                    if (handler == leftHandler)
                    {
                        return 1;
                    }
                    if (handler == rightHandler)
                    {
                        return -1;
                    }
                }

                Assert(false);
                return 0;
            });

            // Stitch together the tree
            var previousID = EHRegionID.Invalid;
            foreach (var currentID in regionIDs)
            {
                ref var current = ref EHRegionData(currentID);

                // Find parent and previous sibling IDs by walking parents of previous.
                var searchID = previousID;
                var priorSearchID = EHRegionID.Invalid;

                while (searchID != EHRegionID.Invalid)
                {
                    ref var region = ref EHRegionData(searchID);
                    var relation = region.CompareLexicalExtent(ref current);

                    if (relation == ControlFlow.EHRegion.LexicalRelation.Before)
                    {
                        // Need to keep walking up to find parent
                        priorSearchID = searchID;
                        searchID = region.lexicalParent;
                        continue;
                    }

                    // We must have found the parent.
                    Assert((relation == ControlFlow.EHRegion.LexicalRelation.Outside)
                        || (relation == ControlFlow.EHRegion.LexicalRelation.Same));
                    break;
                }

                var parentID = current.lexicalParent = searchID;
                if (priorSearchID == EHRegionID.Invalid)
                {
                    // There were no prior children of this parent.
                    if (parentID != EHRegionID.Invalid)
                    {
                        EHRegionData(parentID).firstLexicalChild = currentID;
                    }
                }
                else
                {
                    // Give the prior sibling a pointer to this one.
                    EHRegionData(priorSearchID).nextLexicalSibling = currentID;
                }

                // Update prevousID and continue loop.
                previousID = currentID;
            }

            // Record the first region so we can walk the region graph later.
            if (regionIDs.Count > 0)
            {
                firstRegion = regionIDs[0];
            }
        }

        private BlockID[] AllocateBlockDescriptors(Cil.MethodBody method, out InstructionVector blockFirstInstructions)
        {
            // Initialize empty lists
            this.blocks = new RefList<BasicBlock.Descriptor>();
            this.handlerBlocks = new RefList<HandlerBlock.Descriptor>();

            // Start by identifying the instructions that start basic blocks.
            var firstInstructions = blockFirstInstructions = FindBlockFirstInstructions(method);
            var blockCount = firstInstructions.Count;

            var blockIDs = new BlockID[blockCount];

            // Use some bookkeeping to track the innermost lexically enclosing
            // region as we walk the blocks in lexical order.
            var currentRegionID = EHRegionID.Invalid;
            var nextChildRegionID = this.firstRegion;
            var nextRegionChange = (nextChildRegionID == EHRegionID.Invalid ? null : EHRegionData(nextChildRegionID).StartInstruction);

            void AdvanceCurrentRegion(Cil.Instruction currentInstruction)
            {
                do
                {
					if ((nextChildRegionID != EHRegionID.Invalid)
						&& (currentInstruction == EHRegionData(nextChildRegionID).StartInstruction))
                    {
                        // Starting the next child.
                        currentRegionID = nextChildRegionID;
                        nextChildRegionID = EHRegionData(currentRegionID).firstLexicalChild;
                    }
                    else
                    {
                        // Ending the current region.
                        Assert(currentInstruction == EHRegionData(currentRegionID).EndInstruction);
                        nextChildRegionID = EHRegionData(currentRegionID).nextLexicalSibling;
                        currentRegionID = EHRegionData(currentRegionID).lexicalParent;
                    }

                    if (nextChildRegionID != EHRegionID.Invalid)
                    {
                        // Since there is a next child, entering it is the next transition.

                        nextRegionChange = EHRegionData(nextChildRegionID).StartInstruction;
                    }
                    else if (currentRegionID != EHRegionID.Invalid)
                    {
                        // Otherwise, if we're not done, exiting this region is the
                        // next transition.

                        nextRegionChange = EHRegionData(currentRegionID).EndInstruction;
                    }
                    else
                    {
                        // Otherwise, we're done.

                        nextRegionChange = null;
                    }

                    // Keep advancing states if the transitions coincide.
                } while (currentInstruction == nextRegionChange);
            }

            // Now walk the blocks, marking first and last instruction of each,
            // as well as recording innermost lexically enclosing handler.
            BlockID id;
            var previousID = BlockID.Invalid;
            for (int blockIndex = 0; blockIndex < blockCount; previousID = id, ++blockIndex)
            {
                var firstInstruction = firstInstructions[blockIndex];

                // Allocate a descriptor for the block.
                if (firstInstruction == nextRegionChange)
                {
                    // Advance to the next region
                    AdvanceCurrentRegion(firstInstruction);

                    if ((currentRegionID == EHRegionID.Invalid)
						|| (EHRegionData(currentRegionID).kind == EHRegionKind.Try))
                    {
                        // Normal blocks are used for top-level and try regions.

                        id = AllocateBasicBlock();
                    }
                    else
                    {
                        // The first block of a non-try region gets annotated
                        // with which region it starts.

                        id = AllocateHandlerBlock();
                        HandlerBlockData(id).handlerRegion = currentRegionID;
                    }

                    // Record the first block of each region.
                    var startingRegionID = currentRegionID;
                    while (startingRegionID != EHRegionID.Invalid)
                    {
                        ref var startingRegion = ref EHRegionData(startingRegionID);
                        if (startingRegion.StartInstruction != firstInstruction)
                        {
                            // Not the first block of this or parent regions.
                            break;
                        }
                        startingRegion.firstBlock = id;
                        startingRegionID = startingRegion.lexicalParent;
                    }
                }
                else
                {
                    id = AllocateBasicBlock();
                }

                // Record first/last instructions.
                ref var blockData = ref BlockData(id);
                blockData.firstInstruction = firstInstruction;
				if (blockIndex < blockCount - 1)
                {
                    blockData.lastInstruction = firstInstructions[blockIndex + 1].Previous;
                }
                else
                {
                    blockData.lastInstruction = method.Instructions[method.Instructions.Count - 1];
                }

                // Record region
                blockData.lexicalRegion = currentRegionID;

                // Keep track of the ordered set of block IDs.
                blockIDs[blockIndex] = id;
                blockData.previous = previousID;
                if (previousID != BlockID.Invalid)
                {
                    BlockData(previousID).next = id;
                }
            }

            // Record the first block
            this.firstBlock = blockIDs[0];

            return blockIDs;
        }

        // Returns a sorted list of instructions which start blocks.
        private static InstructionVector FindBlockFirstInstructions(Cil.MethodBody method)
        {
            var blockFirstInstructions = new InstructionVector();

            // The first block starts with the first instruction.
            blockFirstInstructions.Add(method.Instructions[0]);

            // Branches imply block transitions.
            foreach (var instruction in method.Instructions)
            {
                switch (instruction.Operand)
                {
                    case Cil.Instruction target:
                        blockFirstInstructions.Add(target);
                        break;
                    case Cil.Instruction[] targets:
                        foreach (var target in targets)
                        {
                            blockFirstInstructions.Add(target);
                        }
                        break;
                }

                if (!instruction.MustFallThrough())
                {
                    // This instruction is a split, so the next instruction
                    // starts a block.
                    var next = instruction.Next;
                    if (next != null)
                    {
                        blockFirstInstructions.Add(next);
                    }
                }
            }

            // Protected regions and handlers start blocks.
            foreach (var clause in method.ExceptionHandlers)
            {
                blockFirstInstructions.Add(clause.TryStart);
                blockFirstInstructions.Add(clause.HandlerStart);
                if (clause.HandlerType == Cil.ExceptionHandlerType.Filter)
                {
                    blockFirstInstructions.Add(clause.FilterStart);
                }
            }

            // Sort and de-dup the start offset list.
            blockFirstInstructions.SortAndRemoveDuplicates();
            return blockFirstInstructions;
        }

        private void InitializeEdges(BlockID[] blockIDs, InstructionVector blockFirstInstructions)
        {
            // Allocate the descriptor lists.
            this.edges = new RefList<FlowEdge.Descriptor>();
            this.exceptionEdges = new RefList<ExceptionEdge.Descriptor>();
            this.leaveEdges = new RefList<LeaveEdge.Descriptor>();

            // Walk the block descriptors, filling in edge links.
            foreach (var block in this.Blocks)
            {
                AddNonExceptionSuccessorEdges(block, blockIDs, blockFirstInstructions);

                AddExceptionSuccessorEdges(block);
            }
        }

        private void AddNonExceptionSuccessorEdges(BasicBlock predecessor, BlockID[] blockIDs, InstructionVector blockFirstInstructions)
        {
            var predecessorID = predecessor.ID;
            var lastInstruction = BlockData(predecessorID).lastInstruction;

            void AddEdgeTo(EdgeKind kind, Cil.Instruction target) =>
                AddEdge(kind, predecessorID, blockIDs[blockFirstInstructions.BinarySearch(target)]);
            void AddEdgeToNext(EdgeKind kind) =>
                AddEdge(kind, predecessorID, BlockData(predecessorID).next);

            switch (lastInstruction.Operand)
            {
                case Cil.Instruction target:
                    {
                        switch (lastInstruction.OpCode.Code)
                        {
                            case Cil.Code.Br:
                            case Cil.Code.Br_S:
                                AddEdgeTo(EdgeKind.Goto, target);
                                break;
                            case Cil.Code.Leave:
                            case Cil.Code.Leave_S:
                                AddLeaveEdges(lastInstruction, Block(predecessorID), target, blockIDs, blockFirstInstructions);
                                break;
                            default:
                                Assert(lastInstruction.MayFallThrough());
                                AddEdgeTo(EdgeKind.TakenConditional, target);
                                AddEdgeToNext(EdgeKind.UntakenConditional);
                                break;
                        }
                        break;
                    }
                case Cil.Instruction[] targets:
                    foreach (var target in targets)
                    {
                        AddEdgeTo(EdgeKind.SwitchCase, target);
                    }
                    AddEdgeToNext(EdgeKind.SwitchDefault);
                    break;
                default:
                    {
                        switch (lastInstruction.OpCode.Code)
                        {
                            case Cil.Code.Ret:
                            case Cil.Code.Jmp:
                                // No edge
                                break;
                            case Cil.Code.Endfinally:
                            case Cil.Code.Endfilter:
                            case Cil.Code.Throw:
                            case Cil.Code.Rethrow:
                                // Redundant with exception edge
                                break;
                            default:
                                Assert(lastInstruction.MustFallThrough());
                                AddEdgeToNext(EdgeKind.FallThrough);
                                break;
                        }
                        break;
                    }
            }
        }

        private void AddLeaveEdges(Cil.Instruction leave, BasicBlock leaveBlock, Cil.Instruction target, BlockID[] blockIDs, InstructionVector blockFirstInstructions)
        {
            BlockID FindBlock(Cil.Instruction instruction) => blockIDs[blockFirstInstructions.BinarySearch(instruction)];
            void AddLeaveEdge(EdgeKind leaveKind, BlockID successor)
            {
                var id = AddEdge(leaveKind, leaveBlock.ID, successor);
                LeaveEdgeData(id).leaveBlock = leaveBlock.ID;
            }

            if (!leaveBlock.TryGetLexicalRegion(out EHRegion leaveRegion)
                || !leaveRegion.TryGetThrowSuccessor(out EHRegion handlerRegion))
            {
                // This is bogus MSIL (no containing protected region)... treat the leave like a goto.
                AddEdge(EdgeKind.Goto, leaveBlock.ID, FindBlock(target));
                return;
            }

            // Helper to skip over non-finally handlers
            bool WalkToFinally(ref EHRegion region)
            {
                while (region.Kind != EHRegionKind.Finally)
                {
                    if (!region.TryGetContinueDispatchSuccessor(ExceptionPass.SecondPass, out region))
                    {
                        return false;
                    }
                }
                return true;
            }

            if (!WalkToFinally(ref handlerRegion))
            {
                // Leave is not protected by a finally; treat like a goto
                AddEdge(EdgeKind.Goto, leaveBlock.ID, FindBlock(target));
                return;
            }

            // We've found at least one finally.
            var innerFinally = handlerRegion;
            AddLeaveEdge(EdgeKind.BeginLeave, innerFinally.FirstBlock.ID);

            EdgeKind currentKind = EdgeKind.ContinueLeave;
            while (currentKind != EdgeKind.FinishLeave)
            {
                BlockID currentTarget;
                if (innerFinally.TryGetContinueDispatchSuccessor(ExceptionPass.SecondPass, out handlerRegion)
                    && WalkToFinally(ref handlerRegion)
                    && handlerRegion.Handler.Protects(target))
                {
                    // Found an intermediate finally to add edges to.
                    currentTarget = handlerRegion.FirstBlock.ID;
                }
                else
                {
                    // We've walked all intermediate finallies, now
                    // just need edges to the final leave target.
                    currentTarget = FindBlock(target);
                    currentKind = EdgeKind.FinishLeave;
                }

                bool foundEndFinally = false;
                foreach (var block in innerFinally.ExclusiveBlocks)
                {
                    if (block.LastInstruction.OpCode.Code == Cil.Code.Endfinally)
                    {
                        foundEndFinally = true;
                        AddLeaveEdge(currentKind, currentTarget);
                    }
                }
                if (!foundEndFinally)
                {
                    // The leave instruction we are processing would get stuck
                    // in this finally (it has no endfinally), so there's no
                    // need to continue onward.
                    return;
                }

                // Loop to find the next-outer finally
                innerFinally = handlerRegion;
            }
        }

        private void AddExceptionSuccessorEdges(BasicBlock predecessor)
        {
            if (!predecessor.TryGetLexicalRegion(out EHRegion region))
            {
                // Any exception propagation from here would be out
                // to the caller, which we don't have edges for.
                return;
            }

            var predecessorID = predecessor.ID;

            // This block may have different successors for new exceptions
            // vs continuing dispatch, and in either of those cases the first
            // and second pass successors will differ if they are a filter
            // and its handler.  So, this block may have up to four unique
            // exceptional successors.
            var edge1 = EdgeID.Invalid;
            var edge2 = EdgeID.Invalid;
            var edge3 = EdgeID.Invalid;
            var edge4 = EdgeID.Invalid;

            // Helper to check if one of the cached successors is the
            // given one, or claim it as the given one if it's still
            // unclaimed.
            bool HasSuccessor(ref EdgeID edge, BlockID successorID)
            {
                if (edge == EdgeID.Invalid)
                {
                    edge = AddEdge(EdgeKind.Exception, predecessorID, successorID);
                    return true;
                }

                return (EdgeData(edge).successor == successorID);
            }

            // Helper that gets the ID of an edge to the given
            // successor region, creating the edge if it doesn't
            // yet exist.
            EdgeID EdgeTo(EHRegion successor)
            {
                var successorID = successor.FirstBlock.ID;

                if (HasSuccessor(ref edge1, successorID))
                {
                    return edge1;
                }
                if (HasSuccessor(ref edge2, successorID))
                {
                    return edge2;
                }
                if (HasSuccessor(ref edge3, successorID))
                {
                    return edge3;
                }
                if (HasSuccessor(ref edge4, successorID))
                {
                    return edge4;
                }
                Assert(false);
                return EdgeID.Invalid;
            }

            // Helper that adds edges for the given source(s) to the given outer
            // handler.
            void AddEdges(EHRegion outerHandler, ExceptionEdgeKinds kinds)
            {
                // Find the edge to the given successor, and add the modes.
                var edgeID = EdgeTo(outerHandler);
                ExceptionEdgeData(edgeID).kinds |= kinds;
            }

            // Now see which outgoing exception edges this block needs.

            if (region.TryGetThrowSuccessor(out EHRegion throwSuccessor))
            {
                // Add edge for any exceptions raised in this block itself.
                AddEdges(throwSuccessor, ExceptionEdgeKinds.RaiseException);
            }

            if ((region.Kind != EHRegionKind.Try)
                && (predecessor == region.FirstBlock)
                && (region.Kind != EHRegionKind.FilterFilter))
            {
                if (region.Kind != EHRegionKind.FilterHandler)
                {
                    // A catch, finally, or fault handler may be skipped past during
                    // pass 1 of exception dispatch.
                    Assert((region.Kind == EHRegionKind.Catch)
                        || (region.Kind == EHRegionKind.Finally)
                        || (region.Kind == EHRegionKind.Fault));

                    if (region.TryGetContinueDispatchSuccessor(ExceptionPass.FirstPass, out EHRegion successor))
                    {
                        var kinds = (region.Kind == EHRegionKind.Catch ? ExceptionEdgeKinds.CatchWrongType
                            : (region.Kind == EHRegionKind.Finally ? ExceptionEdgeKinds.BypassFinally : ExceptionEdgeKinds.BypassFault));
                        AddEdges(successor, kinds);
                    }
                }

                if ((region.Kind == EHRegionKind.Catch)
                    || (region.Kind == EHRegionKind.FilterHandler))
                {
                    // A catch or filter handler may be skipped past during
                    // pass 2 of exception dispatch (if it isn't the
                    // ultimate handler but lies between an inner finally/fault
                    // and the ultimate handler).
                    if (region.TryGetContinueDispatchSuccessor(ExceptionPass.SecondPass, out EHRegion successor))
                    {
                        AddEdges(successor,
                            (region.Kind == EHRegionKind.Catch ? ExceptionEdgeKinds.BypassCatch : ExceptionEdgeKinds.BypassFilterHandler));
                    }
                }
            }

            if (predecessor.LastInstruction.OpCode.Code == Cil.Code.Endfinally)
            {
                // NOTE: this opcode is used to end both finally and fault handlers.
                // When the handler completes (in the second pass), execution passes
                // on to the next outer handler.
                if (region.TryGetContinueDispatchSuccessor(ExceptionPass.SecondPass, out EHRegion successor))
                {
                    AddEdges(successor,
                        (region.Kind == EHRegionKind.Finally ? ExceptionEdgeKinds.EndFinally : ExceptionEdgeKinds.EndFault));
                }
            }
            else if (predecessor.LastInstruction.OpCode.Code == Cil.Code.Endfilter)
            {
                // If a filter declines to handle an exception, the Pass 1 search
                // continues to the next outer handler.
                if (region.TryGetContinueDispatchSuccessor(ExceptionPass.FirstPass, out EHRegion successor))
                {
                    AddEdges(successor, ExceptionEdgeKinds.FilterFail);
                }

                // If a filter decides to handle an exception, the first pass ends
                // and the second pass begins.
                AddTakenFilterEdges(region, predecessorID);
            }
        }

        private void AddTakenFilterEdges(EHRegion filter, BlockID predecessorID)
        {
            Assert(filter.Kind == EHRegionKind.FilterFilter);
            filter.TryGetHandlerRegion(out EHRegion filterHandler);
            var handlerFirstID = filterHandler.FirstBlock.ID;

            // If the exception was initiated in the filter's own try region,
            // then the second pass begins in the associated FilterHandler.
            var edgeId = AddEdge(EdgeKind.Exception, predecessorID, handlerFirstID);
            ExceptionEdgeData(edgeId).kinds = ExceptionEdgeKinds.FilterMatch;

            // If the exception was initiated in an inner try region, then
            // the second pass begins with any inner finally/fault regions.
            // Generate these edges as though they "drive by" the FilterHandler,
            // rather than emanating directly from the endfilter, so that any
            // outer filter can hook its endfilter edges to this filter's
            // FilterHandler and transitively the edges connect to all inner
            // finally/fault handlers.
            var worklist = new Stack<EHRegionID>();
            filter.TryGetTryRegion(out EHRegion tryRegion);
            worklist.Push(tryRegion.ID);
            while (worklist.Count > 0)
            {
                var innerRegion = EHRegion(worklist.Pop());
                foreach (var childRegion in innerRegion.LexicalChildren)
                {
                    if (childRegion.Kind != EHRegionKind.Try)
                    {
                        // Walk through non-try regions, we're looking
                        // for inner try regions.
                        worklist.Push(childRegion.ID);
                        continue;
                    }

                    // Get the handler for exceptions in this inner try.
                    childRegion.TryGetHandlerRegion(out EHRegion childHandler);

                    if (childHandler.Kind == EHRegionKind.Catch)
                    {
                        // We don't need an endfilter edge back to this catch;
                        // if the catch were going to handle the exception, then
                        // pass 1 wouldn't have made it up to the filter.
                    }
                    else
                    {
                        // For finally/fault handlers, we will need to visit them
                        // on pass 2 if the exception originated within them.
                        // For FilterHandlers, we still add an endfilter edge
                        // targeting them, so that the transitive endfilter "drive by"
                        // edges will reach all inner finally/fault handlers (factoring
                        // those edges in this way avoids quadratic edge counts in
                        // deeply nested filter-protected try cases).
                        var id = AddEdge(EdgeKind.Exception, handlerFirstID, childHandler.FirstBlock.ID);
                        ExceptionEdgeData(id).kinds = ExceptionEdgeKinds.FilterRewind;
                    }

                    if (childHandler.Kind == EHRegionKind.FilterHandler)
                    {
                        // Don't recurse, since this FilterHandler will have its
                        // own "drive by" edges reaching inner handlers.
                    }
                    else
                    {
                        // Continue searching for inner try regions.
                        worklist.Push(childRegion.ID);
                    }
                }
            }
        }

        private EdgeID AddEdge(EdgeKind kind, BlockID predecessorID, BlockID successorID)
        {
            var edgeID = AllocateEdge(kind);
            ref var edgeData = ref EdgeData(edgeID);
            edgeData.predecessor = predecessorID;
            edgeData.successor = successorID;

            ref var predecessorData = ref BlockData(predecessorID);
            ref var successorData = ref BlockData(successorID);

            // Prepend the new edge to both lists
            edgeData.nextPredecessorEdgeID = successorData.firstPredecessor;
            edgeData.nextSuccessorEdgeID = predecessorData.firstSuccessor;
            predecessorData.firstSuccessor = edgeID;
            successorData.firstPredecessor = edgeID;

            return edgeID;
        }
    }
}
