#region using

using System ;
using System.Collections.Generic ;

using UnityEngine ;

#endregion

namespace USAFOrion
{
    // Constants
    public enum MoveState
    {
        Motionless,
        BombFlight,
        NeutralToCompress,
        CompressToNeutral,
        NeutralToExtend,
        ExtendToNeutral
    };

    //=======
    // Main module for all Orion engines
    public class OrionPusherPlate : Part
    {
        private NukeRound aNukeRound ; // current bomb being detonated
        private VInfoBox bombCounter ; // HUD of bomb inventory
        private Transform bombModel ; // Transform used to alter position of bomb
        private bool bombObstacle ;
        private float bombOffset ; // when bomb animation has finished

        [KSPField]
        public float bombStartY = -5.0f ; // distance between vessel center and start postion of bomb, negative number

//		private GameObject plasmaPlume;
//		private float plasmaPlumeStopTime = 0f;
//		private int dockedShipID = 0;
//		public int maxDockedShipID;
//		private Stack<int> dockedShipIDStack = new Stack<int>();
        private float bombTravelDistance ;
        private bool canFire = true ; // Is plate in bomb firing position?
        private float detonationDelay = -1f ; // current delay between detonations, set by throttle

        [KSPField]
        public float detonationDeltaY = -23f ;
                     // distance between bottom of pusher plate and detonation point in meters, negative number

        [KSPField]
        public float detonationMaxDelay = 3.5f ; // throttle 30% to 0.1%

        // bomb detonation delay times
        [KSPField]
        public float detonationMinDelay = 0.8f ; // throttle 100% to 60%, should not be less than plateCycleTime

        [KSPField]
        public float detonationStdDelay = 1.85f ; // throttle 60% to 30%

        public float detonationY ;
                     // distance between vessel center and detonation point, = plateBottomY + detonationDeltaY

        private Transform engineModel ; // Transform used to alter position of pusher plate

        [KSPField]
        public string engineModelName = "USAF10MeterOrionEngine2" ; // name of the Unity 3D model

        private Transform entireModel ; // Transform used to set position of explosion
        private FXGroup explosionGroup ; // explosion special effect  

        [KSPField]
        public float explosionGroupPower = 10f ; // power setting for FXGroup of bomb blast

        [KSPField]
        public float fxmongerHowHard = 1000f ; // howHard setting for FXMonger.Explode(), for bomb blast

        private int lastNumBombs ; // cache, only update number of bomb indicator if the number has actually changed
        private float lastThrottle ; // cache, only print throttle change msg if throttle has changed
        public List<String> magazineKeys = new List<String> () ;
        private int maxMagazine ;
//		private LineRenderer debugLine = null;
        private float newBombYStart ;
        private float nextFire ; // next allowed bomb detonatation time, used to delay bombs launched by Z key
        public NukeManager nukeManager ; // in charge of supplying nukes from various magazines in a balanced fashion

        [KSPField]
        public float plateBottomY = -12.5655f ;
                     // distance between vessel center and bottom of pusher plate, negative number

        private float plateCycleSpeed ;
                      // how fast plate moves in meters per second, used to time the segments of pusher plate animation

        [KSPField]
        public float plateCycleTime = 0.8f ; // time in seconds taken by entire animation cycle

        // Note this engine looks into the attached vessel to find other parts of module type "OrionMagazine", for fuel purposes.

        [KSPField]
        public float plateOffsetCompressed = 4.88f ;
                     // distance pusher plate moves upward above neutral point in animation

        [KSPField]
        public float plateOffsetExtended = 0.4f ;
                     // distance pusher plate moves downward below neutral point when recoiling 

        [KSPField]
        public float plateTopY = -4.4447f ; // distance between vessel center and top of pusher plate, negative number

        private MoveState pusherMoveState = MoveState.Motionless ;
                          // what part of a pusher cycle the animiation is currently in	

        private Guid pusherOwnerGuid ; // id of vessel this plate is part of. Use to prevent bombs from affecting self
        private Transform pusherPlate ; // Transform used to alter position of pusher plate
        private float quinterCycleStart ; // start time for current 1/5 cycle

        [KSPField]
        public string subModelBombName = "obj_pulseUnit" ; // name of the bomb sub model in the Unity 3D model

        [KSPField]
        public string subModelPlateName = "obj_pusherplate" ;
                      // name of the pusher plate sub model in the Unity 3D model

        private float theDuration ; // time it takes for plate to move through one-quarter of pusher cycle

        private readonly Dictionary<string, string> bombFilterActivateEvents = new Dictionary<string, string> () ;
                                                    // list of activate events for bomb filter pop-ups

        private readonly Dictionary<string, string> bombFilterDeactivateEvents = new Dictionary<string, string> () ;
                                                    // list of deactivate events for bomb filter pop-ups

        private readonly Dictionary<String, String> magazineTitles = new Dictionary<String, String> () ;
        private readonly int maxActionSlots = 10 ; // must match number of activate/deactivate KSPActions
        private readonly int maxEventSlots = 10 ; // must match number of activate/deactivate KSPEvents

        private readonly List<Transform> pushAnimationTransforms = new List<Transform> () ;
                                         // parts attached to pusher plate, are animated with the plate

        private readonly Dictionary<float, float> sineRate = new Dictionary<float, float>
            // table for incrementing with sine-wave spacing
                                                             {
                                                                 {0.1f, 1.56f},
                                                                 {0.2f, 1.53f},
                                                                 {0.3f, 1.45f},
                                                                 {0.4f, 1.34f},
                                                                 {0.5f, 1.19f},
                                                                 {0.6f, 1.02f},
                                                                 {0.7f, 0.82f},
                                                                 {0.8f, 0.60f},
                                                                 {0.9f, 0.37f},
                                                                 {1.0f, 0.12f}
                                                             } ;

        // Note about pusher plate animation:
        // Segment 0 (MoveState.Motionless) is the motionless state, when rocket is not accelerating.
        // Pulse unit (bomb) travels from mouth of cannon, through center of pusher plate, reaching detonation point. There it teleports back to cannon.
        // Detonation point is at z = plateBottomY + detonationDeltaY
        // (Segment 1, MoveState.BombFight)
        // Plate starts in Neutral position. Upon bomb detonation, plate moves upward plateOffsetCompressed meters. (Segment 2, MoveState.NeutralToCompress)
        // At maximum upward offset, plate reverses direction and moves downward to Neutral position. (Segment 3, MoveState.CompressToNeutral)
        // When Neutral is reached, the plate continues moving downward plateOffsetExtended meters. (Segment 4, MoveState.NeutralToExtend)
        // At maximum downward offset, plate reverses direction and moves upwards to Neutral postion. (Segment 5, MoveState.ExtendToNeutral)
        // When Neutral postion is reached, that is the end of the animation.
        // 
        // The entire animation takes plateCycleTime seconds.
        // The duration is divided up among the five segments of the animation. The amount of duration alloted to each
        // segment is proportional to the distance the plate travels in that segment. In other words, the plate's
        // meters per second rate is constant through the entire animation.
        // The rate is stored in plateCycleSpeed
        //
        // Yes, in reality bomb will have a velocity different from the plate speed, but the difference is not worth the
        // headache of calculating the deltas. The USAF 10 meter Orion had a bomb speed of 90 m/s, with detonations every 0.8 sec to every 1.5 sec.
        //
        // The segment of the animation currently being performed is stored in pusherMoveState. 
        // If this is equal to MoveState.Motionless, there is no animation underway.

        //=======
        // Initial call in VAB when picking up part
        // Also called when part comes into range of focussed ship (<2.5km)
        // And at initial part loading at program start
        protected override void onPartAwake ()
        {
            base.onPartAwake () ;

            // painstakingly compile a list of all parts in the entire game of type "OrionMagazine"
            foreach (var eachPart in PartLoader.LoadedPartsList)
            {
                var thePart = eachPart.partPrefab ;
                if (thePart != null)
                {
                    if (thePart.ClassName == "OrionMagazine")
                    {
                        var theMagazine = thePart as OrionMagazine ;
                        this.magazineKeys.Add (eachPart.name) ;
                        this.magazineTitles.Add (eachPart.name, theMagazine.bombTitle) ;
                    }
                }
            }
            this.maxMagazine = this.magazineKeys.Count ;

            // dynamically create the engine pop-up menu, using the list of magazine types in the game.

            // deactive them all
            foreach (var theAction in this.Actions)
                theAction.active = false ;

            // fill slots with magazine types
            var slotCounter = 0 ;
            string activateActionName ;
            string deactivateActionName ;
            foreach (var magKey in this.magazineKeys)
            {
                activateActionName = "ActionGroupActivateMag" + slotCounter.ToString ("00") ;
                deactivateActionName = "ActionGroupDeactivateMag" + slotCounter.ToString ("00") ;
                this.Actions[activateActionName].guiName = "Start " + this.magazineTitles[magKey] ;
                this.Actions[deactivateActionName].guiName = "Kill " + this.magazineTitles[magKey] ;
                this.Actions[activateActionName].active = true ;
                this.Actions[deactivateActionName].active = true ;

                slotCounter = slotCounter + 1 ;
                if (slotCounter >= this.maxActionSlots)
                    break ;
            }
        }

        //=======
        // Called at beginning of flight scene after onPartStart()
        // also when part comes in range of focussed ship (<2.5km) after onPartStart()
        protected override void onFlightStart ()
        {
            this.pusherOwnerGuid = this.vessel.id ;

            // get pusher plate section of propulsion system model
            this.entireModel = this.transform.FindChild ("model") ;
            this.engineModel = this.entireModel.FindChild (this.engineModelName) ;
            this.pusherPlate = this.engineModel.FindChild (this.subModelPlateName) ;
            this.bombModel = this.engineModel.FindChild (this.subModelBombName) ;

            this.newBombYStart = this.bombModel.localPosition.y ;
                // to reset bomb to original position at end of animation
            this.bombTravelDistance =
                Math.Abs ((this.plateBottomY + this.detonationDeltaY) - this.bombModel.localPosition.y) ;

            if (this.pusherPlate == null)
            {
                Debug.Log (
                           ">>> Debug.Log OrionPusherPlate:onFlightStart() ***ERROR: failure to retrieve pusher plate transform") ;
            }
            if (this.bombModel == null)
                Debug.Log (">>> Debug.Log OrionPusherPlate:onFlightStart() ***ERROR: failure to retrieve bomb transform") ;

            // Parts attached to pusher plate should share in plate animation
            // otherwise they hover stationary in the air while plate moves
            // Current kludge is to find all parts whose Y is between plateTopY and plateBottomY
            var plateTopYNormalized = this.plateTopY + this.orgPos.y ;
            foreach (var aPart in this.children)
            {
                if (aPart.orgPos.y < plateTopYNormalized)
                    this.pushAnimationTransforms.Add (aPart.FindModelTransform ("model")) ;
            }

            this.calcDetonationPoint () ;

            this.explosionGroup = this.findFxGroup ("explosionGroup") ;

            this.nukeManager = new NukeManager (this.vessel, this.magazineKeys, this.magazineTitles) ;

            this.pusherMoveState = MoveState.Motionless ;

            this.detonationDelay = -1f ;

            // plasma plume particle system
//			this.plasmaPlume = Instantiate(UnityEngine.Resources.Load("Effects/fx_exhaustFlame_blue")) as GameObject;
//			this.plasmaPlume.transform.parent = this.transform;
//			this.plasmaPlume.transform.localEulerAngles = Vector3.zero;
//			this.plasmaPlume.transform.localPosition = new Vector3d(0f, this.detonationY, 0f);
//			this.plasmaPlume.particleEmitter.emit = false;
//			this.plasmaPlume.particleEmitter.minSize = 0.5f;
//			this.plasmaPlume.particleEmitter.maxSize = 1f;
//			// plume time = segment duration / 4, with 4 being somewhat arbitrary
//			this.plasmaPlume.particleEmitter.maxEnergy = (this.plateCycleTime / 5f); 
//			this.plasmaPlume.particleEmitter.minEnergy = this.plasmaPlume.particleEmitter.maxEnergy * 0.95f;
//			//  plasma velocity = R=D/T = 23 / 0.04 = 575 m/s
//			//  plasma velocity = R=D/T = 23 / 0.16 = 143.75 m/s
//			this.plasmaPlume.particleEmitter.localVelocity = new Vector3(0f, 143.75f, 0f);
//			// Plate radius 5 meters
//			// detonationDeltaY = 23 meters
//			// 5 / 23 = 0.22
//			// 143.75 * 0.22 = 31.625
//			this.plasmaPlume.particleEmitter.rndAngularVelocity = 31.625f;
//			// Plate radius 5 meters
//			// detonationDeltaY = 23 meters
//			// angular radius = arcTan(5/23) = 12.3 degrees
//			// time to reach pusher plate 0.04 seconds
//			// angular velocity = 12.3 / 0.04 = 307.5 degrees per second
//			// angular velocity = 12.3 / 0.16 = 76.875 degrees per second
////			this.plasmaPlume.particleEmitter.rndAngularVelocity = 76.875f;
//			this.plasmaPlume.particleEmitter.useWorldSpace = false;

//			this.plasmaPlume = Instantiate(UnityEngine.Resources.Load("Effects/fx_exhaustFlame_blue")) as GameObject;
//			this.plasmaPlume.transform.parent = transform;
//			this.plasmaPlume.transform.localEulerAngles = Vector3.zero;
//			this.plasmaPlume.transform.localPosition = new Vector3(0, -23f, 0);
//			ParticleEmitter flare_emitter = this.plasmaPlume.particleEmitter;
//			flare_emitter.emit = true;
//			flare_emitter.minSize = 0.5f;
//			flare_emitter.maxSize = 1f;
//			flare_emitter.rndVelocity = Vector3.one * 0.15f;
//			flare_emitter.localVelocity = Vector3.zero;
//			flare_emitter.useWorldSpace = false;


            // dynamically create the engine pop-up menu, using the list of magazine types in the game.
            // can only accomodate maxEventSlots magazine types
            // all slots start off invisible
            var slotCounter = 0 ;
            string activateFunctionName ;
            string deactivateFunctionName ;
            foreach (var magKey in this.magazineKeys)
            {
                activateFunctionName = "Activate_Magazine_" + slotCounter.ToString ("00") ;
                deactivateFunctionName = "Deactivate_Magazine_" + slotCounter.ToString ("00") ;
                this.bombFilterActivateEvents.Add (magKey, activateFunctionName) ;
                this.bombFilterDeactivateEvents.Add (magKey, deactivateFunctionName) ;
                this.Events[activateFunctionName].guiName = this.magazineTitles[magKey] + " off  (Start->)" ;
                this.Events[deactivateFunctionName].guiName = this.magazineTitles[magKey] + " feeding  (Kill->)" ;

                this.Events[activateFunctionName].active = false ;
                this.Events[activateFunctionName].guiActive = false ;
                this.Events[deactivateFunctionName].active = true ;
                this.Events[deactivateFunctionName].guiActive = true ;
                this.nukeManager.unfilterMagazine (magKey) ;

                slotCounter = slotCounter + 1 ;
                if (slotCounter >= this.maxEventSlots)
                    break ;
            }

//			Debug.Log(">>> Ship #0: " + this.vessel.rootPart.name);
//			this.dockedShipID = 0;
//			this.maxDockedShipID = this.dockedShipID;
//			this.DepthFirstParts(this.vessel.rootPart);


//			GameObject obj = new GameObject ("Line");
//			this.debugLine = obj.AddComponent<LineRenderer>();
//			this.debugLine.transform.parent = this.transform;	// child to our part
//			this.debugLine.useWorldSpace = false;	// and moving with our part instead of staying in fixed world coordinates
//			this.debugLine.transform.localPosition = this.bombModel.localPosition;
//			this.debugLine.transform.localEulerAngles = Vector3.zero;
//			this.debugLine.transform.Rotate(Vector3.right * 90f);
//			// make red to yellow triangle, 1 meter wide and 2 meters long
//			this.debugLine.material = new Material(Shader.Find("Particles/Additive"));
//			this.debugLine.SetColors(Color.red, Color.yellow);
//			this.debugLine.SetWidth(1f, 0f);
//			this.debugLine.SetVertexCount(2);
//			this.debugLine.SetPosition(0, Vector3.zero);
//			this.debugLine.SetPosition(1,Vector3.forward * this.bombTravelDistance);

            base.onFlightStart () ;
        }

//		private void DepthFirstParts(Part p) 
//		{
//			if (p.children != null || p.children.Count != 0) {
//				foreach (Part q in p.children) {
//					// DEAL WITH PART q
//					if (q.Modules.Contains("ModuleDockingNode") && p.Modules.Contains("ModuleDockingNode")) {
//						// two ModuleDockingNodes back to back is the separator between two docked ships
//						// Therefore the rest of this branch constitutes a docked ship
//						dockedShipIDStack.Push(this.dockedShipID);
//						// signify new ship
//						this.maxDockedShipID++;
//						this.dockedShipID = this.maxDockedShipID;
//					}
//					
//					Debug.Log(">>> Ship #" + this.dockedShipID.ToString() + ": " + q.name);
//					foreach (Component aComponent in q.gameObject.GetComponents<Component>()) {
//						Debug.Log(">>> Ship #" + this.dockedShipID.ToString() + ": " + q.name + ": " + aComponent.GetType());
//					}
//					
//					// DEAL WITH PART q's CHILDREN
//					DepthFirstParts(q);	// recursion
//					
//					if (q.Modules.Contains("ModuleDockingNode") && p.Modules.Contains("ModuleDockingNode")) {
//						// End of branch that is a docked ship: revert back to parent
//						this.dockedShipID = dockedShipIDStack.Pop();
//					}
//				}
//			}
//		}


        //=======
        // Called during initial part load at start of game
        protected override void onPartLoad ()
        {
            this.fxGroups.Add (new FXGroup ("explosionGroup")) ;

            base.onPartLoad () ;
        }

        //=======
        protected bool launchBomb ()
        {
            if (this.vessel.isCommandable)
            {
                this.aNukeRound = this.nukeManager.requestNuke () ;
                if (this.aNukeRound.isBomb)
                {
                    this.bombObstacle = this.calcDetonationPoint () ;
                        // make sure bomb does not hit launch pad or anything

                    // Start pusher animation
                    if (this.pusherPlate != null)
                    {
                        this.pusherMoveState = MoveState.BombFlight ; // start pusher animation going
                        this.quinterCycleStart = Time.time ;
                    }

                    return true ; // successful bomb launch
                } // bomb launch fail, no nukes available in allowed magazines
                return false ;
            } // bomb launch fail, vessel is not commandable
            return false ;
        }

        // =======
        // Calculate the detonation point, the duration of NeutralToCompress segment, and animated plate speed
        // Ordinarily this would be calculated once in onFlightStart()
        // However, as it turns out, if the moving bomb animation strikes anything (such as the launch pad)
        // the vessel blows up.
        // So each time a bomb is launched, a check is made for any obstacles along bomb flight path.
        // If obstacle is found, the detonation point is adjusted to be just ahead of the obstacle.
        protected bool calcDetonationPoint ()
        {
            RaycastHit hit ;
            var bombPosition = new Vector3 (0f, 0f, 0f) ;
            var bombDirection = new Vector3 (0f, -1f, 0f) ;
            var hitSomething = false ;

            bombPosition = this.bombModel.transform.position ;

            // plateZero is where a line starting at the bomb dropping straight down intersects the bottom of the pusher plate
            // It is assumed to be the point where the path of the bomb emerges from the bottom of the plate
            // and thus the start of the part of the bomb trajectory where other objects can get in the way of the bomb
            var plateZero = new Vector3d (this.bombModel.localPosition.x,
                                          this.entireModel.localPosition.y + this.plateBottomY,
                                          this.bombModel.localPosition.z) ;
            plateZero = this.entireModel.TransformPoint (plateZero) ;
            // groundZero is where a line starting at the bomb dropping straight down reaches the planned standoff distance below the pusher plate.
            // In other words, the planned point where the bomb explodes
            var groundZero = new Vector3d (this.bombModel.localPosition.x,
                                           this.entireModel.localPosition.y + this.plateBottomY + this.detonationY,
                                           this.bombModel.localPosition.z) ;
            groundZero = this.entireModel.TransformPoint (groundZero) ;
            bombDirection = groundZero - plateZero ;

            // do a raycast to see if anything is in the way
            if (Physics.Raycast (plateZero, bombDirection, out hit, this.bombTravelDistance))
            {
                // there is an obstacle in the way of the bomb path, shorten the path
                this.detonationY = this.plateBottomY + (hit.distance - 1f) ; // one meter before obstacle
                hitSomething = true ;
            }
            else
            {
                // the bomb has a free path with no obstacles
                this.detonationY = this.plateBottomY + this.detonationDeltaY ;
                hitSomething = false ;
            }


            this.bombOffset = this.detonationY - this.bombStartY ;
            // calculate plateCycleSpeed for use in timing the segments of the pusher plate animation
            var bombMoveDistance = 0f ;
            if (this.bombModel != null)
                bombMoveDistance = Math.Abs (this.detonationY - this.bombStartY) ;
            var totalPlateMoveDistance = (2 * this.plateOffsetCompressed) + (2 * this.plateOffsetExtended) ;
            var totalMovementDistance = bombMoveDistance + totalPlateMoveDistance ;
            var movePercent = this.plateOffsetCompressed / totalMovementDistance ;
                // % NeutralToCompress travel is of total animation movement distance
            this.theDuration = movePercent * this.plateCycleTime ; // duration of NeutralToCompress segment
            this.plateCycleSpeed = this.plateOffsetCompressed / this.theDuration ;
                // speed of plate, rate = distance / time

            return hitSomething ;
        }

        // =======
        protected void detonateBomb ()
        {
            var totalVesselMass = 0f ;
            foreach (var current in this.vessel.parts)
                totalVesselMass += current.mass ;

            // add heat from bomb detonation to pusher plate
            var ForwardVel = Vector3.Dot(this.vessel.rb_velocity , this.vessel.upAxis);
            var heatMultiplier = 0.01 + this.vessel.atmDensity;
            if (this.vessel.atmDensity > 0.1)
            {
                print("Calculated forward velocity to be " + ForwardVel);
                if (ForwardVel < 10.0)
                {
                    var kiloTons = Math.Pow((this.aNukeRound.destroyZone / 230), 3);
                    var fireballSize = 34.0 * Math.Pow(kiloTons, 0.41);
                    var fireballTime = 0.2 * Math.Pow(kiloTons, 0.45);
                    var fireballSpeed = 170.0 / Math.Pow(kiloTons, 0.04);
                    ForwardVel = ForwardVel + this.aNukeRound.bombImpulse * ((float)(Math.Pow(((double)this.vessel.atmDensity), 0.333)) * 12 + 1) / totalVesselMass;
                    var timeToFireball = this.detonationY / (fireballSpeed - ForwardVel);
                    if (timeToFireball < fireballTime)
                    {
                        var timeInFireBall = fireballTime - timeToFireball;
                        var timeOutTheOtherEnd = this.detonationY / (fireballSpeed + ForwardVel);
                        if (timeOutTheOtherEnd < fireballTime) { timeInFireBall = timeOutTheOtherEnd - timeToFireball; }
                        heatMultiplier += this.vessel.atmDensity * 4 * timeInFireBall;
                        print("Cranking the Heat WAY up.");
                    }
                }
            }
            this.skinTemperature += this.aNukeRound.bombHeat * heatMultiplier;

            // FX: make explosion sound
            this.explosionGroup.Power = this.explosionGroupPower ;
            this.explosionGroup.Burst () ;
            this.gameObject.audio.pitch = 1f ;
            this.gameObject.audio.PlayOneShot (this.explosionGroup.sfx) ;

            // FX: make explosion animation
            var groundZero = new Vector3d (this.bombModel.localPosition.x,
                                           this.entireModel.localPosition.y + this.detonationY,
                                           this.bombModel.localPosition.z) ;
            groundZero = this.entireModel.TransformPoint (groundZero) ; // change from local to global coordinates
            FXMonger.Explode (this, groundZero, this.fxmongerHowHard) ;

            // FX: make flash of light
            var nukeFlashHolder = new GameObject ("flashHolder") ;
            nukeFlashHolder.transform.position = groundZero ;
            nukeFlashHolder.AddComponent<NukeFlash> () ;

            //			// FX: start plasma plume particle system
            //			this.plasmaPlume.particleEmitter.emit = true;
            //			this.plasmaPlumeStopTime = Time.time + 0.64f;	// todo replace this with something calculated
            ////			this.plasmaPlumeStopTime = Time.time + 0.16f;	// todo replace this with something calculated
            //			Debug.Log(">>> OrionPusherPlate.detonateBomb: plasma plume started at " + Time.time.ToString());

            // add velocity
            float atmo = (float)(Math.Pow(((double)this.vessel.atmDensity),0.333))*12;
            this.aNukeRound.bombImpulse = this.aNukeRound.bombImpulse*((atmo) + 1);
            this.vessel.ChangeWorldVelocity (this.transform.up * (this.aNukeRound.bombImpulse  / totalVesselMass)) ;
            // float accel = (this.aNukeRound.bombImpulse / totalVesselMass);

            // apply force to frame, flimsy rockets shake apart
            // note, do not change this to ForceMode.Impulse , or whatever is on top of the engine will make the jump to lightspeed
            // note, strictly speaking, should not divide by totalVesselMass. But without, rest of vessel blows off like atomic champagne cork
            this.rigidbody.AddRelativeForce (new Vector3 (0f, (this.aNukeRound.bombImpulse / totalVesselMass), 0f),
                                             ForceMode.Force) ;

            Collider[] colliders ; // for blast radius and destruction radius filtering

            // shake up any object within blast radius, unless they are part of this ship
            // Also add heat
            colliders = Physics.OverlapSphere (groundZero, this.aNukeRound.damageZone) ;
            foreach (var hit in colliders)
            {
                if (hit.attachedRigidbody != null)
                {
                    var hitPart = hit.collider.attachedRigidbody.GetComponent<Part> () ;
                    // do not damage yourself, your ship is immune from your own nukes
                    if (hitPart.vessel.id != this.pusherOwnerGuid)
                    {
                        hitPart.skinTemperature += this.aNukeRound.bombHeat * (this.vessel.atmDensity + 0.01)  ; // add heat
                        hitPart.vessel.GoOffRails () ;
                        hit.attachedRigidbody.AddExplosionForce (this.aNukeRound.damageShock,
                                                                 groundZero,
                                                                 this.aNukeRound.damageZone,
                                                                 0.0f) ;
                    }
                }
            }

            // destroy any object within destruction radius, unless they are part of this ship
            // sparing anything more massive than 100 tons. Do not destroy asteroids like Roche.
            colliders = Physics.OverlapSphere (groundZero, this.aNukeRound.destroyZone) ;
            foreach (var hit in colliders)
            {
                if (hit.attachedRigidbody != null)
                {
                    var hitPart = hit.collider.attachedRigidbody.GetComponent<Part> () ;
                    // do not damage yourself, your ship is immune from your own nukes
                    if (hitPart.vessel.id != this.pusherOwnerGuid)
                    {
                        if (hitPart.mass < this.aNukeRound.destroyMass)
                        {
                            // spare any part over destroyMass tons, like asteroids
                            hitPart.explode () ; // destroy everything else
                        }
                    }
                }
            }
        }

        //=======
        // called continously during flight scene if in active stage
        protected override void onActiveFixedUpdate ()
        {
            var sucessfulBombLaunch = true ;

            // Manual bomb launch
            // "z" key detonates a bomb
            if (Input.GetKey (KeyCode.Z))
            {
                if (this.canFire)
                {
                    sucessfulBombLaunch = this.launchBomb () ;
                    this.canFire = !sucessfulBombLaunch ;
                }
            }

            // set automatic bomb launch as per throttle setting
            var mainThrottleSetting = this.vessel.ctrlState.mainThrottle ;
            if (this.lastThrottle != mainThrottleSetting)
            {
                this.lastThrottle = mainThrottleSetting ;
                if (mainThrottleSetting < 0.01f)
                {
                    // throttle at 0%
                    this.detonationDelay = -1f ;
                }
                else if (mainThrottleSetting < 0.3f)
                {
                    // throttle from 10% to 30%
                    this.detonationDelay = this.detonationMaxDelay ;
                }
                else if (mainThrottleSetting < 0.6f)
                {
                    // throttle from 30% to 60%
                    this.detonationDelay = this.detonationStdDelay ;
                }
                else
                {
                    // throttle form 60% to 100%
                    this.detonationDelay = this.detonationMinDelay ;
                }
            }

            // Automatic bomb launch
            if (this.detonationDelay > 0f)
            {
                // throttle is non-zero
                if (this.canFire)
                {
                    // no explosion in progress
                    if (Time.time > this.nextFire)
                    {
                        // at least detonationDelay has elapsed since last bomb
                        this.nextFire = Time.time + this.detonationDelay ;
                        sucessfulBombLaunch = this.launchBomb () ;
                        this.canFire = !sucessfulBombLaunch ;
                    }
                }
            }

            this.doPlateAnimation () ;

            base.onActiveFixedUpdate () ;
        }

        // =======
        protected void doPlateAnimation ()
        {
//			if (this.plasmaPlume.particleEmitter.emit == true && Time.time >= this.plasmaPlumeStopTime) {
//				this.plasmaPlume.particleEmitter.emit = false;
//				Debug.Log(">>> OrionPusherPlate.doPlateAnimation: plasma plume stopped at " + Time.time.ToString());
//			}


            if (this.pusherMoveState == MoveState.Motionless)
                return ; // no animation in progress

            // perform next frame of pusher plate animation

            if (this.pusherMoveState == MoveState.BombFlight)
            {
                if (!this.bombObstacle)
                {
                    // bomb is assumed to travel straight down
                    var newBombX = this.bombModel.localPosition.x ;
                    var newBombY = this.bombModel.localPosition.y ;
                    var newBombZ = this.bombModel.localPosition.z ;

                    // multiply by Time.get_deltaTime to convert plateCycleSpeed into displacement per second
                    var bombDelta = this.plateCycleSpeed * Time.deltaTime ;

                    newBombY -= bombDelta ; // move down

                    if (newBombY < this.bombOffset)
                    {
                        // bomb has reached the finish point, MoveState.BombFlight is over
                        newBombY = this.newBombYStart ; // teleport bomb back to start position
                        this.detonateBomb () ; // do bomb special effects
                        // switch to next state
                        this.pusherMoveState = MoveState.NeutralToCompress ;
                        this.quinterCycleStart = Time.time ;
                    }
                    this.bombModel.localPosition = new Vector3 (newBombX, newBombY, newBombZ) ; // move bomb to position
                }
                else
                {
                    // There is an obstacle in the bomb path. Don't even bother doing the bomb animation, just skip to the end
                    this.bombObstacle = false ;
                    this.detonateBomb () ; // do bomb special effects
                    // switch to next state
                    this.pusherMoveState = MoveState.NeutralToCompress ;
                    this.quinterCycleStart = Time.time ;
                }
            }
            else
            {
                var newPlateY = this.pusherPlate.localPosition.y ;
                var quinterCyclePercent = (Time.time - this.quinterCycleStart) / this.theDuration ;
                    // percentage of 1/5 cycle time consumed
                if ((this.pusherMoveState == MoveState.CompressToNeutral) ||
                    (this.pusherMoveState == MoveState.ExtendToNeutral))
                {
                    // these segments have sine wave inverted
                    quinterCyclePercent = 1f - quinterCyclePercent ;
                }
                quinterCyclePercent = (float) Math.Round (quinterCyclePercent, 1) ; // round to key values in sineRate[]
                if (quinterCyclePercent < 0.1f)
                {
                    // keep in bounds of key values
                    quinterCyclePercent = 0.1f ;
                }
                else if (quinterCyclePercent > 1f)
                    quinterCyclePercent = 1f ;
                var sineFactor = this.sineRate[quinterCyclePercent] ;

                // multiply by Time.get_deltaTime to convert plateCycleSpeed into displacement per second
                var plateDelta = (this.plateCycleSpeed * sineFactor) * Time.deltaTime ;
                    // scale plateCycleSpeed by sine wave

                switch (this.pusherMoveState)
                {
                    case MoveState.NeutralToCompress: // Segment 1
                        newPlateY += plateDelta ; // move up
                        if (newPlateY > this.plateOffsetCompressed)
                        {
                            // switch to next state
                            this.pusherMoveState = MoveState.CompressToNeutral ;
                            newPlateY = this.plateOffsetCompressed ;
                            this.quinterCycleStart = Time.time ;
                        }
                        break ;
                    case MoveState.CompressToNeutral: // Segment 2
                        newPlateY -= plateDelta ; // move down
                        if (newPlateY < 0.01f)
                        {
                            // switch to next state
                            this.pusherMoveState = MoveState.NeutralToExtend ;
                            newPlateY = 0f ;
                            this.quinterCycleStart = Time.time ;
                        }
                        break ;
                    case MoveState.NeutralToExtend: // Segment 3
                        newPlateY -= plateDelta ; // still move down
                        if (newPlateY < -this.plateOffsetExtended)
                        {
                            // switch to next state
                            this.pusherMoveState = MoveState.ExtendToNeutral ;
                            newPlateY = -this.plateOffsetExtended ;
                            this.quinterCycleStart = Time.time ;
                        }
                        break ;
                    case MoveState.ExtendToNeutral: // Segment 4
                        newPlateY += plateDelta ; // move up
                        if (newPlateY > 0.01f)
                        {
                            // switch to next state
                            this.pusherMoveState = MoveState.Motionless ;
                            newPlateY = 0f ;
                            this.canFire = true ; // can launch new bomb
                            this.quinterCycleStart = Time.time ;
                        }
                        break ;
                }
                this.pusherPlate.localPosition = new Vector3 (0f, newPlateY, 0f) ; // move plate to position

                // move parts that are attached to pusher plate as well
                foreach (var aniTransform in this.pushAnimationTransforms)
                {
                    if (aniTransform != null)
                        aniTransform.localPosition = new Vector3 (0f, newPlateY, 0f) ;
                }
            }
        }

        //=======
        // called continuously during flight scene if on focussed vessel
        protected override void onPartFixedUpdate ()
        {
            // update HUD of bomb inventory
            if (this.bombCounter == null)
            {
                this.bombCounter = this.stackIcon.DisplayInfo () ;
                this.bombCounter.SetMsgBgColor (XKCDColors.DarkGreen) ;
                this.bombCounter.SetMsgTextColor (XKCDColors.ElectricLime) ;
                this.bombCounter.SetProgressBarBgColor (new Color (0f, 0f, 0f, 0f)) ;
                this.bombCounter.SetProgressBarColor (new Color (0f, 0f, 0f, 0f)) ;
            }
            else
            {
                var numBombs = this.nukeManager.effectiveStockpileSize () ;

                if (this.lastNumBombs != numBombs)
                {
                    this.lastNumBombs = numBombs ;
                    if (numBombs > 0)
                    {
                        this.bombCounter.SetMsgBgColor (XKCDColors.DarkGreen) ;
                        this.bombCounter.SetMsgTextColor (XKCDColors.ElectricLime) ;
                    }
                    else
                    {
                        // make HUD indicator red to alert player there are no nukes
                        this.bombCounter.SetMsgBgColor (XKCDColors.DeepPink) ;
                        this.bombCounter.SetMsgTextColor (XKCDColors.Yellow) ;
//					this.stackIcon.SetIconColor(Color.red); TODO
                    }
                    this.bombCounter.SetMessage ("Nuke:" + numBombs) ;
                }
            }

            // refresh the pop-up menu
            bool hasMagazineValue ;
            string activateFunctionName ;
            string deactivateFunctionName ;

            foreach (var hasMagazineEntry in this.nukeManager.hasMagazine)
            {
                hasMagazineValue = hasMagazineEntry.Value ;
                activateFunctionName = this.bombFilterActivateEvents[hasMagazineEntry.Key] ;
                deactivateFunctionName = this.bombFilterDeactivateEvents[hasMagazineEntry.Key] ;

                if (hasMagazineValue)
                {
                    if ((this.Events[activateFunctionName].active == false) &&
                        (this.Events[deactivateFunctionName].active == false))
                    {
                        this.Events[activateFunctionName].active = false ;
                        this.Events[activateFunctionName].guiActive = false ;
                        this.Events[deactivateFunctionName].active = true ;
                        this.Events[deactivateFunctionName].guiActive = true ;
                    }
                }
                else
                {
                    this.Events[activateFunctionName].active = false ;
                    this.Events[activateFunctionName].guiActive = false ;
                    this.Events[deactivateFunctionName].active = false ;
                    this.Events[deactivateFunctionName].guiActive = false ;
                }
            }

            base.onPartFixedUpdate () ;
        }

        // === ACTIONS ===
        [KSPAction ("Start Magazine 00", actionGroup = KSPActionGroup.None)]
        public void ActionGroupActivateMag00 (KSPActionParam param)
        {
            if (this.maxMagazine > 0)
                this.Activate_Magazine_00 () ;
        }

        [KSPAction ("Kill Magazine 00", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDeactivateMag00 (KSPActionParam param)
        {
            if (this.maxMagazine > 0)
                this.Deactivate_Magazine_00 () ;
        }

        [KSPAction ("Start Magazine 01", actionGroup = KSPActionGroup.None)]
        public void ActionGroupActivateMag01 (KSPActionParam param)
        {
            if (this.maxMagazine > 1)
                this.Activate_Magazine_01 () ;
        }

        [KSPAction ("Kill Magazine 01", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDeactivateMag01 (KSPActionParam param)
        {
            if (this.maxMagazine > 1)
                this.Deactivate_Magazine_01 () ;
        }

        [KSPAction ("Start Magazine 02", actionGroup = KSPActionGroup.None)]
        public void ActionGroupActivateMag02 (KSPActionParam param)
        {
            if (this.maxMagazine > 2)
                this.Activate_Magazine_02 () ;
        }

        [KSPAction ("Kill Magazine 02", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDeactivateMag02 (KSPActionParam param)
        {
            if (this.maxMagazine > 2)
                this.Deactivate_Magazine_02 () ;
        }

        [KSPAction ("Start Magazine 03", actionGroup = KSPActionGroup.None)]
        public void ActionGroupActivateMag03 (KSPActionParam param)
        {
            if (this.maxMagazine > 3)
                this.Activate_Magazine_03 () ;
        }

        [KSPAction ("Kill Magazine 03", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDeactivateMag03 (KSPActionParam param)
        {
            if (this.maxMagazine > 3)
                this.Deactivate_Magazine_03 () ;
        }

        [KSPAction ("Start Magazine 04", actionGroup = KSPActionGroup.None)]
        public void ActionGroupActivateMag04 (KSPActionParam param)
        {
            if (this.maxMagazine > 4)
                this.Activate_Magazine_04 () ;
        }

        [KSPAction ("Kill Magazine 04", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDeactivateMag04 (KSPActionParam param)
        {
            if (this.maxMagazine > 4)
                this.Deactivate_Magazine_04 () ;
        }

        [KSPAction ("Start Magazine 05", actionGroup = KSPActionGroup.None)]
        public void ActionGroupActivateMag05 (KSPActionParam param)
        {
            if (this.maxMagazine > 5)
                this.Activate_Magazine_05 () ;
        }

        [KSPAction ("Kill Magazine 05", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDeactivateMag05 (KSPActionParam param)
        {
            if (this.maxMagazine > 5)
                this.Deactivate_Magazine_05 () ;
        }

        [KSPAction ("Start Magazine 06", actionGroup = KSPActionGroup.None)]
        public void ActionGroupActivateMag06 (KSPActionParam param)
        {
            if (this.maxMagazine > 6)
                this.Activate_Magazine_06 () ;
        }

        [KSPAction ("Kill Magazine 06", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDeactivateMag06 (KSPActionParam param)
        {
            if (this.maxMagazine > 6)
                this.Deactivate_Magazine_06 () ;
        }

        [KSPAction ("Start Magazine 07", actionGroup = KSPActionGroup.None)]
        public void ActionGroupActivateMag07 (KSPActionParam param)
        {
            if (this.maxMagazine > 7)
                this.Activate_Magazine_07 () ;
        }

        [KSPAction ("Kill Magazine 07", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDeactivateMag07 (KSPActionParam param)
        {
            if (this.maxMagazine > 7)
                this.Deactivate_Magazine_07 () ;
        }

        [KSPAction ("Start Magazine 08", actionGroup = KSPActionGroup.None)]
        public void ActionGroupActivateMag08 (KSPActionParam param)
        {
            if (this.maxMagazine > 8)
                this.Activate_Magazine_08 () ;
        }

        [KSPAction ("Kill Magazine 08", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDeactivateMag08 (KSPActionParam param)
        {
            if (this.maxMagazine > 8)
                this.Deactivate_Magazine_08 () ;
        }

        [KSPAction ("Start Magazine 09", actionGroup = KSPActionGroup.None)]
        public void ActionGroupActivateMag09 (KSPActionParam param)
        {
            if (this.maxMagazine > 9)
                this.Activate_Magazine_09 () ;
        }

        [KSPAction ("Kill Magazine 09", KSPActionGroup.None)]
        public void ActionGroupDeactivateMag09 (KSPActionParam param)
        {
            if (this.maxMagazine > 9)
                this.Deactivate_Magazine_09 () ;
        }

        // === EVENTS ===

        // empty dynamic slots for the various magazine types. All start out invisible

        [KSPEvent (active = false, guiName = "00 off  (Start->)", guiActive = false, category = "Thrust Control")]
        public void Activate_Magazine_00 ()
        {
            this.activateBombEvent ("Activate_Magazine_00", "Deactivate_Magazine_00", this.magazineKeys[0]) ;
        }

        [KSPEvent (active = false, guiName = "00 feeding  (Kill->)", guiActive = false, category = "Thrust Control")]
        public void Deactivate_Magazine_00 ()
        {
            this.deactivateBombEvent ("Activate_Magazine_00", "Deactivate_Magazine_00", this.magazineKeys[0]) ;
        }

        [KSPEvent (active = false, guiName = "01 off  (Start->)", guiActive = false, category = "Thrust Control")]
        public void Activate_Magazine_01 ()
        {
            this.activateBombEvent ("Activate_Magazine_01", "Deactivate_Magazine_01", this.magazineKeys[1]) ;
        }

        [KSPEvent (active = false, guiName = "01 feeding  (Kill->)", guiActive = false, category = "Thrust Control")]
        public void Deactivate_Magazine_01 ()
        {
            this.deactivateBombEvent ("Activate_Magazine_01", "Deactivate_Magazine_01", this.magazineKeys[1]) ;
        }

        [KSPEvent (active = false, guiName = "02 off  (Start->)", guiActive = false, category = "Thrust Control")]
        public void Activate_Magazine_02 ()
        {
            this.activateBombEvent ("Activate_Magazine_02", "Deactivate_Magazine_02", this.magazineKeys[2]) ;
        }

        [KSPEvent (active = false, guiName = "02 feeding  (Kill->)", guiActive = false, category = "Thrust Control")]
        public void Deactivate_Magazine_02 ()
        {
            this.deactivateBombEvent ("Activate_Magazine_02", "Deactivate_Magazine_02", this.magazineKeys[2]) ;
        }

        [KSPEvent (active = false, guiName = "03 off  (Start->)", guiActive = false, category = "Thrust Control")]
        public void Activate_Magazine_03 ()
        {
            this.activateBombEvent ("Activate_Magazine_03", "Deactivate_Magazine_03", this.magazineKeys[3]) ;
        }

        [KSPEvent (active = false, guiName = "03 feeding  (Kill->)", guiActive = false, category = "Thrust Control")]
        public void Deactivate_Magazine_03 ()
        {
            this.deactivateBombEvent ("Activate_Magazine_03", "Deactivate_Magazine_03", this.magazineKeys[3]) ;
        }

        [KSPEvent (active = false, guiName = "04 off  (Start->)", guiActive = false, category = "Thrust Control")]
        public void Activate_Magazine_04 ()
        {
            this.activateBombEvent ("Activate_Magazine_04", "Deactivate_Magazine_04", this.magazineKeys[4]) ;
        }

        [KSPEvent (active = false, guiName = "04 feeding  (Kill->)", guiActive = false, category = "Thrust Control")]
        public void Deactivate_Magazine_04 ()
        {
            this.deactivateBombEvent ("Activate_Magazine_04", "Deactivate_Magazine_04", this.magazineKeys[4]) ;
        }

        [KSPEvent (active = false, guiName = "05 off  (Start->)", guiActive = false, category = "Thrust Control")]
        public void Activate_Magazine_05 ()
        {
            this.activateBombEvent ("Activate_Magazine_05", "Deactivate_Magazine_05", this.magazineKeys[5]) ;
        }

        [KSPEvent (active = false, guiName = "05 feeding  (Kill->)", guiActive = false, category = "Thrust Control")]
        public void Deactivate_Magazine_05 ()
        {
            this.deactivateBombEvent ("Activate_Magazine_05", "Deactivate_Magazine_05", this.magazineKeys[5]) ;
        }

        [KSPEvent (active = false, guiName = "06 off  (Start->)", guiActive = false, category = "Thrust Control")]
        public void Activate_Magazine_06 ()
        {
            this.activateBombEvent ("Activate_Magazine_06", "Deactivate_Magazine_06", this.magazineKeys[6]) ;
        }

        [KSPEvent (active = false, guiName = "06 feeding  (Kill->)", guiActive = false, category = "Thrust Control")]
        public void Deactivate_Magazine_06 ()
        {
            this.deactivateBombEvent ("Activate_Magazine_06", "Deactivate_Magazine_06", this.magazineKeys[6]) ;
        }

        [KSPEvent (active = false, guiName = "07 off  (Start->)", guiActive = false, category = "Thrust Control")]
        public void Activate_Magazine_07 ()
        {
            this.activateBombEvent ("Activate_Magazine_07", "Deactivate_Magazine_07", this.magazineKeys[7]) ;
        }

        [KSPEvent (active = false, guiName = "07 feeding  (Kill->)", guiActive = false, category = "Thrust Control")]
        public void Deactivate_Magazine_07 ()
        {
            this.deactivateBombEvent ("Activate_Magazine_07", "Deactivate_Magazine_07", this.magazineKeys[7]) ;
        }

        [KSPEvent (active = false, guiName = "08 off  (Start->)", guiActive = false, category = "Thrust Control")]
        public void Activate_Magazine_08 ()
        {
            this.activateBombEvent ("Activate_Magazine_08", "Deactivate_Magazine_08", this.magazineKeys[0]) ;
        }

        [KSPEvent (active = false, guiName = "08 feeding  (Kill->)", guiActive = false, category = "Thrust Control")]
        public void Deactivate_Magazine_08 ()
        {
            this.deactivateBombEvent ("Activate_Magazine_08", "Deactivate_Magazine_08", this.magazineKeys[8]) ;
        }

        [KSPEvent (active = false, guiName = "09 off  (Start->)", guiActive = false, category = "Thrust Control")]
        public void Activate_Magazine_09 ()
        {
            this.activateBombEvent ("Activate_Magazine_09", "Deactivate_Magazine_09", this.magazineKeys[9]) ;
        }

        [KSPEvent (active = false, guiName = "09 feeding  (Kill->)", guiActive = false, category = "Thrust Control")]
        public void Deactivate_Magazine_09 ()
        {
            this.deactivateBombEvent ("Activate_Magazine_09", "Deactivate_Magazine_09", this.magazineKeys[9]) ;
        }

        private void activateBombEvent (string activateFunctionName, string deactivateFunctionName, string filterKey)
        {
            this.Events[activateFunctionName].active = false ;
            this.Events[activateFunctionName].guiActive = false ;
            this.Events[deactivateFunctionName].active = true ;
            this.Events[deactivateFunctionName].guiActive = true ;

            this.nukeManager.unfilterMagazine (filterKey) ;
                // if no magazine of this type attached, nukeManager knows to do nothing
        }

        private void deactivateBombEvent (string activateFunctionName, string deactivateFunctionName, string filterKey)
        {
            this.Events[activateFunctionName].active = true ;
            this.Events[activateFunctionName].guiActive = true ;
            this.Events[deactivateFunctionName].active = false ;
            this.Events[deactivateFunctionName].guiActive = false ;

            this.nukeManager.filterMagazine (filterKey) ;
                // if no magazine of this type attached, nukeManager knows to do nothing
        }

        // todo this is not working
        [KSPEvent (active = true, guiActive = false, name = "DoInventoryEvent")]
        public void doInventory ()
        {
            Debug.Log (">>> OrionPusherPlate.doInventory: OrionPusherPlate got DoInventoryEvent") ;
            this.nukeManager.doInventory () ;
        }
    }
}
