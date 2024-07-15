using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped
{
    public class GuildViewModel
    {
        public GuildViewModel(CachedSaveGame source, GuildInstance guild)
        {
            ModelObject = guild;

            AvailableMembers = new List<PlayerViewModel>
            {
                PlayerViewModel.Any
            };

            if (source != null)
            {
                AvailableMembers.AddRange(
                    guild.MemberIds
                        .Select(id => source.Players.Single(p => p.PlayerId == id))
                        .Select(player => new PlayerViewModel(player))
                        .ToList()
                );
            }

            Name = IsWildcard
                ? Translator.Translations[LocalizationCodes.LC_ANY_GUILD].Bind()
                : new HardCodedText(ModelObject.Name);
        }

        public GuildInstance ModelObject { get; }

        public bool IsWildcard => ModelObject == null;

        public List<PlayerViewModel> AvailableMembers { get; }

        public ILocalizedText Name { get; }

        public static readonly GuildViewModel Any = new GuildViewModel(null, null);
    }
}
