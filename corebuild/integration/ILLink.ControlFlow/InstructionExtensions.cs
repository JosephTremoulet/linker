using Cil = Mono.Cecil.Cil;

namespace ILLink.ControlFlow.CecilExtensions
{
    public static class InstructionExtensions
    {
        public static bool MayFallThrough(this Cil.Instruction instruction)
        {
            switch (instruction.OpCode.Code)
            {
                case Cil.Code.Br:
                case Cil.Code.Br_S:
                case Cil.Code.Ret:
                case Cil.Code.Jmp:
                case Cil.Code.Leave:
                case Cil.Code.Leave_S:
                case Cil.Code.Endfinally:
                case Cil.Code.Endfilter:
                case Cil.Code.Throw:
                case Cil.Code.Rethrow:
                    return false;
            }
            return true;
        }
        public static bool MustFallThrough(this Cil.Instruction instruction)
        {
            if (instruction.Operand is Cil.Instruction || instruction.Operand is Cil.Instruction[])
            {
                // These opcodes take instructions as operands to indicate
                // that they may flow to those instructions.
                return false;
            }
            if (!MayFallThrough(instruction))
            {
                // Some opcodes like ret/throw don't have an operand indicating where they
                // flow to, because they flow out of their region/method.  It happens that
                // such opcodes flow out of their region/method unconditionally.
                return false;
            }
            return true;
        }

        public static bool References(this Cil.Instruction referent, Cil.Instruction referee)
        {
            if (referent.Operand == referee)
            {
                return true;
            }
            else if (referent.Operand is Cil.Instruction[] cases)
            {
                foreach (var arm in cases)
                {
                    if (arm == referee)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
