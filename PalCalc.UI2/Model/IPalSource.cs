using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.Model
{
    internal interface IPalSource
    {
        List<PalInstance> Filter(SaveGameDetails save);
    }

    internal class PlayerPalSource : IPalSource
    {
        public PlayerPalSource(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }

        public List<PalInstance> Filter(SaveGameDetails save) => save.OwnedPals.Where(p => p.OwnerPlayerId == PlayerId).ToList();
    }

    internal class GuildPalSource : IPalSource
    {
        public GuildPalSource(string guildId)
        {
            GuildId = guildId;
        }

        public string GuildId { get; }

        public List<PalInstance> Filter(SaveGameDetails save)
        {
            var playerIds = save.Guilds.Single(g => g.Id == GuildId).MemberIds;
            return save.OwnedPals.Where(p => playerIds.Contains(p.OwnerPlayerId)).ToList();
        }
    }

    internal class AllPalsPalSource : IPalSource
    {
        public List<PalInstance> Filter(SaveGameDetails save) => save.OwnedPals;
    }
}
