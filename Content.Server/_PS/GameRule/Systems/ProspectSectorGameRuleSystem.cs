using Content.Server._NF.Bank;
using Content.Server._NF.GameRule;
using Content.Server._PS.GameRule.Components;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._PS.GameRule.Systems;

public sealed class ProspectSectorGameRuleSystem : GameRuleSystem<ProspectSectorGameRuleComponent>
{

    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    // A list of player bank account information stored by the controlled character's entity.
    [ViewVariables]
    private Dictionary<EntityUid, NfAdventureRuleSystem.PlayerRoundBankInformation> _players = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawningEvent);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetachedEvent);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _playerManager.PlayerStatusChanged += PlayerManagerOnPlayerStatusChanged;
    }

    private void OnPlayerSpawningEvent(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Player.AttachedEntity is { Valid: true } mobUid)
        {
            EnsureComp<CargoSellBlacklistComponent>(mobUid);

            // Store player info with the bank balance - we have it directly, and BankSystem won't have a cache yet.
            if (!_players.ContainsKey(mobUid))
                _players[mobUid] = new NfAdventureRuleSystem.PlayerRoundBankInformation(ev.Profile.BankBalance, MetaData(mobUid).EntityName, ev.Player.UserId);
        }
    }

    private void OnPlayerDetachedEvent(PlayerDetachedEvent ev)
    {
        if (ev.Entity is not { Valid: true } mobUid)
            return;

        if (_players.ContainsKey(mobUid))
        {
            if (_players[mobUid].UserId == ev.Player.UserId &&
                _bank.TryGetBalance(ev.Player, out var bankBalance))
            {
                _players[mobUid].EndBalance = bankBalance;
            }
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _players.Clear();
    }

    private void PlayerManagerOnPlayerStatusChanged(object? _, SessionStatusEventArgs e)
    {
        // Treat all disconnections as being possibly final.
        if (e.NewStatus != SessionStatus.Disconnected ||
            e.Session.AttachedEntity == null)
            return;

        var mobUid = e.Session.AttachedEntity.Value;
        if (_players.ContainsKey(mobUid))
        {
            if (_players[mobUid].UserId == e.Session.UserId &&
                _bank.TryGetBalance(e.Session, out var bankBalance))
            {
                _players[mobUid].EndBalance = bankBalance;
            }
        }
    }

}
