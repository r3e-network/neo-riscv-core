using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.VM;
using Neo.VM.Types;

namespace Neo.UnitTests.VMT
{
    [TestClass]
    public class UT_PureNeoVmBenchmarks
    {
        [TestMethod]
        public void TestPureNeoVmExecutesTrivialScript()
        {
            var engine = new ExecutionEngine(JumpTable.Default);
            engine.LoadScript(new Script(new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET }, true), -1, 0);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(1, engine.ResultStack.Pop<Integer>().GetInteger());
        }

        [TestMethod]
        public void TestPureNeoVmReturnsFullResultStack()
        {
            var engine = new ExecutionEngine(JumpTable.Default);
            engine.LoadScript(new Script(new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.PUSH2, (byte)OpCode.RET }, true), -1, 0);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(2, engine.ResultStack.Count);
            Assert.AreEqual(2, engine.ResultStack.Pop<Integer>().GetInteger());
            Assert.AreEqual(1, engine.ResultStack.Pop<Integer>().GetInteger());
        }
    }
}
