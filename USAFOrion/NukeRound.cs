namespace USAFOrion
{
    // utility class used to pass bomb parameters from object to object
    public class NukeRound
    {
        public float bombHeat ; // temperture increase per bomb detonation
        public float bombImpulse ; // kiloNewtons per bomb
        public float bombMass ; // mass of one bomb

        public float damageShock ;
                     // amount of shock damage infliced on the components of other vessels witihn damage zone

        public float damageZone ;
                     // range from detonation point in meters where other vessels will be subjected to damage shock. Orion ship is immune

        public float destroyMass ; // minimum mass of part that is immune from destruction if inside destroyZone

        public float destroyZone ;
                     // range from detonation point in meters where other vessels will be destroyed. Orion ship is immune

        public bool isBomb ; // false if this is the result of requesting a bomb from an empty magazine

        public NukeRound (bool initIsBomb,
                          float initBombMass,
                          float initBombImpulse,
                          float initBombHeat,
                          float initDamageZone,
                          float initDamageShock,
                          float initDestroyZone,
                          float initDestroyMass)
        {
            this.isBomb = initIsBomb ;
            this.bombMass = initBombMass ;
            this.bombImpulse = initBombImpulse ;
            this.bombHeat = initBombHeat ;
            this.damageZone = initDamageZone ;
            this.damageShock = initDamageShock ;
            this.destroyZone = initDestroyZone ;
            this.destroyMass = initDestroyMass ;
        }

        public NukeRound ()
        {
            this.isBomb = false ;
            this.bombMass = 0f ;
            this.bombImpulse = 0f ;
            this.bombHeat = 0f ;
            this.damageZone = 0f ;
            this.damageShock = 0f ;
            this.destroyZone = 0f ;
            this.destroyMass = 0f ;
        }
    }
}
