using System;
using System.Collections;
using System.Collections.Generic;

using Cil = Mono.Cecil.Cil;

namespace ILLink.ControlFlow
{
    public struct InstructionRange : IEnumerable<Cil.Instruction>, IEnumerable
    {
        private Cil.Instruction start;
        private Cil.Instruction end;

        internal InstructionRange(Cil.Instruction start, Cil.Instruction end)
        {
            this.start = start;
            this.end = end;
        }

        public Enumerator GetEnumerator() => new Enumerator(start, end);

        IEnumerator<Cil.Instruction> IEnumerable<Cil.Instruction>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<Cil.Instruction>, IEnumerator
        {
            private Cil.Instruction current;
            private Cil.Instruction end;
            private bool started;

            internal Enumerator(Cil.Instruction start, Cil.Instruction end)
            {
                this.current = start;
                this.end = end;
                this.started = false;
            }

            public bool MoveNext()
            {
                if (!started)
                {
                    started = true;
                    return true;
                }
                if (current == end)
                {
                    return false;
                }
                current = current.Next;
                return (current != end);
            }

            public Cil.Instruction Current => current;

            object IEnumerator.Current => Current;
            void IEnumerator.Reset() => throw new NotImplementedException();
            void IDisposable.Dispose() { }
        }
    }

}
