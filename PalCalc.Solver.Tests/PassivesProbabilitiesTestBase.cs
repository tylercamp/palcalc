using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests
{
    public class PassivesProbabilitiesTestBase
    {
        protected PalDB db = PalDB.LoadEmbedded();

        protected PassiveSkill Runner => "Runner".ToPassive(db);
        protected PassiveSkill Swift => "Swift".ToPassive(db);
        protected PassiveSkill Nimble => "Nimble".ToPassive(db);
        protected PassiveSkill Lucky => "Lucky".ToPassive(db);

        private int irrelevantId = 0;
        protected PassiveSkill Irrelevant => new($"Irrelevant {irrelevantId}", $"Irrelevant {irrelevantId}", 1);

        protected float SubCombinationProbability(int numAvail, int numDesired, int numChosen) =>
            Probabilities.Passives.Choose(numAvail - numDesired, numChosen - numDesired) / (float)Probabilities.Passives.Choose(numAvail, numChosen);


        // if we want exactly 2 passives and there are 4 total available, we'll
        // need the chance of getting exactly 2 direct passives
        //
        // BUT if there are only 2 passives, then a roll for 2, 3, or 4 direct passives
        // will all work, since no more than 2 will actually be inherited
        protected float PassiveProbabilityDirectUpTo(int numAvailable, int numRequired)
        {
            if (numRequired >= numAvailable)
                return GameConstants.PassiveProbabilityAtLeastN[numRequired];
            else
                return GameConstants.PassiveProbabilityDirect[numRequired];
        }
    }
}
