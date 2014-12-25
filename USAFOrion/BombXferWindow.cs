#region using

using System ;

using KSP.IO ;

using Tac ;

using UnityEngine ;

using USAFOrion ;

#endregion

// Requires TacLibGUI.dll

/*
 assumed to be attached to an OrionPusherPlate part, add this to part.cfg:
 
MODULE
{
	name = BombXferWindow
	debug = false
}
*/

public class BombXferWindow : PartModule
{
    // Set debug according to setting in part.cfg
    [KSPField]
    public bool debug = false ;

    private XferDialog xferDialog ;
    // Fired first - this is at KSP load-time (When the loading bar hits a part with this mod)
    public override void OnAwake () { base.OnAwake () ; }

    // Fires at multiple times, but mainly when scene loads - node contains scene ConfigNode data (all craft in savegame)
    // IMPORTANT! This also fires at KSP load-time. DO NOT try and start the GUI here.
    public override void OnLoad (ConfigNode node)
    {
        base.OnLoad (node) ;

        // Only fire Load when we are loading a scene, not loading KSP.
        if (HighLogic.LoadedSceneIsFlight)
        {
            // Load settings for xferDialog
            // Second parameter is used to limit this window to only show for the vessel that created it
            // xferDialog = new XferDialog("My Window", this.vessel);
            this.xferDialog = new XferDialog ("Nuke Transfer", this) ;
            this.xferDialog.Load (node) ;
        }
    }

    // Fired when scene containing part Saves (Ends)
    public override void OnSave (ConfigNode node)
    {
        base.OnSave (node) ;
        // OnSave seems to fire in the Editor when you place the part or when you load a craft containing it
        // Code bombs if mainwindow.Save called at this point
        if (HighLogic.LoadedSceneIsFlight)
        {
            if (this.xferDialog != null)
                this.xferDialog.Save (node) ;
        }
    }

    // Fired once, when a scene starts containing the part
    public void Start ()
    {
        if (this.xferDialog != null)
            this.xferDialog.SetVisible (false) ;
    }

    // Fires ?every frame? while the GUI is active
    public void OnGUI () { }

    // =====================================================================================================================================================
    // Flight UI and Action Group Hooks

    [KSPEvent (guiActive = true, guiName = "Show Xfer Menu", active = true)]
    public void ShowMainMenu ()
    {
        if (this.xferDialog != null)
        {
            this.xferDialog.refreshNukeManager () ;
            this.xferDialog.SetVisible (true) ;
        }
    }

    [KSPEvent (guiActive = true, guiName = "Hide Xfer Menu", active = false)]
    public void HideMainMenu ()
    {
        if (this.xferDialog != null)
            this.xferDialog.SetVisible (false) ;
    }

    [KSPAction ("Show Xfer Menu")]
    public void ShowMainMenuAction (KSPActionParam param)
    {
        this.ShowMainMenu () ;
    }

    [KSPAction ("Hide Xfer Menu")]
    public void HideMainMenuAction (KSPActionParam param)
    {
        this.HideMainMenu () ;
    }

    public override void OnUpdate ()
    {
        if (this.xferDialog != null)
        {
            this.Events["ShowMainMenu"].active = !this.xferDialog.IsVisible () ;
            this.Events["HideMainMenu"].active = this.xferDialog.IsVisible () ;
        }
    }
}

/**
 * Instructions for use:
 *  (1) Create an instance of this class somewhere, preferrably where it will not be deleted/garbage collected.
 *  (2) Call SetVisible(true) to start showing it.
 *  (3) Call Load/Save if you want it to remember its position and size between runs.
 */

internal class XferDialog : Window<XferDialog>
{
    private NukeManager aNukeManager ;
    private float lastButtonFireTime ;
    private GUIStyle theBoxStyle ;
    public UIStatus uistatus = new UIStatus () ;
    private readonly float currentFireDelay ;
    private readonly float slowFireDelay = 0.17f ; // 6 per second

    public XferDialog (string name, PartModule p)
        : base (name, p)
    {
        // Force default size
        this.windowPos = new Rect (60, 60, 400, 400) ;

        this.aNukeManager = (p.part as OrionPusherPlate).nukeManager ;
        this.currentFireDelay = this.slowFireDelay ;
    }

//    public delegate void TestHandler(string message);
//
//    public void Test(TestHandler myHandler)
//    {
//        if (myHandler != null)
//        {
//            myHandler("Test Pressed!");
//        }
//    }

    // Called when UI is starting
    public void Start () { }

    protected override void DrawWindow ()
    {
        //Example feature - prevent the window from being shown if the vessel is not prelaunch or landed.
        if (FlightGlobals.fetch && FlightGlobals.fetch.activeVessel != null)
            base.DrawWindow () ;
    }

    protected override void ConfigureStyles ()
    {
        base.ConfigureStyles () ;
        // Initialize your styles here (optional)

        // make box style same dimensions as button
        this.theBoxStyle = new GUIStyle (GUI.skin.box) ;
        this.theBoxStyle.margin = GUI.skin.button.margin ;
        this.theBoxStyle.padding = GUI.skin.button.padding ;
        this.theBoxStyle.fixedHeight = GUI.skin.button.fixedHeight ;
    }

    // Called every time the GUI paints (Often!)
    protected override void DrawWindowContents (int windowId)
    {
        String magazineTitle ;
        bool atLeastOneButtonRow ;

        if ((this.aNukeManager != null) && (this.aNukeManager.doDockedInventoryCalled) &&
            (this.aNukeManager.maxDockedShipID > 0))
        {
            // start scroller
            this.uistatus.ContentScroller = GUILayout.BeginScrollView (this.uistatus.ContentScroller, true, true, null) ;

            GUILayout.BeginHorizontal () ;

            atLeastOneButtonRow = false ;
            for (var dockedShipIndex = 0; dockedShipIndex <= this.aNukeManager.maxDockedShipID; dockedShipIndex++)
            {
                // start drawing a column of controls
                GUILayout.BeginVertical () ;

                GUILayout.Space (10) ;

                // Draw Label with name of ship this column is for
                GUILayout.Box (this.uistatus.vesselNames[dockedShipIndex]) ;

                // Draw the "Use this ship as part of a transfer pair" Toggle checkbox
                this.uistatus.vesselSelect[dockedShipIndex] =
                    GUILayout.Toggle (this.uistatus.vesselSelect[dockedShipIndex], "Use") ;

//				if (GUILayout.Toggle(uistatus.vesselSelect[dockedShipIndex], "Use")) {
//					uistatus.vesselSelect[dockedShipIndex] = true;
//					// only two toggles can be set at one time
//					int numTrue = 0;
//					for (int dockedShipIndex2 = 0;  dockedShipIndex2 <= this.aNukeManager.maxDockedShipID; dockedShipIndex2++) {
//						if (uistatus.vesselSelect[dockedShipIndex2]) {
//							numTrue++;
//						}
//					}
//					if (numTrue > 2) {
//						for (int dockedShipIndex3 = this.aNukeManager.maxDockedShipID;  dockedShipIndex3 <= 0; dockedShipIndex3--) {
//							if (uistatus.vesselSelect[dockedShipIndex3]) {
//								uistatus.vesselSelect[dockedShipIndex3] = false;
//								break;
//							}
//						}
//					}
//				} else {
//					uistatus.vesselSelect[dockedShipIndex] = false;
//				}

                // Draw series of buttons for "Transfer this nuke type from this column's ship to other selected column"
                foreach (var magKey in this.aNukeManager.magazineKeys)
                {
                    if (this.aNukeManager.hasXferMagazinePair[magKey])
                    {
                        // at least 2 docked ships with this magazine type
                        atLeastOneButtonRow = true ;
                        if (this.aNukeManager.dockedHasMagazine[dockedShipIndex][magKey])
                        {
                            // this ship has at least one magazine of this type
                            magazineTitle = this.aNukeManager.magazineTitles[magKey] + ":" +
                                            this.aNukeManager.dockedBombInventory[dockedShipIndex][magKey] ;
                            if (this.uistatus.vesselSelect[dockedShipIndex])
                            {
                                // if this column has "Use" checkbox selected
                                if (GUILayout.RepeatButton (magazineTitle))
                                {
                                    // draw button
                                    this.MoveBombs (dockedShipIndex, magKey) ; // if button is pushed, do the command
                                }
                            }
                            else
                            {
                                // this column does NOT have "Use" checkbox selected
                                GUILayout.Box (magazineTitle, this.theBoxStyle) ;
                                    // draw static label instead of a button control
                            }
                        }
                        else
                            GUILayout.Box ("N/A", this.theBoxStyle) ; // ship does not own magazine of this type
                    }
                }

                GUILayout.EndVertical () ;
            }


            if (!atLeastOneButtonRow)
                GUILayout.Box ("No matching magazine types") ;

            GUILayout.EndHorizontal () ;

            // End the scroller
            GUILayout.EndScrollView () ;
        }
        else
        {
            GUILayout.Box (this.uistatus.vesselNames[0]) ;
            GUILayout.Box ("No vessels are docked") ;
        }
    }

    // Bomb button was pressed
    // it is assumed that supplierShipIndex has "Use" checkbox selected
    private void MoveBombs (int supplierShipIndex, String theMagKey)
    {
        if ((Time.time - this.lastButtonFireTime) > this.currentFireDelay)
        {
            this.lastButtonFireTime = Time.time ;

            var consumerShipIndex = -1 ;

            // figure out consumerShipIndex
            for (var dockedShipIndex = 0; dockedShipIndex <= this.aNukeManager.maxDockedShipID; dockedShipIndex++)
            {
                if ((dockedShipIndex != supplierShipIndex) && (this.uistatus.vesselSelect[dockedShipIndex]))
                {
                    consumerShipIndex = dockedShipIndex ;
                    break ;
                }
            }

            if (consumerShipIndex != -1)
            {
                // otherwise there is only one "Use" Toggle selected, not two as is required
                // moveNuke will abort move if consumer ship has no proper magazines, or proper magazines but none with spare capacity
                this.aNukeManager.moveNuke (theMagKey, supplierShipIndex, consumerShipIndex) ;
                this.aNukeManager.doDockedInventory () ;
            }
        }
    }

    public override void Load (ConfigNode node)
    {
        // Load base settings from global
        var configFilename = IOUtils.GetFilePathFor (this.GetType (), "BombXferWindow.cfg", null) ;
        var config = ConfigNode.Load (configFilename) ;

        // Merge with per-ship settings
        if (config != null)
            config.CopyTo (node) ;

        // Apply settings
        base.Load (node) ;
    }

    public override void Save (ConfigNode node)
    {
        // Start with fresh node
        var configFilename = IOUtils.GetFilePathFor (this.GetType (), "BombXferWindow.cfg", null) ;
        var config = new ConfigNode () ;

        // Add Window information to node
        base.Save (config) ;

        // Save global settings
        config.Save (configFilename) ;

        // Save Per-Ship settings
        config.CopyTo (node) ;
    }

    public void refreshNukeManager ()
    {
        if (this.aNukeManager == null)
        {
            if (this.myPartModule != null)
                this.aNukeManager = (this.myPartModule.part as OrionPusherPlate).nukeManager ;
        }

        if (this.aNukeManager != null)
        {
            this.aNukeManager.doDockedInventory () ;
            this.uistatus.vesselNames = new string[this.aNukeManager.maxDockedShipID + 1] ;
            this.uistatus.vesselNames[0] = this.myPartModule.vessel.GetName () ;
            this.uistatus.vesselSelect = new bool[this.aNukeManager.maxDockedShipID + 1] ;
            this.uistatus.vesselSelect[0] = true ;
            if (this.aNukeManager.maxDockedShipID > 0)
            {
                for (var dockedShipIndex = 1; dockedShipIndex <= this.aNukeManager.maxDockedShipID; dockedShipIndex++)
                {
                    this.uistatus.vesselNames[dockedShipIndex] = "Docked #" + dockedShipIndex ;
                    this.uistatus.vesselSelect[dockedShipIndex] = true ;
                }
            }
            this.uistatus.vesselSelect = new bool[this.aNukeManager.maxDockedShipID + 1] ;
        }
        else
        {
            this.uistatus.vesselNames = new string[1] ;
            this.uistatus.vesselNames[0] = this.myPartModule.vessel.GetName () ;
            this.uistatus.vesselSelect = new bool[1] ;
            this.uistatus.vesselSelect[0] = true ;
        }
    }

    // Use this class to store the current state of the UI
    public class UIStatus
    {
        public Vector2 ContentScroller ;
        public string[] vesselNames ;
        public bool[] vesselSelect ;
    }
}
