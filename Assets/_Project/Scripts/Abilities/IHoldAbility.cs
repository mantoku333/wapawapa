namespace Wapawapa.Abilities
{
    public interface IHoldAbility
    {
        void BeginHold(in AbilityContext context);
        void UpdateHold(in AbilityContext context);
        void EndHold(in AbilityContext context, bool activate);
    }
}
