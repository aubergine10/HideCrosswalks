using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HideCrosswalks.Utils {
    using Patches;
    using Settings;
    using static TextureUtils;

    public static class PrefabUtils {
        public static string[] ARPMapExceptions = new[] { "" }; // TODO complete list.

        public static void CachePrefabs() {
#if !DEBUG
            if (Extensions.InAssetEditor) {
                Extensions.Log("skipped caching prefabs in asset editor release build");
                return;
            }
#endif
            MaterialUtils.Init();
            TextureUtils.Init();
            for (ushort segmentID = 0; segmentID < NetManager.MAX_SEGMENT_COUNT; ++segmentID) {
                foreach (bool bStartNode in new bool[] { false, true }) {
                    if (TMPEUTILS.HasCrossingBan(segmentID, bStartNode)) {
                        NetSegment segment = segmentID.ToSegment();
                        ushort nodeID = bStartNode ? segment.m_startNode : segment.m_endNode;
                        foreach (var node in segment.Info.m_nodes) {
                            //cache:
                            Extensions.Log("Caching " + segment.Info.name);
                            CalculateMaterialCommons.CalculateMaterial(node.m_nodeMaterial, nodeID, segmentID);
                        }
                    }
                }
            }
            Extensions.Log("all prefabs cached");
        }

        public static void ClearCache() {
            MaterialUtils.Clear();
            TextureUtils.Clear();
        }

        public static IEnumerable<NetInfo> Networks() {
#if !DEBUG // exclude in asset editor
            if (Extensions.currentMode == AppMode.AssetEditor)
                yield return null;
#endif
            int count = PrefabCollection<NetInfo>.LoadedCount();
            for (uint i = 0; i < count; ++i) {
                NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);
                if (info.CanHideMarkings()) {
                    yield return info;
                }
            }
        }

        public static bool isAsym(this NetInfo info) => info.m_forwardVehicleLaneCount != info.m_backwardVehicleLaneCount;
        public static bool isOneWay(this NetInfo info) => info.m_forwardVehicleLaneCount == 0 ||  info.m_backwardVehicleLaneCount == 0;

        public static bool HasMedian(this NetInfo info) {
            foreach (var lane in info.m_lanes) {
                if (lane.m_laneType == NetInfo.LaneType.None) {
                    return true;
                }
            }
            return false;
        }

        public static bool HasDecoration(this NetInfo info) {
            string title = info.GetUncheckedLocalizedTitle().ToLower();
            return title.Contains("tree") || title.Contains("grass") || title.Contains("arterial");
        }

        public static float ScaleRatio(this NetInfo info) {
            float ret = 1f;
            if (info.m_netAI is RoadAI) {
                bool b = info.HasDecoration() || info.HasMedian() || info.m_isCustomContent;
                b |= info.isAsym() && !info.isOneWay() && info.name != "AsymAvenueL2R3";
                if(!b)
                    ret = 0.91f;
                Extensions.Log(info.name + " : Scale: " + ret);
            }
            return ret;
        }

        public static bool IsNExt(this NetInfo info) {
            string c = info.m_class.name.ToLower();
            bool ret = c.StartsWith("next");
            //Extensions.Log($"IsNExt returns {ret} : {info.GetUncheckedLocalizedTitle()} : " + c);
            return ret;
        }

        public static bool HasSameNodeAndSegmentTextures(NetInfo info, Material nodeMaterial, int texID) {
            foreach (var seg in info.m_segments) {
                Texture t1 = nodeMaterial.GetTexture(texID);
                Texture t2 = seg.m_segmentMaterial.GetTexture(texID);
                if (t1 == t2)
                    return true;
            }
            return false;
        }

        public static bool CanHideCrossings(this NetInfo info) {
            // roads without pedesterian lanes (eg highways) have no crossings to hide to the best of my knowledege.
            // not sure about custom highways. Processing texture for such roads may reduce smoothness of the transition.
            return info.CanHideMarkings() && info.m_hasPedestrianLanes && info.m_hasForwardVehicleLanes;
        }

        public static bool CanHideMarkings(this NetInfo  info) {
            return (info.m_netAI is RoadBaseAI ) & HideCrosswalksMod.IsEnabled;
        }

    } // end class
} // end namespace
