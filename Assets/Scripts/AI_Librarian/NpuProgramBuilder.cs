using System;
using System.Collections.Generic;
using static FracturedMind.AI.NpuVm;

namespace FracturedMind.AI
{
    /// <summary>
    /// Small helper to author instruction sequences and sample programs.
    /// </summary>
    public sealed class NpuProgramBuilder
    {
        readonly List<Instruction> _ins = new List<Instruction>(64);

        public NpuProgramBuilder Emit(Op op, byte dst = 0, byte a = 0, byte b = 0, byte c = 0, int imm = 0)
        {
            _ins.Add(new Instruction { Op = op, Dst = dst, SrcA = a, SrcB = b, SrcC = c, Imm = imm });
            return this;
        }

        public NpuProgramBuilder MoveImm(byte dst, float value) => Emit(Op.MoveImm, dst, imm: BitConverter.SingleToInt32Bits(value));
        public NpuProgramBuilder LoadMem(byte dst, byte addr) => Emit(Op.LoadMem, dst, addr);
        public NpuProgramBuilder StoreMem(byte addr, byte src) => Emit(Op.StoreMem, addr, src);
        public NpuProgramBuilder Add(byte dst, byte a, byte b) => Emit(Op.Add, dst, a, b);
        public NpuProgramBuilder Mul(byte dst, byte a, byte b) => Emit(Op.Mul, dst, a, b);
        public NpuProgramBuilder Mad(byte dst, byte a, byte b, byte c) => Emit(Op.Mad, dst, a, b, c);
        public NpuProgramBuilder Lerp(byte dst, byte a, byte b, byte t) => Emit(Op.Lerp, dst, a, b, t);
        public NpuProgramBuilder Saturate(byte dst, byte a) => Emit(Op.Saturate, dst, a);
        public NpuProgramBuilder Decay(byte dst, float rate) => Emit(Op.Decay, dst, imm: BitConverter.SingleToInt32Bits(rate));
        public NpuProgramBuilder CmpGt(byte flag, byte a, byte b) => Emit(Op.CmpGt, flag, a, b);
        public NpuProgramBuilder CmpGe(byte flag, byte a, byte b) => Emit(Op.CmpGe, flag, a, b);
        public NpuProgramBuilder Jnz(byte flag, int target) => Emit(Op.Jnz, a: flag, imm: target);
        public NpuProgramBuilder End() => Emit(Op.End);

        public Instruction[] ToArray() => _ins.ToArray();

        /// <summary>
        /// Sample program: vision score with decay and threshold.
        /// Reg layout: r0=dot, r1=distNorm, r2=light, r3=motion, r4=temp, r5=score, r6=flag, r7=memory.
        /// Mem layout: mem[0] holds persistent belief.
        /// </summary>
        public static Instruction[] BuildSampleVisionDetector(float weightDot = 0.55f, float weightDist = 0.25f, float weightLight = 0.1f, float weightMotion = 0.1f, float threshold = 0.6f, float memoryBlend = 0.25f, float decayRate = 1.5f)
        {
            var b = new NpuProgramBuilder();

            // Load persistent belief into r7.
            b.LoadMem(7, 0);

            // Weighted sum into r5.
            b.MoveImm(4, weightDot).Mul(4, 0, 4);      // r4 = dot * wDot
            b.MoveImm(5, weightDist).Mul(5, 1, 5);     // r5 = dist * wDist
            b.Add(4, 4, 5);                            // r4 += r5
            b.MoveImm(5, weightLight).Mul(5, 2, 5);    // r5 = light * wLight
            b.Add(4, 4, 5);                            // r4 += r5
            b.MoveImm(5, weightMotion).Mul(5, 3, 5);   // r5 = motion * wMotion
            b.Add(5, 4, 5);                            // r5 = total score
            b.Saturate(5, 5);

            // Decay memory and blend new score into mem[0].
            b.Decay(7, decayRate);
            b.MoveImm(4, 1f - memoryBlend).Mul(4, 7, 4);    // r4 = memory * (1-blend)
            b.MoveImm(6, memoryBlend).Mul(6, 5, 6);         // r6 = score * blend
            b.Add(7, 4, 6);                                 // r7 = blended belief
            b.StoreMem(0, 7);

            // Threshold compare into r6 (flag).
            b.MoveImm(4, threshold);
            b.CmpGe(6, 7, 4);

            // Jump over end if alert -> could add behaviors; here just End regardless.
            b.End();
            return b.ToArray();
        }
    }
}
