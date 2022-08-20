﻿using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class KerbalConstructionTimeData : ScenarioModule
    {
        public static Dictionary<string, string> techNameToTitle = new Dictionary<string, string>();
        public static Dictionary<string, List<string>> techNameToParents = new Dictionary<string, List<string>>();

        [KSPField(isPersistant = true)]
        private PersistentList<LCEfficiency> _lcEfficiencies = new PersistentList<LCEfficiency>();
        public PersistentList<LCEfficiency> LCEfficiencies => _lcEfficiencies;

        public Dictionary<LCItem, LCEfficiency> LCToEfficiency = new Dictionary<LCItem, LCEfficiency>();

        [KSPField(isPersistant = true)]
        private PersistentList<TechItem> TechList = new PersistentList<TechItem>();

        public static KerbalConstructionTimeData Instance { get; protected set; }

        public override void OnAwake()
        {
            base.OnAwake();
            if (Instance != null)
                Destroy(Instance);

            Instance = this;
        }

        public void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        protected void LoadTree()
        {
            if (HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX || HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                // On starting a new game, MM has not yet patched the tech tree URL so we're
                // going to use that directly instead of the one in HighLogic.
                if (HighLogic.CurrentGame.Parameters.Career.TechTreeUrl.Contains("Squad"))
                    HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = System.IO.Path.Combine("GameData", "ModuleManager.TechTree");

                string fullPath = KSPUtil.ApplicationRootPath + HighLogic.CurrentGame.Parameters.Career.TechTreeUrl;
                KCTDebug.Log($"Loading tech tree from {fullPath}");

                if (ConfigNode.Load(fullPath) is ConfigNode fileNode && fileNode.HasNode("TechTree"))
                {
                    techNameToTitle.Clear();
                    techNameToParents.Clear();

                    ConfigNode treeNode = fileNode.GetNode("TechTree");
                    foreach (ConfigNode n in treeNode.GetNodes("RDNode"))
                    {
                        string techID = n.GetValue("id");
                        if (techID != null)
                        {
                            string title = n.GetValue("title");
                            if (title != null)
                                techNameToTitle[techID] = title;

                            var pList = new List<string>();
                            foreach (ConfigNode p in n.GetNodes("Parent"))
                            {
                                string pID = p.GetValue("parentID");
                                if(pID != null)
                                    pList.Add(pID);
                            }
                            techNameToParents[techID] = pList;
                        }
                    }
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            if (Utilities.CurrentGameIsMission()) return;

            KCTDebug.Log("Writing to persistence.");
            base.OnSave(node);
            var kctVS = new KCT_DataStorage();
            node.AddNode(kctVS.AsConfigNode());
            foreach (KSCItem KSC in KCTGameStates.KSCs.Where(x => x?.KSCName?.Length > 0))
            {
                // Don't bother saving KSCs that aren't active
                if (KSC.IsEmpty && KSC != KCTGameStates.ActiveKSC)
                    continue;

                node.AddNode(KSC.AsConfigNode());
            }
            var tech = new ConfigNode("TechList");
            foreach (TechItem techItem in KCTGameStates.TechList)
            {
                var cnTemp = new ConfigNode("Tech");
                techItem.Save(cnTemp);
                cnTemp = ConfigNode.CreateConfigFromObject(techItem, cnTemp);
                var protoNode = new ConfigNode("ProtoNode");
                techItem.ProtoNode.Save(protoNode);
                cnTemp.AddNode(protoNode);
                tech.AddNode(cnTemp);
            }
            node.AddNode(tech);

            var cnPlans = new ConfigNode("Plans");
            foreach (BuildListVessel blv in KCTGameStates.Plans.Values)
            {
                cnPlans.AddNode(blv.BuildVesselAndShipNodeConfigs());
            }
            node.AddNode(cnPlans);

            KCT_GUI.GuiDataSaver.Save();
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                base.OnLoad(node);
                LoadTree();

                if (Utilities.CurrentGameIsMission()) return;

                KCTDebug.Log("Reading from persistence.");
                KCTGameStates.KSCs.Clear();
                KCTGameStates.ActiveKSC = null;
                KCTGameStates.InitAndClearTechList();
                KCTGameStates.SciPointsTotal = -1;
                KCTGameStates.UnassignedPersonnel = 0;
                KCTGameStates.Researchers = 0;
                KCTGameStates.Plans.Clear();

                var kctVS = new KCT_DataStorage();
                if (node.GetNode(kctVS.GetType().Name) is ConfigNode cn)
                {
                    ConfigNode.LoadObjectFromConfig(kctVS, cn);
                }

                bool foundStockKSC = false;
                foreach (ConfigNode ksc in node.GetNodes("KSC"))
                {
                    string name = ksc.GetValue("KSCName");
                    var loaded_KSC = new KSCItem(name);
                    loaded_KSC.FromConfigNode(ksc);
                    if (loaded_KSC?.KSCName?.Length > 0)
                    {
                        if (KCTGameStates.KSCs.Find(k => k.KSCName == loaded_KSC.KSCName) == null)
                            KCTGameStates.KSCs.Add(loaded_KSC);
                        foundStockKSC |= string.Equals(loaded_KSC.KSCName, Utilities._legacyDefaultKscId, StringComparison.OrdinalIgnoreCase);
                    }
                }

                Utilities.SetActiveKSCToRSS();
                if (foundStockKSC)
                    TryMigrateStockKSC();

                var protoTechNodes = new Dictionary<string, ProtoTechNode>(); // list of all the protoTechNodes that have been researched
                var inDevProtoTechNodes = new Dictionary<string, ProtoTechNode>(); // list of all the protoTechNodes that are being researched

                // get the TechList node containing the TechItems with the tech nodes currently being researched from KCT's ConfigNode
                if (node.GetNode("TechList") is ConfigNode tmp)
                {
                    foreach (ConfigNode techNode in tmp.GetNodes("Tech"))
                    {
                        TechItem techItem = new TechItem();
                        techItem.Load(techNode);
                        KCTGameStates.TechList.Add(techItem);

                        // save proto nodes that are in development
                        inDevProtoTechNodes.Add(techItem.ProtoNode.techID, techItem.ProtoNode);
                    }
                }
                if (HighLogic.LoadedSceneIsEditor)
                {
                    // get the nodes that have been researched from ResearchAndDevelopment
                    protoTechNodes = Utilities.GetUnlockedProtoTechNodes();
                    // iterate through all loaded parts to check if any of them should be experimental
                    foreach (AvailablePart ap in PartLoader.LoadedPartsList)
                    {
                        if (Utilities.PartIsUnlockedButNotPurchased(protoTechNodes, ap) || inDevProtoTechNodes.ContainsKey(ap.TechRequired))
                        {
                            Utilities.AddExperimentalPart(ap);
                        }
                    }
                }
                tmp = node.GetNode("Plans");
                if (tmp != null)
                {
                    foreach (ConfigNode cnV in tmp.GetNodes("KCTVessel"))
                    {
                        var blv = BuildListVessel.CreateBLVFromNode(cnV, null);
                        KCTGameStates.Plans.Remove(blv.ShipName);
                        KCTGameStates.Plans.Add(blv.ShipName, blv);
                    }
                }
                if (KCTGameStates.LoadedSaveVersion < KCTGameStates.VERSION)
                {
                    if (KCTGameStates.LoadedSaveVersion < 2)
                    {
                        KCTGameStates.StarterLCBuilding = KCTGameStates.KSCs.FirstOrDefault(k => k.LaunchComplexes.Count > 1) != null;
                    }
                    if (KCTGameStates.LoadedSaveVersion < 8)
                    {
                        foreach (var k in KCTGameStates.KSCs)
                        {
                            foreach (var lc in k.LaunchComplexes)
                            {
                                if (!lc.IsOperational)
                                {
                                    if (k.LCConstructions.Find(c => c.lcID == lc.ID) == null)
                                        lc.IsOperational = true;
                                }
                            }
                        }
                    }
                }

                KCTGameStates.LoadedSaveVersion = KCTGameStates.VERSION;

                LCEfficiency.RelinkAll();
            }
            catch (Exception ex)
            {
                KCTGameStates.ErroredDuringOnLoad = true;
                Debug.LogError("[KCT] ERROR! An error while KCT loading data occurred. Things will be seriously broken!\n" + ex);
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "errorPopup", "Error Loading KCT Data", "ERROR! An error occurred while loading KCT data. Things will be seriously broken! Please report this error to RP-1 GitHub and attach the log file. The game will be UNPLAYABLE in this state!", "Understood", false, HighLogic.UISkin);
            }
        }

        private void TryMigrateStockKSC()
        {
            KSCItem stockKsc = KCTGameStates.KSCs.Find(k => string.Equals(k.KSCName, Utilities._legacyDefaultKscId, StringComparison.OrdinalIgnoreCase));
            if (KCTGameStates.KSCs.Count == 1)
            {
                // Rename the stock KSC to the new default (Cape)
                stockKsc.KSCName = Utilities._defaultKscId;
                Utilities.SetActiveKSC(stockKsc.KSCName);
                return;
            }

            if (stockKsc.IsEmpty)
            {
                // Nothing provisioned into the stock KSC so it's safe to just delete it
                KCTGameStates.KSCs.Remove(stockKsc);
                Utilities.SetActiveKSCToRSS();
                return;
            }

            int numOtherUsedKSCs = KCTGameStates.KSCs.Count(k => !k.IsEmpty && k != stockKsc);
            if (numOtherUsedKSCs == 0)
            {
                string kscName = Utilities.GetActiveRSSKSC() ?? Utilities._defaultKscId;
                KSCItem newDefault = KCTGameStates.KSCs.Find(k => string.Equals(k.KSCName, kscName, StringComparison.OrdinalIgnoreCase));
                if (newDefault != null)
                {
                    // Stock KSC isn't empty but the new default one is - safe to rename the stock and remove the old default item
                    stockKsc.KSCName = newDefault.KSCName;
                    KCTGameStates.KSCs.Remove(newDefault);
                    Utilities.SetActiveKSC(stockKsc);
                    return;
                }
            }

            // Can't really do anything if there's multiple KSCs in use.
            if (!Utilities.IsKSCSwitcherInstalled)
            {
                // Need to switch back to the legacy "Stock" KSC if KSCSwitcher isn't installed
                Utilities.SetActiveKSC(stockKsc.KSCName);
            }
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
