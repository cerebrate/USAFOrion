#region using

using System.Collections.Generic ;

using UnityEngine ;

#endregion

namespace USAFOrion
{
    // Constants
    public enum BombTypeEnum
    {
        Bomb3_5MN,
        Bomb2MN,
        Bomb80MN,
        Bomb400MN,
        Bomb0_88MN,
        Bomb10MN,
        Bomb20MN
    };

    //=======
    // Main module for all Orion fuel tanks
    public class OrionMagazine : Part
    {
        [KSPField]
        public float bombHeat = 150f ; // temperture increase per bomb detonation

        [KSPField (guiActive = true, guiName = "kN per bomb")]
        public float bombImpulse = 1000f ; // kiloNewtons per bomb

        [KSPField]
        public float bombMass = 0.079f ; // mass of one bomb

        [KSPField (guiActive = true, guiName = "Bombs", guiUnits = "B")]
        public int bombStockpile = 600 ;
                   // current number of bombs, will be updated by OrionPusherPlate as bombs are used

        // OrionMagazine part.cfg
        [KSPField (guiActive = true, guiName = "Type")]
        public string bombTitle = "3.5MN Pulse" ; // title that appears in pop-up menu for activating magazines

        [KSPField]
        public float damageShock = 100f ;
                     // amount of shock damage infliced on the components of other vessels witihn damage zone

        [KSPField (guiActive = true, guiName = "Damage Zone")]
        public float damageZone = 500f ;
                     // range from detonation point in meters where other vessels will be subjected to damage shock. Orion ship is immune

        [KSPField]
        public float destroyMass = 100f ; // miminum mass of part that will survive even if caught inside destroyZone

        [KSPField (guiActive = true, guiName = "Destroy Zone")]
        public float destroyZone = 250f ;
                     // range from detonation point in meters where other vessels will be destroyed. Orion ship is immune

        [KSPField]
        public float dryMass = 1f ; // mass of entire magazine with zero bombs

        public int maxBombStockpile ; // number of bombs when magazine is filled to capacity
//		private float virtualMass = 0f;		// virtual mass of magazine

        private int oldBombStockpile ; // used to detect when number of bombs has changed
        private Vessel originalVessel ; // used to detect when magazine has become detached from vessel
        private float specificImpulse ;

        private readonly List<ConfigurableJoint> atomicClampList = new List<ConfigurableJoint> () ;
                                                 // list of atomic clamps for later removal

        private readonly Part parentEngine = null ; // engine used to hold the virtal mass of the magazine
        // Note about magazines and mass
        // As it turns out, the Kerbal Space Program engine does not work very well with objects that are
        // dense, that is, with a high mass and low volume. The individual magazines have a smaller volume than most
        // fuel tanks, and masses that can get up to 200 tons.
        // What happens is that stacks of the heavier magazines will "dance around" even if the vessel is
        // sitting on the launch pad. They may hit something and explode, or break the physics engine
        // resulting in the rocket and the magazines floating in mid air in a contorted position.
        // 
        // The first attempt to fix this was with "atomic clamps." When flight starts, the magazine finds all the
        // Orion engines inside the same vessel, and creates a ConfigurableJoint with all motion locked.
        // If the magazine is removed or jettisoned, the atomic clamp is deleted.
        // This is only a partial fix. The larger magazines still dance a bit
        // todo Create an atomic clamp if the magazine is added to a vessel during a flight, to allow refueling 
        //
        // The second attempt to fix this is with "virtual mass". The mass of the magazines is removed from the magazine
        // and added to the mass of the engine. The engine has more volume.


        //=======
        protected void addAtomicClamps ()
        {
            // if the mass difference between two objects is larger than x10, they chatter around
            // OrionMagazines are very massive, and very explosive
            // Find all the orion engines and attach them to this magazine with rigid joints.
            // I tried making joints between magazines and whatever they were attached to but it didn't work.

            ConfigurableJoint atomicClamp ;

            foreach (var aPart in this.vessel.parts)
            {
                if (aPart.ClassName == "OrionPusherPlate")
                {
                    atomicClamp = aPart.gameObject.AddComponent<ConfigurableJoint> () ;
                    atomicClamp.connectedBody = this.rigidbody ;

                    atomicClamp.anchor = new Vector3 (0,
                                                      0,
                                                      Vector3.Distance (aPart.transform.position,
                                                                        this.transform.position)) ;
                    atomicClamp.axis = new Vector3 (0, 0, 1) ;
                    atomicClamp.xMotion = ConfigurableJointMotion.Locked ;
                    atomicClamp.yMotion = ConfigurableJointMotion.Locked ;
                    atomicClamp.zMotion = ConfigurableJointMotion.Locked ;
                    atomicClamp.angularXMotion = ConfigurableJointMotion.Locked ;
                    atomicClamp.angularYMotion = ConfigurableJointMotion.Locked ;
                    atomicClamp.angularZMotion = ConfigurableJointMotion.Locked ;

                    Debug.Log (">>> " + this.name + " #" + this.uid + " found " + aPart.name + " #" + aPart.uid +
                               ", attaching atomic clamp") ;

                    this.atomicClampList.Add (atomicClamp) ; // cache atomic clamp for later removal
                }
            }
        }

        //=======
        protected void removeAtomicClamps ()
        {
            // remove atomic clamps, otherwise when you try to undock or decouple a magazine, it will still be attached by an invisible bond

            foreach (var atomicClamp in this.atomicClampList)
            {
                Debug.Log (">>> " + this.name + " #" + this.uid + " destroying atomic clamp") ;

                DestroyImmediate (atomicClamp) ;
            }
            this.atomicClampList.Clear () ;
        }

        //=======
        protected void initVirtualMass ()
        {
            // find parent engine to store virtual mass in
            // TODO handle the case where vessel has multiple Orion engines
//			this.parentEngine = null;
//			foreach (Part aPart in this.vessel.parts)
//			{
//				if (aPart.ClassName == "OrionPusherPlate") {
//					this.parentEngine = aPart;
//					break;
//				}
//			}
        }

        //=======
        protected void activateVirtualMass ()
        {
            // todo replace 1f with 0.01 * this.mass, in case magazine is under 1 ton
            // shift mass from magazine to engine
//			if (this.parentEngine != null) {
//				this.virtualMass = this.mass - 1f;
//				this.mass = 1f;
//				this.parentEngine.mass += this.virtualMass;
//			}
        }

        //=======
        protected void deactivateVirtualMass ()
        {
            // shift mass from engine to magazine
//			if (this.parentEngine != null) {
//				this.parentEngine.mass -= this.virtualMass;
//				this.mass = this.virtualMass + 1f;
//				this.virtualMass = 0;
//			}
        }

        //=======
        // alter the mass to reflect the current number of bombs contained in magazine
        protected void upMagazineMass ()
        {
            if (this.oldBombStockpile != this.bombStockpile)
            {
                // only do this CPU intensive stuff if necessary
                this.oldBombStockpile = this.bombStockpile ;

                if (this.parentEngine != null)
                {
                    // do virtual mass
//					float oldVirtualMass = this.virtualMass;
//			        this.virtualMass = ((float)this.bombStockpile * this.bombMass) + this.dryMass - 1f;	// recalculate virual mass
//					float virtualMassDelta = this.virtualMass - oldVirtualMass;
//					this.parentEngine.mass += virtualMassDelta;	// add new virtual mass delta to engine
                    this.deactivateVirtualMass () ;
                    this.mass = (this.bombStockpile * this.bombMass) + this.dryMass ;
                    this.activateVirtualMass () ;

//					this.parentEngine.mass -= this.virtualMass;	// remove old virtual mass from engine
//			        this.virtualMass = ((float)this.bombStockpile * this.bombMass) + this.dryMass - 1f;	// recalculate virual mass
//					this.parentEngine.mass += this.virtualMass;	// add new virtual mass to engine
                }
                else
                {
                    // no engine, do real mass
                    this.mass = (this.bombStockpile * this.bombMass) + this.dryMass ; // recalculate real mass
                }
            }
        }

        //=======
        protected override void onFlightStart ()
        {
            this.specificImpulse = (this.bombImpulse * 1000f) / (9.81f * (this.bombMass * 1000f)) ;

            this.originalVessel = this.vessel ; // used later to see if still attached.
            this.addAtomicClamps () ;

            this.oldBombStockpile = this.bombStockpile ;
            this.initVirtualMass () ;
            this.activateVirtualMass () ;

            base.onFlightStart () ;
        }

        //=======
        protected override void onPartLoad ()
        {
            base.onPartLoad () ;

            this.upMagazineMass () ;
        }

        //=======
        public NukeRound requestNuke ()
        {
            if (this.bombStockpile > 0)
            {
                this.oldBombStockpile = this.bombStockpile ;
                this.bombStockpile = this.bombStockpile - 1 ;
                return new NukeRound (true,
                                      this.bombMass,
                                      this.bombImpulse,
                                      this.bombHeat,
                                      this.damageZone,
                                      this.damageShock,
                                      this.destroyZone,
                                      this.destroyMass) ;
            }
            return new NukeRound () ; // no nuke
        }

        //=======
        public bool addNuke ()
        {
            if (this.bombStockpile < this.maxBombStockpile)
            {
                this.bombStockpile = this.bombStockpile + 1 ;
                return true ;
            }
            return false ;
        }

        //=======
        protected override void onPartFixedUpdate ()
        {
            this.upMagazineMass () ;

            // todo make this handle the case if the magazine is newly attached to a different vessel, for refueling
            if (this.originalVessel.id != this.vessel.id)
            {
                // if this magazine is no longer attached to original vessel
                Debug.Log (">>> OrionMagazine.onPartFixedUpdate: send DoInventoryEvent") ;
                this.originalVessel.rootPart.SendEvent ("DoInventoryEvent") ;
                this.originalVessel = this.vessel ;

                this.removeAtomicClamps () ;
                this.addAtomicClamps () ;

                this.deactivateVirtualMass () ;
                this.initVirtualMass () ;
                this.activateVirtualMass () ;
            }

            base.onPartFixedUpdate () ;
        }

        //=======
        public override void onFlightStateSave (Dictionary<string, KSPParseable> partDataCollection)
        {
            // save current stockpile upon game save
            partDataCollection.Add ("bombs", new KSPParseable (this.bombStockpile, KSPParseable.Type.INT)) ;
        }

        //=======
        public override void onFlightStateLoad (Dictionary<string, KSPParseable> parsedData)
        {
            this.maxBombStockpile = this.bombStockpile ;

            // load current stockpile from game save
            if (parsedData.ContainsKey ("bombs"))
                this.bombStockpile = int.Parse (parsedData["bombs"].value) ;
        }
    }
}
