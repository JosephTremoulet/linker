using System;
using System.Collections.Generic;
using System.Linq;

using ILLink.ControlFlow;

using Mono.Linker.Steps;
using Cil = Mono.Cecil.Cil;

namespace ILLink.CustomSteps
{
    using static System.Diagnostics.Debug;

    public class NonExceptionMarkStep : MarkStepWithReflectionHeuristics
    {
        public NonExceptionMarkStep(MarkStepWithReflectionHeuristics oldStep)
			: this(oldStep, typeof(MarkStepWithReflectionHeuristics), oldStep.ReflectionHeuristics.ToList()) { }

        public NonExceptionMarkStep(MarkStep oldStep) : this(oldStep, typeof(MarkStep), new string[0]) { }

        private NonExceptionMarkStep(MarkStep oldStep, Type exactType,
			ICollection<string> reflectionHeuristics) : base(reflectionHeuristics)
        {
            if (oldStep.GetType() != exactType)
            {
                throw new NotSupportedException($"Unrecognized mark step type {oldStep.GetType().Name}");
            }
        }

        protected override void MarkMethodBody(Cil.MethodBody methodBody)
        {
            var flowGraph = new FlowGraph(methodBody);

            var markedBlocks = new HashSet<BlockID>();
            var worklist = new Queue<BlockID>();

            bool EnsureQueued(BlockID blockID)
            {
                if (markedBlocks.Add(blockID))
                {
                    worklist.Enqueue(blockID);
                    return true;
                }

                return false;
            }

            foreach (var block in flowGraph.Blocks)
            {
                var opcode = block.LastInstruction.OpCode.Code;

                if ((opcode == Cil.Code.Ret) || (opcode == Cil.Code.Jmp))
                {
                    EnsureQueued(block.ID);
                }
            }

            while (worklist.Count > 0)
            {
                var block = flowGraph.Block(worklist.Dequeue());

                foreach (var instruction in block.Instructions)
                {
                    MarkInstruction(instruction);
                }

                foreach (var edge in block.PredecessorEdges)
                {
                    switch (edge.Kind)
                    {
                        case EdgeKind.BeginLeave:
                        case EdgeKind.ContinueLeave:
                        case EdgeKind.Exception:
                            // Ignore all these (this step explicitly
                            // ignores exception edges, and the begin/
                            // continue leave edges will get processed
                            // when we see the corresponding FinishLeave).
                            continue;
                        case EdgeKind.FinishLeave:
                            {
                                // Every endfinally in the outermost finally which a
                                // `leave` instruction exits will have its own FinishLeave
                                // edge to this successor (the target of the `leave`
                                // instruction).  We now know we need to mark this particular
                                // endfinally.
                                var outerEndFinallyBlock = edge.Predecessor;
                                EnsureQueued(outerEndFinallyBlock.ID);

                                // If this `leave` instruction exits multiple finally-protected
                                // regions, find and mark the intermediate endfinally blocks.
                                edge.Is(out LeaveEdge leaveEdge);
                                var leaveBlock = leaveEdge.LeaveBlock;

                                // Queue up the `leave` block itself.
                                if (!EnsureQueued(leaveBlock.ID))
                                {
                                    // This leave block was already enqueued.
                                    // Since we eagerly mark the intermediate endfinally
                                    // blocks the first time we visit a FinishLeave edge
                                    // for this leave block, and since leave edges are the
                                    // only successors of a block ending with a `leave`
                                    // instruction, we know the intermediate endfinally
                                    // blocks have already been marked.
                                    continue;
                                }

                                outerEndFinallyBlock.TryGetLexicalRegion(out EHRegion finallyRegion);

                                for (var nextFinallyRegion = finallyRegion; ; finallyRegion = nextFinallyRegion)
                                {
                                    var finallyStart = finallyRegion.FirstBlock;

                                    foreach (var finallyPredEdge in finallyStart.PredecessorEdges)
                                    {
                                        if ((finallyPredEdge.Kind == EdgeKind.ContinueLeave)
                                            && finallyPredEdge.Is(out LeaveEdge continueLeave)
                                            && (continueLeave.LeaveBlock == leaveBlock))
                                        {
                                            // Make sure we process this endfinally's block
                                            var innerEndFinallyBlock = continueLeave.Predecessor;
                                            EnsureQueued(innerEndFinallyBlock.ID);

                                            if (nextFinallyRegion == finallyRegion)
                                            {
                                                // This is the first ContinueLeave edge we've
                                                // seen leading to the `finallyRegion`, so we
                                                // have a new `nextFinallyRegion`.
                                                innerEndFinallyBlock.TryGetLexicalRegion(out nextFinallyRegion);
                                            }
                                            else
                                            {
                                                // All continue finally edges for this same `leave`
                                                // should be endfinally instructions in the next-
                                                // innermost finally exited by this `leave`
                                                Assert(innerEndFinallyBlock.TryGetLexicalRegion(out EHRegion innerFinallyRegion)
                                                    && (innerFinallyRegion == nextFinallyRegion));
                                            }
                                        }
                                    }

                                    if (nextFinallyRegion == finallyRegion)
                                    {
                                        // Did not find an inner finally region.
                                        break;
                                    }
                                }

                                break;
                            }
                        default:
                            EnsureQueued(edge.Predecessor.ID);
                            break;
                    }
                }
            }
        }
    }
}
