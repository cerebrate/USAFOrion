#region using

using System ;
using System.Collections.Generic ;

#endregion

namespace USAFOrion
{
    // Keeps track of inventory of bombs in all attached OrionMagazines
    // Tries to fullfil orders from OrionPusherPlates by removing bombs from all OrionMagazines evenly, in an attempt to keep the ship balanced around center of gravity.
    // Can filter out magazine types, so user can specify which bomb type to use.
    // Remember that users might have several OrionPusherPlates on a given vessel (i.e., several instances of NukeManger might be active)
    // Remember that users might randomly jettison magazines at any time
    // TODO insert code so jettison magazine emits a signal that NukeManager subcribes to, force a doInventory()
    // Remember that it might be possible to add new magazines at any time
    public class NukeManager
    {
        private const uint NullMagazineKey = 999 ;
        public Dictionary<String, int> bombInventory = new Dictionary<String, int> () ; // number of bombs of that type
        public List<Dictionary<String, int>> dockedBombInventory = new List<Dictionary<String, int>> () ;
        public List<Dictionary<String, bool>> dockedHasMagazine = new List<Dictionary<String, bool>> () ;
        // for following lists, index is dockedShipID index
        public List<Dictionary<uint, OrionMagazine>> dockedMagazineArrays = new List<Dictionary<uint, OrionMagazine>> () ;
        // docked versions
        private int dockedShipID ;
        public bool doDockedInventoryCalled ;

        public Dictionary<String, bool> hasMagazine = new Dictionary<String, bool> () ;
                                        // has at least one non-empty magazine of type OrionMagazine.name

        public Dictionary<String, bool> hasXferMagazinePair = new Dictionary<String, bool> () ;
                                        // at least 2 docked ship have this magazine type

        public Dictionary<String, bool> magazineFilter = new Dictionary<String, bool> () ;
                                        // type of magazines user wants to draw nukes from

        public List<String> magazineKeys ; // names of all the magazine types currently in the game
        public Dictionary<String, String> magazineTitles = new Dictionary<String, String> () ;
        public int maxDockedShipID ;
        private readonly Stack<int> dockedShipIDStack = new Stack<int> () ;
        private readonly Dictionary<uint, OrionMagazine> magazineArray = new Dictionary<uint, OrionMagazine> () ;
        private readonly Vessel nuclearVessel ;

        public NukeManager (Vessel theVessel, List<String> theMagazineKeys, Dictionary<String, String> theMagazineTitles)
        {
            this.nuclearVessel = theVessel ;
            this.magazineKeys = theMagazineKeys ;
                // list of all parts of type OrionMagazine in the entire game (not just this vessel)
            this.magazineTitles = theMagazineTitles ;

            // initialize the hasMagazine and filter array
            foreach (var magKey in this.magazineKeys)
            {
                this.hasMagazine.Add (magKey, false) ;
                this.magazineFilter.Add (magKey, false) ;
                this.hasXferMagazinePair.Add (magKey, false) ;
            }

            this.doInventory () ;
//			this.doDockedInventory();
        }

        // recreate from scratch the list of magazines with at least one nuke
        // Remember that users might have several OrionPusherPlates on a given vessel
        // Remember that users might randomly jettison magazines at any time
        // Remember that it might be possible to add new magazines at any time
        public void doInventory ()
        {
            OrionMagazine aMagazine ;

            // zero out the arrays
            this.bombInventory.Clear () ;
            this.magazineArray.Clear () ;
            foreach (var magKey in this.magazineKeys)
                this.hasMagazine[magKey] = false ;

            // fill the arrays
            foreach (var aPart in this.nuclearVessel.parts)
            {
                if (aPart.ClassName == "OrionMagazine")
                {
                    aMagazine = (aPart as OrionMagazine) ;

                    if (!this.bombInventory.ContainsKey (aMagazine.name))
                        this.bombInventory.Add (aMagazine.name, 0) ;
                    this.bombInventory[aMagazine.name] += aMagazine.bombStockpile ;
                        // total number of bombs of that type in all magazines

                    if (aMagazine.bombStockpile > 0)
                    {
                        // true = at least one bomb of that type in at least one magazine
                        this.magazineArray.Add (aMagazine.uid, aMagazine) ;
                        this.hasMagazine[aMagazine.name] = true ;
                    }
                }
            }
        }

        // fill dockedMagazineArrays with magazines sorted by docked ships
        public void doDockedInventory ()
        {
            this.doDockedInventoryCalled = true ;

            this.dockedShipID = 0 ;
            this.maxDockedShipID = this.dockedShipID ;

            // zero out the arrays
            foreach (var invDict in this.dockedBombInventory)
                invDict.Clear () ;
            this.dockedBombInventory.Clear () ;
            this.dockedBombInventory.Add (new Dictionary<String, int> ()) ;

            foreach (var magDict in this.dockedMagazineArrays)
                magDict.Clear () ;
            this.dockedMagazineArrays.Clear () ;
            this.dockedMagazineArrays.Add (new Dictionary<uint, OrionMagazine> ()) ;

            foreach (var hasDict in this.dockedHasMagazine)
                hasDict.Clear () ;
            this.dockedHasMagazine.Clear () ;
            this.dockedHasMagazine.Add (new Dictionary<String, bool> ()) ;
            foreach (var magKey in this.magazineKeys)
                this.dockedHasMagazine[this.dockedShipID].Add (magKey, false) ;

            foreach (var magKey in this.magazineKeys)
                this.hasXferMagazinePair[magKey] = false ;

            // recursively load dockedMagazineArrays
            this.DepthFirstParts (this.nuclearVessel.rootPart) ;

            // fill hasXferMagazinePair with 'true' if two or more docked ships have that magazine type
            int numShipsWithMagaine ;
            foreach (var magKey in this.magazineKeys)
            {
                numShipsWithMagaine = 0 ;
                for (var dockedShipIndex = 0; dockedShipIndex <= this.maxDockedShipID; dockedShipIndex++)
                {
                    if (this.dockedHasMagazine[dockedShipIndex][magKey])
                        numShipsWithMagaine++ ;
                }
                if (numShipsWithMagaine >= 2)
                    this.hasXferMagazinePair[magKey] = true ;
            }
        }

        // recursive routine to find all magazines, and sort them by which docked ship they belong to
        // Docked ships are merged into one vessel.
        // In the part three, it is assumed that any branch that starts with a pair of docking nodes is a separate docked ship
        private void DepthFirstParts (Part p)
        {
            OrionMagazine aMagazine ;

            if (p.children != null || p.children.Count != 0)
            {
                foreach (var q in p.children)
                {
                    // DEAL WITH PART q
                    if (q.Modules.Contains ("ModuleDockingNode") && p.Modules.Contains ("ModuleDockingNode"))
                    {
                        // two ModuleDockingNodes back to back is the separator between two docked ships
                        // Therefore the rest of this branch constitutes a docked ship
                        this.dockedShipIDStack.Push (this.dockedShipID) ;
                        // signify new ship
                        this.maxDockedShipID++ ;
                        this.dockedShipID = this.maxDockedShipID ;
                    }

                    if (q.ClassName == "OrionMagazine")
                    {
                        // Orion magazine, store it. Even if it is empty, other ship could fill it
                        aMagazine = (q as OrionMagazine) ;

                        // FILL dockedBombInventory
                        // grow dockedBombInventory if necessary
                        if (this.dockedBombInventory.Count <= this.dockedShipID)
                            this.dockedBombInventory.Add (new Dictionary<String, int> ()) ;
                        // add key to docked ship if necessary
                        if (!this.dockedBombInventory[this.dockedShipID].ContainsKey (aMagazine.name))
                            this.dockedBombInventory[this.dockedShipID].Add (aMagazine.name, 0) ;
                        // add the new bombs to the running total
                        this.dockedBombInventory[this.dockedShipID][aMagazine.name] += aMagazine.bombStockpile ;

                        // FILL dockedMagazineArrays
                        // grow dockedMagazineArrays if necessary
                        if (this.dockedMagazineArrays.Count <= this.dockedShipID)
                            this.dockedMagazineArrays.Add (new Dictionary<uint, OrionMagazine> ()) ;
                        // insert magazine into dockedMagazineArrays
                        this.dockedMagazineArrays[this.dockedShipID].Add (aMagazine.uid, aMagazine) ;

                        // FILL dockedHasMagazine
                        // grow dockedHasMagazine if necessary
                        if (this.dockedHasMagazine.Count <= this.dockedShipID)
                        {
                            this.dockedHasMagazine.Add (new Dictionary<String, bool> ()) ;
                            // add keys
                            foreach (var magKey in this.magazineKeys)
                                this.dockedHasMagazine[this.dockedShipID].Add (magKey, false) ;
                        }
                        // mark existance of magazine, empty or not
                        this.dockedHasMagazine[this.dockedShipID][aMagazine.name] = true ;
                    }

                    // DEAL WITH PART q's CHILDREN
                    this.DepthFirstParts (q) ; // recursion

                    if (q.Modules.Contains ("ModuleDockingNode") && p.Modules.Contains ("ModuleDockingNode"))
                    {
                        // End of branch that is a docked ship: revert back to parent
                        this.dockedShipID = this.dockedShipIDStack.Pop () ;
                    }
                }
            }
        }

        // Engine requests one nuke to detonate
        // Try to balance bomb magazines by always removing a bomb from the fullest magazine
        // in an attempt to keep the vessel balanced around the center of gravity.
        public NukeRound requestNuke ()
        {
            this.doInventory () ;

            // Find unfilterd magazine with highest amount of bombs
            // This is an attempt to keep the magazines balanced by drawing evenly from all magazines
            // Otherwise it will shift the vessel's center of gravity
            var highMagazineKey = NullMagazineKey ;
            var highInventory = -1 ;
            foreach (var magazineEntry in this.magazineArray)
            {
                if (magazineEntry.Value != null)
                {
                    // in case a magazine was jettisoned
                    if (this.magazineFilter[magazineEntry.Value.name])
                    {
                        // if this bombtype has not been filtered
                        if (magazineEntry.Value.bombStockpile > highInventory)
                        {
                            // if this has more bombs than current front runner
                            highInventory = magazineEntry.Value.bombStockpile ;
                            highMagazineKey = magazineEntry.Key ;
                        }
                    }
                }
            }

            if (highMagazineKey != NullMagazineKey)
            {
                // found an allowed magazine with at least 1 nuke
                return this.magazineArray[highMagazineKey].requestNuke () ;
            }
            return new NukeRound () ; // no nuke
        }

        // move 1 nuke round from one docked ship to another
        // This assumes that doDockedInventory() has already been called
        public void moveNuke (String nukeTypeKey, int supplierShipIndex, int consumerShipIndex)
        {
            // Find supplier magazine with highest amount of bombs
            // This is an attempt to keep the magazines balanced by drawing evenly from all magazines
            // Otherwise it will shift the vessel's center of gravity
            var highMagazineKey = NullMagazineKey ;
            var highInventory = -1 ;
            foreach (var magazineEntry in this.dockedMagazineArrays[supplierShipIndex])
            {
                if (magazineEntry.Value != null)
                {
                    // in case a magazine was jettisoned
                    if (magazineEntry.Value.name == nukeTypeKey)
                    {
                        // magazine must hold requested type of nuke
                        if (magazineEntry.Value.bombStockpile > highInventory)
                        {
                            // if this has more bombs than current front runner
                            highInventory = magazineEntry.Value.bombStockpile ;
                            highMagazineKey = magazineEntry.Key ;
                        }
                    }
                }
            }

            // Find consumer magazine with lowest amount of bombs
            // This is an attempt to keep the magazines balanced by drawing evenly from all magazines
            // Otherwise it will shift the vessel's center of gravity
            var lowMagazineKey = NullMagazineKey ;
            var lowInventory = 99999 ;
            foreach (var magazineEntry in this.dockedMagazineArrays[consumerShipIndex])
            {
                if (magazineEntry.Value != null)
                {
                    // in case a magazine was jettisoned
                    if (magazineEntry.Value.name == nukeTypeKey)
                    {
                        // magazine must hold requested type of nuke
                        if (magazineEntry.Value.bombStockpile < lowInventory)
                        {
                            // if this has fewer bombs than current front runner
                            lowInventory = magazineEntry.Value.bombStockpile ;
                            lowMagazineKey = magazineEntry.Key ;
                        }
                    }
                }
            }

            // transfer the nuke
            NukeRound theNuke ;
            if ((highMagazineKey != NullMagazineKey) && (lowMagazineKey != NullMagazineKey))
            {
                theNuke = this.magazineArray[highMagazineKey].requestNuke () ;
                if (theNuke.isBomb)
                {
                    // supplier had at least one nuke in that magazine to supply
                    if (!this.magazineArray[lowMagazineKey].addNuke ())
                    {
                        // magazine had spare empty capacity to accomodate adding 1 nuke
                        // consumer magazine has no room for new nuke
                        this.magazineArray[highMagazineKey].addNuke () ; // return the nuke
                    }
                }
            }
        }

        // what is the total of all the nukes in all the attached OrionMagazines (that are not filtered out)?
        // ToDo this is called by OrionPusherPlate.onPartFixedUpdate every clock cycle. Figure out a less expensive way to keep inventory number current.
        public int effectiveStockpileSize ()
        {
            var bombInventory = 0 ;
            foreach (var magazineEntry in this.magazineArray)
            {
                if (magazineEntry.Value != null)
                {
                    // in case a magazine was jettisoned
                    if (this.magazineFilter[magazineEntry.Value.name])
                    {
                        // if this bombtype has not been filtered
                        bombInventory = bombInventory + magazineEntry.Value.bombStockpile ;
                    }
                }
            }

            return bombInventory ;
        }

        // user wants to exclude this type of bomb in requestNuke()
        public void filterMagazine (String noWant)
        {
            if (this.magazineFilter.ContainsKey (noWant))
                this.magazineFilter[noWant] = false ;
        }

        // user wants to include this type of bomb in requestNuke()
        public void unfilterMagazine (String doWant)
        {
            if (this.magazineFilter.ContainsKey (doWant))
                this.magazineFilter[doWant] = true ;
        }
    }
}
