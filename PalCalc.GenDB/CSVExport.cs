using PalCalc.GenDB.GameDataReaders;
using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB
{
    // some manually use Pal Calc's data as a reference, provide it in CSV form for those folks

    class CSVWriter : IDisposable
    {
        private bool wroteColumns;
        private StreamWriter writer;
        public CSVWriter(Stream outputStream)
        {
            wroteColumns = false;

            writer = new StreamWriter(outputStream);
        }

        public void Write(List<(string, object)> values)
        {
            if (!wroteColumns)
            {
                writer.WriteLine(string.Join(",", values.Select(kvp => kvp.Item1)));
                wroteColumns = true;
            }

            writer.WriteLine(string.Join(",", values.Select(kvp => kvp.Item2)));
        }

        public void Dispose()
        {
            writer.Dispose();
        }
    }

    internal class CSVExport
    {
        private const string ExportLocale = "en";

        public static void Write(
            string outDir,
            PalDB db,
            List<UHumanInfo> humans,
            Dictionary<string, Dictionary<string, string>> humanLocalizations
        )
        {
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, true);
            
            Directory.CreateDirectory(outDir);

            using (var f = File.OpenWrite($"{outDir}/pals.csv"))
            using (var palWriter = new CSVWriter(f))
            {
                foreach (var pal in db.Pals.OrderBy(p => p.Id.PalDexNo).ThenBy(p => p.Id.IsVariant))
                    palWriter.Write([
                        ("Name", pal.LocalizedNames?.GetValueOrDefault(ExportLocale) ?? pal.Name),
                        ("CodeName", pal.InternalName),
                        ("PalDexNo", pal.Id.PalDexNo),
                        ("IsVariant", pal.Id.IsVariant),
                        ("BreedPower", pal.BreedingPower),
                        ("MaleProbability", (int)Math.Round(db.BreedingGenderProbability[pal][PalGender.MALE] * 100)),
                        ("Price", pal.Price),
                        ("IndexOrder", pal.InternalIndex),
                        ("MinWildLevel", pal.MinWildLevel),
                        ("MaxWildLevel", pal.MaxWildLevel)
                    ]);
            }

            using (var f = File.OpenWrite($"{outDir}/passives.csv"))
            using (var passiveWriter = new CSVWriter(f))
                foreach (var passive in db.PassiveSkills.OrderByDescending(p => p.IsStandardPassiveSkill).ThenBy(p => p.InternalName))
                    passiveWriter.Write([
                        ("Name", passive?.LocalizedNames?.GetValueOrDefault(ExportLocale) ?? passive.Name),
                        ("CodeName", passive.InternalName),
                        ("Rank", passive.Rank),
                        ("IsPalPassive", passive.IsStandardPassiveSkill)
                    ]);

            using (var f = File.OpenWrite($"{outDir}/attacks.csv"))
            using (var attackWriter = new CSVWriter(f))
                foreach (var attack in db.ActiveSkills.OrderBy(a => a.InternalName))
                    attackWriter.Write([
                        ("Name", attack.LocalizedNames?.GetValueOrDefault(ExportLocale) ?? attack.Name),
                        ("CodeName", attack.InternalName),
                        ("Element", attack.Element.Name),
                        ("Power", attack.Power),
                        ("CooldownSeconds", attack.CooldownSeconds),
                        ("CanInherit", attack.CanInherit),
                        ("HasSkillFruit", attack.HasSkillFruit),
                    ]);

            using (var f = File.OpenWrite($"{outDir}/humans.csv"))
            using (var humanWriter = new CSVWriter(f))
            {
                foreach (var human in humans)
                {
                    humanWriter.Write([
                        ("Name", humanLocalizations[ExportLocale].GetValueOrDefault(human.OverrideNameTextID)),
                        ("CodeName", human.InternalName)
                    ]);
                }
            }
        }
    }
}
