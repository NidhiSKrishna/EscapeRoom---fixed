using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeRoom.Editor
{
    /// <summary>
    /// Configuration data for generating escape room environments.
    /// Completely parameterized and reusable across different room prototypes.
    /// 1 Unity unit = 1 meter.
    /// </summary>
    [Serializable]
    public class RoomConfiguration
    {
        [Header("Room Dimensions (Meters: 1 Unit = 1m)")]
        [Tooltip("Room length along the Z axis (meters)")]
        [Min(1f)]
        public float length = 12.0f;

        [Tooltip("Room width along the X axis (meters)")]
        [Min(1f)]
        public float width = 8.0f;

        [Tooltip("Interior room height along the Y axis (meters)")]
        [Min(1f)]
        public float height = 3.5f;

        [Tooltip("Wall, floor, and ceiling thickness in meters")]
        [Min(0.05f)]
        public float wallThickness = 0.20f;

        [Header("Exit Doorway Specification")]
        public bool includeExitDoorway = true;

        [Tooltip("Clear width of the exit doorway opening (meters)")]
        [Min(0.5f)]
        public float doorwayWidth = 1.20f;

        [Tooltip("Clear height of the exit doorway opening (meters)")]
        [Min(1.0f)]
        public float doorwayHeight = 2.20f;

        [Tooltip("Width of door frame trim elements (meters)")]
        [Min(0.02f)]
        public float doorFrameTrimWidth = 0.10f;

        [Tooltip("Depth of door frame casing extending beyond wall (meters)")]
        [Min(0.05f)]
        public float doorFrameTrimDepth = 0.26f;

        [Header("Furniture - Shelf Area")]
        public bool includeShelfArea = true;
        public Vector3 shelfPosition = new Vector3(-3.70f, 0.0f, -2.0f);
        public Vector3 shelfSize = new Vector3(0.50f, 2.40f, 2.20f);
        [Range(2, 8)]
        public int shelfTiers = 4;

        [Header("Furniture - Table")]
        public bool includeTable = true;
        public Vector3 tablePosition = new Vector3(1.50f, 0.0f, -1.0f);
        public Vector3 tableSize = new Vector3(1.00f, 0.85f, 2.00f);
        public float tableTopThickness = 0.08f;
        public float tableLegThickness = 0.08f;

        [Header("Furniture - Cabinet")]
        public bool includeCabinet = true;
        public Vector3 cabinetPosition = new Vector3(3.65f, 0.0f, 2.50f);
        public Vector3 cabinetSize = new Vector3(0.60f, 2.00f, 1.10f);

        [Header("Props - Crates")]
        public bool includeCrates = true;
        public List<CratePlacement> crates = new List<CratePlacement>();

        [Header("Optional Environment Light")]
        [Tooltip("Add prototype interior point light to illuminate the room")]
        public bool includeCeilingLight = true;
        public Color ceilingLightColor = new Color(1.0f, 0.95f, 0.88f);
        public float ceilingLightIntensity = 400.0f;
        public float ceilingLightRange = 15.0f;

        /// <summary>
        /// Creates the official prototype specification:
        /// 12m long, 8m wide, 3.5m high, 0.20m wall thickness.
        /// Includes exit doorway, shelf area, table, cabinet, and multiple crates.
        /// </summary>
        public static RoomConfiguration CreatePrototypeDefault()
        {
            var config = new RoomConfiguration
            {
                length = 12.0f,
                width = 8.0f,
                height = 3.5f,
                wallThickness = 0.20f,
                includeExitDoorway = true,
                doorwayWidth = 1.20f,
                doorwayHeight = 2.20f,
                doorFrameTrimWidth = 0.10f,
                doorFrameTrimDepth = 0.26f,

                includeShelfArea = true,
                shelfPosition = new Vector3(-3.70f, 0.0f, -2.0f),
                shelfSize = new Vector3(0.50f, 2.40f, 2.20f),
                shelfTiers = 4,

                includeTable = true,
                tablePosition = new Vector3(1.50f, 0.0f, -1.0f),
                tableSize = new Vector3(1.00f, 0.85f, 2.00f),
                tableTopThickness = 0.08f,
                tableLegThickness = 0.08f,

                includeCabinet = true,
                cabinetPosition = new Vector3(3.65f, 0.0f, 2.50f),
                cabinetSize = new Vector3(0.60f, 2.00f, 1.10f),

                includeCrates = true,
                includeCeilingLight = true,
                crates = new List<CratePlacement>
                {
                    // Back-left corner cluster (stacked pair + adjacent)
                    new CratePlacement("Crate_01_Large", new Vector3(-3.20f, 0.40f, -4.80f), new Vector3(0.80f, 0.80f, 0.80f), 10.0f),
                    new CratePlacement("Crate_02_Stacked", new Vector3(-3.15f, 1.10f, -4.75f), new Vector3(0.60f, 0.60f, 0.60f), -15.0f),
                    new CratePlacement("Crate_03_Medium", new Vector3(-2.25f, 0.35f, -5.10f), new Vector3(0.70f, 0.70f, 0.70f), 22.0f),

                    // Near shelf area
                    new CratePlacement("Crate_04_Small", new Vector3(-3.35f, 0.25f, 0.30f), new Vector3(0.50f, 0.50f, 0.50f), -8.0f),

                    // Back-right corner cluster
                    new CratePlacement("Crate_05_Medium", new Vector3(3.20f, 0.325f, -4.90f), new Vector3(0.65f, 0.65f, 0.65f), -18.0f),
                    new CratePlacement("Crate_06_Small", new Vector3(2.40f, 0.275f, -5.00f), new Vector3(0.55f, 0.55f, 0.55f), 14.0f)
                }
            };

            return config;
        }
    }

    /// <summary>
    /// Placement and dimensional specifications for an individual crate prop.
    /// </summary>
    [Serializable]
    public class CratePlacement
    {
        public string name;
        public Vector3 position;
        public Vector3 size;
        public float rotationY;

        public CratePlacement(string name, Vector3 position, Vector3 size, float rotationY)
        {
            this.name = name;
            this.position = position;
            this.size = size;
            this.rotationY = rotationY;
        }
    }
}
