using UnityEngine;

namespace FracturedMind.AI
{
    /// <summary>
    /// Simple MonoBehaviour that owns one VM instance and ticks it in fixed update.
    /// </summary>
    public sealed class NpuAgentRunner : MonoBehaviour
    {
        [Header("Input signals")]
        [Range(-1f, 1f)] [SerializeField] float dotToTarget = 1f;
        [Range(0f, 1f)] [SerializeField] float distanceNorm = 0f;
        [Range(0f, 1f)] [SerializeField] float lightLevel = 1f;
        [Range(0f, 1f)] [SerializeField] float motionAmount = 0f;

        [Header("Program settings")]
        [SerializeField] bool autoBuildSampleProgram = true;
        [SerializeField] int registerCount = 8;
        [SerializeField] int memorySize = 1;

        [Header("Debug output")]
        [SerializeField] float currentBelief;
        [SerializeField] float alertFlag;

        NpuVm _vm;
        NpuVm.Instruction[] _program;

        void OnEnable()
        {
            if (autoBuildSampleProgram)
            {
                _program = NpuProgramBuilder.BuildSampleVisionDetector();
            }

            _vm = new NpuVm(_program ?? new NpuVm.Instruction[0], registerCount, memorySize);
        }

        void OnDisable()
        {
            _vm = null;
        }

        void FixedUpdate()
        {
            if (_vm == null) return;

            // Feed inputs into expected registers.
            _vm.SetRegister(0, dotToTarget);
            _vm.SetRegister(1, distanceNorm);
            _vm.SetRegister(2, lightLevel);
            _vm.SetRegister(3, motionAmount);

            _vm.Tick(Time.fixedDeltaTime);

            currentBelief = _vm.ReadMem(0);
            alertFlag = _vm.GetRegister(6);
        }
    }
}
