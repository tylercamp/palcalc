using PalCalc.Solver.FImpl.AttrId;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests.FAttrId
{
    [TestClass]
    public class FSurgeryOpTests : FAttrIdTests
    {
        [TestMethod]
        public void CtorIdentity_None()
        {
            var op = FSurgeryOp.None;
            Assert.AreEqual(0, op.Store);
            Assert.IsFalse(op.IsGenderChange);
            Assert.IsFalse(op.IsAddPassive);
            Assert.IsFalse(op.IsReplacePassive);
        }

        [TestMethod]
        public void CtorIdentity_ChangeGender()
        {
            var maleGender = new FGender(Model.PalGender.MALE);
            var femaleGender = new FGender(Model.PalGender.FEMALE);
            
            var maleOp = FSurgeryOp.ChangeGender(maleGender);
            var femaleOp = FSurgeryOp.ChangeGender(femaleGender);

            Assert.IsTrue(maleOp.IsGenderChange);
            Assert.IsFalse(maleOp.IsAddPassive);
            Assert.IsFalse(maleOp.IsReplacePassive);
            Assert.AreEqual(maleGender.Store, maleOp.GenderChange_Value.Store);

            Assert.IsTrue(femaleOp.IsGenderChange);
            Assert.IsFalse(femaleOp.IsAddPassive);
            Assert.IsFalse(femaleOp.IsReplacePassive);
            Assert.AreEqual(femaleGender.Store, femaleOp.GenderChange_Value.Store);
        }

        [TestMethod]
        public void CtorIdentity_AddPassive()
        {
            var refSet = FPassiveSet.FromModel(db, [runner, swift, nimble]);
            
            var addRunnerOp = FSurgeryOp.AddPassive(refSet, new FPassive(db, runner));
            var addSwiftOp = FSurgeryOp.AddPassive(refSet, new FPassive(db, swift));
            var addNimbleOp = FSurgeryOp.AddPassive(refSet, new FPassive(db, nimble));

            Assert.IsFalse(addRunnerOp.IsGenderChange);
            Assert.IsTrue(addRunnerOp.IsAddPassive);
            Assert.IsFalse(addRunnerOp.IsReplacePassive);
            Assert.AreEqual(new FPassive(db, runner), addRunnerOp.AddPassive_NewPassive(refSet));

            Assert.IsFalse(addSwiftOp.IsGenderChange);
            Assert.IsTrue(addSwiftOp.IsAddPassive);
            Assert.IsFalse(addSwiftOp.IsReplacePassive);
            Assert.AreEqual(new FPassive(db, swift), addSwiftOp.AddPassive_NewPassive(refSet));

            Assert.IsFalse(addNimbleOp.IsGenderChange);
            Assert.IsTrue(addNimbleOp.IsAddPassive);
            Assert.IsFalse(addNimbleOp.IsReplacePassive);
            Assert.AreEqual(new FPassive(db, nimble), addNimbleOp.AddPassive_NewPassive(refSet));
        }

        [TestMethod]
        public void CtorIdentity_ReplacePassive()
        {
            var originalSet = FPassiveSet.FromModel(db, [runner, swift]);
            var refSet = FPassiveSet.FromModel(db, [nimble, lucky, legend]);

            var replaceRunnerWithNimble = FSurgeryOp.ReplacePassive(
                originalSet, new FPassive(db, runner),
                refSet, new FPassive(db, nimble)
            );

            var replaceSwiftWithLucky = FSurgeryOp.ReplacePassive(
                originalSet, new FPassive(db, swift),
                refSet, new FPassive(db, lucky)
            );

            Assert.IsFalse(replaceRunnerWithNimble.IsGenderChange);
            Assert.IsFalse(replaceRunnerWithNimble.IsAddPassive);
            Assert.IsTrue(replaceRunnerWithNimble.IsReplacePassive);
            Assert.AreEqual(
                expected: originalSet.ModelObjects.IndexOf(runner),
                actual: replaceRunnerWithNimble.ReplacePassive_RemovedIndex
            );
            Assert.AreEqual(
                expected: refSet.ModelObjects.IndexOf(nimble),
                actual: replaceRunnerWithNimble.ReplacePassive_AddedIndex
            );

            Assert.IsFalse(replaceSwiftWithLucky.IsGenderChange);
            Assert.IsFalse(replaceSwiftWithLucky.IsAddPassive);
            Assert.IsTrue(replaceSwiftWithLucky.IsReplacePassive);
            Assert.AreEqual(
                expected: originalSet.ModelObjects.IndexOf(swift),
                actual: replaceSwiftWithLucky.ReplacePassive_RemovedIndex
            );
            Assert.AreEqual(
                expected: refSet.ModelObjects.IndexOf(lucky),
                actual: replaceSwiftWithLucky.ReplacePassive_AddedIndex
            );
        }

        [TestMethod]
        public void CtorIdentity_ReplacePassive_IndexValidation()
        {
            var originalSet = FPassiveSet.FromModel(db, [random1, swift, nimble, lucky]);
            var refSet = FPassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]);

            var replaceLastOriginalWithLastRef = FSurgeryOp.ReplacePassive(
                originalSet, new FPassive(db, lucky),
                refSet, new FPassive(db, workaholic)
            );

            Assert.IsTrue(replaceLastOriginalWithLastRef.IsReplacePassive);
            Assert.AreEqual(
                expected: originalSet.ModelObjects.IndexOf(lucky),
                actual: replaceLastOriginalWithLastRef.ReplacePassive_RemovedIndex
            );
            Assert.AreEqual(
                expected: refSet.ModelObjects.IndexOf(workaholic),
                actual: replaceLastOriginalWithLastRef.ReplacePassive_AddedIndex
            );
        }
    }
}