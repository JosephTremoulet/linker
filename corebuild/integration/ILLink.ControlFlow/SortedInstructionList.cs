using System.Collections.Generic;

using Cil = Mono.Cecil.Cil;

namespace ILLink.ControlFlow.Collections
{
    using static System.Diagnostics.Debug;

    internal sealed class InstructionVector
    {
        private sealed class Comparer : IComparer<Cil.Instruction>
        {
            int IComparer<Cil.Instruction>.Compare(Cil.Instruction x, Cil.Instruction y) => x.Offset - y.Offset;
        }

        private List<Cil.Instruction> list;
        private Comparer comparer;
#if DEBUG
        private bool sorted;
#else
        private bool sorted { get { return false; } set { } }
#endif

        public InstructionVector() {
            list = new List<Cil.Instruction>();
            comparer = new Comparer();
            sorted = true;
        }

        public void Add(Cil.Instruction instruction)
        {
            list.Add(instruction);
            sorted = false;
        }

        public int Count => list.Count;

        public Cil.Instruction this[int index]
        {
            get => list[index];
            set => list[index] = value;
        }

        public void Sort()
        {
            list.Sort(comparer);

            int writeIndex = 0;
            Cil.Instruction previousInstruction = null;
            for (int probeIndex = 0; probeIndex < list.Count; ++probeIndex)
            {
                Cil.Instruction instruction = list[probeIndex];
                if (instruction != previousInstruction)
                {
                    list[writeIndex++] = instruction;
                }
            }

            list.RemoveRange(writeIndex, list.Count - writeIndex);
            sorted = true;
        }

        public int BinarySearch(Cil.Instruction instruction)
        {
            Assert(sorted);
            return list.BinarySearch(instruction, comparer);
        }
    }
}
