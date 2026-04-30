using System;

namespace FracturedMind.AI
{
    /// <summary>
    /// Deterministic, branch-capable, stateful VM for lightweight AI logic.
    /// </summary>
    public sealed class NpuVm
    {
        public enum Op : byte
        {
            Noop = 0,
            LoadMem,     // dst = mem[srcA]
            StoreMem,    // mem[dst] = reg[srcA]
            MoveImm,     // dst = imm (float bits)
            Add,         // dst = a + b
            Mul,         // dst = a * b
            Mad,         // dst = a * b + c
            Lerp,        // dst = lerp(a,b,t)
            Saturate,    // dst = clamp(a,0,1)
            Decay,       // dst = lerp(dst,0,rate*dt)
            CmpGt,       // flag = a > b ? 1 : 0
            CmpGe,       // flag = a >= b ? 1 : 0
            Jnz,         // if flag != 0 jump imm
            End
        }

        public struct Instruction
        {
            public Op Op;
            public byte Dst;
            public byte SrcA;
            public byte SrcB;
            public byte SrcC;
            public int Imm; // immediate or jump target, float bits where relevant
        }

        readonly Instruction[] _code;
        readonly float[] _regs;
        readonly float[] _mem;
        int _ip;
        bool _running;

        public int RegisterCount => _regs.Length;
        public int MemorySize => _mem.Length;

        public NpuVm(Instruction[] code, int registerCount, int memorySize)
        {
            _code = code ?? Array.Empty<Instruction>();
            _regs = new float[Math.Max(1, registerCount)];
            _mem = new float[Math.Max(1, memorySize)];
            Reset();
        }

        public void Reset()
        {
            Array.Clear(_regs, 0, _regs.Length);
            Array.Clear(_mem, 0, _mem.Length);
            _ip = 0;
            _running = true;
        }

        public void SetRegister(int index, float value)
        {
            _regs[index] = Sanitize(value);
        }

        public float GetRegister(int index) => Sanitize(_regs[index]);

        public float ReadMem(int index) => Sanitize(_mem[index]);

        public void WriteMem(int index, float value)
        {
            _mem[index] = Sanitize(value);
        }

        public void Tick(float dt)
        {
            // Run the program every call; End only stops this tick.
            _running = true;

            var code = _code;
            var regs = _regs;
            var mem = _mem;
            int ip = 0;

            while (ip < code.Length)
            {
                ref var ins = ref code[ip];

                switch (ins.Op)
                {
                    case Op.Noop:
                        break;
                    case Op.LoadMem:
                        regs[ins.Dst] = Sanitize(mem[ins.SrcA]);
                        break;
                    case Op.StoreMem:
                        mem[ins.Dst] = Sanitize(regs[ins.SrcA]);
                        break;
                    case Op.MoveImm:
                        regs[ins.Dst] = Sanitize(BitConverter.Int32BitsToSingle(ins.Imm));
                        break;
                    case Op.Add:
                        regs[ins.Dst] = Sanitize(regs[ins.SrcA] + regs[ins.SrcB]);
                        break;
                    case Op.Mul:
                        regs[ins.Dst] = Sanitize(regs[ins.SrcA] * regs[ins.SrcB]);
                        break;
                    case Op.Mad:
                        regs[ins.Dst] = Sanitize(regs[ins.SrcA] * regs[ins.SrcB] + regs[ins.SrcC]);
                        break;
                    case Op.Lerp:
                        regs[ins.Dst] = Sanitize(Lerp(regs[ins.SrcA], regs[ins.SrcB], regs[ins.SrcC]));
                        break;
                    case Op.Saturate:
                        regs[ins.Dst] = Sanitize(Saturate(regs[ins.SrcA]));
                        break;
                    case Op.Decay:
                        // Exponential decay with dt scaling; Imm holds rate.
                        regs[ins.Dst] = Sanitize(Lerp(regs[ins.Dst], 0f, 1f - (float)Math.Exp(-BitConverter.Int32BitsToSingle(ins.Imm) * dt)));
                        break;
                    case Op.CmpGt:
                        regs[ins.Dst] = regs[ins.SrcA] > regs[ins.SrcB] ? 1f : 0f;
                        break;
                    case Op.CmpGe:
                        regs[ins.Dst] = regs[ins.SrcA] >= regs[ins.SrcB] ? 1f : 0f;
                        break;
                    case Op.Jnz:
                        if (regs[ins.SrcA] != 0f)
                        {
                            ip = ins.Imm;
                            continue;
                        }
                        break;
                    case Op.End:
                        _running = false;
                        ip = code.Length; // exit loop but allow next Tick
                        continue;
                }

                ip++;
            }

            _ip = ip;
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        static float Saturate(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return 0f;
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        static float Sanitize(float value)
        {
            return (float.IsNaN(value) || float.IsInfinity(value)) ? 0f : value;
        }
    }
}
