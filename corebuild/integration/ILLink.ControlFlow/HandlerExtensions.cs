using Cil = Mono.Cecil.Cil;

namespace ILLink.ControlFlow.CecilExtensions
{
    public static class ExceptionHandlerExtensions
    {
        public static bool Protects(this Cil.ExceptionHandler handler, Cil.Instruction instruction)
            => handler.Protects(instruction.Offset);

        public static bool Protects(this Cil.ExceptionHandler handler, int instructionOffset)
            => (handler.TryStart.Offset <= instructionOffset) && (handler.TryEnd.Offset > instructionOffset);
    }
}
