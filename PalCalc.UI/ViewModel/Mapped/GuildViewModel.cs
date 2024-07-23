using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped
{
    public class GuildViewModel
    {
        private static ILogger logger = Log.ForContext<GuildViewModel>();

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
                        .Select(id =>
                        {
                            var player = source.Players.SingleOrDefault(p => p.PlayerId == id);
                            if (player == null)
                            {
                                // (seems to be some quirk of game save data, e.g. deleted player no longer in character list but guild members
                                //  list left unchanged?)
                                logger.Warning("Unable to find details for player with ID {id} in guild {guildName}, skipping", id, guild.Name);
                            }
                            return player;
                        })
                        .Where(p => p != null)
                        .Select(player => new PlayerViewModel(player))
                        .ToList()
                );
            }

            Name = IsWildcard
                ? LocalizationCodes.LC_ANY_GUILD.Bind()
                : new HardCodedText(ModelObject.Name);
        }

        public GuildInstance ModelObject { get; }

        public bool IsWildcard => ModelObject == null;

        public List<PlayerViewModel> AvailableMembers { get; }

        public ILocalizedText Name { get; }

        public static readonly GuildViewModel Any = new GuildViewModel(null, null);
    }
}
