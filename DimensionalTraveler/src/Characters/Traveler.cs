using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using DimensionalTraveler.Content.Pools;

namespace DimensionalTraveler.Characters;

[RegisterCharacter]
public sealed class Traveler
    : ModCharacterTemplate<TravelerCardPool, TravelerRelicPool, TravelerPotionPool>
{
    public override CharacterGender Gender => CharacterGender.Neutral;

    public override Color NameColor => new("57C7C9");

    public override int StartingHp => 70;

    public override int StartingGold => 99;

    public override float AttackAnimDelay => 0.15f;

    public override float CastAnimDelay => 0.25f;

    public override Color EnergyLabelOutlineColor => new("174E59FF");

    public override Color DialogueColor => new("123E48");

    public override VfxColor SpeechBubbleColor => VfxColor.Cyan;

    public override Color MapDrawingColor => new("57C7C9");

    public override Color RemoteTargetingLineColor => new("74DFD8FF");

    public override Color RemoteTargetingLineOutline => new("174E59FF");

    public override bool RequiresEpochAndTimeline => false;

    public override List<string> GetArchitectAttackVfx() =>
    [
        "vfx/vfx_attack_blunt",
        "vfx/vfx_attack_slash",
        "vfx/vfx_rock_shatter",
    ];
}