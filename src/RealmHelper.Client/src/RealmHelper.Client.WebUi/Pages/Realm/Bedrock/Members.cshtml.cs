﻿using Microsoft.AspNetCore.Mvc;

using RealmHelper.Client.Application.Services;
using RealmHelper.Client.Domain.Models.XboxLive;
using RealmHelper.Client.WebUi.Models;
using RealmHelper.Client.WebUi.Utils;
using RealmHelper.Realm.Bedrock.Abstractions.Models;
using RealmHelper.Realm.Bedrock.Abstractions.Services;

namespace RealmHelper.Client.WebUi.Pages.Realm.Bedrock;

public class Members : BedrockRealmModel
{
    private readonly IPeopleService _peopleService;

    [FromQuery(Name = "Page")] 
    public int MembersPage { get; set; } = 1;

    public (Profile, BedrockPlayer)[] OnlinePlayers { get; set; } = default!;
    public (Profile, ClubMember)[] OfflinePlayers { get; set; } = default!;

    public Page CurrentPage { get; set; } = default!;

    public Members(IPeopleService peopleService, IBedrockRealmService realmService, IClubService clubService)
        : base(realmService, clubService) =>
        _peopleService = peopleService;

    public override async Task<IActionResult> OnGet(long realmId, CancellationToken cancellationToken)
    {
        await base.OnGet(realmId, cancellationToken);

        var members = Club.Members.Length;

        var (start, end, pageSize) = PaginationHelper.GetIndexes(MembersPage, members);

        var xuids = new string[pageSize];
        for (var i = start; i < end; i++)
            xuids[i - start] = Club.Members[i].Xuid;

        var profilesTask = _peopleService.GetProfilesAsync(xuids, cancellationToken);

        var page = PaginationHelper.GetCurrentPage(end);

        if (page > 1)
        {
            var profiles = await profilesTask;

            OnlinePlayers = Array.Empty<(Profile, BedrockPlayer)>();
            OfflinePlayers = new (Profile, ClubMember)[pageSize];
            for (int i = start, j = 0; i < end; i++, j++)
                OfflinePlayers[j] = (profiles[j], Club.Members[i]);
        }
        else
        {
            var activitiesTask = RealmService.GetOnlinePlayersAsync(Realm.Id, cancellationToken);

            await Task.WhenAll(profilesTask, activitiesTask);

            var profiles = await profilesTask;

            var activity = await activitiesTask;
            var onlineLength = Math.Min(activity.Length, pageSize); // Sometimes offline players are returned, resulting in overflow

            OnlinePlayers = new (Profile, BedrockPlayer)[onlineLength];
            OfflinePlayers = onlineLength == pageSize
                ? Array.Empty<(Profile, ClubMember)>()
                : new (Profile, ClubMember)[Math.Min(members, pageSize - onlineLength)];
            for (int i = start, j = 0; i < end; i++, j++)
            {
                var presence = Club.Members[i];
                var profile = profiles[j];
                var player = j < onlineLength ? activity[j] : null;
        
                if (player is not null)
                    OnlinePlayers[j] = (profile, player);
                else
                    OfflinePlayers[j - onlineLength] = (profile, presence);
            }
        }

        CurrentPage = new Page
        {
            Number = page,
            Last = PaginationHelper.IsLastPage(end, members)
        };

        return Page();
    }
}