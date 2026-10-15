using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Validation
{
    public enum MechanicsTestFocus
    {
        Assembly, Drawing, Gun, Piercer, Mortar, Fan, Ricochet, Stapler, Blotter, Armor, Feed, Tracking, Rail,
        EnemyTiers, Ranged, Blots, Wells, Color, Construction, Economy, Profile, Integration, Pressure
    }
    [Serializable] public sealed class MechanicsTarget
    {
        public Vector2 position;
        public float health = 500;
        public EnemyKind kind;
        public DrawingTier drawingTier;
        public bool stationary = true;
        public PreparedEnemy Prepare() => new PreparedEnemy(new Point2(position.x, position.y), health, kind, drawingTier, stationary);
    }
    [Serializable] public sealed class MechanicsBlot
    {
        public Vector2Int cell;
        public float lifetime = 60;
        public PreparedBlot Prepare() => new PreparedBlot(new Cell(cell.x, cell.y), lifetime);
    }
    [Serializable] public sealed class MechanicsPlacement
    {
        public Vector2Int cell = new Vector2Int(18, 16);
        public string chassisId = "chassis.standard";
        public string[] weaponIds = { "weapon.gun" };
        public string[] moduleIds = Array.Empty<string>();
        public MaterialTier material = MaterialTier.Pencil;
        public InkColor color;
    }

    /// <summary>Every scene owns data and a catalog; production configuration is never edited by a fixture.</summary>
    [CreateAssetMenu(menuName = "Paper Defense/Validation/Mechanics Scenario")]
    public sealed class MechanicsTestScenarioAsset : ScriptableObject
    {
        public string scenarioId, titleKey, instructionsKey;
        public MechanicsTestFocus focus;
        public BalancePresetAsset preset;
        public TextResourcesAsset textResources;
        public int seed = 1337, startingInk = 600, startingColoredInk = 60;
        public bool startPaused = true, workshopMode;
        public MechanicsConfiguration mechanics = new MechanicsConfiguration();
        public List<MechanicsPlacement> placements = new List<MechanicsPlacement>();
        public List<MechanicsTarget> targets = new List<MechanicsTarget>();
        public List<MechanicsBlot> blots = new List<MechanicsBlot>();
        public string[] observationKeys = Array.Empty<string>();

        public GameBalance CreateBalance()
        {
            if (string.IsNullOrWhiteSpace(scenarioId) || preset == null || textResources == null || mechanics == null)
                throw new ArgumentException("scenario: ID, preset, text and mechanics are required.");
            if (startingInk < 0 || startingInk > 100000 || startingColoredInk < 0 || startingColoredInk > TrialRules.MaximumBudget)
                throw new ArgumentException("scenario: resources outside fixture bounds.");
            var result = preset.CreateBalance();
            result.Mechanics = mechanics.CreateSnapshot();
            // Set both legacy gates from the exact enabled dependencies; inspector master flags cannot mask the focus.
            result.EnableEnemyMechanics = mechanics.EnemyDrawingTiers || mechanics.CorpseBlots || mechanics.RangedEnemies;
            result.EnableInkWells = mechanics.InkWells;
            result.StartingInk = startingInk;
            result.PassiveInkPerSecond = 0; result.EarlyLaunchInkPerSecond = 0; result.MaxEarlyLaunchInk = 0;
            BalanceValidation.Validate(result);
            return result;
        }

        public GameSession CreatePreparedSession()
        {
            var enemies = new List<PreparedEnemy>(); foreach (var target in targets) enemies.Add(target.Prepare());
            var marks = new List<PreparedBlot>(); foreach (var blot in blots) marks.Add(blot.Prepare());
            int stock = mechanics.ColorCartridges ? startingColoredInk : 0;
            return GameSession.CreatePrepared(CreateBalance(), seed, enemies, marks, stock, stock, stock);
        }
    }
}
