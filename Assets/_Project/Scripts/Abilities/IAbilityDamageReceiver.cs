namespace Wapawapa.Abilities
{
    public interface IAbilityDamageReceiver
    {
        void ApplyDamage(in AbilityDamage damage);
    }
}
