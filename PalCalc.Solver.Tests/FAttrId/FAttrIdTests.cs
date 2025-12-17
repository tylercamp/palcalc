using PalCalc.Model;
using PalCalc.Solver.FImpl.AttrId;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests.FAttrId
{
    public class FAttrIdTests
    {
        protected PalDB db;

        protected PassiveSkill runner;
        protected PassiveSkill swift;
        protected PassiveSkill nimble;
        protected PassiveSkill lucky;
        protected PassiveSkill legend;
        protected PassiveSkill workaholic;

        protected PassiveSkill random1 = new RandomPassiveSkill();
        protected PassiveSkill random2 = new RandomPassiveSkill();
        protected PassiveSkill random3 = new RandomPassiveSkill();
        protected PassiveSkill random4 = new RandomPassiveSkill();
        protected PassiveSkill random5 = new RandomPassiveSkill();
        protected PassiveSkill random6 = new RandomPassiveSkill();

        [TestInitialize]
        public void InitDB()
        {
            db = PalDB.LoadEmbedded();

            runner = "Runner".ToStandardPassive(db);
            swift = "Swift".ToStandardPassive(db);
            nimble = "Nimble".ToStandardPassive(db);
            lucky = "Lucky".ToStandardPassive(db);
            legend = "Legend".ToStandardPassive(db);
            workaholic = "Workaholic".ToStandardPassive(db);
        }
    }
}
