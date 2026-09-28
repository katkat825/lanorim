namespace Core.Characters
{
    public sealed partial class Actor
    {
        // --- borrowed shapes ------------------------------------------------------------------

        // the body the actor is wearing that is not its own, or null. while it is set the
        // physical scores, the armor class and the speed are the shape's; everything else is not
        public Shape Shape { get; private set; }

        public bool IsShifted => Shape != null;

        // what the shape covered up, kept so taking it off puts back exactly what was there
        readonly int[] _ownBody = new int[3];
        int _ownSpeed;

        public bool Assume(Shape shape)
        {
            if (shape == null) return false;

            // one borrowed body at a time: a second shape goes on over the actor, not the first
            Revert();

            for (int i = 0; i < Shape.Physical.Count; i++)
            {
                Ability ability = Shape.Physical[i];

                _ownBody[i] = Scores.Base(ability);
                Scores.SetBase(ability, shape.Score(ability));
            }

            _ownSpeed = Speed;
            Speed = shape.Speed;

            // the creature's training rides on as a boon rather than as Train, because Train never
            // demotes - and a thing that cannot be taken off cannot be worn. a skill the actor is
            // already trained in gains nothing: SRD 5.2.1 keeps the better of the two
            foreach (Skill skill in shape.Skills)
                if (TrainingIn(skill) == Training.Untrained)
                    Boons.Add(Boon.Of(new BoonSpec
                    {
                        Duration = Duration.Rest,
                        Flat = ProficiencyBonus,
                        Touches = Sways.Checks,
                        Skill = skill
                    },
                                      shape.Source, shape.Source));

            Shape = shape;
            return true;
        }

        // back to the actor's own body. returns the shape that came off, or null if none was on
        public Shape Revert()
        {
            Shape was = Shape;

            if (was == null) return null;

            Shape = null;

            for (int i = 0; i < Shape.Physical.Count; i++)
                Scores.SetBase(Shape.Physical[i], _ownBody[i]);

            Speed = _ownSpeed;

            Boons.EndFrom(was.Source);

            return was;
        }
    }
}
