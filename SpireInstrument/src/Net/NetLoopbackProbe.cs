using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Multiplayer.Transport.ENet;
using MegaCrit.Sts2.Core.Platform;

namespace SpireInstrument.Net;

/// <summary>
/// 本地双人测试：**同一进程内起 1 台主机 + 2 个客户端**，把真实两玩家路径整条跑一遍。
///
/// 为什么是两个客户端：只连一个客户端时，主机"转发给其他客机"这一环根本没被执行。
/// 而两玩家（A 弹 → 主机 → B 听到）的关键恰恰是这一环 ——
/// 所以这里让 A 发带 ShouldBroadcast 的音符，断言 **B 也收到**。
///
/// 关于转发开关：主机的广播只发给 <c>readyForBroadcasting</c> 为真的 peer，
/// 该标志平时由游戏大厅设置。裸回环没有大厅，所以这里显式调用
/// <see cref="NetHostGameService.SetPeerReadyForBroadcasting"/>（大厅用的同一个公开接口）。
/// </summary>
public partial class NetLoopbackProbe : Node
{
    private const ushort Port = 47821;
    private const ulong ClientANetId = 2;
    private const ulong ClientBNetId = 3;
    private const double TimeoutSeconds = 12.0;

    private NetHostGameService? _host;
    private NetClientGameService? _clientA;
    private NetClientGameService? _clientB;
    private ENetClient? _enetA;
    private ENetClient? _enetB;

    /// <summary>主机收到的音符数（来自 A）。</summary>
    public int HostReceived { get; private set; }

    /// <summary>A 收到的音符数（主机定向发）。</summary>
    public int ClientAReceived { get; private set; }

    /// <summary>B 收到的音符数 —— **由主机转发而来，这一项证明两个玩家能互相听到**。</summary>
    public int ClientBReceived { get; private set; }

    public string Status { get; private set; } = "init";
    public bool Finished { get; private set; }
    public string Error { get; private set; } = "";

    private double _elapsed;
    private double _sendTimer;
    private ushort _seq;
    private bool _markedReady;

    public override void _Ready()
    {
        try
        {
            var version = PeerVersionInfo.LocalDefault();

            _host = new NetHostGameService(version);
            var hostError = _host.StartENetHost(Port, 3);
            if (hostError != null)
            {
                Error = $"host start failed: {hostError}";
                Status = "host-failed";
                Finished = true;
                return;
            }

            _host.RegisterMessageHandler<NoteMessage>((msg, sender) =>
            {
                HostReceived++;
                Status = $"host-got#{msg.Seq}";
            });

            _clientA = MakeClient(ClientANetId, out _enetA, n => ClientAReceived++);
            _clientB = MakeClient(ClientBNetId, out _enetB, n => ClientBReceived++);

            Status = "connecting";
            _ = ConnectAsync(_enetA, ClientANetId);
            _ = ConnectAsync(_enetB, ClientBNetId);
        }
        catch (Exception e)
        {
            Error = e.Message;
            Status = "exception";
            Finished = true;
        }
    }

    private NetClientGameService MakeClient(ulong netId, out ENetClient enet, Action<NoteMessage> onNote)
    {
        var service = new NetClientGameService(PeerVersionInfo.LocalDefault());
        enet = new ENetClient(service);
        service.Initialize(enet, PlatformType.None);
        service.RegisterMessageHandler<NoteMessage>((msg, sender) => onNote(msg));
        return service;
    }

    private async Task ConnectAsync(ENetClient? enet, ulong netId)
    {
        try
        {
            if (enet == null) return;
            var err = await enet.ConnectToHost(netId, "127.0.0.1", Port);
            if (err != null)
            {
                Error = $"client {netId} connect failed: {err}";
                Status = $"connect-failed-{netId}";
                Finished = true;
            }
        }
        catch (Exception e)
        {
            Error = e.Message;
            Status = "connect-exception";
            Finished = true;
        }
    }

    public override void _Process(double delta)
    {
        if (Finished) return;
        _elapsed += delta;

        try
        {
            _host?.Update();
            _clientA?.Update();
            _clientB?.Update();
        }
        catch (Exception e)
        {
            Error = e.Message;
            Status = "update-exception";
            Finished = true;
            return;
        }

        bool bothConnected = _clientA is { IsConnected: true } && _clientB is { IsConnected: true };

        // 两个客户端都握手成功后，按大厅的做法把 peer 标记为可转发 —— 否则主机不会转发
        if (bothConnected && _host is { IsConnected: true })
        {
            try
            {
                _host.SetPeerReadyForBroadcasting(ClientANetId);
                _host.SetPeerReadyForBroadcasting(ClientBNetId);
                // 幂等，可重复调用；只有主机侧真的登记了两个 peer 才算成功
                if (_host.ConnectedPeers.Count >= 2) _markedReady = true;
                Status = "ready-marked";
                GD.Print($"{SpireInstrumentMod.LogTag} loopback: peers marked ready " +
                         $"(peers={_host.ConnectedPeers.Count})");
            }
            catch (Exception e)
            {
                Error = "markReady: " + e.Message;
            }
        }

        _sendTimer += delta;
        if (_sendTimer >= 0.2 && bothConnected && _markedReady && _host is { IsConnected: true })
        {
            _sendTimer = 0;
            _seq++;

            try
            {
                // A 弹一个音：带 ShouldBroadcast ⇒ 主机收到后应转发给 B（这就是"两个玩家互相听到"）
                _clientA!.SendMessage(new NoteMessage
                {
                    Kind = NoteMessage.KindOn,
                    KeyIndex = 60,
                    Velocity = 100,
                    Seq = _seq,
                });

                // 主机定向发给 A：验证主机 → 客机方向
                _host.SendMessage(new NoteMessage
                {
                    Kind = NoteMessage.KindOn,
                    KeyIndex = 64,
                    Velocity = 90,
                    Seq = _seq,
                }, ClientANetId);
            }
            catch (Exception e)
            {
                Error = e.Message;
            }
        }

        if (_elapsed > TimeoutSeconds ||
            (HostReceived >= 2 && ClientAReceived >= 2 && ClientBReceived >= 2))
        {
            Finished = true;
            Status = "done";
        }
    }

    public string Describe()
        => $"host={HostReceived} clientA={ClientAReceived} clientB={ClientBReceived} " +
           $"status={Status} ready={_markedReady} " +
           $"connA={_clientA?.IsConnected} connB={_clientB?.IsConnected} err='{Error}'";

    public override void _ExitTree()
    {
        try { _enetA?.DisconnectFromHost(NetError.None, now: true); } catch { }
        try { _enetB?.DisconnectFromHost(NetError.None, now: true); } catch { }
        try { _host?.Disconnect(NetError.None, now: true); } catch { }
    }
}
