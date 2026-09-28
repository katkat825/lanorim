namespace Core.Characters
{
    // SRD 5.2.1 speed changes that are not a number of feet: Haste doubles, Slow halves, and
    // Hypnotic Pattern or Power Word Stun's fallback make it 0. a boon holds one of these; the
    // actor asks the three questions separately, because a Haste and a Slow together cancel
    public enum SpeedChange
    {
        None,
        Double,
        Half,
        Zero,
    }
}
